/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.OS;
using Java.Interop;
using Usul.Providers.SerialPort.Maui.Implementation.Drivers;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Extensions;

public sealed class UsbSerialPortInfo : Java.Lang.Object, IParcelable
{
    private static readonly IParcelableCreator _creator = new ParcelableCreator();

    [ExportField("CREATOR")]
    public static IParcelableCreator GetCreator() => _creator;

    public UsbSerialPortInfo()
    {
    }

    public UsbSerialPortInfo(UsbSerialPort port)
    {
        var device = port.Driver.Device;
        VendorId = device.VendorId;
        DeviceId = device.DeviceId;
        PortNumber = port.PortNumber;
    }

    private UsbSerialPortInfo(Parcel parcel)
    {
        VendorId = parcel.ReadInt();
        DeviceId = parcel.ReadInt();
        PortNumber = parcel.ReadInt();
    }

    public int VendorId { get; set; }

    public int DeviceId { get; set; }

    public int PortNumber { get; set; }

    public int DescribeContents() => 0;

    public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
    {
        dest.WriteInt(VendorId);
        dest.WriteInt(DeviceId);
        dest.WriteInt(PortNumber);
    }

    public sealed class ParcelableCreator : Java.Lang.Object, IParcelableCreator
    {
        public Java.Lang.Object CreateFromParcel(Parcel parcel) => new UsbSerialPortInfo(parcel);

        // ReSharper disable once CoVariantArrayConversion
        public Java.Lang.Object[] NewArray(int size) => new UsbSerialPortInfo[size];
    }
}
