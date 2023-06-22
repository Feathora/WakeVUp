using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using System.Diagnostics;
using Windows.UI.Xaml;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Media.Core;
using System.IO;
using Windows.Networking.PushNotifications;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using Windows.System.Threading;
using Microsoft.Devices.Tpm;
using Windows.Devices.I2c;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace WakeVUp
{
    public sealed class StartupTask : IBackgroundTask
    {
        private const int DOOR_SENSOR_PIN = 21;
        private const int ARDUINO_RESET_PIN = 20;

        private BackgroundTaskDeferral deferral;

        private GpioPin contactSensorPin;
        private GpioPin arduinoResetPin;

        private I2cDevice arduino;

        private MediaPlayer radioPlayer;

        private CancellationTokenSource cancellationTokenSource;

        private ThreadPoolTimer timeTimer;

        private HueBridge hueBridge;

        private AmbiencePlayer ambiencePlayer;

        private const string IOT_HUB_URI = "<iot_hub_uri>";
        private const string IOT_HUB_DEVICE_ID = "<iot_hub_device_id>";
        private const string IOT_HUB_DEVICE_KEY = "<iot_hub_device_key>";

        private readonly Dictionary<char, byte> VALUES = new Dictionary<char, byte>
        {
            { '0', 0b_1111_1100 },
            { '1', 0b_0110_0000 },
            { '2', 0b_1101_1010 },
            { '3', 0b_1111_0010 },
            { '4', 0b_0110_0110 },
            { '5', 0b_1011_0110 },
            { '6', 0b_1011_1110 },
            { '7', 0b_1110_0000 },
            { '8', 0b_1111_1110 },
            { '9', 0b_1111_0110 },
            { '-', 0b_0000_0010 },
            { ' ', 0b_0000_0000 }
        };

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            deferral = taskInstance.GetDeferral();

            var gpioController = await GpioController.GetDefaultAsync();

            contactSensorPin = gpioController.OpenPin(DOOR_SENSOR_PIN);
            contactSensorPin.SetDriveMode(GpioPinDriveMode.InputPullDown);
            contactSensorPin.DebounceTimeout = new TimeSpan(1000);
            contactSensorPin.ValueChanged += ContactSensorPin_ValueChanged;

            arduinoResetPin = gpioController.OpenPin(ARDUINO_RESET_PIN);
            arduinoResetPin.SetDriveMode(GpioPinDriveMode.Output);
            arduinoResetPin.Write(GpioPinValue.High);

            InitArduino();

            InitAzureClient();

            hueBridge = new HueBridge();

            ambiencePlayer = new AmbiencePlayer();
        }

        private async void InitArduino()
        {
            var settings = new I2cConnectionSettings(0x40);
            settings.BusSpeed = I2cBusSpeed.StandardMode;

            string aqs = I2cDevice.GetDeviceSelector("I2C1");
            var dis = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(aqs);

            arduino = await I2cDevice.FromIdAsync(dis[0].Id, settings);

            await ResetArduino();
            await SendTime(DateTime.Now);

            InitTimeTimer();
        }

        private async Task ResetArduino()
        {
            arduinoResetPin.Write(GpioPinValue.Low);

            await Task.Delay(100);

            arduinoResetPin.Write(GpioPinValue.High);

            await Task.Delay(TimeSpan.FromSeconds(1.0));
        }

        private void InitTimeTimer()
        {
            timeTimer = ThreadPoolTimer.CreatePeriodicTimer(SendTimeTick, TimeSpan.FromMinutes(1.0));
        }

        private async void InitAzureClient()
        {
            DeviceClient deviceClient = DeviceClient.Create(IOT_HUB_URI, AuthenticationMethodFactory.CreateAuthenticationWithRegistrySymmetricKey(IOT_HUB_DEVICE_ID, IOT_HUB_DEVICE_KEY), TransportType.Amqp);
            
            while (true)
            {
                try
                {
                    Message receivedMessage = await deviceClient.ReceiveAsync();
                    if (receivedMessage == null) continue;

                    var dataType = new { CMD = "", AlarmTime = "", VideoUrl = "" };

                    var data = JsonConvert.DeserializeAnonymousType(Encoding.ASCII.GetString(receivedMessage.GetBytes()), dataType);

                    if(data.CMD == "SetAlarm")
                    {
                        TimeSpan time = TimeSpan.Parse(data.AlarmTime);
                        var alarmTime = DateTime.Today + time;

                        if (alarmTime < DateTime.Now) alarmTime = alarmTime.AddDays(1);

                        SoundRadio(alarmTime);

                        await deviceClient.CompleteAsync(receivedMessage);
                    }
                    else if(data.CMD == "StartAmbience")
                    {
                        await ambiencePlayer.StartAmbience(data.VideoUrl);
                    }
                    else if(data.CMD == "StopAmbience")
                    {
                        ambiencePlayer.StopAmbience();
                    }
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
            }
        }

        private void ContactSensorPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if(args.Edge == GpioPinEdge.FallingEdge)
            {
                radioPlayer?.Pause();

                hueBridge.BedroomDoorOpened();
            }
            else
            {
                hueBridge.BedroomDoorClosed();
            }
        }

        private async void SoundRadio(DateTime time)
        {
            cancellationTokenSource?.Cancel();

            cancellationTokenSource = new CancellationTokenSource();

            SendTime(DateTime.Now);

            try
            {
                await Task.Delay(time - DateTime.Now, cancellationTokenSource.Token);
            }
            catch(TaskCanceledException)
            {
                Debug.WriteLine("Cancel requested");
                return;
            }

            cancellationTokenSource = null;

            radioPlayer = new MediaPlayer();
            radioPlayer.Source = MediaSource.CreateFromUri(new Uri("http://playerservices.streamtheworld.com/api/livestream-redirect/SRGSTR11.mp3"));
            radioPlayer.Volume = .05;
            radioPlayer.Play();

            hueBridge.StartWakeUp();

            while(radioPlayer.Volume < 1.0)
            {
                radioPlayer.Volume += .05;
                await Task.Delay(TimeSpan.FromMinutes(.25));
            }
        }

        private async void SendTimeTick(ThreadPoolTimer timer)
        {
            var now = DateTime.Now;
            await SendTime(now);
        }

        private async Task SendTime(DateTime time)
        {
            string str = $"{time.Hour:00}{time.Minute:00}";
            await SendTimeString(str);
        }

        private async Task SendTimeString(string str)
        {
            var data = new byte[str.Length];
            for (int i = 0; i < data.Length; ++i)
            {
                data[i] = VALUES[str[i]];
            }

            data[1] |= 1;
            if(cancellationTokenSource != null) data[3] |= 1;

            try
            {
                arduino.Write(data);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);

                await ResetArduino();
                await SendTime(DateTime.Now);
            }
        }
    }
}
