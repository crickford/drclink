using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// An unmanaged test double exercises actual function-pointer calls across the ABI.
// It does not depend on Windows, WinRT, Intel, or the radio.
internal sealed unsafe class FakeLegacySettings : IDisposable
{
    private struct State
    {
        public nint Vtable;
        public int SetterResult;
        public int GetterResult;
        public int SetterCalls;
        public int GetterCalls;
        public nint LastArgument;
        public nint Readback;
    }
    private readonly State* _state;
    private readonly nint* _vtable;
    public nint Pointer => (nint)_state;
    public int SetterCalls => _state->SetterCalls;
    public int GetterCalls => _state->GetterCalls;
    public nint LastArgument => _state->LastArgument;

    public FakeLegacySettings(int setterResult = 0, int getterResult = 0, nint readback = 0)
    {
        _state = (State*)NativeMemory.AllocZeroed((nuint)sizeof(State));
        _vtable = (nint*)NativeMemory.AllocZeroed(12, (nuint)sizeof(nint));
        _state->Vtable = (nint)_vtable;
        _state->SetterResult = setterResult;
        _state->GetterResult = getterResult;
        _state->Readback = readback;
        _vtable[10] = (nint)(delegate* unmanaged[Stdcall]<nint, nint*, int>)&Get;
        _vtable[11] = (nint)(delegate* unmanaged[Stdcall]<nint, nint, int>)&Put;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Put(nint self, nint value)
    {
        var state = (State*)self;
        state->SetterCalls++;
        state->LastArgument = value;
        return state->SetterResult;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Get(nint self, nint* value)
    {
        var state = (State*)self;
        state->GetterCalls++;
        *value = state->Readback;
        return state->GetterResult;
    }

    public void Dispose() { NativeMemory.Free(_vtable); NativeMemory.Free(_state); }
}
