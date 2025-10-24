using System;
using UsbSerialForAndroid.Net.Drivers;

namespace UsbSerialForAndroid.Net.Modbus
{
    public class ModbusClient : ModbusClientBase
    {
        private readonly UsbDriverBase _usbDriver;
        private int readTimeout = 1000;
        public int ReadTimeout
        {
            get { return readTimeout; }
            set
            {
                _usbDriver.ReadTimeout = value;
                readTimeout = value;
            }
        }
        public ModbusClient(UsbDriverBase usbDriver)
        {
            _usbDriver = usbDriver;
            DriverId = usbDriver.UsbDevice.DeviceName;
        }
        protected override byte[] Read()
        {
            return _usbDriver.Read() ?? Array.Empty<byte>();
        }
        protected override void Write(byte[] buffer)
        {
            _usbDriver.Write(buffer);
        }
    }
}