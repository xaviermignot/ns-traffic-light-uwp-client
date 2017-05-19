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
        BackgroundTaskDeferral _deferral;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly Dictionary<TrafficLightState, GpioPin> _gpioLightPins = new Dictionary<TrafficLightState, GpioPin>();

        private GpioPin _gpioButtonPin;
        private bool _isDead;

        private IDisposable _signalRHub;
        private HubConnection _signalRConnection;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Tells that the program will not exit at the end of the Run method
            _deferral = taskInstance.GetDeferral();

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
            _gpioLightPins.Add(TrafficLightState.Green, CreateAndOpenPin(pinController, 27));
            _gpioLightPins.Add(TrafficLightState.Orange, CreateAndOpenPin(pinController, 18));
            _gpioLightPins.Add(TrafficLightState.Red, CreateAndOpenPin(pinController, 4));

            _gpioButtonPin = CreateAndOpenPin(pinController, 23, GpioPinDriveMode.InputPullUp);
            _gpioButtonPin.DebounceTimeout = TimeSpan.FromMilliseconds(50);
            _gpioButtonPin.ValueChanged += ButtonPressed;
        }

        private void ButtonPressed(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (args.Edge != GpioPinEdge.FallingEdge)
            {
                return;
            }

            _isDead = !_isDead;

            if (_isDead)
            {
                LightBulb(TrafficLightState.Broken);
                _signalRConnection.Stop();
                _signalRHub.Dispose();
                NotifyState(TrafficLightState.Broken).Wait();
            }
            else
            {
                NotifyState(TrafficLightState.Green).Wait();
                StartSignalR().Wait();
            }
        }

        private GpioPin CreateAndOpenPin(GpioController controller, int pinNumber, GpioPinDriveMode driveMode = GpioPinDriveMode.Output)
        {
            var gpioPin = controller.OpenPin(pinNumber);
            gpioPin.SetDriveMode(driveMode);

            return gpioPin;
        }

        private async Task StartSignalR()
        {
            await PollApi();

            _signalRConnection = new HubConnection(Configuration.Instance.ApiUri.AbsoluteUri);
            var hub = _signalRConnection.CreateHubProxy("TrafficLightHub");

            _signalRConnection.EnsureReconnecting();

            await _signalRConnection.Start().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    LightBulbs(new[] { TrafficLightState.Orange, TrafficLightState.Red });
                }
            });

            _signalRHub = hub.On<TrafficLightState>("UpdateLight", s => LightBulb(s));
        }

        private async Task Startup()
        {
            foreach (var color in _gpioLightPins.Keys)
            {
                LightBulb(color);
                await Task.Delay(1000);
            }

            LightBulb(TrafficLightState.Off);
        }

        private void LightBulb(TrafficLightState lightValue)
        {
            if (lightValue == TrafficLightState.Broken)
            {
                LightBulbs(new[] { TrafficLightState.Red, TrafficLightState.Green });
                return;
            }

            foreach (var pair in this._gpioLightPins)
            {
                pair.Value.Write(pair.Key == lightValue ? GpioPinValue.High : GpioPinValue.Low);
            }
        }

        private void LightBulbs(ICollection<TrafficLightState> lightValues)
        {
            foreach (var pair in this._gpioLightPins)
            {
                pair.Value.Write(lightValues.Contains(pair.Key) ? GpioPinValue.High : GpioPinValue.Low);
            }
        }

        private async Task PollApi()
        {
            var uri = new Uri(Configuration.Instance.ApiUri, "api/trafficlight");
            var getResult = await _httpClient.GetAsync(uri);
            if (getResult.IsSuccessStatusCode)
            {
                var response = (await getResult.Content.ReadAsStringAsync()).Replace("\"", string.Empty);
                TrafficLightState lightValue;
                if (Enum.TryParse(response, out lightValue))
                {
                    LightBulb(lightValue);
                    return;
                }
            }

            // We get here if a problem occured, so let's light the error bulbs
            LightBulbs(new[] { TrafficLightState.Orange, TrafficLightState.Red });
        }

        private Task NotifyState(TrafficLightState state)
        {
            var uri = new Uri(Configuration.Instance.ApiUri, $"api/trafficlight/{state.ToString().ToLower()}");
            return _httpClient.PutAsync(uri, null);
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {
            PollApi().Wait();
        }

        private void Timer_Destroyed(ThreadPoolTimer timer)
        {
            _deferral.Complete();
            _httpClient.Dispose();
        }
    }
}
