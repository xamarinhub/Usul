using Android.App;
using Android.Content;
using Android.Hardware.Usb;

namespace Usul.Providers.SerialPort.Maui.Implementation;

public class SerialPortImplementation
{
    public void Open()
    {
        var activity = Platform.CurrentActivity;
        var manager = (UsbManager)activity!.GetSystemService(Context.UsbService);
        var devicesDictionary = manager!.DeviceList;

        var dvc = devicesDictionary!.ElementAt(0).Value;

        var ACTION_USB_PERMISSION = "rzepak";

        var interf = dvc.GetInterface(1);

        var outEndpoint = interf.GetEndpoint(1);

        var mPermissionIntent = PendingIntent.GetBroadcast(activity, 0, new Intent(ACTION_USB_PERMISSION), 0);
        var filter = new IntentFilter(ACTION_USB_PERMISSION);

        if (manager.HasPermission(dvc) == false) manager.RequestPermission(dvc, mPermissionIntent);

        var deviceConnection = manager.OpenDevice(dvc);

        if (deviceConnection != null) deviceConnection.ClaimInterface(interf, true);

    }
}

