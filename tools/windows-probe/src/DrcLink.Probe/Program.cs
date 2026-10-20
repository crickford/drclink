namespace DrcLink.Probe;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Options options;
        try { options = Options.Parse(args); }
        catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        if (options.Command == "help")
        {
            Console.WriteLine("""
                DrcLink.Probe 0.1.1 - Windows GamePad link feasibility experiments
                inspect [--out PATH]
                probe [--mode protected|open] [--ssid NAME] [--seconds 10..300] [--out PATH]

                Turn OFF Windows Mobile hotspot before a probe. Keep Wi-Fi enabled.
                protected: temporary WPA2 test network and connection-event logging.
                open: call native Passphrase(NULL), record raw HRESULTs, no policy changes.
                Windows selects the band. Verify band and security from another device.
                There is no Nintendo WPS/WPA2, video streaming, or controller bridge yet.
                Exit 0 means the observation completed, not that the GamePad paired.
                """);
            return 0;
        }
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            Console.Error.WriteLine("Windows 10 build 19041 or later is required; this prototype targets Windows 11.");
            return 3;
        }
        try
        {
            using var log = new ProbeLog(options.Output);
            Console.WriteLine($"Logs: {log.DirectoryPath}");
            using var cancel = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancel.Cancel(); };
            Console.CancelKeyPress += handler;
            try
            {
                await Inventory.CollectAsync(log, cancel.Token);
                if (options.Command == "inspect") return 0;
                return await new RadioProbe(options, log).RunAsync(cancel.Token);
            }
            finally { Console.CancelKeyPress -= handler; }
        }
        catch (OperationCanceledException) { return 130; }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal: {ex.GetType().Name}: {ex.Message} (0x{unchecked((uint)ex.HResult):X8})");
            return 20;
        }
    }
}
