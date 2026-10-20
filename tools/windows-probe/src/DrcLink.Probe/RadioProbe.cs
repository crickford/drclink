using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Windows.Devices.WiFiDirect;
using Windows.Security.Credentials;

namespace DrcLink.Probe;

internal sealed class RadioProbe(Options options, ProbeLog log)
{
    private readonly PublicationTracker _tracker = new();
    private readonly ConcurrentDictionary<string, WiFiDirectDevice> _peers = new();
    private readonly Channel<WiFiDirectConnectionRequest> _requests = Channel.CreateBounded<WiFiDirectConnectionRequest>(8);
    private int _requestCount;
    private int _connectionCount;
    private int _closing;
    private bool? _passphraseReadbackNull;
    private NativeOpenResult? _nativeOpenResult;
    private string _stage = "activate-publisher";
    private string _lastStatus = "Unknown";
    private string? _lastError;

    public async Task<int> RunAsync(CancellationToken userToken)
    {
        WiFiDirectAdvertisementPublisher? publisher = null;
        WiFiDirectConnectionListener? listener = null;
        using var workers = CancellationTokenSource.CreateLinkedTokenSource(userToken);
        Task? worker = null;
        var exitCode = 10;
        var result = "initialization-failed";
        var cleanupStatus = "publisher-not-created";
        var startWasAttempted = false;
        object? failure = null;
        try
        {
            log.Write("scope", new {
                requestedMode = options.Mode, options.Ssid, options.Seconds,
                band = "Windows-selected; verify externally",
                implementsNintendoWps = false, implementsNintendoWpa2 = false,
                observesRawProbeOrAuthenticationFrames = false
            });
            publisher = new WiFiDirectAdvertisementPublisher();
            Observe(publisher.Status.ToString());
            publisher.StatusChanged += OnPublisherStatusChanged;
            _stage = "configure-advertisement";
            var advertisement = publisher.Advertisement;
            advertisement.IsAutonomousGroupOwnerEnabled = true;
            advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;
            var legacy = advertisement.LegacySettings;
            legacy.IsEnabled = true;
            legacy.Ssid = options.Ssid;

            if (options.Mode == "open")
            {
                _stage = "native-null-passphrase-probe";
                // Return raw HRESULTs instead of inferring native rejection from a managed exception.
                _nativeOpenResult = NativeOpenProbe.Run(legacy, log);
                _passphraseReadbackNull = _nativeOpenResult.GetterReturnedNull;
                if (!_nativeOpenResult.SettingConfirmedNull)
                {
                    result = !_nativeOpenResult.SetterWasInvoked ? "open-native-interface-unavailable"
                        : _nativeOpenResult.GetterHresult is null ? "open-native-setter-rejected"
                        : "open-native-readback-not-confirmed";
                    exitCode = 11;
                    return exitCode;
                }
            }
            else
            {
                _stage = "set-protected-passphrase";
                var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
                legacy.Passphrase = new PasswordCredential { Password = password };
                // Console only. The temporary test password is not written into the log files.
                Console.WriteLine($"Temporary test network password: {password}");
            }

            _stage = "activate-connection-listener";
            listener = new WiFiDirectConnectionListener();
            listener.ConnectionRequested += OnConnectionRequested;
            worker = HandleRequestsAsync(workers.Token);
            _stage = "publisher-start";
            startWasAttempted = true;
            publisher.Start();
            Observe(publisher.Status.ToString());
            _stage = "await-publisher-start";
            string first;
            try { first = await _tracker.FirstState.WaitAsync(TimeSpan.FromSeconds(20), userToken); }
            catch (TimeoutException)
            {
                result = "start-timeout";
                exitCode = 13;
                return exitCode;
            }
            if (first != "Started")
            {
                result = $"publisher-{first.ToLowerInvariant()}-before-start";
                exitCode = 12;
                return exitCode;
            }

            _stage = "observe-running-publisher";
            log.Write("ready-for-observation", new {
                requestedMode = options.Mode, options.Ssid,
                instruction = options.Mode == "open"
                    ? "On a second device, verify this SSID is OPEN and inspect its band. A Started event does not prove either."
                    : "Connect a phone using the temporary password to validate connection-event logging.",
                note = "No connection event does not establish that a GamePad sent no packets. Nintendo pairing is not implemented."
            });
            var duration = Task.Delay(TimeSpan.FromSeconds(options.Seconds), userToken);
            var winner = await Task.WhenAny(duration, _tracker.TerminalState);
            userToken.ThrowIfCancellationRequested();
            if (winner == _tracker.TerminalState || _tracker.TerminalState.IsCompleted)
            {
                result = $"publisher-{(await _tracker.TerminalState).ToLowerInvariant()}-during-test";
                exitCode = 14;
            }
            else
            {
                await duration;
                result = "publication-observation-complete";
                exitCode = 0;
            }
        }
        catch (OperationCanceledException) when (userToken.IsCancellationRequested)
        {
            result = "cancelled";
            exitCode = 130;
        }
        catch (Exception ex)
        {
            log.Exception(_stage, ex);
            failure = new { stage = _stage, type = ex.GetType().FullName,
                hresult = LegacySettingsAbi.Hex(ex.HResult), ex.Message, stackTrace = ex.StackTrace };
            result = _stage == "native-null-passphrase-probe"
                ? "native-interop-probe-failed" : "api-operation-failed";
            exitCode = 10;
        }
        finally
        {
            Interlocked.Exchange(ref _closing, 1);
            _tracker.BeginCleanup();
            if (listener is not null)
            {
                try { listener.ConnectionRequested -= OnConnectionRequested; }
                catch (Exception ex) { log.Exception("unsubscribe-listener", ex); }
            }
            _requests.Writer.TryComplete();
            workers.Cancel();
            if (worker is not null)
            {
                try { await worker; }
                catch (OperationCanceledException) { }
                catch (Exception ex) { log.Exception("connection-worker-cleanup", ex); }
            }
            while (_requests.Reader.TryRead(out var pending)) pending.Dispose();
            foreach (var peer in _peers.Values)
            {
                try { peer.ConnectionStatusChanged -= OnPeerStatusChanged; peer.Dispose(); }
                catch (Exception ex) { log.Exception("dispose-peer", ex); }
            }
            _peers.Clear();
            if (publisher is not null)
            {
                try
                {
                    var status = publisher.Status.ToString();
                    Observe(status);
                    if (PublicationTracker.ShouldRequestStop(startWasAttempted, status))
                    {
                        publisher.Stop();
                        var stopDeadline = DateTime.UtcNow.AddSeconds(5);
                        while (publisher.Status is not (WiFiDirectAdvertisementPublisherStatus.Stopped
                            or WiFiDirectAdvertisementPublisherStatus.Aborted) && DateTime.UtcNow < stopDeadline)
                            await Task.Delay(50);
                        cleanupStatus = publisher.Status is WiFiDirectAdvertisementPublisherStatus.Stopped
                            or WiFiDirectAdvertisementPublisherStatus.Aborted ? "terminal-state-observed" : "stop-not-confirmed";
                    }
                    else cleanupStatus = status == "Created" ? "not-needed-never-started" : "already-terminal";
                }
                catch (Exception ex) { cleanupStatus = "stop-failed"; log.Exception("publisher-stop", ex); }
                try { publisher.StatusChanged -= OnPublisherStatusChanged; }
                catch (Exception ex) { log.Exception("unsubscribe-publisher", ex); }
            }
            if (exitCode == 0 && cleanupStatus is "stop-failed" or "stop-not-confirmed")
            {
                exitCode = 16;
                result = "publication-completed-but-cleanup-unconfirmed";
            }
            var summary = new {
                version = "0.1.1", requestedMode = options.Mode, options.Ssid,
                exitCode, result, failedOrLastStage = _stage,
                apiReportedStarted = _tracker.EverStarted,
                passphraseReadbackWasNull = _passphraseReadbackNull,
                nativeOpenProbe = _nativeOpenResult, failure, startWasAttempted,
                lastPublisherStatus = _lastStatus, lastPublisherError = _lastError,
                cleanupStatus,
                connectionRequestCount = _requestCount, attachedPeerCount = _connectionCount,
                actualBand = "not-measured", actualAuthentication = "not-verified-over-air",
                gamePadPaired = false, nintendoHandshakeImplemented = false,
                interpretation = "Publication and API visibility experiment only. Exit 0 is not successful GamePad pairing."
            };
            log.Save("summary.json", summary);
            log.Write("finished", summary);
        }
        return exitCode;
    }

