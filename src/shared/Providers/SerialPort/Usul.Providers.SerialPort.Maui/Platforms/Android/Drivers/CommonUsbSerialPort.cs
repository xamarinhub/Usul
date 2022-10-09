/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Drivers;

public abstract class CommonUsbSerialPort : UsbSerialPort
{
    public static int DEFAULT_READ_BUFFER_SIZE = 16 * 1024;
    public static int DEFAULT_WRITE_BUFFER_SIZE = 16 * 1024;

    protected UsbDevice _device;
    protected int _portNumber;

    // non-null when open()
    protected UsbDeviceConnection _connection = null;

    // check if connection is still available
    public bool HasConnection => _connection != null;

    protected object _readBufferLock = new();
    protected object _writeBufferLock = new();

    /** Internal read buffer.  Guarded by {@link #_readBufferLock}. */
    protected byte[] _readBuffer;

    /** Internal write buffer.  Guarded by {@link #_writeBufferLock}. */
    protected byte[] _writeBuffer;

    protected CommonUsbSerialPort(UsbDevice device, int portNumber)
    {
        _device = device;
        _portNumber = portNumber;

        _readBuffer = new byte[DEFAULT_READ_BUFFER_SIZE];
        _writeBuffer = new byte[DEFAULT_WRITE_BUFFER_SIZE];
    }
    public override string ToString() => $"<{this.GetType().Name} device_name={_device.DeviceName} device_id={_device.DeviceId} port_number={_portNumber}>";

    /**
        * Returns the currently-bound USB device.
        *
        * @return the device
        */
    public UsbDevice GetDevice() => _device;

    public override int GetPortNumber() => _portNumber;

    /**
        * Returns the device serial number
        *  @return serial number
        */
    public override string GetSerial() => _connection.Serial;

    /**
        * Sets the size of the internal buffer used to exchange data with the USB
        * stack for read operations.  Most users should not need to change this.
        *
        * @param bufferSize the size in bytes
        */
    public void SetReadBufferSize(int bufferSize)
    {
        lock(_readBufferLock) {
            if (bufferSize == _readBuffer.Length)
            {
                return;
            }
            _readBuffer = new byte[bufferSize];
        }
    }

    /**
        * Sets the size of the internal buffer used to exchange data with the USB
        * stack for write operations.  Most users should not need to change this.
        *
        * @param bufferSize the size in bytes
        */
    public void SetWriteBufferSize(int bufferSize)
    {
        lock(_writeBufferLock) {
            if (bufferSize == _writeBuffer.Length)
            {
                return;
            }
            _writeBuffer = new byte[bufferSize];
        }
    }

    public abstract override void Open(UsbDeviceConnection connection);

    public abstract override void Close();

    public abstract override int Read(byte[] dest, int timeoutMillis);

    public abstract override int Write(byte[] src, int timeoutMillis);

    public abstract override void SetParameters(
        int baudRate, int dataBits, UsbSerialStopBits stopBits, UsbSerialParity parity);

    public abstract override bool GetCD();

    public abstract override bool GetCTS();

    public abstract override bool GetDSR();

    public abstract override bool GetDTR();

    public abstract override void SetDTR(bool value);

    public abstract override bool GetRI();

    public abstract override bool GetRTS();

    public abstract override void SetRTS(bool value);

    public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers) => !flushReadBuffers && !flushWriteBuffers;
}
