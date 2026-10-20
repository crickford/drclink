using System.Runtime.InteropServices;
using Windows.Devices.WiFiDirect;
using WinRT;

namespace DrcLink.Probe;

internal sealed record NativeOpenResult(string QueryInterfaceHresult, bool SetterWasInvoked,
    string? SetterHresult, string? GetterHresult, bool? GetterReturnedNull)
{
    public bool SettingConfirmedNull => SetterWasInvoked && SetterHresult is not null
        && Convert.ToUInt32(SetterHresult[2..], 16) < 0x80000000
        && GetterHresult is not null && Convert.ToUInt32(GetterHresult[2..], 16) < 0x80000000
        && GetterReturnedNull == true;
}

internal static class NativeOpenProbe
{
    public static NativeOpenResult Run(WiFiDirectLegacySettings legacy, ProbeLog log)
    {
        // Use the C#/WinRT-supported native object reference, then QI explicitly.
        // Only the null property argument bypasses the C# projection.
        var nativeObject = ((IWinRTObject)legacy).NativeObject;
        var iid = LegacySettingsAbi.InterfaceId;
        nint settings = 0;
        nint credential = 0;
        try
        {
            var qi = Marshal.QueryInterface(nativeObject.ThisPtr, ref iid, out settings);
            log.Write("native-open-query-interface", new { hresult = LegacySettingsAbi.Hex(qi), hasInterfacePointer = settings != 0 });
            if (qi < 0 || settings == 0)
                return new(LegacySettingsAbi.Hex(qi), false, null, null, null);

            var setter = LegacySettingsAbi.PutNullPassphrase(settings);
            log.Write("native-open-setter", new {
                hresult = LegacySettingsAbi.Hex(setter), nullArgumentPassed = true,
                succeeded = setter >= 0,
                interpretation = setter < 0 ? "Native Windows interface returned a failure HRESULT." : "Native setter returned success; readback and radio verification are still required."
            });
            if (setter < 0)
                return new(LegacySettingsAbi.Hex(qi), true, LegacySettingsAbi.Hex(setter), null, null);

            var getter = LegacySettingsAbi.GetPassphrase(settings, out credential);
            log.Write("native-open-getter", new {
                hresult = LegacySettingsAbi.Hex(getter), succeeded = getter >= 0,
                credentialIsNull = getter >= 0 ? credential == 0 : (bool?)null
            });
            return new(LegacySettingsAbi.Hex(qi), true, LegacySettingsAbi.Hex(setter),
                LegacySettingsAbi.Hex(getter), getter >= 0 ? credential == 0 : null);
        }
        finally
        {
            if (credential != 0) Marshal.Release(credential);
            if (settings != 0) Marshal.Release(settings);
            GC.KeepAlive(nativeObject);
            GC.KeepAlive(legacy);
        }
    }
}
