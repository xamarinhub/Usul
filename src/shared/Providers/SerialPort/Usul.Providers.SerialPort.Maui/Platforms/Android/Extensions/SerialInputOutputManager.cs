/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;
using Android.Util;
using Usul.Providers.SerialPort.Maui.Implementation.Drivers;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Extensions;

public class SerialInputOutputManager : IDisposable
{
    private const string TAG = nameof(SerialInputOutputManager);
    private const int READ_WAIT_MILLIS = 200;
    private const int DEFAULT_BUFFERSIZE = 4096;
    private const int DEFAULT_BAUDRATE = 9600;
    private const int DEFAULT_DATABITS = 8;
    private const UsbSerialParity DEFAULT_PARITY = UsbSerialParity.None;
    private const UsbSerialStopBits DEFAULT_STOPBITS = UsbSerialStopBits.One;

    private readonly UsbSerialPort _port;
    private byte[] _buffer;
    private bool _disposed;
    private CancellationTokenSource _cancelationTokenSource;

    public SerialInputOutputManager(UsbSerialPort port)
    {
        _port = port;
        BaudRate = DEFAULT_BAUDRATE;
        Parity = DEFAULT_PARITY;
        DataBits = DEFAULT_DATABITS;
        StopBits = DEFAULT_STOPBITS;
    }

    public int BaudRate { get; set; }

    public UsbSerialParity Parity { get; set; }

    public int DataBits { get; set; }

    public UsbSerialStopBits StopBits { get; set; }

    public event EventHandler<SerialDataReceivedArgs> DataReceived;

    public event EventHandler<UnhandledExceptionEventArgs> ErrorReceived;

    public void Open(UsbManager usbManager, int bufferSize = DEFAULT_BUFFERSIZE)
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
        if (IsOpen)
            throw new InvalidOperationException();

        var connection = usbManager.OpenDevice(_port.GetDriver().GetDevice());
        if (connection == null)
            throw new Java.IO.IOException("Failed to open device");
        IsOpen = true;

        _buffer = new byte[bufferSize];
        _port.Open(connection);
        _port.SetParameters(BaudRate, DataBits, StopBits, Parity);

        _cancelationTokenSource = new CancellationTokenSource();
        var cancelationToken = _cancelationTokenSource.Token;
        cancelationToken.Register(() => Log.Info(TAG, "Cancellation Requested"));

        Task.Run(() => {
            Log.Info(TAG, "Task Started!");
            try
            {
                while (true)
                {
                    cancelationToken.ThrowIfCancellationRequested();

                    Step(); // execute step
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Warn(TAG, "Task ending due to exception: " + e.Message, e);
                ErrorReceived.Raise(this, new UnhandledExceptionEventArgs(e, false));
            }
            finally
            {
                _port.Close();
                _buffer = null;
                IsOpen = false;
                Log.Info(TAG, "Task Ended!");
            }
        }, cancelationToken);
    }

    public void Close()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
        if (!IsOpen)
            throw new InvalidOperationException();

        // cancel task
        _cancelationTokenSource.Cancel();
    }

    public bool IsOpen { get; private set; }

    private void Step()
    {
        // handle incoming data.
        var len = _port.Read(_buffer, READ_WAIT_MILLIS);
        if (len <= 0) return;
        Log.Debug(TAG, "Read data len=" + len);

        var data = new byte[len];
        Array.Copy(_buffer, data, len);
        DataReceived.Raise(this, new SerialDataReceivedArgs(data));
    }


    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && (_cancelationTokenSource != null))
        {
            Close();
        }

        _disposed = true;
    }

    ~SerialInputOutputManager() =>
        Dispose(false);
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }



}
