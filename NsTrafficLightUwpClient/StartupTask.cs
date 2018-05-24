using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Devices.Tpm;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.System.Threading;

namespace NsTrafficLightUwpClient
{
    public sealed class StartupTask : IBackgroundTask
    {
        BackgroundTaskDeferral _deferral;

        private TrafficLightState _currentState;
        private readonly Dictionary<TrafficLightState, GpioPin> _gpioLightPins = new Dictionary<TrafficLightState, GpioPin>();

        private GpioPin _gpioButtonPin;

        private static DeviceClient Client = null;

        private const string LightProperty = "Light";

        private ThreadPoolTimer _alertTimer;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Tells that the program will not exit at the end of the Run method
            _deferral = taskInstance.GetDeferral();

            // Opens Gpio ports and initializes pins dictionary
            InitGpio();

            // Performs startup sequence: lights every bulb for one second to inform that the program is up
            await Startup();

            await InitializeDeviceClient();

            await GetAndApplyDeviceTwins();
        }

        private async Task GetAndApplyDeviceTwins()
        {
            var twin = await Client.GetTwinAsync();
            if (!LightBulbFromTwins(twin.Properties.Desired))
            {
                LightBulb(TrafficLightState.Off);
                await ReportCurrentLight();
            }
        }

        private async Task InitializeDeviceClient()
        {
            var cnxString = GetIotHubConnectionString();
            Client = DeviceClient.CreateFromConnectionString(cnxString, TransportType.Mqtt);
            await Client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, null);
            await Client.SetMethodHandlerAsync("Alert", StartAlert, null);
        }

        private static string GetIotHubConnectionString()
        {
            if (Configuration.Instance.UseTpm)
            {
                var device = new TpmDevice(0);
                return device.GetConnectionString();
            }

            return Configuration.Instance.ConnectionString;
        }

        private bool LightBulbFromTwins(TwinCollection twins)
        {
            if (twins == null || !twins.Contains(LightProperty))
            {
                return false;
            }

            var light = twins[LightProperty];
            if (Enum.TryParse(light.Value, out TrafficLightState lightEnum))
            {
                LightBulb(lightEnum);
                return true;
            }

            return false;
        }

        private async Task ReportCurrentLight()
        {
            var reportedProperties = new TwinCollection
            {
                [LightProperty] = this._currentState.ToString()
            };

            await Client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            if (LightBulbFromTwins(desiredProperties))
            {
                ReportCurrentLight().Wait();
            }

            return Task.CompletedTask;
        }

        private Task<MethodResponse> StartAlert(MethodRequest request, object userContext)
        {
            this._alertTimer = ThreadPoolTimer.CreatePeriodicTimer(AlertTimerTick, TimeSpan.FromMilliseconds(500));
            return Task.FromResult(new MethodResponse(200));
        }

        private void AlertTimerTick(ThreadPoolTimer timer)
        {
            LightBulb(this._currentState == TrafficLightState.Orange ? TrafficLightState.Red : TrafficLightState.Orange);
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

            if (this._alertTimer != null && this._alertTimer.Delay != TimeSpan.Zero)
            {
                this._alertTimer.Cancel();
                GetAndApplyDeviceTwins().Wait();

                return;
            }

            if (this._currentState == TrafficLightState.Red)
            {
                this.LightBulb(TrafficLightState.Off);
            }
            else
            {
                this.LightBulb(++this._currentState);
            }

            ReportCurrentLight().Wait();
        }

        private GpioPin CreateAndOpenPin(GpioController controller, int pinNumber, GpioPinDriveMode driveMode = GpioPinDriveMode.Output)
        {
            var gpioPin = controller.OpenPin(pinNumber);
            gpioPin.SetDriveMode(driveMode);

            return gpioPin;
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
            foreach (var pair in this._gpioLightPins)
            {
                pair.Value.Write(pair.Key == lightValue ? GpioPinValue.High : GpioPinValue.Low);
            }

            this._currentState = lightValue;
        }
    }
}
