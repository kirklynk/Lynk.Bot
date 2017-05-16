using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Support.V7.App;
using Android.Database;
using System.Collections.Generic;
//using Java.Lang;

namespace Lynk.Bot.Bluetooth.LE.Droid
{
    [Activity(Label = "Lynk Bot Bluetooth", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape)]
    public class MainActivity : Activity, SeekBar.IOnSeekBarChangeListener, CompoundButton.IOnCheckedChangeListener
    {

        private BluetoothManager _bluetoothManager;
        private BluetoothAdapter _bluetoothAdapter;
        private BluetoothDevice _device;
        private BluetoothLeGattCallback _bluetoothLeGattCallback;
        private BluetoothGatt _bluetoothGATT;

        private AccelerometerSensorService _accelerometerSensorService;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private Button findDevicesButton;
        private Switch startStopToggleButton;
        private SeekBar accelerationSlider;
        private ToggleButton flipDriveDirectionSwitch;
        private ProgressBar accelerationIndicatorProgressBar;
        private ProgressBar connectingIndicatorProgressBar;
        private TextView accelerationIndicatorTextView;
        private TextView tiltAngleTextView;
        private TextView lynkBotStatusTextView;
        private Spinner speedLimitSpinner;
        private TextView objectCollisionTextView;
        private TextView batteryStatusTextView;

        private bool _isRunning = false;
        private bool _isTurningEngaged = false;
        private bool _isDirectionFlipped = false;
        private bool _shouldCleanUp = true;
        private double _driveValue = 0.0;
        private double _turnValue = 0.0;
        private int _speedTarget = 1000;

        private Handler _handler;

        private const int FIND_DEVICE_CODE = 100001;
        private const int ENABLE_BLUETOOTH_REQUEST_CODE = 100002;

        public const string UUID_SERVICE = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";
        public const string UUID_RX = "6e400003-b5a3-f393-e0a9-e50e24dcca9e";
        public const string UUID_TX = "6e400002-b5a3-f393-e0a9-e50e24dcca9e";
        public const string UUID_DFU = "00001530-1212-EFDE-1523-785FEABCD123";
        public const string CHARACTERISTIC_CONFIG = "00002902-0000-1000-8000-00805f9b34fb";


        protected async override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.MainActivityLayout);

            _handler = new Handler();

            // Set our view from the "main" layout resource
            findDevicesButton = FindViewById<Button>(Resource.Id.findDevicesButton);
            findDevicesButton.Visibility = ViewStates.Visible;


            startStopToggleButton = FindViewById<Switch>(Resource.Id.startStopToggleButton);
            startStopToggleButton.SetOnCheckedChangeListener(this);
            startStopToggleButton.Enabled = false;

            accelerationSlider = FindViewById<SeekBar>(Resource.Id.accelerationSlider);
            accelerationSlider.SetOnSeekBarChangeListener(this);
            accelerationSlider.Enabled = false;

            accelerationIndicatorProgressBar = FindViewById<ProgressBar>(Resource.Id.accelerationIndicatorProgressBar);
            accelerationIndicatorTextView = FindViewById<TextView>(Resource.Id.accelerationIndicatorTextView);
            connectingIndicatorProgressBar = FindViewById<ProgressBar>(Resource.Id.connectingIndicator);

            speedLimitSpinner = FindViewById<Spinner>(Resource.Id.speedLimitSpinner);
            ArrayAdapter adapter = ArrayAdapter.CreateFromResource(this, Resource.Array.speeds_array, Android.Resource.Layout.SimpleSpinnerItem);
            adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
            speedLimitSpinner.Adapter = adapter;
            speedLimitSpinner.Enabled = false;
            speedLimitSpinner.ItemSelected += SpeedLimitSpinner_ItemSelected;

            flipDriveDirectionSwitch = FindViewById<ToggleButton>(Resource.Id.flipDriveDirectionSwitch);
            flipDriveDirectionSwitch.SetOnCheckedChangeListener(this);


            tiltAngleTextView = FindViewById<TextView>(Resource.Id.tiltAngleTextView);
            lynkBotStatusTextView = FindViewById<TextView>(Resource.Id.lynkBotStatusTextView);
            objectCollisionTextView = FindViewById<TextView>(Resource.Id.objectCollisionTextView);
            batteryStatusTextView = FindViewById<TextView>(Resource.Id.batteryStatusTextView);

