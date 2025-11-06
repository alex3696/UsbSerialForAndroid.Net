using Android.Hardware.Usb;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UsbSerialForAndroid.Net.Extensions
{
    public static class UsbDeviceConnectionExtension
    {
        public static List<byte[]> GetDescriptors(this UsbDeviceConnection connection)
        {
            var descriptors = new List<byte[]>();
            var rawDescriptors = connection.GetRawDescriptors();
            if (rawDescriptors is null) return descriptors;
            int pos = 0;
            while (pos < rawDescriptors.Length)
            {
                int len = rawDescriptors[pos] & 0xFF;
                if (len == 0) break;

                if (pos + len > rawDescriptors.Length)
                    len = rawDescriptors.Length - pos;

                byte[] descriptor = new byte[len];
                Array.Copy(rawDescriptors, pos, descriptor, 0, descriptor.Length);
                descriptors.Add(descriptor);
                pos += len;
            }
            return descriptors;
        }

        public static async Task<UsbRequest?> RequestWaitAsync(this UsbDeviceConnection connection, UsbRequest req, int timeout)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                return await connection.RequestWaitAsync(timeout);
            using var cts = new CancellationTokenSource(timeout);
            using var crReg = cts.Token.Register(() => req?.Cancel());
            UsbRequest? ret = await connection.RequestWaitAsync();
            return ret;
        }
        public static UsbRequest? RequestWait(this UsbDeviceConnection connection, UsbRequest req, int timeout)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                return connection.RequestWait(timeout);
            using var cts = new CancellationTokenSource(timeout);
            using var crReg = cts.Token.Register(() => req?.Cancel());
            return connection.RequestWait();
        }
    }
}
