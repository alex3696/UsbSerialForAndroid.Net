using Android.Hardware.Usb;
using Java.Nio;
using System;
using System.IO;

namespace UsbSerialForAndroid.Net.Extensions;

public static class UsbRequestExtension
{
    public static void QueueReq(this UsbRequest req, ByteBuffer buffer)
    {
        bool isOk;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            isOk = req.Queue(buffer);
        else
            isOk = req.Queue(buffer, buffer.Capacity());
        if (!isOk)
            throw new IOException("Error queueing request.");
    }
}
