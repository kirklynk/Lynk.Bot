using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using System.Collections.ObjectModel;
using Android.Support.V7.App;

namespace Lynk.Bot.Bluetooth.LE.Droid
{
    [Activity(Label = "FindDevicesActivity", ScreenOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape)]
    public class FindDevicesActivity : Activity
    {
        private BluetoothManager _bluetoothManager;
        private BluetoothAdapter _bluetoothAdapter;
        private BluetoothLeScanCallback _bleScanCallback;

        private bool _isScanning;
        private Handler _handler;
        private ObservableCollection<BluetoothDevice> _bluetoothLeDevices = new ObservableCollection<BluetoothDevice>();

        private ProgressBar scanningIndicatorProgressBar;
        private Button btnBluetoothLeDevices;
        private ListView listview;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.FindDevicesActivityLayout);

            // Create your application here
            _bluetoothManager = (BluetoothManager)GetSystemService(Context.BluetoothService);
            _bluetoothAdapter = _bluetoothManager.Adapter;
            _handler = new Handler();
            _bleScanCallback = new BluetoothLeScanCallback(_bluetoothLeDevices);

            listview = (ListView)FindViewById(Resource.Id.bleDevicesFoundlistView);
            listview.Adapter = new BluetoothLeDeviceScanListAdapter(this, _bluetoothLeDevices);
            listview.ItemClick += (s, e) =>
            {
                if (_isScanning)
                    _bluetoothAdapter.BluetoothLeScanner.StopScan(_bleScanCallback);

                var intent = new Intent();
                intent.PutExtra("address", _bluetoothLeDevices[(int)e.Id].Address);
                SetResult(Result.Ok, intent);
                Finish();
            };

            btnBluetoothLeDevices = (Button)FindViewById(Resource.Id.btnBluetoothLeDevices);
            btnBluetoothLeDevices.Click += BtnBluetoothLeDevices_Click;

            scanningIndicatorProgressBar = FindViewById<ProgressBar>(Resource.Id.scanningIndicatorProgressBar);
        }


        private void BtnBluetoothLeDevices_Click(object sender, EventArgs e)
        {
            _bluetoothLeDevices.Clear();
            _handler.PostDelayed(() =>
            {
                if (_isScanning)
                {
                    _bluetoothAdapter.BluetoothLeScanner.StopScan(_bleScanCallback);
                    btnBluetoothLeDevices.Enabled = true;
                    Toast.MakeText(this, "Scan completed", ToastLength.Short).Show();
                    _isScanning = false;
                    scanningIndicatorProgressBar.Visibility = ViewStates.Invisible;
                }
            }, 15000); //Stop scanning after 15 sec 

            _isScanning = true;
            _bluetoothAdapter.BluetoothLeScanner.StartScan(_bleScanCallback);
            btnBluetoothLeDevices.Enabled = false;
            scanningIndicatorProgressBar.Visibility = ViewStates.Visible;
            Toast.MakeText(this, "Scan started", ToastLength.Short).Show();
        }
    }
    public class BluetoothLeDeviceScanListAdapter : BaseAdapter<BluetoothDevice>
    {
        private Activity _context;
        private ObservableCollection<BluetoothDevice> _deviceCollection;

        public BluetoothLeDeviceScanListAdapter(Activity context, ObservableCollection<BluetoothDevice> deviceCollection)
        {
            _deviceCollection = deviceCollection;
            _deviceCollection.CollectionChanged += (s, e) =>
            {
                this.NotifyDataSetChanged();
            };
            _context = context;
        }
        public override BluetoothDevice this[int position]
        {
            get
            {
                return _deviceCollection[position];
            }
        }

        public override int Count
        {
            get
            {
                return _deviceCollection.Count;
            }
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) // otherwise create a new one
                view = _context.LayoutInflater.Inflate(Android.Resource.Layout.SimpleListItem2, null);
            view.FindViewById<TextView>(Android.Resource.Id.Text1).Text = _deviceCollection[position].Name;
            view.FindViewById<TextView>(Android.Resource.Id.Text2).Text = _deviceCollection[position].Address;

            return view;
        }
    }
    public class BluetoothLeScanCallback : ScanCallback
    {
        private ObservableCollection<BluetoothDevice> _deviceCollection;
        public BluetoothLeScanCallback(ObservableCollection<BluetoothDevice> deviceCollection)
        {
            _deviceCollection = deviceCollection;
        }

        public override void OnScanFailed([GeneratedEnum] ScanFailure errorCode)
        {
            base.OnScanFailed(errorCode);
        }

        public override void OnScanResult([GeneratedEnum] ScanCallbackType callbackType, ScanResult result)
        {
            var device = _deviceCollection.Where(x => x.Address == result.Device.Address).FirstOrDefault();
            if (device == null)
                _deviceCollection.Add(result.Device);
        }
        public override void OnBatchScanResults(IList<ScanResult> results)
        {
            base.OnBatchScanResults(results);
        }
    }
}