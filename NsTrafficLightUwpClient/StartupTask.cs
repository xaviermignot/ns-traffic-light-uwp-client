using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.System.Threading;

namespace NsTrafficLightUwpClient
{
    public sealed class StartupTask : IBackgroundTask
    {
        BackgroundTaskDeferral deferral;
        private readonly HttpClient httpClient = new HttpClient();
        private readonly Dictionary<string, GpioPin> pins = new Dictionary<string, GpioPin>();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Tells that the program will not exit at the end of the Run method
            deferral = taskInstance.GetDeferral();

            // Opens Gpio ports and initializes pins dictionary
            InitGpio();

            // Performs startup sequence: lights every bulb for one second to inform that the program is up
            await Startup();

            if (Configuration.Instance.UseSignalR)
            {
                // SignalR mode: intializes hub connection & wait for server to push changes
                await StartSignalR();
            }
            else
            {
                // Api polling mode: gets the value from the api every X milliseconds
                ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromMilliseconds(Configuration.Instance.ApiPollingPeriodInMs), Timer_Destroyed);
            }
        }

        private void InitGpio()
        {
            var pinController = GpioController.GetDefault();
            pins.Add("Green", CreateAndOpenPin(pinController, 27));
            pins.Add("Orange", CreateAndOpenPin(pinController, 18));
            pins.Add("Red", CreateAndOpenPin(pinController, 4));
        }

        private GpioPin CreateAndOpenPin(GpioController controller, int pinNumber)
        {
            var gpioPin = controller.OpenPin(pinNumber);
            gpioPin.SetDriveMode(GpioPinDriveMode.Output);

            return gpioPin;
        }

        private async Task StartSignalR()
        {
            await PollApi();

            var connection = new HubConnection(Configuration.Instance.ApiUri.AbsoluteUri);
            var hub = connection.CreateHubProxy("TrafficLightHub");

            connection.EnsureReconnecting();

            await connection.Start().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    LightBulbs(new[] { "Orange", "Red" });
                }
            });

            hub.On<TrafficLightState>("UpdateLight", s => LightBulb(s.ToString()));
        }

        private async Task Startup()
        {
            foreach (var color in pins.Keys)
            {
                LightBulb(color);
                await Task.Delay(1000);
            }

            LightBulb("Off");
        }

        private void LightBulb(string lightValue)
        {
            foreach (var pair in this.pins)
            {
                pair.Value.Write(pair.Key == lightValue ? GpioPinValue.High : GpioPinValue.Low);
            }
        }

        private void LightBulbs(ICollection<string> lightValues)
        {
            foreach(var pair in this.pins)
            {
                pair.Value.Write(lightValues.Contains(pair.Key) ? GpioPinValue.High : GpioPinValue.Low);
            }
        }

        private async Task PollApi()
        {
            var uri = new Uri(Configuration.Instance.ApiUri, "api/trafficlight");
            var getResult = await httpClient.GetAsync(uri);
            if (getResult.IsSuccessStatusCode)
            {
                var lightValue = (await getResult.Content.ReadAsStringAsync()).Replace("\"", string.Empty);
                LightBulb(lightValue);
            }
            else
            {
                LightBulbs(new[] { "Orange", "Red" });
            }
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            PollApi().Wait();
        }

        private void Timer_Destroyed(ThreadPoolTimer timer)
        {
            deferral.Complete();
            httpClient.Dispose();
        }
    }
}
