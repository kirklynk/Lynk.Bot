using Android.App;
using Android.Bluetooth;
using Android.Runtime;
using Java.Util;
using Lynk.Bot.Bluetooth.LE.Droid;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

public class BluetoothLeGattCallback : BluetoothGattCallback
{
    private Activity _context;
    private BluetoothGattService _bluetoothGattService;
    private BluetoothGattCharacteristic _bluetoothGattCharacteristic_RX;
    private AutoResetEvent _autoResetEvent = new AutoResetEvent(false);


    public bool IsConnected { get; set; } = false;
    public bool IsWritingData { get; set; } = false;

    public Action<string> ReadIncomingResultCallback;
    public EventHandler<bool> CommunicationReadyStateChanged;

    public BluetoothLeGattCallback(Activity context) { _context = context; }

    public BluetoothLeGattCallback(IntPtr ptr, JniHandleOwnership owner) : base(ptr, owner) { }

    public void WriteDataToDevice(BluetoothGatt gatt, byte[] data)
    {
        IsWritingData = true;
        var charac = _bluetoothGattService.GetCharacteristic(UUID.FromString(MainActivity.UUID_TX));
        charac.WriteType = GattWriteType.Default;
        charac.SetValue(data);
        gatt.WriteCharacteristic(charac);
        // var f = service.GetCharacteristic(UUID.FromString(MainActivity.UUID_TX));

    }

    public void StopNotification(BluetoothGatt gatt)
    {

        if (_bluetoothGattCharacteristic_RX == null)
            return;

        IsConnected = false;

        var config = _bluetoothGattCharacteristic_RX.GetDescriptor(UUID.FromString(MainActivity.CHARACTERISTIC_CONFIG));
        gatt.SetCharacteristicNotification(_bluetoothGattCharacteristic_RX, false);

        //Remove remote from sending nofications
        config.SetValue(BluetoothGattDescriptor.DisableNotificationValue.ToArray());
        gatt.WriteDescriptor(config);
    }

    public override void OnServicesDiscovered(BluetoothGatt gatt, [GeneratedEnum] GattStatus status)
    {
        if (status == GattStatus.Success)
        {
            try
            {
                //Gets the primary service from the Adafruit Bluefruit Le device;
                _bluetoothGattService = gatt.GetService(UUID.FromString(MainActivity.UUID_SERVICE));
                _bluetoothGattCharacteristic_RX = _bluetoothGattService.GetCharacteristic(UUID.FromString(MainActivity.UUID_RX));
                var config = _bluetoothGattCharacteristic_RX.GetDescriptor(UUID.FromString(MainActivity.CHARACTERISTIC_CONFIG));

                //Register the local notifications
                gatt.SetCharacteristicNotification(_bluetoothGattCharacteristic_RX, true);

                //Register remote to send nofications
                config.SetValue(BluetoothGattDescriptor.EnableNotificationValue.ToArray());
                gatt.WriteDescriptor(config);

            }
            catch (Exception)
            {
                //TODO: Do something with here
            }

        }
    }
    string incoming = "";
    public override void OnCharacteristicChanged(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic)
    {
        if (!IsConnected)
        {
            CommunicationReadyStateChanged?.Invoke(this, true);
            IsConnected = true;
        }
        //Todo: read value or raise notification
        var readBytes = characteristic.GetValue();
        char[] inChars = System.Text.Encoding.UTF8.GetChars(readBytes);

        var sb = new StringBuilder(incoming);
        for (int i = 0; i < inChars.Length; i++)
        {
            char c = inChars[i];
            if (c == '|')
            {
              
                incoming = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(incoming))
                {
                    ReadIncomingResultCallback?.Invoke(incoming);
                   incoming = string.Empty;
                    sb = sb.Clear();
                }
            }
            else {
                sb.Append(c);
            }
        }
        incoming = sb.ToString();
    
    }

    public override void OnConnectionStateChange(BluetoothGatt gatt, [GeneratedEnum] GattStatus status, [GeneratedEnum] ProfileState newState)
    {
        //Listens if the connection was successful.  If it is, then start discovery service.
        switch (newState)
        {
            case ProfileState.Connected:
                gatt.DiscoverServices();
                break;
            case ProfileState.Connecting:
            case ProfileState.Disconnected:
            case ProfileState.Disconnecting:
            default:
                IsConnected = false;
                CommunicationReadyStateChanged?.Invoke(this, false);
                break;
        }
    }
    public override void OnCharacteristicWrite(BluetoothGatt gatt, BluetoothGattCharacteristic characteristic, [GeneratedEnum] GattStatus status)
    {
        IsWritingData = false;
        base.OnCharacteristicWrite(gatt, characteristic, status);
    }
}