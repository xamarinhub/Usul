/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Drivers;

public class Cp21xxSerialDriver : UsbSerialDriver
{
    private const string TAG = nameof(Cp21xxSerialDriver);

    public Cp21xxSerialDriver(UsbDevice device)
    {
        _device = device;
        _port = new Cp21xxSerialPort(_device, 0, this);
    }

    public class Cp21xxSerialPort : CommonUsbSerialPort
    {
        private const int DEFAULT_BAUD_RATE = 9600;

        private const int USB_WRITE_TIMEOUT_MILLIS = 5000;

        /*
         * Configuration Request Types
         */
        private const int REQTYPE_HOST_TO_DEVICE = 0x41;
        private const int REQTYPE_DEVICE_TO_HOST = 0xc1;

        /*
         * Configuration Request Codes
         */
        private const int SILABSER_IFC_ENABLE_REQUEST_CODE = 0x00;
        private const int SILABSER_SET_BAUDDIV_REQUEST_CODE = 0x01;
        private const int SILABSER_SET_LINE_CTL_REQUEST_CODE = 0x03;
        private const int SILABSER_SET_MHS_REQUEST_CODE = 0x07;
        private const int SILABSER_SET_BAUDRATE = 0x1E;
        private const int SILABSER_FLUSH_REQUEST_CODE = 0x12;

        private const int FLUSH_READ_CODE = 0x0a;
        private const int FLUSH_WRITE_CODE = 0x05;

        private const int GET_MODEM_STATUS_REQUEST = 0x08; // 0x08 Get modem status. 
        private const int MODEM_STATUS_CTS = 0x10;
        private const int MODEM_STATUS_DSR = 0x20;
        private const int MODEM_STATUS_RI = 0x40;
        private const int MODEM_STATUS_CD = 0x80;
        /*
         * SILABSER_IFC_ENABLE_REQUEST_CODE
         */
        private const int UART_ENABLE = 0x0001;
        private const int UART_DISABLE = 0x0000;

        /*
         * SILABSER_SET_BAUDDIV_REQUEST_CODE
         */
        private const int BAUD_RATE_GEN_FREQ = 0x384000;

        /*
         * SILABSER_SET_MHS_REQUEST_CODE
         */
        private const int MCR_DTR = 0x0001;
        private const int MCR_RTS = 0x0002;
        private const int MCR_ALL = 0x0003;

        private const int CONTROL_WRITE_DTR = 0x0100;
        private const int CONTROL_WRITE_RTS = 0x0200;

        private UsbEndpoint mReadEndpoint;
        private UsbEndpoint mWriteEndpoint;

        private readonly IUsbSerialDriver _driver;

        private static string TAG => Cp21xxSerialDriver.TAG;


        public Cp21xxSerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber)
        {
            _driver = driver;
        }

        public override IUsbSerialDriver GetDriver() =>
            _driver;

        private void SetConfigSingle(int request, int value)
        {
            _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, request, value,
                    0, null, 0, USB_WRITE_TIMEOUT_MILLIS);
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
                for (var i = 0; i < _device.InterfaceCount; i++)
                {
                    var usbIface = _device.GetInterface(i);
                    Log.Debug(TAG,
                        _connection.ClaimInterface(usbIface, true)
                            ? $"claimInterface {i} SUCCESS"
                            : $"claimInterface {i} FAIL");
                }

                var dataIface = _device.GetInterface(_device.InterfaceCount - 1);
                for (var i = 0; i < dataIface.EndpointCount; i++)
                {
                    var ep = dataIface.GetEndpoint(i);
                    if (ep is not { Type: (UsbAddressing)UsbSupport.UsbEndpointXferBulk }) continue;
                    if (ep.Direction == (UsbAddressing)UsbSupport.UsbDirIn)
                    {
                        mReadEndpoint = ep;
                    }
                    else
                    {
                        mWriteEndpoint = ep;
                    }
                }

