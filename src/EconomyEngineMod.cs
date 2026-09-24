using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using greg.Mods.EconomyEngine.Core;
using greg.Mods.EconomyEngine.UI;

[assembly: MelonInfo(typeof(greg.Mods.EconomyEngine.EconomyEngineMod), "gregMod.EconomyEngine", "1.0.1", "teamGreg")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace greg.Mods.EconomyEngine;

public class EconomyEngineMod : MelonMod
{
    private static Key _marketKey = Key.F11;
    private static Key _diagnoseKey = Key.F10;

    public static string MarketKeyLabel
    {
        get { try { return _marketKey.ToString(); } catch { return "F11"; } }
    }

    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Economy Engine v1.0.1 initialized.");
        try
        {
            var cat = MelonPreferences.CreateCategory("EconomyEngine");
            var marketEntry = cat.CreateEntry("MarketKey", "F11", "MarketKey", "Hotkey to open/close the marketplace.");
            var diagEntry = cat.CreateEntry("DiagnoseKey", "F10", "DiagnoseKey", "Hotkey to run a demand diagnosis.");
            if (Enum.TryParse<Key>(marketEntry.Value, true, out var mk) && mk != Key.None)
                _marketKey = mk;
            else
                MelonLogger.Warning($"[Economy] Unknown MarketKey '{marketEntry.Value}', defaulting to F11.");
            if (Enum.TryParse<Key>(diagEntry.Value, true, out var dk) && dk != Key.None)
                _diagnoseKey = dk;
            else
                MelonLogger.Warning($"[Economy] Unknown DiagnoseKey '{diagEntry.Value}', defaulting to F10.");
        }
        catch { }
        DemandIntegration.Initialize();
        if (GregHost.HasCore)
        {
            try { RegisterCoreExtras(); } catch { }
        }
    }

    // Mod contract + key HUD + opener for the F1 hub. Only call with gregCore
    // (separate method for JIT isolation without the gregCore DLL).
    private static void RegisterCoreExtras()
    {
        try
        {
            gregCore.Core.Mods.GregModRegistry.Register(
                "gregMod.EconomyEngine", "Economy", "1.0.1",
                new string[] { "economy.marketplace" });
            gregCore.UI.GregMenuRegistry.RegisterMenu("economy.marketplace",
                new gregCore.UI.GregMenuOptions
                {
                    LockCamera = true,
                    LockMovement = true,
                    LockInteract = true,
                    ShowCursor = true,
                });
            gregCore.UI.GregHudRegistry.Register("economy", _marketKey.ToString(), "Market");
            gregCore.UI.GregMenuRegistry.RegisterOpener("economy.marketplace", () =>
            {
                try
                {
                    MarketplaceUI.Toggle();
                    gregCore.UI.GregMenuRegistry.SetOpen("economy.marketplace", MarketplaceUI.IsVisible);
                }
                catch { }
            });
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("[Economy] Hub-Registrierung fehlgeschlagen: " + ex.GetBaseException().Message);
        }
    }

    public override void OnUpdate()
    {
        try { MarketplaceManager.Update(Time.deltaTime); }
        catch (Exception ex)
        {
            MelonLogger.Error("[Economy] Marketplace update failed: " + ex.GetBaseException().Message);
        }
        try { DemandIntegration.Update(Time.deltaTime); }
        catch (Exception ex)
        {
            MelonLogger.Error("[Economy] Demand update failed: " + ex.GetBaseException().Message);
        }

        if (Keyboard.current != null)
        {
            try
            {
                var ctrl = Keyboard.current[_marketKey];
                if (ctrl != null && ctrl.wasPressedThisFrame)
                {
                    MarketplaceUI.Toggle();
                    if (GregHost.HasCore)
                    {
                        try { SetMarketOpen(MarketplaceUI.IsVisible); } catch { }
                    }
                }
            }
            catch { }
            try
            {
                var ctrl = Keyboard.current[_diagnoseKey];
                if (ctrl != null && ctrl.wasPressedThisFrame)
                {
                    DemandIntegration.DiagnoseNow();
                }
            }
            catch { }
        }
    }

    // gregCore SetOpen in a separate method (JIT isolation without the gregCore DLL).
    private static void SetMarketOpen(bool open)
    {
        gregCore.UI.GregMenuRegistry.SetOpen("economy.marketplace", open);
    }
}
