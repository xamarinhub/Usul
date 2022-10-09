/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.App;
using Android.Content;
using Android.Hardware.Usb;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Extensions;

public static class UsbManagerExtensions
{
    private const string ACTION_USB_PERMISSION = "com.Hoho.Android.UsbSerial.Util.USB_PERMISSION";

    public static Task<bool> RequestPermissionAsync(this UsbManager manager, UsbDevice device, Context context)
    {
        var completionSource = new TaskCompletionSource<bool>();

        var usbPermissionReceiver = new UsbPermissionReceiver(completionSource);
        context.RegisterReceiver(usbPermissionReceiver, new IntentFilter(ACTION_USB_PERMISSION));

        var intent = PendingIntent.GetBroadcast(context, 0, new Intent(ACTION_USB_PERMISSION), 0);
        manager.RequestPermission(device, intent);

        return completionSource.Task;
    }

    private class UsbPermissionReceiver: BroadcastReceiver
    {
        readonly TaskCompletionSource<bool> completionSource;

        public UsbPermissionReceiver(TaskCompletionSource<bool> completionSource)
        {
            this.completionSource = completionSource;
        }

        public override void OnReceive(Context context, Intent intent)
        {
            _ = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
            var permissionGranted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
            context.UnregisterReceiver(this);
            completionSource.TrySetResult(permissionGranted);
        }
    }

}
