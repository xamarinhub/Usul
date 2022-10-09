namespace Usul.Providers.SerialPort.Default;

internal static class ParityExtensions
{
    public static System.IO.Ports.Parity ToParity(this Parity parity) =>
        parity switch
        {
            Parity.Even => System.IO.Ports.Parity.Even,
            Parity.None => System.IO.Ports.Parity.None,
            Parity.Odd => System.IO.Ports.Parity.Odd,
            Parity.Mark => System.IO.Ports.Parity.Mark,
            Parity.Space => System.IO.Ports.Parity.Space,
            _ => System.IO.Ports.Parity.None
        };
}
