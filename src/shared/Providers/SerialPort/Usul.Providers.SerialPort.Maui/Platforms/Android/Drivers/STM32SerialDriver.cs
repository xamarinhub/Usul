/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;

using Java.Nio;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Drivers;

public class STM32SerialDriver : UsbSerialDriver
{
	private readonly string TAG = nameof(STM32SerialDriver);

	int _ctrlInterf;

	public STM32SerialDriver(UsbDevice device)
	{
		_device = device;
		_port = new STM32SerialPort(_device, 0, this);
	}

	public class STM32SerialPort : CommonUsbSerialPort
	{
        private const int USB_WRITE_TIMEOUT_MILLIS = 5000;

        private const int USB_RECIP_INTERFACE = 0x01;
        private const int USB_RT_AM = UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

        private const int SET_LINE_CODING = 0x20; // USB CDC 1.1 section 6.2
        private const int SET_CONTROL_LINE_STATE = 0x22;

        private const string TAG = nameof(STM32SerialDriver);

        private readonly bool ENABLE_ASYNC_READS;
		private UsbInterface _controlInterface;
        private UsbInterface _dataInterface;

        private UsbEndpoint _readEndpoint;
        private UsbEndpoint writeEndpoint;

        private bool _rts;
        private bool _dtr;

        private readonly IUsbSerialDriver _driver;

		

		public STM32SerialPort(UsbDevice device, int portNumber, IUsbSerialDriver driver) : base(device, portNumber)
		{
			_driver = driver;
			ENABLE_ASYNC_READS = true;
		}

		public override IUsbSerialDriver GetDriver() =>
			_driver;

		private int SendAcmControlMessage(int request, int value, byte[] buf) =>
			_connection.ControlTransfer((UsbAddressing)USB_RT_AM, request, value, (_driver as STM32SerialDriver)._ctrlInterf, buf, buf?.Length ?? 0, USB_WRITE_TIMEOUT_MILLIS);

		public override void Open(UsbDeviceConnection connection)
		{
			if (_connection != null)
				throw new IOException("Already opened.");

			_connection = connection;
			bool opened = false;
			bool controlInterfaceFound = false;
			try
			{
				for (var i = 0; i < _device.InterfaceCount; i++)
				{
					_controlInterface = _device.GetInterface(i);
					if(_controlInterface.InterfaceClass == UsbClass.Comm)
					{
						if (!_connection.ClaimInterface(_controlInterface, true))
							throw new IOException("Could not claim control interface");
						(_driver as STM32SerialDriver)._ctrlInterf = i;
						controlInterfaceFound = true;
						break;
					}
				}
				if (!controlInterfaceFound)
					throw new IOException("Could not claim control interface");
				for (var i = 0; i < _device.InterfaceCount; i++)
				{
					_dataInterface = _device.GetInterface(i);
					if(_dataInterface.InterfaceClass == UsbClass.CdcData)
					{
						if (!_connection.ClaimInterface(_dataInterface, true))
							throw new IOException("Could not claim data interface");
						_readEndpoint = _dataInterface.GetEndpoint(1);
						writeEndpoint = _dataInterface.GetEndpoint(0);
						opened = true;
						break;
					}
				}
				if(!opened)
					throw new IOException("Could not claim data interface.");
			}
			finally
			{
				if (!opened)
					_connection = null;
			}
		}

		public override void Close()
		{
			if (_connection == null)
				throw new IOException("Already closed");
			_connection.Close();
			_connection = null;
		}

