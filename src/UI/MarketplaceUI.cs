using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using greg.Mods.EconomyEngine.Core;

namespace greg.Mods.EconomyEngine.UI;

public sealed class MarketplaceUI : MonoBehaviour
{
    private static MarketplaceUI _instance;
    private static bool _typesRegistered;
    private bool _visible;

    private sealed class Toast
    {
        public string Message;
        public float Until;
    }

    private static readonly List<Toast> _toasts = new List<Toast>();
    private const int MaxToasts = 3;

    public MarketplaceUI(IntPtr ptr) : base(ptr) { }

    public static void Toggle()
    {
        if (_instance == null)
        {
            EnsureTypesRegistered();
            var go = new GameObject("EconomyEngineMarketplaceUI");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MarketplaceUI>();
        }
        _instance._visible = !_instance._visible;
    }

    public static bool IsVisible => _instance != null && _instance._visible;

    /// <summary>
    /// Registriert den UI-Typ in IL2CPP, bevor er als Component hinzugefuegt
    /// wird. Ohne Registrierung schlaegt AddComponent fehl.
    /// </summary>
    private static void EnsureTypesRegistered()
    {
        if (_typesRegistered) return;
        _typesRegistered = true;
        try
        {
            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<MarketplaceUI>();
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[Economy] MarketplaceUI-Typregistrierung fehlgeschlagen: " +
                ex.GetBaseException().Message);
            throw;
        }
    }

    // Dependency-free in-game toast: queued here, rendered in OnGUI.
    public static void PushToast(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        float duration = 6f;
        try
        {
            if (DemandIntegration.ToastDurationEntry != null)
                duration = DemandIntegration.ToastDurationEntry.Value;
        }
        catch { }
        if (duration < 1f) duration = 1f;
        lock (_toasts)
        {
            _toasts.Add(new Toast { Message = message, Until = Time.realtimeSinceStartup + duration });
            while (_toasts.Count > MaxToasts) _toasts.RemoveAt(0);
        }
    }

    private void OnGUI()
    {
        DrawToasts();
        if (_visible)
        {
            var area = new Rect(Screen.width - 380, 80, 350, 250);
            GUI.Box(area, "Global Market", ModImGuiTheme.Box());
            var inner = new Rect(area.x, area.y + 28, area.width, area.height - 36);
            GUI.BeginGroup(inner);
            DrawWindow(new Rect(0, 0, inner.width, inner.height));
            GUI.EndGroup();
        }
    }

    private void DrawToasts()
    {
        float now = Time.realtimeSinceStartup;
        lock (_toasts) _toasts.RemoveAll(t => t == null || now > t.Until);
        if (_toasts.Count == 0) return;
        float y = Screen.height - 40 - _toasts.Count * 30;
        lock (_toasts)
        {
            foreach (Toast t in _toasts)
            {
                GUI.Box(new Rect(16, y, 460, 26), "[Economy] " + t.Message);
                y += 30f;
            }
        }
    }

    private void DrawWindow(Rect area)
    {
        float x = 12f, w = area.width - 24f, y = 6f, lh = 20f;
        GUI.Label(new Rect(x, y, w, lh), $"Price: ${MarketplaceManager.MarketPricePerIops:F4} / IOPS", ModImGuiTheme.Label());
        y += lh;
        GUI.Label(new Rect(x, y, w, lh), $"Demand: {MarketplaceManager.GlobalDemandMultiplier:P0}", ModImGuiTheme.Label());
        y += lh;
        GUI.Label(new Rect(x, y, w, lh), "Automatic overcapacity sales run every 30 seconds.", ModImGuiTheme.Label(12, false, ModImGuiTheme.Dim));
        y += lh + 6f;
        GUI.Label(new Rect(x, y, w, lh), "Demand automation (gregCore):", ModImGuiTheme.Label(13, true));
        y += lh;
        GUI.Label(new Rect(x, y, w, lh), "  Scan: " + DemandIntegration.LastScanSummary, ModImGuiTheme.Label(12));
        y += lh;
        GUI.Label(new Rect(x, y, w, lh), string.Format("  Route: {0} Speed: {1} Produkte: {2} DryRun: {3}",
            OnOff(DemandIntegration.AutoRouteEntry),
            OnOff(DemandIntegration.AutoFeedEntry),
            OnOff(DemandIntegration.AutoProductsEntry),
            OnOff(DemandIntegration.DryRunEntry)), ModImGuiTheme.Label(12));
        y += lh;
        GUI.Label(new Rect(x, y, w, lh), string.Format("  Demand: x{0:F2} | Products last: {1}",
            MarketplaceManager.GlobalDemandMultiplier, DemandIntegration.LastProductsAdded), ModImGuiTheme.Label(12));
        y += lh + 6f;
        if (GUI.Button(new Rect(x, y, (w - 8f) / 2f, 28f), "Diagnose (Dry-Run)", ModImGuiTheme.Button())) DemandIntegration.DiagnoseNow();
        if (GUI.Button(new Rect(x + (w - 8f) / 2f + 8f, y, (w - 8f) / 2f, 28f), "Close", ModImGuiTheme.Button())) _visible = false;
    }

    private static string OnOff(MelonLoader.MelonPreferences_Entry<bool> e)
    {
        try { return e != null && e.Value ? "an" : "aus"; }
        catch { return "?"; }
    }
}
