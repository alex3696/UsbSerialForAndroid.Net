/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Runtime;
using Java.Nio;

namespace UsbSerialForAndroid.Net.Extensions
{
    /// <summary>
    /// Work around for faulty JNI wrapping in Xamarin library.  Fixes a bug 
    /// where binding for Java.Nio.ByteBuffer.Get(byte[], int, int) allocates a new temporary 
    /// Java byte array on every call 
    /// See https://bugzilla.xamarin.com/show_bug.cgi?id=31260
    /// and http://stackoverflow.com/questions/30268400/xamarin-implementation-of-bytebuffer-get-wrong
    /// </summary>
    public static class BufferExtensions
    {
        static nint _byteBufferClassRef;
        static nint _byteBufferGetBii;
        static nint _byteBufferGetArrayMethodRef;

        // init on first call
        static BufferExtensions()
        {
            _byteBufferClassRef = JNIEnv.FindClass("java/nio/ByteBuffer");
            _byteBufferGetArrayMethodRef = JNIEnv.GetMethodID(_byteBufferClassRef, "array", "()[B");
        }

        public static ByteBuffer? Get(this ByteBuffer buffer, JavaArray<Java.Lang.Byte> dst, int dstOffset, int byteCount)
        {
            if (_byteBufferClassRef == nint.Zero)
                _byteBufferClassRef = JNIEnv.FindClass("java/nio/ByteBuffer");
            if (_byteBufferGetBii == nint.Zero)
                _byteBufferGetBii = JNIEnv.GetMethodID(_byteBufferClassRef, "get", "([BII)Ljava/nio/ByteBuffer;");

            return Java.Lang.Object.GetObject<ByteBuffer>(
                JNIEnv.CallObjectMethod(buffer.Handle, _byteBufferGetBii, [new(dst), new(dstOffset), new(byteCount)]),
                JniHandleOwnership.TransferLocalRef);
        }

        public static byte[]? ToByteArray(this ByteBuffer buffer)
        {
            nint resultHandle = JNIEnv.CallObjectMethod(buffer.Handle, _byteBufferGetArrayMethodRef);
            byte[]? result = JNIEnv.GetArray<byte>(resultHandle);
            JNIEnv.DeleteLocalRef(resultHandle);
            return result;
        }
    }
}