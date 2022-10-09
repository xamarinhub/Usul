/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Hardware.Usb;

// ReSharper disable once CheckNamespace
namespace Usul.Providers.SerialPort.Maui.Implementation.Drivers;

public interface IUsbSerialDriver
{
    UsbDevice Device { get; }

    UsbDevice GetDevice();

    List<UsbSerialPort> Ports { get; }
    List<UsbSerialPort> GetPorts();
}

public class UsbSerialDriver : IUsbSerialDriver
{
    protected UsbDevice _device;
    
    protected UsbSerialPort _port;

    public UsbDevice Device => GetDevice();

    public List<UsbSerialPort> Ports => GetPorts();

    public UsbDevice GetDevice() => _device;

    public virtual List<UsbSerialPort> GetPorts() => new() { _port };
}
