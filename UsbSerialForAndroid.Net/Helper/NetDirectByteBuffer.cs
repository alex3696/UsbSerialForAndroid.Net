using Android.Runtime;
using Java.Nio;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace UsbSerialForAndroid.Net.Helper;

// Direct buffer does not require additional copying: line 320
// https://android.googlesource.com/platform/frameworks/base/+/master/core/java/android/hardware/usb/UsbRequest.java

public class NetDirectByteBuffer : IDisposable
{
    /// <summary>
    /// it`s not copy its just wrapper for array
    /// </summary>
    /// <param name="array"></param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    public NetDirectByteBuffer(byte[] array, int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, array.Length - offset);
        _netBuffer = array;
        _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        MemBuffer = MemoryMarshal.CreateFromPinnedArray(array, offset, length);
        IntPtr ndb = JNIEnv.NewDirectByteBuffer(_handle.AddrOfPinnedObject() + offset, length);
        ByteBuffer? jdb = Java.Lang.Object.GetObject<ByteBuffer>(ndb, JniHandleOwnership.TransferLocalRef);
        ArgumentNullException.ThrowIfNull(jdb);
        JavaBuffer = jdb;
    }
    /// <summary>
    /// creates a Net array, pinned it and makes a DirectByteBuffer from it
    /// </summary>
    /// <param name="capacity"></param>
    public NetDirectByteBuffer(int capacity = 512)
        : this(new byte[capacity], 0, capacity)
    {
        //_netBuffer = new byte[capacity];
        //_handle = GCHandle.Alloc(_netBuffer, GCHandleType.Pinned);
        //MemBuffer = MemoryMarshal.CreateFromPinnedArray(_netBuffer, 0, capacity);
        //IntPtr ndb = JNIEnv.NewDirectByteBuffer(_handle.AddrOfPinnedObject(), capacity);
        //ByteBuffer? jdb = Java.Lang.Object.GetObject<ByteBuffer>(ndb, JniHandleOwnership.TransferLocalRef);
        //ArgumentNullException.ThrowIfNull(jdb);
        //JavaBuffer = jdb;
    }

    public readonly Memory<byte> MemBuffer;
    public readonly ByteBuffer JavaBuffer;
    readonly byte[] _netBuffer;
    GCHandle _handle;
    public static explicit operator ByteBuffer(NetDirectByteBuffer nb) => nb.JavaBuffer;
    //public static explicit operator byte[](NetDirectByteBuffer nb) => nb._netBuffer;

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
