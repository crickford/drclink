namespace DrcLink.Probe;

// IWiFiDirectLegacySettings ABI from Windows SDK 10.0.19041.56.
// IUnknown: 0..2; IInspectable: 3..5; IsEnabled: 6..7; Ssid: 8..9;
// Passphrase: get=10, put=11. The SDK projection IL confirms these slots.
// This class has no WinRT object marshaling and returns the native HRESULT unchanged.
internal static unsafe class LegacySettingsAbi
{
    public static readonly Guid InterfaceId = new("A64FDBBA-F2FD-4567-A91B-F5C2F5321057");

    public static int PutNullPassphrase(nint settings)
    {
        ArgumentOutOfRangeException.ThrowIfZero(settings);
        var table = *(nint**)settings;
        if (table == null || table[11] == 0) throw new InvalidOperationException("Missing Passphrase setter in native vtable.");
        var setter = (delegate* unmanaged[Stdcall]<nint, nint, int>)table[11];
        return setter(settings, 0);
    }

    // The caller owns any non-null returned IInspectable reference and must release it.
    public static int GetPassphrase(nint settings, out nint credential)
    {
        ArgumentOutOfRangeException.ThrowIfZero(settings);
        var table = *(nint**)settings;
        if (table == null || table[10] == 0) throw new InvalidOperationException("Missing Passphrase getter in native vtable.");
        var getter = (delegate* unmanaged[Stdcall]<nint, nint*, int>)table[10];
        nint value = 0;
        var hr = getter(settings, &value);
        credential = value;
        return hr;
    }

    public static string Hex(int hr) => $"0x{unchecked((uint)hr):X8}";
}
