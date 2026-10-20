using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DrcLink.Probe;

internal static class Inventory
{
    public static async Task CollectAsync(ProbeLog log, CancellationToken token)
    {
        log.Save("environment.json", new {
            os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            runtime = RuntimeInformation.FrameworkDescription,
            probeVersion = "0.1.1"
        });
        // These commands only read capabilities. No profiles, passwords, or nearby SSID scans.
        await CaptureAsync(log, "drivers.txt", ["wlan", "show", "drivers"], token);
        await CaptureAsync(log, "wirelesscapabilities.txt", ["wlan", "show", "wirelesscapabilities"], token);
    }

    private static async Task CaptureAsync(ProbeLog log, string file, string[] args, CancellationToken token)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo {
            FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        }};
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        try
        {
            if (!process.Start()) throw new InvalidOperationException("netsh did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync(token);
            var stderr = process.StandardError.ReadToEndAsync(token);
            try { await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(20), token); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
            File.WriteAllText(Path.Combine(log.DirectoryPath, file), await stdout + "\n" + await stderr);
            log.Write("inventory-command", new { args, process.ExitCode, file });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { log.Exception($"inventory:{file}", ex); }
    }
}
