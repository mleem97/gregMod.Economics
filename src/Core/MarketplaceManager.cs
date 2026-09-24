using System;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace greg.Mods.EconomyEngine.Core;

public static class MarketplaceManager
{
    public static float GlobalDemandMultiplier { get; private set; } = 1.0f;
    public static float MarketPricePerIops { get; private set; } = 0.05f;
    private static float _marketUpdateTimer;
    private const float MarketTickInterval = 30f;
    // Full switch search is expensive (iterates all scene objects): max 1x/120s.
// New builds take effect with a delay - accepted, no lag for that.
    private static float _switchCacheTime = -1e9f;
    private const float SwitchCacheTtl = 120f;
    private static readonly System.Collections.Generic.List<NetworkSwitch> _switchCache =
        new System.Collections.Generic.List<NetworkSwitch>();
    private static MethodInfo _updateCoinMethod;
    private static bool _updateCoinLookupDone;
    private static bool _updateCoinFailureLogged;

    public static void Update(float dt)
    {
        _marketUpdateTimer += dt;
        if (_marketUpdateTimer < MarketTickInterval) return;
        _marketUpdateTimer = 0f;
        // No more random walk: demand dynamics live in the customers
        // (DemandShift via CronWorker), not in price dice. Multiplier
        // constant 1.0 = feed the exact deficit.
        GlobalDemandMultiplier = 1.0f;
        MarketPricePerIops = 0.05f;
        try
        {
            ProcessAutomaticSales();
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[Economy] Market tick failed: " + ex.GetBaseException().Message);
        }
    }

    private static void ProcessAutomaticSales()
    {
        // Single writer in multiplayer: only the host writes money.
        // Clients would otherwise double-credit against the game syncs (XpDelta/Balance).
        // Standalone: always host (solo).
        try
        {
            if (GregHost.HasCore)
            {
                if (!HostCanMutateWorld()) return;
            }
        }
        catch { }
        // Direct typed access instead of FindObjectsOfType<MonoBehaviour>() +
        // reflection per component (which blocked the main thread for seconds
        // on large saves). NetworkSwitch.cableLinkSwitchPorts is
        // public API in the current game.
        float totalIops = 0f;
        try
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now - _switchCacheTime >= SwitchCacheTtl)
            {
                _switchCacheTime = now;
                _switchCache.Clear();
                var switches = UnityEngine.Object.FindObjectsOfType<NetworkSwitch>();
                if (switches != null)
                {
                    foreach (var sw in switches)
                    {
                        try
                        {
                            if (sw == null || sw.Pointer == IntPtr.Zero) continue;
                            var _ = sw.gameObject;
                            _switchCache.Add(sw);
                        }
                        catch { }
                    }
                }
            }
            foreach (var sw in _switchCache)
            {
                try
                {
                    if (sw == null || sw.Pointer == IntPtr.Zero) continue;
                    var ports = sw.cableLinkSwitchPorts;
                    if (ports != null) totalIops += ports.Count * 1000f;
                }
                catch { }
            }
        }
        catch { return; }

        float revenue = totalIops * 0.4f * MarketPricePerIops;
        var player = PlayerManager.instance?.playerClass;
        if (revenue > 0 && player != null)
        {
            if (TryUpdateCoin(player, revenue))
                MelonLogger.Msg($"[Economy] Market sale: +${revenue:F2}.");
        }
    }

    // Data Center 1.1 added the allowOverdraft parameter to Player.UpdateCoin.
    // Older reference packs only expose the original two-parameter method, so
    // resolve the available signature at runtime. The lookup itself is guarded:
    // ambiguous overloads must not take down the market tick.
    private static bool HostCanMutateWorld()
    {
        return gregCore.Infrastructure.Networking.GregNetSession.CanMutateWorld;
    }

    private static bool TryUpdateCoin(Player player, float amount)
    {
        if (!_updateCoinLookupDone)
        {
            _updateCoinLookupDone = true;
            try
            {
                _updateCoinMethod = ResolveUpdateCoinMethod();
            }
            catch (Exception ex)
            {
                LogUpdateCoinFailure("Player.UpdateCoin lookup failed: " + ex.GetBaseException().Message);
            }
        }

        if (_updateCoinMethod == null)
        {
            LogUpdateCoinFailure("Player.UpdateCoin was not found in the loaded Il2Cpp proxy.");
            return false;
        }

        try
        {
            var parameterCount = _updateCoinMethod.GetParameters().Length;
            object result = parameterCount switch
            {
                2 => _updateCoinMethod.Invoke(player, new object[] { amount, false }),
                3 => _updateCoinMethod.Invoke(player, new object[] { amount, false, false }),
                _ => throw new MissingMethodException(
                    $"Unsupported Player.UpdateCoin parameter count: {parameterCount}.")
            };

            return result is bool success && success;
        }
        catch (Exception ex)
        {
            LogUpdateCoinFailure($"Player.UpdateCoin invocation failed: {ex.GetBaseException().Message}");
            return false;
        }
    }

    /// <summary>
    /// Explicitly selects the matching UpdateCoin overload (2 or 3 parameters).
    /// AmbiguousMatchException-safe: no blind GetMethod with types:null.
    /// </summary>
    private static MethodInfo ResolveUpdateCoinMethod()
    {
        var candidates = typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        MethodInfo two = null;
        MethodInfo three = null;
        foreach (var m in candidates)
        {
            if (m.Name != nameof(Player.UpdateCoin)) continue;
            int n = m.GetParameters().Length;
            if (n == 2 && two == null) two = m;
            else if (n == 3 && three == null) three = m;
        }
        // Prefer the newer game version (allowOverdraft), otherwise legacy.
        return three ?? two;
    }

    private static void LogUpdateCoinFailure(string message)
    {
        if (_updateCoinFailureLogged) return;
        _updateCoinFailureLogged = true;
        MelonLogger.Error($"[Economy] {message}");
    }
}
