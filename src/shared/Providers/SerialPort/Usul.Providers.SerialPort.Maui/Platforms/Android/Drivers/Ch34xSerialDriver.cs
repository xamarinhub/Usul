/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;

namespace Usul.Providers.SerialPort.Maui.Implementation.Drivers;

    public class Ch34xSerialDriver : UsbSerialDriver
    {
        private readonly string TAG = typeof (ProlificSerialDriver).Name;

        public Ch34xSerialDriver(UsbDevice device)
        {
            _device = device;
            _port = new Ch340SerialPort(_device, 0, this);
        }

        public class Ch340SerialPort : CommonUsbSerialPort
        {
            private static int USB_TIMEOUT_MILLIS = 5000;

            private int DEFAULT_BAUD_RATE = 9600;

            private Boolean dtr = false;
            private Boolean rts = false;

            private UsbEndpoint mReadEndpoint;
            private UsbEndpoint mWriteEndpoint;

            private IUsbSerialDriver Driver;
            private string TAG => (Driver as Ch34xSerialDriver)?.TAG;

            public Ch340SerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber)
            {
                Driver = driver;
            }

            public override IUsbSerialDriver GetDriver()
            {
                return Driver;
            }

            public override void Open(UsbDeviceConnection connection)
            {
                if (_connection != null)
                {
                    throw new IOException("Already opened.");
                }

                _connection = connection;
                Boolean opened = false;
                try
                {
                    for (int i = 0; i < _device.InterfaceCount; i++)
                    {
                        UsbInterface usbIface = _device.GetInterface(i);
                        if (_connection.ClaimInterface(usbIface, true))
                        {
                            Log.Debug(TAG, "claimInterface " + i + " SUCCESS");
                        }
                        else
                        {
                            Log.Debug(TAG, "claimInterface " + i + " FAIL");
                        }
                    }

                    UsbInterface dataIface = _device.GetInterface(_device.InterfaceCount - 1);
                    for (int i = 0; i < dataIface.EndpointCount; i++)
                    {
                        UsbEndpoint ep = dataIface.GetEndpoint(i);
                        if (ep.Type == (UsbAddressing) UsbSupport.UsbEndpointXferBulk)
                        {
                            if (ep.Direction == (UsbAddressing) UsbSupport.UsbDirIn)
                            {
                                mReadEndpoint = ep;
                            }
                            else
                            {
                                mWriteEndpoint = ep;
                            }
                        }
                    }


                    Initialize();
                    SetBaudRate(DEFAULT_BAUD_RATE);

                    opened = true;
                }
                finally
                {
                    if (!opened)
                    {
                        try
                        {
                            Close();
                        }
                        catch (IOException e)
                        {
                            // Ignore IOExceptions during close()
                        }
                    }
                }
            }

            public override void Close()
            {
                if (_connection == null)
                {
                    throw new IOException("Already closed");
                }

                // TODO: nothing sended on close, maybe needed?

                try
                {
                    _connection.Close();
                }
                finally
                {
                    _connection = null;
                }
            }

            public override int Read(byte[] dest, int timeoutMillis)
            {
                int numBytesRead;
                lock (_readBufferLock)
                {
                    int readAmt = Math.Min(dest.Length, _readBuffer.Length);
                    numBytesRead = _connection.BulkTransfer(mReadEndpoint, _readBuffer, readAmt,
                        timeoutMillis);
                    if (numBytesRead < 0)
                    {
                        // This sucks: we get -1 on timeout, not 0 as preferred.
                        // We *should* use UsbRequest, except it has a bug/api oversight
                        // where there is no way to determine the number of bytes read
                        // in response :\ -- http://b.android.com/28023
                        return 0;
                    }
                    Buffer.BlockCopy(_readBuffer, 0, dest, 0, numBytesRead);
                }
                return numBytesRead;
            }

            public override int Write(byte[] src, int timeoutMillis)
            {
                int offset = 0;

                while (offset < src.Length)
                {
                    int writeLength;
                    int amtWritten;

                    lock (_writeBufferLock)
                    {
                        byte[] writeBuffer;

                        writeLength = Math.Min(src.Length - offset, _writeBuffer.Length);
                        if (offset == 0)
                        {
                            writeBuffer = src;
                        }
                        else
                        {
                            // bulkTransfer does not support offsets, make a copy.
                            Buffer.BlockCopy(src, offset, _writeBuffer, 0, writeLength);
                            writeBuffer = _writeBuffer;
                        }

                        amtWritten = _connection.BulkTransfer(mWriteEndpoint, writeBuffer, writeLength,
                            timeoutMillis);
                    }
                    if (amtWritten <= 0)
                    {
                        throw new IOException(
                            $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                    }

                    Log.Debug(TAG, $"Wrote amt={amtWritten} attempted={writeLength}");
                    offset += amtWritten;
                }
                return offset;
            }

            private int ControlOut(int request, int value, int index)
            {
                int REQTYPE_HOST_TO_DEVICE = 0x41;
                return _connection.ControlTransfer((UsbAddressing) REQTYPE_HOST_TO_DEVICE, request,
                    value, index, null, 0, USB_TIMEOUT_MILLIS);
            }


            private int ControlIn(int request, int value, int index, byte[] buffer)
            {
                int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | UsbSupport.UsbDirIn;
                return _connection.ControlTransfer((UsbAddressing) REQTYPE_HOST_TO_DEVICE, request,
                    value, index, buffer, buffer.Length, USB_TIMEOUT_MILLIS);
            }

            private void CheckState(String msg, int request, int value, int[] expected)
            {
                byte[] buffer = new byte[expected.Length];
                int ret = ControlIn(request, value, 0, buffer);

                if (ret < 0)
                {
                    throw new IOException($"Failed send cmd [{msg}]");
                }

                if (ret != expected.Length)
                {
                    throw new IOException($"Expected {expected.Length} bytes, but get {ret} [{msg}]");
                }

                for (int i = 0; i < expected.Length; i++)
                {
                    if (expected[i] == -1)
                    {
                        continue;
                    }

                    int current = buffer[i] & 0xff;
                    if (expected[i] != current)
                    {
                        throw new IOException($"Expected 0x{expected[i]:X} bytes, but get 0x{current:X} [ {msg} ]");
                    }
                }
            }

            private void WriteHandshakeByte()
            {
                if (ControlOut(0xa4, ~((dtr ? 1 << 5 : 0) | (rts ? 1 << 6 : 0)), 0) < 0)
                {
                    throw new IOException("Failed to set handshake byte");
                }
            }

            private void Initialize()
            {
                CheckState("init #1", 0x5f, 0, new int[] {-1 /* 0x27, 0x30 */, 0x00});

                if (ControlOut(0xa1, 0, 0) < 0)
                {
                    throw new IOException("init failed! #2");
                }

                SetBaudRate(DEFAULT_BAUD_RATE);

                CheckState("init #4", 0x95, 0x2518, new int[] {-1 /* 0x56, c3*/, 0x00});

                if (ControlOut(0x9a, 0x2518, 0x0050) < 0)
                {
                    throw new IOException("init failed! #5");
                }

                CheckState("init #6", 0x95, 0x0706, new int[] {0xff, 0xee});

                if (ControlOut(0xa1, 0x501f, 0xd90a) < 0)
                {
                    throw new IOException("init failed! #7");
                }

                SetBaudRate(DEFAULT_BAUD_RATE);

                WriteHandshakeByte();

                CheckState("init #10", 0x95, 0x0706, new int[] {-1 /* 0x9f, 0xff*/, 0xee});
            }

            private void SetBaudRate(int baudRate)
            {
                int[] baud = new int[]
                {
                    2400, 0xd901, 0x0038, 4800, 0x6402,
                    0x001f, 9600, 0xb202, 0x0013, 19200, 0xd902, 0x000d, 38400,
                    0x6403, 0x000a, 115200, 0xcc03, 0x0008
                };

                for (int i = 0; i < baud.Length/3; i++)
                {
                    if (baud[i*3] == baudRate)
                    {
                        int ret = ControlOut(0x9a, 0x1312, baud[i*3 + 1]);
                        if (ret < 0)
                        {
                            throw new IOException("Error setting baud rate. #1");
                        }
                        ret = ControlOut(0x9a, 0x0f2c, baud[i*3 + 2]);
                        if (ret < 0)
                        {
                            throw new IOException("Error setting baud rate. #1");
                        }

                        return;
                    }
                }


                throw new IOException("Baud rate " + baudRate + " currently not supported");
            }

            public override void SetParameters(int baudRate, int dataBits, UsbSerialStopBits stopBits, UsbSerialParity parity)
            {
                SetBaudRate(baudRate);
            }

            public override bool GetCD() => false;

            public override bool GetCTS() => false;

            public override bool GetDSR() => false;

            public override bool GetDTR()
            {
                return dtr;
            }

            public override void SetDTR(bool value)
            {
                dtr = value;
                WriteHandshakeByte();
            }

            public override bool GetRI() => false;

            public override bool GetRTS() => rts;

            public override void SetRTS(bool value)
            {
                rts = value;
                WriteHandshakeByte();
            }

            public override bool PurgeHwBuffers(bool flushReadBuffers, bool flushWriteBuffers) => true;
        }

        public static Dictionary<int, int[]> GetSupportedDevices() =>
            new()
            {
                {
                    UsbId.VENDOR_QINHENG, new int[]
                    {
                        UsbId.QINHENG_HL340
                    }
                }
            };
    }
