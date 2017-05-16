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
using Android.Hardware;

namespace Lynk.Bot.Bluetooth.LE.Droid
{
    public class AccelerometerSensorService : Java.Lang.Object, ISensorEventListener
    {

        private static object _lock = new object();
        private Sensor _sensor;
        SensorManager _sensorManager;
        /// <summary>
        /// Method to invoke when Accelerometer data is available. XYZ data output
        /// </summary>
        public Action<double, double, double> GetAccelerometerReadingCallback;

        public AccelerometerSensorService()
        {
            _sensorManager = (SensorManager)Application.Context.GetSystemService(Context.SensorService);
            _sensor = _sensorManager.GetDefaultSensor(SensorType.RotationVector);

        }

        public void OnAccuracyChanged(Sensor sensor, [GeneratedEnum] SensorStatus accuracy) { }

        public void OnSensorChanged(SensorEvent e)
        {
            lock (_lock)
            {
                float[] vals = new float[9];
                SensorManager.GetRotationMatrixFromVector(vals, e.Values.ToArray());
                float[] r = new float[3];
                var r1 = SensorManager.GetOrientation(vals, r);
                var z = ConvertRadiansToDegrees(r1[0]);
                var x = ConvertRadiansToDegrees(r1[1]);
                var y = ConvertRadiansToDegrees(r1[2]);
                //SensorData = new double[] { x, y, z };
                //ReadingX = x;
                //ReadingY = y;
                //ReadingZ = z;
                GetAccelerometerReadingCallback?.Invoke(x, y, z);
            }
        }

        double ConvertRadiansToDegrees(float radian)
        {
            var degree = Math.Round((radian * 180F) / Math.PI, 1);
            return degree;
        }

        public void Start()
        {
            _sensorManager.RegisterListener(this, _sensor, SensorDelay.Game);
        }

        public void Stop()
        {
            _sensorManager.UnregisterListener(this, _sensor);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _sensorManager.UnregisterListener(this, _sensor);
        }
    }
}