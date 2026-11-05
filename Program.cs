using System.Runtime.InteropServices;
using System.Text;

namespace NfcLens;

static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (args.Contains("--self-test")) { SelfTest.Run(); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }
}

sealed class Pcsc : IDisposable
{
    nint context, card; uint protocol;
    public Pcsc() { Check(SCardEstablishContext(0, 0, 0, out context)); }
    public string[] Readers()
    {
        uint length = 0; int code = SCardListReaders(context, null, null, ref length);
        if (unchecked((uint)code) == 0x8010002E) return [];
        Check(code); var buffer = new char[length]; Check(SCardListReaders(context, null, buffer, ref length));
        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }
    public void Connect(string reader) { Check(SCardConnect(context, reader, 2, 3, out card, out protocol)); }
    public byte[] Atr()
    {
        uint names = 1024, atrLength = 64; var name = new char[names]; var atr = new byte[atrLength];
        Check(SCardStatus(card, name, ref names, out _, out _, atr, ref atrLength)); return atr[..(int)atrLength];
    }
    public byte[] Exchange(byte[] command)
    {
        var pci = new IoRequest { Protocol = protocol, Length = 8 }; var output = new byte[65538]; uint length = (uint)output.Length;
        Check(SCardTransmit(card, ref pci, command, (uint)command.Length, 0, output, ref length)); return output[..(int)length];
    }
    static void Check(int code)
    {
        if (code == 0) return;
        string message = unchecked((uint)code) switch
        {
            0x8010000C => "No card detected. Place or insert a card and try again.", 0x80100069 => "Card removed during reading. Keep it in place and retry.",
            0x8010001D => "Windows Smart Card service is unavailable. Check the reader driver and Smart Card service.",
            0x8010000B => "Reader is busy in another application.", 0x80100017 => "Reader unavailable. Reconnect it and refresh.", _ => "Smartcard communication failed"
        };
        throw new IOException($"{message} (0x{unchecked((uint)code):X8})");
    }
    public void Dispose() { if (card != 0) { SCardDisconnect(card, 0); card = 0; } if (context != 0) { SCardReleaseContext(context); context = 0; } }
    [StructLayout(LayoutKind.Sequential)] struct IoRequest { public uint Protocol; public uint Length; }
    [DllImport("winscard.dll")] static extern int SCardEstablishContext(uint scope, nint a, nint b, out nint context);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode, EntryPoint = "SCardListReadersW")] static extern int SCardListReaders(nint context, string? groups, [Out] char[]? readers, ref uint count);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode, EntryPoint = "SCardConnectW")] static extern int SCardConnect(nint context, string reader, uint share, uint preferred, out nint card, out uint protocol);
    [DllImport("winscard.dll", CharSet = CharSet.Unicode, EntryPoint = "SCardStatusW")] static extern int SCardStatus(nint card, [Out] char[] readers, ref uint count, out uint state, out uint protocol, [Out] byte[] atr, ref uint atrLength);
    [DllImport("winscard.dll")] static extern int SCardTransmit(nint card, ref IoRequest pci, byte[] command, uint commandLength, nint receivePci, [Out] byte[] response, ref uint responseLength);
    [DllImport("winscard.dll")] static extern int SCardDisconnect(nint card, uint disposition);
    [DllImport("winscard.dll")] static extern int SCardReleaseContext(nint context);
}

