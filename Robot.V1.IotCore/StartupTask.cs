using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using Windows.Devices.SerialCommunication;
using GHIElectronics.UWP.Shields;
using Windows.Networking.Sockets;
using System.Diagnostics;
using System.Threading;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace Robot.V1.IotCore
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private SerialDevice _port;
        private DataReader _sabertoothDataReader;
        private DataWriter _sabertoothDataWriter;
        private DatagramSocket _datagramSocket;
        private FEZUtility _fezUtility;
        private static bool _isCommandReady = true;
        private bool _isResetting = false;
        private static string _lastCommand = "";
        private static string _deviceStatus = "";
        private bool _isShuttingDown = false;
        private bool _inboundCommandReceived;
        private Timer _timer;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {

            _deferral = taskInstance.GetDeferral();

            await ResetSerialDeviceAsync();

            _datagramSocket = new DatagramSocket();
            var control = _datagramSocket.Control;
            control.DontFragment = true;
            control.QualityOfService = SocketQualityOfService.LowLatency;
            control.InboundBufferSizeInBytes = 1024;
            _datagramSocket.MessageReceived += _datagramSocket_MessageReceived;
            await _datagramSocket.BindServiceNameAsync("10000");


            _fezUtility = await FEZUtility.CreateAsync();
            _fezUtility.SetDigitalDriveMode(FEZUtility.DigitalPin.V00, Windows.Devices.Gpio.GpioPinDriveMode.Input);

            await Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var state = _fezUtility.ReadDigital(FEZUtility.DigitalPin.V00);
                    if (!state && !_isShuttingDown)
                    {
                        _isShuttingDown = true;
                        Windows.System.ShutdownManager.BeginShutdown(Windows.System.ShutdownKind.Shutdown, TimeSpan.FromMilliseconds(0));
                    }
                    await Task.Delay(1000);
                }
            }, TaskCreationOptions.LongRunning);

            _timer = new Timer(MonitorDeviceStatusCallack, null, 1500, 5000);

        }

        private async void MonitorDeviceStatusCallack(object state)
        {

            try
            {
                var d = _fezUtility.ReadDigital(FEZUtility.DigitalPin.V00);
                if (!d && !_isShuttingDown)
                {
                    _isShuttingDown = true;
                    Windows.System.ShutdownManager.BeginShutdown(Windows.System.ShutdownKind.Shutdown, TimeSpan.FromMilliseconds(0));
                }

                string command = $"M1: getc\r\n M2: getc\r\n M2: getb\r\n ";
                await WriteCommandAsync(command);
                var size = await _sabertoothDataReader.LoadAsync(256);
                _deviceStatus = _sabertoothDataReader.ReadString(size);
                Debug.WriteLine($"State: {d}");
            }
            catch (Exception)
            {

            }
        }

        private async void _datagramSocket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            _inboundCommandReceived = true;

            if (!_isResetting)
            {

                var reader = args.GetDataReader();
                string command = reader.ReadString(reader.UnconsumedBufferLength);
                if (!_lastCommand.Equals(command))
                {
                    await WriteCommandAsync(command);
                    _lastCommand = command;
                }

                Task.Run(async () =>
                {
                    if (!string.IsNullOrWhiteSpace(_deviceStatus))
                    {
                        using (var outputStream = await _datagramSocket.GetOutputStreamAsync(args.RemoteAddress, args.RemotePort))
                        {
                            using (var wr = new DataWriter(outputStream))
                            {
                                var bytes = System.Text.Encoding.UTF8.GetBytes(_deviceStatus);
                                wr.WriteBytes(bytes);
                                await wr.StoreAsync();
                            }
                        }
                    }
                });
            }
            _inboundCommandReceived = false;
        }

        private async Task ResetSerialDeviceAsync()
        {
            _isCommandReady = true;
            _isResetting = true;
            var serialString = Windows.Devices.SerialCommunication.SerialDevice.GetDeviceSelector();

            var devices = await DeviceInformation.FindAllAsync(serialString);

            _port = await SerialDevice.FromIdAsync(devices[0].Id);
            _port.BaudRate = 9600;
            _port.StopBits = SerialStopBitCount.One;
            _port.Parity = SerialParity.None;
            _port.WriteTimeout = TimeSpan.FromMilliseconds(30);
            _port.ReadTimeout = TimeSpan.FromMilliseconds(30);
            _sabertoothDataReader = new DataReader(_port.InputStream);
            _sabertoothDataWriter = new DataWriter(_port.OutputStream);


            string command = $"MD: 0{Environment.NewLine} MT: -512{Environment.NewLine}";
            await WriteCommandAsync(command);
            await Task.Delay(500);

            command = $"MT: 512{Environment.NewLine}";
            await WriteCommandAsync(command);
            await Task.Delay(500);

            command = $"MT: 0{Environment.NewLine}";
            await WriteCommandAsync(command);

            _isResetting = false;
        }

        private async Task WriteCommandAsync(string command)
        {
            if (!_isCommandReady)
                return;

            _isCommandReady = false;
            try
            {
                if (command.ToUpper() == "RESET")
                {
                    _sabertoothDataReader.Dispose();
                    _sabertoothDataWriter.Dispose();
                    _port.Dispose();
                    await ResetSerialDeviceAsync();
                }
                else if (command.ToUpper() == "RESTART")
                {
                    _sabertoothDataWriter.WriteString("M1: 0\r\n M2: 0\r\n");
                    var ret = await _sabertoothDataWriter.StoreAsync();
                    Windows.System.ShutdownManager.BeginShutdown(Windows.System.ShutdownKind.Restart, TimeSpan.FromMilliseconds(0));
                }
                else
                {
                    _sabertoothDataWriter.WriteString(command.ToUpper());
                    var ret = await _sabertoothDataWriter.StoreAsync();
                    await Task.Delay(30);
                }
            }
            catch (Exception ex)
            {
                if (_sabertoothDataWriter != null)
                {
                    command = $"M1: 0{Environment.NewLine} M2: 0{Environment.NewLine}";
                    _sabertoothDataWriter.WriteString(command.ToUpper() + Environment.NewLine);
                    await _sabertoothDataWriter.StoreAsync();
                }
            }
            finally
            {
                _isCommandReady = true;
            }

        }
    }
}