                SetConfigSingle(SILABSER_IFC_ENABLE_REQUEST_CODE, UART_ENABLE);
                SetConfigSingle(SILABSER_SET_MHS_REQUEST_CODE, MCR_ALL | CONTROL_WRITE_DTR | CONTROL_WRITE_RTS);
                SetConfigSingle(SILABSER_SET_BAUDDIV_REQUEST_CODE, BAUD_RATE_GEN_FREQ / DEFAULT_BAUD_RATE);
                //            setParameters(DEFAULT_BAUD_RATE, DEFAULT_DATA_BITS, DEFAULT_STOP_BITS, DEFAULT_PARITY);
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
                    catch (IOException)
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
            try
            {
                SetConfigSingle(SILABSER_IFC_ENABLE_REQUEST_CODE, UART_DISABLE);
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
            lock(_readBufferLock) 
            {
                var readAmt = Math.Min(dest.Length, _readBuffer.Length);
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
            var offset = 0;

            while (offset < src.Length)
            {
                int writeLength;
                int amtWritten;
                lock(_writeBufferLock) 
                {

                    writeLength = src.Length - offset;

                    amtWritten = _connection.BulkTransfer(mWriteEndpoint, src, offset, writeLength,
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

        private void SetBaudRate(int baudRate)
        {
            var data = new byte[] {
                (byte) ( baudRate & 0xff),
                (byte) ((baudRate >> 8 ) & 0xff),
                (byte) ((baudRate >> 16) & 0xff),
                (byte) ((baudRate >> 24) & 0xff)
            };
            var ret = _connection.ControlTransfer((UsbAddressing)REQTYPE_HOST_TO_DEVICE, SILABSER_SET_BAUDRATE,
                    0, 0, data, 4, USB_WRITE_TIMEOUT_MILLIS);
            if (ret < 0)
            {
                throw new IOException("Error setting baud rate.");
            }
        }


        public override void SetParameters(int baudRate, int dataBits, UsbSerialStopBits stopBits, UsbSerialParity parity)
        {
            SetBaudRate(baudRate);

            var configDataBits = 0;
            switch (dataBits)
            {
                case DATABITS_5:
                    configDataBits |= 0x0500;
                    break;
                case DATABITS_6:
                    configDataBits |= 0x0600;
                    break;
                case DATABITS_7:
                    configDataBits |= 0x0700;
                    break;
                case DATABITS_8:
                    configDataBits |= 0x0800;
                    break;
                default:
                    configDataBits |= 0x0800;
                    break;
            }

            switch (parity)
            {
                case UsbSerialParity.Odd:
                    configDataBits |= 0x0010;
                    break;
                case UsbSerialParity.Even:
                    configDataBits |= 0x0020;
                    break;
            }

            switch (stopBits)
            {
                case UsbSerialStopBits.One:
                    configDataBits |= 0;
                    break;
                case UsbSerialStopBits.Two:
                    configDataBits |= 2;
                    break;
            }
            SetConfigSingle(SILABSER_SET_LINE_CTL_REQUEST_CODE, configDataBits);
        }

        private int GetStatus()
        {
            var data = new byte[1];
            var result = _connection.ControlTransfer((UsbAddressing)REQTYPE_DEVICE_TO_HOST, GET_MODEM_STATUS_REQUEST,
                    0, 0, data, data.Length, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 1)
            {
                throw new IOException("Get modem status failed: result=" + result);
            }
            return data[0];
        }

        public override bool GetCD() =>
            (GetStatus() & MODEM_STATUS_CD) != 0;

        public override bool GetCTS() => 
            (GetStatus() & MODEM_STATUS_CTS) != 0;
        
        public override bool GetDSR() =>
            (GetStatus() & MODEM_STATUS_DSR) != 0;
        
        public override bool GetDTR() =>
            (GetStatus() & MCR_DTR) != 0;

        public override void SetDTR(bool value)
        {
            SetConfigSingle(SILABSER_SET_MHS_REQUEST_CODE, (value ? MCR_DTR : 0) | CONTROL_WRITE_DTR);
        }

        public override bool GetRI() =>
            (GetStatus() & MODEM_STATUS_RI) != 0;

        public override bool GetRTS() =>
            (GetStatus() & MCR_RTS) != 0;

        public override void SetRTS(bool value)
        {
            SetConfigSingle(SILABSER_SET_MHS_REQUEST_CODE, (value ? MCR_RTS : 0) | CONTROL_WRITE_RTS);
        }

        public override Boolean PurgeHwBuffers(Boolean purgeReadBuffers, Boolean purgeWriteBuffers)
        {
            var value = (purgeReadBuffers ? FLUSH_READ_CODE : 0)
                    | (purgeWriteBuffers ? FLUSH_WRITE_CODE : 0);

            if (value != 0)
            {
                SetConfigSingle(SILABSER_FLUSH_REQUEST_CODE, value);
            }

            return true;
        }
    }

    public static Dictionary<int, int[]> GetSupportedDevices() =>
        new()
        {
            {
                UsbId.VENDOR_SILABS, new int[]
                {
                    UsbId.SILABS_CP2102,
                    UsbId.SILABS_CP2105,
                    UsbId.SILABS_CP2108,
                    UsbId.SILABS_CP2110
                }
            }
        };
}