    private void Observe(string status)
    {
        _lastStatus = status;
        _tracker.Observe(status);
    }

    private void OnPublisherStatusChanged(WiFiDirectAdvertisementPublisher sender,
        WiFiDirectAdvertisementPublisherStatusChangedEventArgs args)
    {
        try
        {
            _lastError = args.Error.ToString();
            Observe(args.Status.ToString());
            log.Write("publisher-status", new { status = args.Status.ToString(), error = _lastError });
        }
        catch (Exception ex) { log.Exception("publisher-status-callback", ex); }
    }

    private void OnConnectionRequested(WiFiDirectConnectionListener sender,
        WiFiDirectConnectionRequestedEventArgs args)
    {
        WiFiDirectConnectionRequest? request = null;
        try
        {
            request = args.GetConnectionRequest();
            Interlocked.Increment(ref _requestCount);
            log.Write("connection-request", new { peer = PeerId(request.DeviceInformation.Id) });
            if (Volatile.Read(ref _closing) != 0 || !_requests.Writer.TryWrite(request))
            {
                request.Dispose();
                log.Write("connection-request-not-queued");
            }
        }
        catch (Exception ex) { request?.Dispose(); log.Exception("connection-request-callback", ex); }
    }

    private async Task HandleRequestsAsync(CancellationToken token)
    {
        await foreach (var request in _requests.Reader.ReadAllAsync(token))
        {
            using (request)
            {
                WiFiDirectDevice? device = null;
                try
                {
                    if (_peers.Count >= 8) { log.Write("peer-limit-reached"); continue; }
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(15));
                    device = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id).AsTask(timeout.Token);
                    if (device is null) { log.Write("peer-attach-returned-null"); continue; }
                    token.ThrowIfCancellationRequested();
                    device.ConnectionStatusChanged += OnPeerStatusChanged;
                    if (!_peers.TryAdd(device.DeviceId, device))
                    {
                        device.ConnectionStatusChanged -= OnPeerStatusChanged;
                        device.Dispose();
                        device = null;
                        continue;
                    }
                    var retainedDevice = device;
                    device = null; // Dictionary now owns its lifetime.
                    Interlocked.Increment(ref _connectionCount);
                    log.Write("peer-attached", new { peer = PeerId(retainedDevice.DeviceId),
                        status = retainedDevice.ConnectionStatus.ToString() });
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { device?.Dispose(); return; }
                catch (Exception ex) { device?.Dispose(); log.Exception("attach-peer", ex); }
            }
        }
    }

    private void OnPeerStatusChanged(WiFiDirectDevice device, object args)
    {
        try { log.Write("peer-status", new { peer = PeerId(device.DeviceId), status = device.ConnectionStatus.ToString() }); }
        catch (Exception ex) { log.Exception("peer-status-callback", ex); }
    }

    private static string PeerId(string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..12];
}
