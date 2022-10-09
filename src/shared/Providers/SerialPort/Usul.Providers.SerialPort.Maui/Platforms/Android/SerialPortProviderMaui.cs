using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Usul.Providers.SerialPort.Maui.Implementation.Drivers;
using Usul.Providers.SerialPort.Maui.Implementation.Extensions;
using FileNotFoundException = System.IO.FileNotFoundException;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui;

internal partial class SerialPortProviderMaui
{
    private const string EXTRA_TAG = "PortInfo";
    private readonly UsbManager _usbManager;
    private readonly UsbSerialPortInfo _serialPortInfo;
    public readonly Activity _activity;

    public SerialPortProviderMaui()
    {
        _activity = Platform.CurrentActivity!;
        _usbManager = _activity!.GetSystemService(Context.UsbService) as UsbManager;
        _serialPortInfo = _activity.Intent!.GetParcelableExtra(EXTRA_TAG) as UsbSerialPortInfo;
    }

    public async partial Task<IList<string>> GetPortNamesAsync()
    {
        if (_serialPortInfo is null) return Array.Empty<string>();

        var vendorId = _serialPortInfo.VendorId;
        var deviceId = _serialPortInfo.DeviceId;
        //var portNumber = _serialPortInfo.PortNumber;

        var drivers = await FindAllDriversAsync(_usbManager);
        var driver = drivers.FirstOrDefault(d => d.Device.VendorId == vendorId && d.Device.DeviceId == deviceId);
        if (driver == null)
        {
            throw new FileNotFoundException("Driver specified in extra tag not found.");
        }

        return driver.Ports.Select(x => x.PortNumber.ToString()).ToList();
    }

    public partial Task<ISerialPort> OpenAsync(SerialPortConfiguration configuration) =>
        throw new FileNotFoundException();

    public partial ValueTask DisposeAsync()
    {
        _usbManager.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private static Task<IList<IUsbSerialDriver>> FindAllDriversAsync(UsbManager usbManager)
    {
        var table = UsbSerialProber.DefaultProbeTable;
        table.AddProduct(0x1b4f, 0x0008, typeof(CdcAcmSerialDriver));
        table.AddProduct(0x09D8, 0x0420, typeof(CdcAcmSerialDriver));

        var prober = new UsbSerialProber(table);
        return prober.FindAllDriversAsync(usbManager);
    }
}

