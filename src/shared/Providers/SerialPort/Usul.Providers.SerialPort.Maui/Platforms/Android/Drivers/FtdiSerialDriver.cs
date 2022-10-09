/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;
using Java.Lang;
using Boolean = System.Boolean;
using Math = System.Math;
using String = System.String;

/*
 * driver is implemented from various information scattered over FTDI documentation
 *
 * baud rate calculation https://www.ftdichip.com/Support/Documents/AppNotes/AN232B-05_BaudRates.pdf
 * control bits https://www.ftdichip.com/Firmware/Precompiled/UM_VinculumFirmware_V205.pdf
 * device type https://www.ftdichip.com/Support/Documents/AppNotes/AN_233_Java_D2XX_for_Android_API_User_Manual.pdf -> bvdDevice
 *
 */

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Drivers;

public class FtdiSerialDriver : UsbSerialDriver
    {
        private readonly List<UsbSerialPort> _ports;
        private enum DeviceType
        {
            TYPE_BM,
            TYPE_AM,
            TYPE_2232C,
            TYPE_R,
            TYPE_2232H,
            TYPE_4232H
        }

        public FtdiSerialDriver(UsbDevice device)
        {
            _device = device;
            _port = new FtdiSerialPort(_device, 0, this);

            _ports = new List<UsbSerialPort>();

            for (var port = 0; port < device.InterfaceCount; port++)
            {
                _ports.Add(new FtdiSerialPort(_device, port, this));
            }
        }

        // Needs to refactored
        public override List<UsbSerialPort> GetPorts() => _ports;

        private class FtdiSerialPort : CommonUsbSerialPort
        {
            private const int USB_WRITE_TIMEOUT_MILLIS = 5000;
            private const int READ_HEADER_LENGTH = 2; // contains MODEM_STATUS

            // https://developer.android.com/reference/android/hardware/usb/UsbConstants#USB_DIR_IN
            private const int REQTYPE_HOST_TO_DEVICE = UsbConstants.UsbTypeVendor | 128; // UsbConstants.USB_DIR_OUT;
            private const int REQTYPE_DEVICE_TO_HOST = UsbConstants.UsbTypeVendor | 0;   // UsbConstants.USB_DIR_IN;

            private const int RESET_REQUEST = 0;
            private const int MODEM_CONTROL_REQUEST = 1;
            private const int SET_BAUD_RATE_REQUEST = 3;
            private const int SET_DATA_REQUEST = 4;
            private const int GET_MODEM_STATUS_REQUEST = 5;
            private const int SET_LATENCY_TIMER_REQUEST = 9;
            private const int GET_LATENCY_TIMER_REQUEST = 10;

            private const int MODEM_CONTROL_DTR_ENABLE = 0x0101;
            private const int MODEM_CONTROL_DTR_DISABLE = 0x0100;
            private const int MODEM_CONTROL_RTS_ENABLE = 0x0202;
            private const int MODEM_CONTROL_RTS_DISABLE = 0x0200;
            private const int MODEM_STATUS_CTS = 0x10;
            private const int MODEM_STATUS_DSR = 0x20;
            private const int MODEM_STATUS_RI = 0x40;
            private const int MODEM_STATUS_CD = 0x80;
            private const int RESET_ALL = 0;
            private const int RESET_PURGE_RX = 1;
            private const int RESET_PURGE_TX = 2;

            private const Boolean _baudRateWithPort = false;
            private Boolean _dtr;
            private Boolean _rts;
            private int _breakConfig;

            private readonly IUsbSerialDriver _driver;

            private readonly String TAG = nameof(FtdiSerialDriver);

            public FtdiSerialPort(UsbDevice device, int portNumber) : base(device, portNumber)
            {
            }

            public FtdiSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) 
                : base(device, portNumber)
            {
                this._driver = driver;
            }

            public override IUsbSerialDriver GetDriver() =>
                _driver;

            private void Reset()
            {
                var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                    RESET_ALL, _portNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new IOException("Reset failed: result=" + result);
                }
            }

            public override void Open(UsbDeviceConnection connection)
            {
                if (_connection != null) {
                    throw new IOException("Already open");
                }
                _connection = connection;

                Boolean opened = false;
                try 
                {
                    for (var i = 0; i < _device.InterfaceCount; i++)
                    {
                        if (connection.ClaimInterface(_device.GetInterface(i), true))
                        {
                            Log.Debug(TAG, "claimInterface " + i + " SUCCESS");
                        }
                        else
                        {
                            throw new IOException("Error claiming interface " + i);
                        }
                    }
                    Reset();
                    opened = true;
                } finally 
                {
                    if (!opened)
                    {
                        Close();
                        _connection = null;
                    }
                }
            }

            public override void Close()
            {
                if (_connection == null)
                {
                    throw new IOException("Already closed");
                }
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
                var endpoint = _device.GetInterface(0).GetEndpoint(0);

                lock(_readBufferLock) 
                {
                    var readAmt = Math.Min(dest.Length, _readBuffer.Length);

                    // todo: replace with async call
                    var totalBytesRead = _connection.BulkTransfer(endpoint, _readBuffer,
                        readAmt, timeoutMillis);

                    if (totalBytesRead < READ_HEADER_LENGTH)
                    {
                        throw new IOException("Expected at least " + READ_HEADER_LENGTH + " bytes");
                    }

                    return ReadFilter(dest, totalBytesRead, endpoint.MaxPacketSize);
                }
            }

            private int ReadFilter(byte[] buffer, int totalBytesRead, int maxPacketSize)
            {
                var destPos = 0;

                for (var srcPos = 0; srcPos < totalBytesRead; srcPos += maxPacketSize)
                {
                    var length = Math.Min(srcPos + maxPacketSize, totalBytesRead) - (srcPos + READ_HEADER_LENGTH);
                    if (length < 0)
                        throw new IOException("Expected at least " + READ_HEADER_LENGTH + " bytes");

                    Buffer.BlockCopy(_readBuffer, srcPos + READ_HEADER_LENGTH, buffer, destPos, length);
                    destPos += length;
                }
                return destPos;
            }

            public override int Write(byte[] src, int timeoutMillis)
            {
                var endpoint = _device.GetInterface(0).GetEndpoint(1);
                var offset = 0;

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

                        amtWritten = _connection.BulkTransfer(endpoint, writeBuffer, writeLength,
                                timeoutMillis);
                    }

                    if (amtWritten <= 0)
                    {
                        throw new IOException("Error writing " + writeLength
                                + " bytes at offset " + offset + " length=" + src.Length);
                    }

                    Log.Debug(TAG, "Wrote amtWritten=" + amtWritten + " attempted=" + writeLength);
                    offset += amtWritten;
                }
                return offset;
            }


            private void SetBaudRate(int baudRate)
            {
                int divisor, subdivisor, effectiveBaudRate;

                switch (baudRate)
                {
                    case > 3500000:
                        throw new UnsupportedOperationException("Baud rate to high");
                    case >= 2500000:
                        divisor = 0;
                        subdivisor = 0;
                        effectiveBaudRate = 3000000;
                        break;
                    case >= 1750000:
                        divisor = 1;
                        subdivisor = 0;
                        effectiveBaudRate = 2000000;
                        break;
                    default:
                    {
                        divisor = (24000000 << 1) / baudRate;
                        divisor = (divisor + 1) >> 1; // round
                        subdivisor = divisor & 0x07;
                        divisor >>= 3;
                        if (divisor > 0x3fff) // exceeds bit 13 at 183 baud
                            throw new UnsupportedOperationException("Baud rate to low");
                        effectiveBaudRate = (24000000 << 1) / ((divisor << 3) + subdivisor);
                        effectiveBaudRate = (effectiveBaudRate + 1) >> 1;
                        break;
                    }
                }
                var baudRateError = Math.Abs(1.0 - (effectiveBaudRate / (double)baudRate));
                if (baudRateError >= 0.031) // can happen only > 1.5Mbaud
                    throw new UnsupportedOperationException(String.Format("Baud rate deviation %.1f%% is higher than allowed 3%%", baudRateError * 100));
                var value = divisor;
                var index = 0;
                switch (subdivisor)
                {
                    case 0: break; // 16,15,14 = 000 - sub-integer divisor = 0
                    case 4: value |= 0x4000; break; // 16,15,14 = 001 - sub-integer divisor = 0.5
                    case 2: value |= 0x8000; break; // 16,15,14 = 010 - sub-integer divisor = 0.25
                    case 1: value |= 0xc000; break; // 16,15,14 = 011 - sub-integer divisor = 0.125
                    case 3: value |= 0x0000; index |= 1; break; // 16,15,14 = 100 - sub-integer divisor = 0.375
                    case 5: value |= 0x4000; index |= 1; break; // 16,15,14 = 101 - sub-integer divisor = 0.625
                    case 6: value |= 0x8000; index |= 1; break; // 16,15,14 = 110 - sub-integer divisor = 0.75
                    case 7: value |= 0xc000; index |= 1; break; // 16,15,14 = 111 - sub-integer divisor = 0.875
                }
                //if (_baudRateWithPort)
                //{
                //    index <<= 8;
                //    index |= _portNumber + 1;
                //}

                var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SET_BAUD_RATE_REQUEST,
                        value, index, null, 0, USB_WRITE_TIMEOUT_MILLIS);

                if (result != 0)
                {
                    throw new IOException("Setting baudrate failed: result=" + result);
                }
            }


            public override void SetParameters(int baudRate, int dataBits, UsbSerialStopBits stopBits, UsbSerialParity parity)
            {
                if (baudRate <= 0)
                {
                    throw new IllegalArgumentException("Invalid baud rate: " + baudRate);
                }

                SetBaudRate(baudRate);

                var config = dataBits;

                switch (dataBits)
                {
                    case DATABITS_5:
                    case DATABITS_6:
                        throw new UnsupportedOperationException("Unsupported data bits: " + dataBits);
                    case DATABITS_7:
                    case DATABITS_8:
                        config |= dataBits;
                        break;
                    default:
                        throw new IllegalArgumentException("Invalid data bits: " + dataBits);
                }

                switch (parity)
                {
                    case UsbSerialParity.None:
                        break;
                    case UsbSerialParity.Odd:
                        config |= 0x100;
                        break;
                    case UsbSerialParity.Even:
                        config |= 0x200;
                        break;
                    case UsbSerialParity.Mark:
                        config |= 0x300;
                        break;
                    case UsbSerialParity.Space:
                        config |= 0x400;
                        break;
                    default:
                        throw new IllegalArgumentException("Unknown parity value: " + parity);
                }

                switch (stopBits)
                {
                    case UsbSerialStopBits.One:
                        break;
                    case UsbSerialStopBits.OnePointFive:
                        throw new UnsupportedOperationException("Unsupported stop bits: 1.5");
                    case UsbSerialStopBits.Two:
                        config |= 0x1000;
                        break;
                    default:
                        throw new IllegalArgumentException("Unknown stopBits value: " + stopBits);
                }

                var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SET_DATA_REQUEST,
                        config, _portNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);

                if (result != 0)
                {
                    throw new IOException("Setting parameters failed: result=" + result);
                }
                _breakConfig = config;
            }

            private int GetStatus()
            {
                var data = new byte[2];
                var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_DEVICE_TO_HOST, GET_MODEM_STATUS_REQUEST,
                        0, _portNumber + 1, data, data.Length, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 2) {
                    throw new IOException("Get modem status failed: result=" + result);
                }
                return data[0];
            }

            public override Boolean GetCD() => 
                (GetStatus() & MODEM_STATUS_CD) != 0;
            

            public override Boolean GetCTS() =>
                (GetStatus() & MODEM_STATUS_CTS) != 0;

            public override Boolean GetDSR() =>
                (GetStatus() & MODEM_STATUS_DSR) != 0;
            

            public override Boolean GetDTR() =>
                _dtr;

            public override void SetDTR(Boolean value)
            {
                var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, MODEM_CONTROL_REQUEST,
                                    value ? MODEM_CONTROL_DTR_ENABLE : MODEM_CONTROL_DTR_DISABLE, _portNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new IOException("Set DTR failed: result=" + result);
                }
                _dtr = value;
            }

            public override Boolean GetRI() =>
                (GetStatus() & MODEM_STATUS_RI) != 0;
            

            public override Boolean GetRTS() =>
                _rts;

            public override void SetRTS(Boolean value)
            {
                var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, MODEM_CONTROL_REQUEST,
                        value ? MODEM_CONTROL_RTS_ENABLE : MODEM_CONTROL_RTS_DISABLE, _portNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new IOException("Set RTS failed: result=" + result);
                }
                _rts = value;
            }

            public override Boolean PurgeHwBuffers(Boolean purgeReadBuffers, Boolean purgeWriteBuffers)
            {
                if (purgeWriteBuffers)
                {
                    var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                            RESET_PURGE_RX, _portNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                    if (result != 0)
                    {
                        throw new IOException("Flushing RX failed: result=" + result);
                    }
                }

                if (!purgeReadBuffers) return true;
                {
                    var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, RESET_REQUEST,
                        RESET_PURGE_RX, _portNumber + 1, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                    if (result != 0)
                    {
                        throw new IOException("Flushing RX failed: result=" + result);
                    }
                }

                return true;
            }
        }

        public static Dictionary<int, int[]> GetSupportedDevices() =>
            new()
            {
                {
                    UsbId.VENDOR_FTDI, new int[]
                    {
                        UsbId.FTDI_FT232R,
                        UsbId.FTDI_FT232H,
                        UsbId.FTDI_FT2232H,
                        UsbId.FTDI_FT4232H,
                        UsbId.FTDI_FT231X,  // same ID for FT230X, FT231X, FT234XD
                    }
                }
            };
    }