		public override int Read(byte[] dest, int timeoutMillis)
		{
			if(ENABLE_ASYNC_READS)
			{
				var request = new UsbRequest();
				try
				{
					request.Initialize(_connection, _readEndpoint);
					ByteBuffer buf = ByteBuffer.Wrap(dest);
					if (!request.Queue(buf, dest.Length))
						throw new IOException("Error queuing request");

					UsbRequest response = _connection.RequestWait();
					if (response == null)
						throw new IOException("Null response");

					int nread = buf.Position();
					if (nread > 0)
						return nread;

					return 0;
				}
				finally
				{
					request.Close();
				}
			}

			int numBytesRead;
			lock(_readBufferLock)
			{
				int readAmt = Math.Min(dest.Length, _readBuffer.Length);
				numBytesRead = _connection.BulkTransfer(_readEndpoint, _readBuffer, readAmt, timeoutMillis);
				if(numBytesRead < 0)
				{
					// This sucks: we get -1 on timeout, not 0 as preferred.
					// We *should* use UsbRequest, except it has a bug/api oversight
					// where there is no way to determine the number of bytes read
					// in response :\ -- http://b.android.com/28023
					if (timeoutMillis == int.MaxValue)
					{
						// Hack: Special case "~infinite timeout" as an error.
						return -1;
					}

					return 0;
				}
				Array.Copy(_readBuffer, 0, dest, 0, numBytesRead);
			}
			return numBytesRead;
		}

		public override int Write(byte[] src, int timeoutMillis)
		{
			int offset = 0;

			while(offset < src.Length)
			{
				int writeLength;
				int amtWritten;

				lock(_writeBufferLock)
				{
					byte[] writeBuffer;

					writeLength = Math.Min(src.Length - offset, _writeBuffer.Length);
					if (offset == 0)
						writeBuffer = src;
					else
					{
						Array.Copy(src, offset, _writeBuffer, 0, writeLength);
						writeBuffer = _writeBuffer;
					}

					amtWritten = _connection.BulkTransfer(writeEndpoint, writeBuffer, writeLength, timeoutMillis);
				}
				if(amtWritten <= 0)
					throw new IOException($"Error writing {writeLength} bytes at offset {offset} length={src.Length}");

				Log.Debug(TAG, $"Wrote amt={amtWritten} attempted={writeLength}");
				offset += amtWritten;
			}

			return offset;
		}

		public override void SetParameters(int baudRate, int dataBits, UsbSerialStopBits stopBits, UsbSerialParity parity)
		{
            byte stopBitsBytes = stopBits switch
            {
                UsbSerialStopBits.One => 0,
                UsbSerialStopBits.OnePointFive => 1,
                UsbSerialStopBits.Two => 2,
                _ => throw new ArgumentException($"Bad value for stopBits: {stopBits}")
            };

            byte parityBitesBytes = parity switch
            {
                UsbSerialParity.None => 0,
                UsbSerialParity.Odd => 1,
                UsbSerialParity.Even => 2,
                UsbSerialParity.Mark => 3,
                UsbSerialParity.Space => 4,
                _ => throw new ArgumentException($"Bad value for parity: {parity}")
            };

            byte[] msg = {
				(byte)(baudRate & 0xff),
				(byte) ((baudRate >> 8 ) & 0xff),
				(byte) ((baudRate >> 16) & 0xff),
				(byte) ((baudRate >> 24) & 0xff),
				stopBitsBytes,
				parityBitesBytes,
				(byte) dataBits
			};
			SendAcmControlMessage(SET_LINE_CODING, 0, msg);
		}

		public override bool GetCD() =>
			false; //TODO

		public override bool GetCTS() =>
			false; //TODO

		public override bool GetDSR() =>
			false; // TODO

		public override bool GetDTR() =>
			_dtr;

		public override void SetDTR(bool value)
		{
			_dtr = value;
			SetDtrRts();
		}

		public override bool GetRI() =>
			false; //TODO

		public override bool GetRTS() =>
			_rts; //TODO

		public override void SetRTS(bool value)
		{
			_rts = value;
			SetDtrRts();
		}

		private void SetDtrRts()
		{
			var value = (_rts ? 0x2 : 0) | (_dtr ? 0x1 : 0);
			SendAcmControlMessage(SET_CONTROL_LINE_STATE, value, null);
		}

		//public static Dictionary<int, int[]> GetSupportedDevices() =>
  //          new()
  //          {
  //              {
  //                  UsbId.VENDOR_STM, new int[]
  //                  {
  //                      UsbId.STM32_STLINK,
  //                      UsbId.STM32_VCOM
  //                  }
  //              }
  //          };
    }
}

