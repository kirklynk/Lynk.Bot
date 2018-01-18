using GHIElectronics.TinyCLR.Devices.Gpio;
using GHIElectronics.TinyCLR.Devices.SerialCommunication;
using GHIElectronics.TinyCLR.Pins;
using GHIElectronics.TinyCLR.Storage.Streams;
using System;
using System.Collections;
using System.Text;
using System.Threading;

namespace Lynk.Robot._0_7.Esp8266
{
    class Program
    {
        static DataReader _dataReader;
        static DataWriter _dataWriter;
        static GpioPin CH_PD;
        static void Main()
        {
            GpioController gpioController = GpioController.GetDefault();
            CH_PD = gpioController.OpenPin(FEZPandaIII.GpioPin.D21);
            CH_PD.SetDriveMode(GpioPinDriveMode.Output);
            CH_PD.Write(GpioPinValue.High);
            SerialDevice esp8266 = SerialDevice.FromId(FEZPandaIII.UartPort.Usart4);
            esp8266.BaudRate = 115200;
            esp8266.DataBits = 8;
            esp8266.StopBits = SerialStopBitCount.One;
            esp8266.Parity = SerialParity.None;
            esp8266.ReadTimeout = TimeSpan.FromMilliseconds(1000);
            esp8266.WriteTimeout = TimeSpan.FromMilliseconds(1000);

            _dataReader = new DataReader(esp8266.InputStream);
            _dataWriter = new DataWriter(esp8266.OutputStream);

            WriteCommand("AT");
            WriteCommand("AT+CWMODE_DEF=2");
            PrintCommandResponse();
            WriteCommand("AT+CWMODE_CUR=2");
            PrintCommandResponse();
            WriteCommand("AT+CWSAP_CUR=\"LYNK-BOT\",\"luck1234\",11,3");
            PrintCommandResponse();
            WriteCommand("AT+CWSAP_DEF=\"LYNK-BOT\",\"luck1234\",11,3");
            PrintCommandResponse();
            WriteCommand("AT+CIPMUX=1");
            PrintCommandResponse();
            WriteCommand("AT+CIPSERVER=1,5555");
            PrintCommandResponse();
            WriteCommand("AT+CIPSTO=30");
            PrintCommandResponse();
            new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        var len = _dataReader.Load(1024);
                        if (len != 0)
                        {
                            var read = _dataReader.ReadString(len);
                            read = read.Trim();
                            var commands = read.Split('\n');
                            for (int i = 0; i < commands.Length; i++)
                            {
                                var command = commands[i];
                                char[] chars = command.ToCharArray();
                                StringBuilder sb = new StringBuilder();
                                for (int c = 0; c < chars.Length; c++)
                                {
                                    if (chars[c] is '\r')
                                        continue;
                                    sb.Append(chars[c]);
                                }
                                command = sb.ToString();
                                if (command.Trim() == string.Empty)
                                    continue;

                                var splits = command.Split(',');
                                if (splits.Length == 3)
                                {
                                    string val = splits[2].Split(':')[1];
                                    if (val.Trim() == string.Empty)
                                        continue;
                                    command = $"you entered: {val}{Environment.NewLine}";
                                    System.Diagnostics.Debug.WriteLine(command);
                                    // WriteData(command, splits[1], command.ToCharArray().Length);
                                    // PrintCommandResponse();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }
            }).Start();
            Thread.Sleep(Timeout.Infinite);
        }

        static void WriteCommand(string command)
        {
            _dataWriter.WriteString(value: command + Environment.NewLine);
            var i = _dataWriter.Store();
        }

        static void WriteData(string data, string id, int len)
        {
            WriteCommand($"AT+CIPSEND={id},{len}");
            // PrintCommandResponse();
            Thread.Sleep(20);
            _dataWriter.WriteString(value: data);
            uint i = _dataWriter.Store();

        }

        static void PrintCommandResponse()
        {

            var length = _dataReader.Load(1024);
            var read = _dataReader.ReadString(length);
            System.Diagnostics.Debug.WriteLine(read);
        }
    }
}
