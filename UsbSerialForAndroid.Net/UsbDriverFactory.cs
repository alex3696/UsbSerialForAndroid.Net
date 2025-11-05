using Android.Hardware.Usb;
using System;
using UsbSerialForAndroid.Net.Drivers;
using UsbSerialForAndroid.Net.Enums;
using UsbSerialForAndroid.Net.Exceptions;
using UsbSerialForAndroid.Net.Helper;
using UsbSerialForAndroid.Net.Receivers;

namespace UsbSerialForAndroid.Net
{
    /// <summary>
    /// USB driver factory
    /// </summary>
    public static class UsbDriverFactory
    {
        /// <summary>
        /// Create USB driver
        /// </summary>
        /// <param name="usbDevice">USB device</param>
        /// <returns>UsbDriver</returns>
        /// <exception cref="NotSupportedDriverException">not supported driver exception</exception>
        private static UsbDriverBase CreateUsbDriver(UsbDevice usbDevice)
        {
            if (!HasSupportedDriver(usbDevice.VendorId, usbDevice.ProductId))
                throw new NotSupportedDriverException(usbDevice);

            return usbDevice.VendorId switch
            {
                (int)VendorIds.FTDI => new FtdiSerialDriver(usbDevice),
                (int)VendorIds.Prolific => new ProlificSerialDriver(usbDevice),
                (int)VendorIds.QinHeng => new QinHengSerialDriver(usbDevice),
                (int)VendorIds.SiliconLabs => new SiliconLabsSerialDriver(usbDevice),
                (int)VendorIds.Arduino => new CdcAcmSerialDriver(usbDevice),
                (int)VendorIds.Atmel => new CdcAcmSerialDriver(usbDevice),
                (int)VendorIds.GigaDevice => new CdcAcmSerialDriver(usbDevice),
                _ => throw new NotSupportedDriverException(usbDevice)
            };
        }
        /// <summary>
        /// Create USB driver
        /// </summary>
        /// <param name="vendorId">Vendor Id</param>
        /// <param name="productId">Product Id</param>
        /// <returns>UsbDriver</returns>
        public static UsbDriverBase CreateUsbDriver(int vendorId, int productId)
        {
            var device = UsbManagerHelper.GetUsbDevice(vendorId, productId);
            return CreateUsbDriver(device);
        }
        /// <summary>
        /// Create USB driver
        /// </summary>
        /// <param name="deviceId">Device Id</param>
        /// <returns>UsbDriver</returns>
        public static UsbDriverBase CreateUsbDriver(int deviceId)
        {
            var device = UsbManagerHelper.GetUsbDevice(deviceId);
            return CreateUsbDriver(device);
        }
        /// <summary>
        /// Whether there is a support driver
        /// </summary>
        /// <param name="vendorId">Vendor Id</param>
        /// <param name="productId">Product Id</param>
        /// <returns></returns>
        public static bool HasSupportedDriver(int vendorId, int productId)
        {
            var vid = (VendorIds)vendorId;
            switch (vid)
            {
                case VendorIds.FTDI:
                    {
                        var pid = (Ftdi)productId;
                        switch (pid)
                        {
                            case Ftdi.FT232R:
                            case Ftdi.FT2232H:
                            case Ftdi.FT4232H:
                            case Ftdi.FT232H:
                            case Ftdi.FT231X:
                                return true;
                        }
                        break;
                    }
                case VendorIds.Prolific:
                    {
                        var pid = (Prolific)productId;
                        switch (pid)
                        {
                            case Prolific.PL2303:
                            case Prolific.PL2303GC:
                            case Prolific.PL2303GB:
                            case Prolific.PL2303GT:
                            case Prolific.PL2303GL:
                            case Prolific.PL2303GE:
                            case Prolific.PL2303GS:
                                return true;
                        }
                        break;
                    }
                case VendorIds.QinHeng:
                    {
                        var pid = (QinHeng)productId;
                        switch (pid)
                        {
                            case QinHeng.HL340:
                            case QinHeng.CH341A:
                                return true;
                        }
                        break;
                    }
                case VendorIds.SiliconLabs:
                    {
                        var pid = (SiliconLabs)productId;
                        switch (pid)
                        {
                            case SiliconLabs.CP2102:
                            case SiliconLabs.CP2105:
                            case SiliconLabs.CP2108:
                            case SiliconLabs.CP2110:
                                return true;
                        }
                        break;
                    }
                case VendorIds.Arduino:
                case VendorIds.Atmel:
                case VendorIds.GigaDevice:
                        return true;
            }
            return false;
        }
        /// <summary>
        /// Register a USB broadcast receiver
        /// </summary>
        /// <param name="isShowToast">true=show toast</param>
        /// <param name="attached">USB insert callback</param>
        /// <param name="detached">USB pull out callback</param>
        /// <param name="errorCallback">Internal error callback</param>
        public static void RegisterUsbBroadcastReceiver(bool isShowToast = true,
            Action<UsbDevice>? attached = default, Action<UsbDevice>? detached = default,
            Action<Exception>? errorCallback = default)
        {
            UsbBroadcastReceiverHelper.RegisterUsbBroadcastReceiver(isShowToast, attached, detached, errorCallback);
        }
        /// <summary>
        /// Unregister the USB broadcast receiver
        /// </summary>
        public static void UnRegisterUsbBroadcastReceiver()
        {
            UsbBroadcastReceiverHelper.UnRegisterUsbBroadcastReceiver();
        }
    }
}