            await ContinuouslySendRobotMovementCommandAsync(_cancellationTokenSource);
        }

        private void SpeedLimitSpinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            var speedLevel = (string)speedLimitSpinner.SelectedItem;
            switch (speedLevel?.ToUpper())
            {
                default:
                case "LOW":
                    _speedTarget = 1000;
                    break;
                case "MED":
                    _speedTarget = 1500;
                    break;
                case "HIGH":
                    _speedTarget = 2100;
                    break;
            }
        }


        protected override void OnResume()
        {
            base.OnResume();

            _shouldCleanUp = true;
            _bluetoothManager = (BluetoothManager)GetSystemService(Context.BluetoothService);
            _bluetoothAdapter = _bluetoothManager.Adapter;
            _bluetoothLeGattCallback = new BluetoothLeGattCallback(this);
            _bluetoothLeGattCallback.CommunicationReadyStateChanged += (s, state) =>
            {
                this.RunOnUiThread(() =>
                {
                    if (state)
                    {
                        startStopToggleButton.Enabled = true;
                        connectingIndicatorProgressBar.Visibility = ViewStates.Gone;
                    }
                });
            };
            _bluetoothLeGattCallback.ReadIncomingResultCallback = (value) =>
            {
                RunOnUiThread(() =>
                {
                    if (value.ToUpper().StartsWith("M"))
                        batteryStatusTextView.SetText(value, TextView.BufferType.Normal);
                    else if (value.ToUpper().StartsWith("D"))
                        objectCollisionTextView.SetText(value, TextView.BufferType.Normal);
                    else
                        lynkBotStatusTextView.SetText(value, TextView.BufferType.Normal);
                });
            };


            _accelerometerSensorService = new AccelerometerSensorService();
            _accelerometerSensorService.GetAccelerometerReadingCallback = (x, y, z) =>
            {
                _turnValue = x;
            };

            if (_bluetoothAdapter != null && _bluetoothAdapter.IsEnabled)
            {
                var address = GetSharedPreferences("app", FileCreationMode.Private).GetString("address", null);
                if (!string.IsNullOrWhiteSpace(address))
                {
                    ConnectToRemoteBluetoothLeDeviceAndService(address);
                }

            }
            else
            {
                var bleIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                StartActivityForResult(bleIntent, ENABLE_BLUETOOTH_REQUEST_CODE);
                _shouldCleanUp = false;
            }
            findDevicesButton.Click += startScanButton_Click;
        }

        protected override void OnPause()
        {
            base.OnPause();
            _isRunning = false;

            _handler.PostDelayed(() =>
            {

                startStopToggleButton.Enabled = false;
                startStopToggleButton.Checked = false;
                _bluetoothLeGattCallback?.StopNotification(_bluetoothGATT);
                _bluetoothGATT?.Disconnect();

                findDevicesButton.Click -= startScanButton_Click;

                //await Task.Delay(1000);
                if (_shouldCleanUp)
                {
                    _bluetoothLeGattCallback?.Dispose();
                    _bluetoothLeGattCallback = null;
                    _bluetoothGATT?.Disconnect();
                    _bluetoothGATT?.Close();
                    _bluetoothGATT?.Dispose();
                    _bluetoothGATT = null;
                    _bluetoothManager?.Dispose();
                    _bluetoothManager = null;
                    _bluetoothAdapter?.Dispose();
                    _bluetoothAdapter = null;
                    _device?.Dispose();
                    _device = null;
                }
            }, 200);

        }

        private void startScanButton_Click(object sender, EventArgs e)
        {
            _shouldCleanUp = false;
            Intent intent = new Intent(this, typeof(FindDevicesActivity));

            this.StartActivityForResult(intent, FIND_DEVICE_CODE);


        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            switch (requestCode)
            {
                case ENABLE_BLUETOOTH_REQUEST_CODE:
                    if (resultCode == Result.Canceled)
                    {
                        Finish();
                        Toast.MakeText(this, "Please enable Bluetooth", ToastLength.Short).Show();
                        return;
                    }
                    else
                    {

                    }
                    break;
                case FIND_DEVICE_CODE:
                    _shouldCleanUp = true;
                    if (data != null)
                    {
                        var address = data.GetStringExtra("address");
                        var preferences = Application.GetSharedPreferences("app", FileCreationMode.Private);
                        var editor = preferences.Edit();
                        editor.PutString("address", address);
                        editor.Commit();
                        ConnectToRemoteBluetoothLeDeviceAndService(address);
                    }
                    else
                    {
                        Toast.MakeText(this, "No device selected", ToastLength.Short).Show();
                    }
                    break;
                default:
                    break;
            }

        }

        private void ConnectToRemoteBluetoothLeDeviceAndService(string address)
        {
            _device = _bluetoothAdapter.GetRemoteDevice(address);
            lynkBotStatusTextView.SetText("Connecting", TextView.BufferType.Normal);
            if (_device == null)
            {
                //Todo: give feedback here
                lynkBotStatusTextView.SetText("Not Connected", TextView.BufferType.Normal);
                return;
            }
            _bluetoothGATT = _device.ConnectGatt(this, true, _bluetoothLeGattCallback, BluetoothTransports.Le);
        }

        public void OnProgressChanged(SeekBar seekBar, int progress, bool fromUser)
        {
            _driveValue = progress;
            accelerationIndicatorProgressBar.SetProgress(progress, true);
            accelerationIndicatorTextView.SetText(progress.ToString(), TextView.BufferType.Normal);
        }

        public void OnStartTrackingTouch(SeekBar seekBar) { }

        public void OnStopTrackingTouch(SeekBar seekBar) { }

        public void OnCheckedChanged(CompoundButton buttonView, bool isChecked)
        {
            if (buttonView.Id == Resource.Id.startStopToggleButton)
            {
                accelerationSlider.SetProgress(0, true);
                if (!isChecked)
                {
                    _isRunning = false;
                    _handler.PostDelayed(() =>
                    {
                        findDevicesButton.Visibility = ViewStates.Visible;
                        accelerationSlider.Enabled = false;
                        speedLimitSpinner.Enabled = false;
                        buttonView.SetText("Stopped", TextView.BufferType.Normal);
                        _accelerometerSensorService.Stop();
                    }, 500);
                }
                else
                {
                    _accelerometerSensorService.Start();
                    buttonView.SetText("Running", TextView.BufferType.Normal);
                    accelerationSlider.Enabled = true;
                    speedLimitSpinner.Enabled = true;
                    findDevicesButton.Visibility = ViewStates.Gone;
                }
                _isRunning = isChecked;
                return;
            }
            else if (buttonView.Id == Resource.Id.flipDriveDirectionSwitch)
            {
                _isDirectionFlipped = !_isDirectionFlipped;
            }
        }

        private Task ContinuouslySendRobotMovementCommandAsync(CancellationTokenSource token)
        {
            return Task.Factory.StartNew(async () =>
            {
                string lastCommand = string.Empty;

                while (!token.IsCancellationRequested)
                {
                    try
                    {

                        var turn = _turnValue;
                        var drive = _driveValue;
                        if (turn < -5.0 || turn > 5)
                        {
                            if (turn < -5.0)
                            {
                                if (turn < -45)
                                    turn = -45;
                                turn += 5;
                            }
                            else
                            {
                                if (turn > 45)
                                    turn = 45;
                                turn -= 5;
                            }
                        }
                        else
                        {
                            turn = 0.0;
                        }

                        turn = (turn / 40) * _speedTarget;
                        drive = (drive / 100) * _speedTarget;

                        if (!_isRunning)
                        {
                            turn = 0.0;
                            drive = 0.0;
                        }

                        RunOnUiThread(() =>
                        {
                            tiltAngleTextView.SetText($"{(int)turn}", TextView.BufferType.Normal);
                        });

                        if (_isDirectionFlipped)
                            drive *= -1;

                        string command = $"{(int)drive}:{(int)turn}|";

                        //only send command as necessary
                        if (!lastCommand.Equals(command) || !_isRunning)
                        {
                            lastCommand = command;

                            byte[] bytesToSend = System.Text.Encoding.UTF8.GetBytes(command);

                            if (_bluetoothLeGattCallback.IsConnected && !_bluetoothLeGattCallback.IsWritingData)
                                _bluetoothLeGattCallback.WriteDataToDevice(_bluetoothGATT, bytesToSend);
                        }
                    }
                    catch (Exception)
                    {

                    }
                    await Task.Delay(20);
                }
            }, token.Token,
            TaskCreationOptions.LongRunning, TaskScheduler.Current);
        }
    }
}
