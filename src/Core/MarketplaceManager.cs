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

    public static void Update(float dt)
    {
        _marketUpdateTimer += dt;
        if (_marketUpdateTimer < MarketTickInterval) return;
        _marketUpdateTimer = 0f;
        var rnd = new System.Random();
        GlobalDemandMultiplier = 0.5f + (float)rnd.NextDouble() * 1.5f;
        MarketPricePerIops = 0.05f * GlobalDemandMultiplier;
        ProcessAutomaticSales();
        MelonLogger.Msg($"[Economy] Market shift. Demand: {GlobalDemandMultiplier:P0}, Price: ${MarketPricePerIops:F4}/IOPS");
    }

    private static void ProcessAutomaticSales()
    {
        float totalIops = 0f;
        foreach (var component in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (component == null || !component.GetType().Name.Contains("Switch")) continue;
            try
            {
                var type = component.GetType();
                var ports = type.GetField("cableLinkSwitchPorts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(component)
                    ?? type.GetProperty("cableLinkSwitchPorts", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(component);
                if (ports is System.Collections.ICollection collection) totalIops += collection.Count * 1000f;
            }
            catch { }
        }

        float revenue = totalIops * 0.4f * MarketPricePerIops;
        var player = PlayerManager.instance?.playerClass;
        if (revenue > 0 && player != null)
        {
            player.UpdateCoin(revenue, false);
            MelonLogger.Msg($"[Economy] Market sale: +${revenue:F2}.");
        }
    }
}
