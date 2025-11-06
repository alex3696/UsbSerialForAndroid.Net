using Android.Hardware.Usb;
using Java.Nio;
using System;

namespace UsbSerialForAndroid.Net.Extensions;

public static class UsbRequestExtension
{
    public static bool QueueReq(this UsbRequest req, ByteBuffer buffer)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            return req.Queue(buffer);
        return req.Queue(buffer, buffer.Capacity());
    }
}
