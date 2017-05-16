using GHIElectronics.TinyCLR.Devices.Adc;
using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.SerialCommunication;
using GHIElectronics.TinyCLR.Storage.Streams;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Robot.V1.FEZ
{
    public class Program
    {
        private SerialDevice _sabertooth;
        private SerialDevice _bluetooth;
        private GpioController _gpioController;
        private GpioPin _led;
        private AdcController _adcController;
        private AdcChannel _irSensor_A0;
        private int _drive = 0;
        private int _turn;
        private int _distance;

        private object _lock = new object();


        public Program()
        {
            _sabertooth = SerialDevice.FromId("COM1");
            _bluetooth = SerialDevice.FromId("COM2");
            _gpioController = GpioController.GetDefault();

            _led = _gpioController.OpenPin(GHIElectronics.TinyCLR.Pins.FEZPandaIII.GpioPin.Led1);
            _led.SetDriveMode(GpioPinDriveMode.Output);

            _adcController = AdcController.GetDefault();
            _adcController.ChannelMode = AdcChannelMode.SingleEnded;

            _irSensor_A0 = _adcController.OpenChannel(GHIElectronics.TinyCLR.Pins.FEZPandaIII.AdcChannel.A0);


        }


        public static void Main()
        {

            new Program().Run();

            Thread.Sleep(Timeout.Infinite);
        }

        private void Run()
        {
            new Thread(() =>
            {
                /*
                 * Continuously monitors incoming data from the sabertooth and write it out to the connected remote
                 */
                while (true)
                {
                    try
                    {
                        string output = "no data";

                        //Execute the get battery voltage of the sabertooth motor driver
                        string command = $"M2: getb\r\n";
                        WriteCommand(command);

                        var buffer = new Buffer(64);
                        if (_sabertooth.InputStream.Read(buffer, buffer.Length, InputStreamOptions.None) > 0)
                        {
                            var sb = new StringBuilder();
                            char[] chars = Encoding.UTF8.GetChars(buffer.Data);
                            for (int i = 0; i < chars.Length; i++)
                            {
                                sb.Append(chars[i]);
                            }
                            Debug.WriteLine(sb.ToString());
                            output = sb.ToString();
                        }

                        //Send result to remote bluetooth device
                        output = $"{output}|";
                        var bytes = System.Text.Encoding.UTF8.GetBytes(output);
                        buffer = new Buffer(bytes);
                        _bluetooth.OutputStream.Write(buffer);
                        Thread.Sleep(10);

                        output = $"D{_distance}|";
                        bytes = System.Text.Encoding.UTF8.GetBytes(output);
                        buffer = new Buffer(bytes);
                        _bluetooth.OutputStream.Write(buffer);
                        Thread.Sleep(10);

                        //Read led status and send it to remote
                        _led.Write(_led.Read() == GpioPinValue.Low ? GpioPinValue.High : GpioPinValue.Low);
                        bytes = System.Text.Encoding.UTF8.GetBytes(_led.Read() == GpioPinValue.Low ? "ON|" : "OFF|");
                        buffer = new Buffer(bytes);
                        _bluetooth.OutputStream.Write(buffer);
                        Thread.Sleep(1000);
                    }
                    catch (Exception)
                    {

                        //Prevents loop from ending prematurely
                    }
                }
            }).Start();

            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        bool isReading = true;

                        //read incoming data from the bluefruit le until '|' delimiter is reached
                        while (isReading)
                        {
                            var readBuffer = new Buffer(20);
                            if (_bluetooth.InputStream.Read(readBuffer, readBuffer.Capacity, InputStreamOptions.None) > 0)
                            {
                                char[] chars = Encoding.UTF8.GetChars(readBuffer.Data);

                                for (int i = 0; i < chars.Length; i++)
                                {
                                    if (chars[i] == '|')
                                    {
                                        //breaking out of the loop
                                        isReading = false;
                                        break;
                                    }
                                    sb.Append(chars[i]);
                                }
                            }
                        }
                        var val = sb.ToString();
                        var splits = val.Split(new char[] { ':' });
                        _drive = int.Parse(splits[0]) * -1;
                        _turn = int.Parse(splits[1]);

                    }
                    catch (Exception)
                    {
                        //Prevents loop from ending prematurely
                    }
                }
            }).Start();

            new Thread(() =>
            {
                int close = 0;
                while (true)
                {
                    try
                    {
                        var irReading = _irSensor_A0.ReadValue();
                        var drive = _drive;
                    
                        //if (close >= 750 && irReading >= 750 && _drive < -1000)
                        //{
                        //    drive = 0;
                   
                        //}
                        //else if (close >= 1000 && irReading >= 1000 && _drive < -512 && _drive >= -1000)
                        //{
                        //    drive = 0;
                        //                 }
                        //else if (close >= 1300 && irReading >= 1300 && _drive < 0 && _drive >= -512)
                        //{
                        //    drive = 0;
                       
                        //}

                        close = irReading;
                        _distance = close;

                        Debug.WriteLine($"{_distance} | {drive}");
                        string command = $"MD: { drive }\r\n MT: {_turn}\r\n";
                        WriteCommand(command);
                    
                    }
                    catch (Exception)
                    {
                        //Prevents loop from ending prematurely
                    }
                    Thread.Sleep(10);
                }
            }).Start();

        }

        public void WriteCommand(string command)
        {
            lock (_lock)
            {
                _sabertooth.OutputStream.Write(new Buffer(Encoding.UTF8.GetBytes(command)));
            }
        }

    }
}
