using System;

namespace greg.Mods.EconomyEngine.Core;

// Detects at runtime whether gregCore is present (without a hard
// runtime dependency: pure type-name lookup, no direct type access).
// With core: optimized central services. Without: mod-local minimal
// implementations (standalone mode, basic features only).
// IMPORTANT: methods touching gregCore types must ONLY be called
// when HasCore is true (otherwise JIT TypeLoad when the DLL is missing).
public static class GregHost
{
    private const string ProbeType = "gregCore.UI.GregNotificationManager, gregCore";
    private static bool? _hasCore;

    public static bool HasCore
    {
        get
        {
            if (_hasCore == null)
            {
                try { _hasCore = Type.GetType(ProbeType) != null; }
                catch { _hasCore = false; }
            }
            return _hasCore.Value;
        }
    }

    // For testing purposes only (e.g. forcing standalone behavior).
    public static void OverrideForTesting(bool? value)
    {
        _hasCore = value;
    }
}
