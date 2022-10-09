namespace Usul.Providers.SerialPort.Default;

internal static class StopBitsExtensions
{
    public static System.IO.Ports.StopBits ToStopBits(this StopBits stopBits) =>
        stopBits switch
        {
            StopBits.None => System.IO.Ports.StopBits.None,
            StopBits.One => System.IO.Ports.StopBits.One,
            StopBits.Two => System.IO.Ports.StopBits.Two,
            StopBits.OnePointFive => System.IO.Ports.StopBits.OnePointFive,
            _ => System.IO.Ports.StopBits.None
        };
}
