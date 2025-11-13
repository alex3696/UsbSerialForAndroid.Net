using Android.Runtime;
using Java.Nio;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace UsbSerialForAndroid.Net.Helper;

public class NetDirectByteBuffer : IDisposable
{
    public NetDirectByteBuffer(int capacity = 512)
    {
        NetBuffer = new byte[capacity];
        _handle = GCHandle.Alloc(NetBuffer, GCHandleType.Pinned);
        IntPtr ndb = JNIEnv.NewDirectByteBuffer(_handle.AddrOfPinnedObject(), capacity);
        ByteBuffer? jdb = Java.Lang.Object.GetObject<ByteBuffer>(ndb, JniHandleOwnership.TransferLocalRef);
        ArgumentNullException.ThrowIfNull(jdb);
        JavaBuffer = jdb;
    }
    public readonly byte[] NetBuffer;
    public readonly ByteBuffer JavaBuffer;
    GCHandle _handle;
    public static explicit operator ByteBuffer(NetDirectByteBuffer nb) => nb.JavaBuffer;
    public static explicit operator byte[](NetDirectByteBuffer nb) => nb.NetBuffer;

    public Java.Nio.Buffer? Rewind() => JavaBuffer.Rewind();
    public int Position
    {
        get => JavaBuffer.Position();
        set => JavaBuffer.Position(value);
    }
    public bool IsDisposed => 0 != _isDisposed;
    int _isDisposed = 0;
    protected virtual void Dispose(bool disposing)
    {
        if (0 != Interlocked.Exchange(ref _isDisposed, 1))
            return;
        if (disposing)
        {
            JavaBuffer.Dispose();
            _handle.Free();
        }
    }
    ~NetDirectByteBuffer() => Dispose(disposing: false);
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
