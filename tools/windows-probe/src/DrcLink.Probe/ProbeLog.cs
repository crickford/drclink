using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DrcLink.Probe;

internal sealed class ProbeLog : IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _disposed;
    public string DirectoryPath { get; }

    public ProbeLog(string? root)
    {
        root ??= Path.Combine(Environment.CurrentDirectory, "logs");
        DirectoryPath = Path.Combine(Path.GetFullPath(root),
            $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
        _writer = new StreamWriter(Path.Combine(DirectoryPath, "events.jsonl"), false, new UTF8Encoding(false))
            { AutoFlush = true };
    }

    public void Write(string kind, object? data = null)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _writer.WriteLine(JsonSerializer.Serialize(new {
                utc = DateTimeOffset.UtcNow, elapsedMs = Math.Round(_clock.Elapsed.TotalMilliseconds, 3), kind, data
            }));
            Console.WriteLine($"[{_clock.Elapsed.TotalSeconds,7:F2}s] {kind}: {JsonSerializer.Serialize(data)}");
        }
    }

    public void Exception(string stage, Exception ex) => Write("exception", new {
        stage, type = ex.GetType().FullName, hresult = $"0x{unchecked((uint)ex.HResult):X8}", ex.Message,
        stackTrace = ex.StackTrace,
        innerException = ex.InnerException is null ? null : new {
            type = ex.InnerException.GetType().FullName, hresult = $"0x{unchecked((uint)ex.InnerException.HResult):X8}", ex.InnerException.Message
        }
    });

    public void Save(string name, object data) => File.WriteAllText(Path.Combine(DirectoryPath, name),
        JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    public void Dispose()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; _writer.Dispose(); }
    }
}
