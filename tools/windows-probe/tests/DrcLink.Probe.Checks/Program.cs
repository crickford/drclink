using DrcLink.Probe;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
    checks++;
}
void Reject(string[] args, string description)
{
    try { Options.Parse(args); }
    catch (ArgumentException) { Check(true, description); return; }
    throw new Exception("FAIL: " + description);
}

Check(Options.Parse(["probe", "--mode", "open"]).Ssid == "DrcLink-Open", "open and protected use distinct SSIDs");
Reject(["probe", "--mode", "opne"], "misspelled open mode never silently becomes protected");
Reject(["probe", "--mode", "open", "--mode", "protected"], "ambiguous security options are rejected");
Reject(["probe", "--seconds", "0"], "duration is bounded");
Reject(["probe", "--seconds", "301"], "longer than 300 seconds is rejected");
Reject(["probe", "--ssid", new string('\u00e9', 17)], "SSID limit counts UTF-8 bytes");
Check(Options.Parse(["probe", "--ssid", new string('\u00e9', 16)]).Ssid.Length == 16, "32-byte UTF-8 SSID accepted");
Reject(["inspect", "--mode", "open"], "read-only inspect cannot accidentally enable an experiment");

var started = new PublicationTracker();
started.Observe("Created");
Check(!started.FirstState.IsCompleted, "Created is not successful publication");
started.Observe("Started");
Check(await started.FirstState == "Started" && started.EverStarted, "Started resolves initial wait");
started.Observe("Aborted");
Check(await started.TerminalState == "Aborted", "abort after startup remains a test failure");

var rejected = new PublicationTracker();
rejected.Observe("Aborted");
Check(await rejected.FirstState == "Aborted" && !rejected.EverStarted, "startup rejection does not become success");

var normal = new PublicationTracker();
normal.Observe("Started");
normal.BeginCleanup();
normal.Observe("Stopped");
Check(!normal.TerminalState.IsCompleted, "intentional cleanup is not a radio failure");

var stopped = new PublicationTracker();
stopped.Observe("Started");
stopped.Observe("Stopped");
Check(await stopped.TerminalState == "Stopped", "unexpected stop during observation is reported");

Check(!PublicationTracker.ShouldRequestStop(false, "Created"), "configuration failure never calls Stop on an unstarted publisher");
Check(PublicationTracker.ShouldRequestStop(true, "Created"), "pending startup is stopped during cleanup");
Check(PublicationTracker.ShouldRequestStop(true, "Started"), "started publisher is stopped during cleanup");
Check(!PublicationTracker.ShouldRequestStop(true, "Aborted"), "already aborted publisher needs no Stop call");
Check(!PublicationTracker.ShouldRequestStop(true, "Stopped"), "already stopped publisher needs no Stop call");

using (var nativeFailure = new FakeLegacySettings(unchecked((int)0x80004003)))
{
    var hr = LegacySettingsAbi.PutNullPassphrase(nativeFailure.Pointer);
    Check(nativeFailure.SetterCalls == 1 && nativeFailure.LastArgument == 0, "ABI setter receives an actual null pointer");
    Check(hr == unchecked((int)0x80004003), "native E_POINTER is preserved without conversion to a managed exception");
    Check(LegacySettingsAbi.Hex(hr) == "0x80004003", "native HRESULT is serialized exactly");
}
using (var nativeSuccess = new FakeLegacySettings())
{
    Check(LegacySettingsAbi.PutNullPassphrase(nativeSuccess.Pointer) == 0, "native setter success is preserved");
    var hr = LegacySettingsAbi.GetPassphrase(nativeSuccess.Pointer, out var value);
    Check(hr == 0 && value == 0 && nativeSuccess.GetterCalls == 1, "native getter can confirm a null readback");
}
using (var nativeReadbackFailure = new FakeLegacySettings(getterResult: unchecked((int)0x80004005)))
{
    var hr = LegacySettingsAbi.GetPassphrase(nativeReadbackFailure.Pointer, out _);
    Check(hr == unchecked((int)0x80004005), "getter failure is preserved for separate interpretation");
}
using (var retainedCredential = new FakeLegacySettings(readback: 123))
{
    var hr = LegacySettingsAbi.GetPassphrase(retainedCredential.Pointer, out var value);
    Check(hr == 0 && value == 123, "non-null readback is exposed rather than assumed to be open authentication");
}
try { LegacySettingsAbi.PutNullPassphrase(0); throw new Exception("FAIL: null interface pointer accepted"); }
catch (ArgumentOutOfRangeException) { Check(true, "null interface pointer is rejected before unmanaged invocation"); }
Console.WriteLine($"{checks} checks passed.");
