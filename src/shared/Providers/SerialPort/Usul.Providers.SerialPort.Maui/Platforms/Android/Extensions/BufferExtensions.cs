/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Runtime;
using Java.Nio;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Extensions;

/// <summary>
/// Work around for faulty JNI wrapping in Xamarin library.  Fixes a bug 
/// where binding for Java.Nio.ByteBuffer.GetBuffer(byte[], int, int) allocates a new temporary 
/// Java byte array on every call 
/// See https://bugzilla.xamarin.com/show_bug.cgi?id=31260
/// and http://stackoverflow.com/questions/30268400/xamarin-implementation-of-bytebuffer-get-wrong
/// </summary>
public static class BufferExtensions
{
    private static IntPtr _byteBufferClassRef;
    private static IntPtr _byteBufferGetBii;

    public static ByteBuffer GetBuffer(this ByteBuffer buffer, JavaArray<Java.Lang.Byte> dst, int dstOffset, int byteCount)
    {
        if (_byteBufferClassRef == IntPtr.Zero)
        {
            _byteBufferClassRef = JNIEnv.FindClass("java/nio/ByteBuffer");
        }

        if (_byteBufferGetBii == IntPtr.Zero)
        {
            _byteBufferGetBii = JNIEnv.GetMethodID(_byteBufferClassRef, "get", "([BII)Ljava/nio/ByteBuffer;");
        }

        return Java.Lang.Object.GetObject<ByteBuffer>(
            JNIEnv.CallObjectMethod(buffer.Handle, _byteBufferGetBii, new(dst), new(dstOffset), new(byteCount)),
            JniHandleOwnership.TransferLocalRef);
    }

    public static byte[] ToByteArray(this ByteBuffer buffer)
    {
        var classHandle = JNIEnv.FindClass("java/nio/ByteBuffer");
        var methodId = JNIEnv.GetMethodID(classHandle, "array", "()[B");
        var resultHandle = JNIEnv.CallObjectMethod(buffer.Handle, methodId);

        var result = JNIEnv.GetArray<byte>(resultHandle);
        JNIEnv.DeleteLocalRef(resultHandle);
        return result;
    }
}
