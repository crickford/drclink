using System.Globalization;
using System.Text;

namespace DrcLink.Probe;

internal sealed record Options(string Command, string Mode, string Ssid, int Seconds, string? Output)
{
    public static Options Parse(string[] args)
    {
        if (args.Length == 0 || args is ["--help"] || args is ["-h"])
            return new("help", "protected", "DrcLink-Probe", 60, null);
        if (args[0] is not ("inspect" or "probe"))
            throw new ArgumentException("Command must be inspect or probe. Use --help.");

        var command = args[0];
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || args[i] is not ("--mode" or "--ssid" or "--seconds" or "--out"))
                throw new ArgumentException($"Unknown option or missing value: {args[i]}");
            if (!values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException($"Duplicate option: {args[i]}");
        }
        if (command == "inspect" && values.Keys.Any(k => k != "--out"))
            throw new ArgumentException("inspect accepts only --out.");
        var mode = values.GetValueOrDefault("--mode", "protected");
        if (mode is not ("protected" or "open"))
            throw new ArgumentException("--mode must be protected or open.");
        var ssid = values.GetValueOrDefault("--ssid", mode == "open" ? "DrcLink-Open" : "DrcLink-Probe");
        if (string.IsNullOrWhiteSpace(ssid) || Encoding.UTF8.GetByteCount(ssid) > 32 || ssid.Any(char.IsControl))
            throw new ArgumentException("SSID must be 1-32 UTF-8 bytes with no control characters.");
        var secondsText = values.GetValueOrDefault("--seconds", "60");
        if (!int.TryParse(secondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds is < 10 or > 300)
            throw new ArgumentException("--seconds must be 10 through 300.");
        var output = values.GetValueOrDefault("--out");
        if (output is not null && string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("--out cannot be empty.");
        return new(command, mode, ssid, seconds, output);
    }
}
