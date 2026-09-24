using System;
using MelonLoader;
using UnityEngine;
using gregCore.PublicApi.Modules;
using gregCore.Infrastructure.Demand;

namespace greg.Mods.EconomyEngine.Core;

// Thin integration: all demand logic lives in gregCore (GregDemandModule).
// This class only owns the MelonPreferences, drives the scan interval,
// forwards gregCore toast events to the IMGUI toasts and exposes the last
// scan state for the MarketplaceUI window.
//
// Hard rule: gregCore missing/not ready must never break this mod -
// degrade to log + toast, keep the market running.
public static class DemandIntegration
{
    internal static MelonPreferences_Entry<bool> AutoRouteEntry;
    internal static MelonPreferences_Entry<bool> AutoFeedEntry;
    internal static MelonPreferences_Entry<bool> AutoProductsEntry;
    internal static MelonPreferences_Entry<bool> DryRunEntry;
    internal static MelonPreferences_Entry<bool> ToastsEnabledEntry;
    internal static MelonPreferences_Entry<float> ScanIntervalEntry;
    internal static MelonPreferences_Entry<float> ToastDurationEntry;
    internal static MelonPreferences_Entry<string> ProductAppIdsEntry;
    internal static MelonPreferences_Entry<int> ProductDifficultyEntry;
    internal static MelonPreferences_Entry<bool> CronWorkerEnabledEntry;
    internal static MelonPreferences_Entry<float> CronIntervalEntry;
    internal static MelonPreferences_Entry<float> CronSpeedScaleEntry;
    internal static MelonPreferences_Entry<int> CronMaxAppsEntry;
    internal static MelonPreferences_Entry<bool> ShiftDipEnabledEntry;
    internal static MelonPreferences_Entry<bool> ShiftNewServiceEnabledEntry;
    internal static MelonPreferences_Entry<float> ShiftDipAmountEntry;
    internal static MelonPreferences_Entry<bool> HighEndEnabledEntry;
    internal static MelonPreferences_Entry<float> HighEndStepEntry;

    private static float _scanTimer;
    private static bool _coreMissingLogged;
    private static bool _toastHooked;
    private static bool _peerHooked;
    private static bool _cronTypesRegistered;
    private static bool _cronBrokenLogged;
    private static GameObject _cronWorkerObj;
    private static GregDemandCronWorker _cronWorker;

    public static string LastScanSummary { get; private set; } = "Noch kein Scan.";
    public static int LastCustomerCount { get; private set; }
    public static int LastAppCount { get; private set; }
    public static int LastProductsAdded { get; private set; }

    public static void Initialize()
    {
        MelonPreferences_Category cat = MelonPreferences.CreateCategory("EconomyEngine_Demand");
        AutoRouteEntry = cat.CreateEntry("AutoRouteSubnets", false, "AutoRouteSubnets",
            "Registriert automatisch alle App-Subnetze per TryRegisterRoutedSubnet (gregCore).");
        AutoFeedEntry = cat.CreateEntry("AutoFeedPerformance", false, "AutoFeedPerformance",
            "Fuettert automatisch Speed (IOPS x Nachfrage-Multiplier) per AddAppPerformance (gregCore).");
        AutoProductsEntry = cat.CreateEntry("AutoAddProducts", false, "AutoAddProducts",
            "Fuegt Kunden automatisch neue Produkte (Apps) per SetUpApp hinzu (gregCore).");
        ProductAppIdsEntry = cat.CreateEntry("ProductAppIds", "", "ProductAppIds",
            "Komma-getrennte App-IDs, die als neue Produkte hinzugefuegt werden duerfen, z.B. \"2,3\". Leer = keine.");
        ProductDifficultyEntry = cat.CreateEntry("ProductDifficulty", 1, "ProductDifficulty",
            "Schwierigkeit neu hinzugefuegter Produkte.");
        DryRunEntry = cat.CreateEntry("DryRun", true, "DryRun",
            "Wenn aktiv, wird nur geloggt + getoastet was passieren WUERDE. Schreibt nichts ins Spiel.");
        ToastsEnabledEntry = cat.CreateEntry("ToastsEnabled", true, "ToastsEnabled",
            "IMGUI-Toast-Benachrichtigungen im Spiel anzeigen.");
        ScanIntervalEntry = cat.CreateEntry("ScanIntervalSeconds", 5f, "ScanIntervalSeconds",
            "Abstand zwischen Demand-Scans in Sekunden. Nicht unter 2 setzen.");
        ToastDurationEntry = cat.CreateEntry("ToastDurationSeconds", 6f, "ToastDurationSeconds",
            "Anzeigedauer eines Toasts in Sekunden.");

        // CronWorker settings
        CronWorkerEnabledEntry = cat.CreateEntry("CronWorkerEnabled", false, "CronWorkerEnabled",
            "Aktiviert den Demand-CronWorker: erzeugt automatisch Nachfrage-Events in festen Intervallen.");
        CronIntervalEntry = cat.CreateEntry("CronIntervalSeconds", 180f, "CronIntervalSeconds",
            "Intervall zwischen Demand-Events in Sekunden (Poisson-verteilt). Minimum 30.");
        CronSpeedScaleEntry = cat.CreateEntry("CronSpeedScaleFactor", 1.3f, "CronSpeedScaleFactor",
            "Skalierungsfaktor fuer Speed-Anforderungen bei Demand-Events (>1.0 = Nachfrage-Erhoehung).");
        CronMaxAppsEntry = cat.CreateEntry("CronMaxAppsPerEvent", 3, "CronMaxAppsPerEvent",
            "Maximale Anzahl von Apps pro Demand-Event (0 = alle).");
        ShiftDipEnabledEntry = cat.CreateEntry("ShiftDipEnabled", true, "ShiftDipEnabled",
            "Kunden senken manchmal ihre Anforderungen (Dip).");
        ShiftNewServiceEnabledEntry = cat.CreateEntry("ShiftNewServiceEnabled", true, "ShiftNewServiceEnabled",
            "Kunden bestellen manchmal neue Dienste (Produkte aus ProductAppIds).");
        ShiftDipAmountEntry = cat.CreateEntry("ShiftDipAmount", 0.3f, "ShiftDipAmount",
            "Staerke der Bedarfs-Senkung (Anteil der Anforderung als Bonus).");
        HighEndEnabledEntry = cat.CreateEntry("HighEndEnabled", false, "HighEndEnabled",
            "HighEnd-Kunden: Anforderungen skalieren mit der Kunden-ID (1 + ID * Schritt). Aus = Vanilla.");
        HighEndStepEntry = cat.CreateEntry("HighEndStep", 0.1f, "HighEndStep",
            "Schritt pro Kunden-ID: ID 1 = x1.1, ID 33 = x4.3.");
    }

    public static void Update(float dt)
    {
        // Standalone (without gregCore): market only, no demand automation.
        if (!GregHost.HasCore) return;

        // Regular demand scan
        float interval = 5f;
        try { if (ScanIntervalEntry != null) interval = ScanIntervalEntry.Value; } catch { }
        if (interval < 2f) interval = 2f;
        _scanTimer += dt;
        if (_scanTimer >= interval)
        {
            _scanTimer = 0f;
            Scan(false, true);
        }

        // CronWorker Management
        UpdateCronWorker(dt);
    }

    /// <summary>
    /// Registers the gregCore CronWorker type in IL2CPP before it is added
    /// as a component. Without registration, AddComponent fails and the
    /// CronWorker stays permanently broken.
    /// </summary>
    private static void EnsureCronTypesRegistered()
    {
        if (_cronTypesRegistered) return;
        _cronTypesRegistered = true;
        try
        {
            Il2CppInterop.Runtime.Injection.ClassInjector.RegisterTypeInIl2Cpp<GregDemandCronWorker>();
        }
        catch (Exception ex)
        {
            if (!_cronBrokenLogged)
            {
                _cronBrokenLogged = true;
                MelonLogger.Error("[Economy][Demand] CronWorker-Typregistrierung fehlgeschlagen: " +
                    ex.GetBaseException().Message);
            }
            throw;
        }
    }

    private static void UpdateCronWorker(float dt)
    {
        bool enabled = false;
        try { if (CronWorkerEnabledEntry != null) enabled = CronWorkerEnabledEntry.Value; } catch { }

        if (!enabled)
        {
            if (_cronWorker != null && _cronWorker.IsActive)
            {
                _cronWorker.Deactivate();
                MelonLogger.Msg("[Economy][Demand] CronWorker deaktiviert.");
            }
            return;
        }

        // Create CronWorker GameObject if needed
        if (_cronWorkerObj == null)
        {
            try
            {
                EnsureCronTypesRegistered();
                _cronWorkerObj = new GameObject("GregDemandCronWorker");
                UnityEngine.Object.DontDestroyOnLoad(_cronWorkerObj);
                _cronWorker = _cronWorkerObj.AddComponent<GregDemandCronWorker>();
                _cronWorker.OnDemandEvent += OnCronDemandEvent;
                MelonLogger.Msg("[Economy][Demand] CronWorker GameObject erstellt.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[Economy][Demand] CronWorker erstellen fehlgeschlagen: " + ex.GetBaseException().Message);
                return;
            }
        }

        if (_cronWorker == null) return;

        // Apply configuration
        try
        {
            float interval = 180f;
            try { if (CronIntervalEntry != null) interval = CronIntervalEntry.Value; } catch { }
            _cronWorker.IntervalSeconds = interval;

            float speedScale = 1.3f;
            try { if (CronSpeedScaleEntry != null) speedScale = CronSpeedScaleEntry.Value; } catch { }
            _cronWorker.SpeedScaleFactor = speedScale;

            int maxApps = 3;
            try { if (CronMaxAppsEntry != null) maxApps = CronMaxAppsEntry.Value; } catch { }
            _cronWorker.MaxAppsPerEvent = maxApps;

            bool allowDip = true;
            try { if (ShiftDipEnabledEntry != null) allowDip = ShiftDipEnabledEntry.Value; } catch { }
            _cronWorker.AllowDip = allowDip;

            bool allowService = true;
            try { if (ShiftNewServiceEnabledEntry != null) allowService = ShiftNewServiceEnabledEntry.Value; } catch { }
            _cronWorker.AllowNewService = allowService;

            float dipAmount = 0.3f;
            try { if (ShiftDipAmountEntry != null) dipAmount = ShiftDipAmountEntry.Value; } catch { }
            _cronWorker.DipAmount = dipAmount;

            try { if (ProductAppIdsEntry != null) _cronWorker.ProductAppIds = DemandPlanner.ParseAppIds(ProductAppIdsEntry.Value ?? ""); } catch { }
            try { if (ProductDifficultyEntry != null) _cronWorker.ProductDifficulty = ProductDifficultyEntry.Value; } catch { }
        }
        catch { }

        if (!_cronWorker.IsActive)
        {
            _cronWorker.Activate();
        }
    }

    private static void OnCronDemandEvent(DemandEvent evt)
    {
        // Toast depending on shift kind
        string msg = evt.Kind switch
        {
            DemandEventKind.Dip =>
                $"[gregCore][Demand] Dip: Kunde {evt.CustomerId} senkt Bedarf " +
                $"({evt.AffectedAppIds.Count} Apps, {-evt.SpeedIncrease:F1}).",
            DemandEventKind.NewService =>
                $"[gregCore][Demand] Neuer Dienst: Kunde {evt.CustomerId} " +
                $"bestellt App {evt.NewAppId}.",
            _ =>
                $"[gregCore][Demand] Surge: Kunde {evt.CustomerId} braucht mehr " +
                $"({evt.AffectedAppIds.Count} Apps, +{evt.SpeedIncrease:F1}).",
        };
        PushToast(msg);

        // Trigger a scan to capture the new state (routes e.g.
        // new services when AutoRoute/AutoFeed are active)
        Scan(false);
    }

    public static void DiagnoseNow()
    {
        if (!GregHost.HasCore)
        {
            MelonLogger.Msg("[Economy][Demand] Diagnose braucht gregCore (Standalone: nur Markt).");
            return;
        }
        Scan(true);
    }

    private static void Scan(bool forcedDryRun, bool isTimer = false)
    {
        // Idle skip: timer scans without active automation are pure
        // main-thread load (customer scan + log spam). Only scan when needed.
        // HighEnd upkeep counts as automation too.
        if (isTimer && !forcedDryRun
            && !ReadFlag(AutoRouteEntry, false)
            && !ReadFlag(AutoFeedEntry, false)
            && !ReadFlag(AutoProductsEntry, false)
            && !ReadFlag(HighEndEnabledEntry, false))
        {
            LastScanSummary = "Idle (Automatisierung aus).";
            return;
        }        GregDemandModule demand;
        try
        {
            if (!global::gregCore.PublicApi.greg.IsInitialized) { NoteCoreMissing(); return; }
            demand = global::gregCore.PublicApi.greg.Demand;
            if (demand == null) { NoteCoreMissing(); return; }
        }
        catch (Exception ex)
        {
            NoteCoreMissing(ex);
            return;
        }

        if (!_toastHooked)
        {
            try { demand.OnToast += OnCoreToast; _toastHooked = true; } catch { }
        }

        if (!_peerHooked)
        {
            try
            {
                gregCore.Infrastructure.Networking.GregNetSession.Refresh();
                gregCore.Infrastructure.Networking.GregNetSession.PeerJoined += OnPeerJoined;
                _peerHooked = true;
            }
            catch { }
        }
        try { gregCore.Infrastructure.Networking.GregNetSession.Refresh(); } catch { }

        bool dryRun = forcedDryRun || ReadFlag(DryRunEntry, true);
        bool canWrite = true;
        try { canWrite = gregCore.Infrastructure.Networking.GregNetSession.CanMutateWorld; } catch { }
        bool highEnd = !forcedDryRun && ReadFlag(HighEndEnabledEntry, false);
        float highEndStep = 0.1f;
        try { if (HighEndStepEntry != null) highEndStep = HighEndStepEntry.Value; } catch { }
        string productIds = "";
        try { if (ProductAppIdsEntry != null) productIds = ProductAppIdsEntry.Value ?? ""; } catch { }
        int difficulty = 1;
        try { if (ProductDifficultyEntry != null) difficulty = ProductDifficultyEntry.Value; } catch { }
        var options = new DemandOptions
        {
            AutoRouteSubnets = !forcedDryRun && ReadFlag(AutoRouteEntry, false),
            AutoFeedPerformance = !forcedDryRun && ReadFlag(AutoFeedEntry, false),
            AutoAddProducts = !forcedDryRun && ReadFlag(AutoProductsEntry, false),
            DryRun = dryRun,
            Quiet = isTimer && !forcedDryRun,
            CanWriteWorld = canWrite,
            DemandMultiplier = MarketplaceManager.GlobalDemandMultiplier,
            ProductAppIds = DemandPlanner.ParseAppIds(productIds),
            ProductDifficulty = difficulty,
            HighEndEnabled = highEnd,
            HighEndStep = highEndStep,
        };

        DemandScanResult result;
        try
        {
            result = forcedDryRun ? demand.Diagnose() : demand.Scan(options);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[Economy][Demand] gregCore-Scan fehlgeschlagen: " + ex.GetBaseException().Message);
            return;
        }

        LastScanSummary = result.Summary;
        LastCustomerCount = result.Customers;
        LastAppCount = result.Apps;
        LastProductsAdded = result.ProductsAdded;

        if (!dryRun && (result.Routed > 0 || result.FedTotal > 0f || result.ProductsAdded > 0) && result.NewlySatisfied.Count == 0)
            PushToast("Demand-Scan: " + result.Summary);
    }

    private static void OnCoreToast(string message)
    {
        PushToast(message);
    }

    // A peer has joined: discard demand memory and re-evaluate immediately
    // so the new state applies to everyone (join convergence).
    private static void OnPeerJoined()
    {
        try
        {
            if (global::gregCore.PublicApi.greg.IsInitialized)
                global::gregCore.PublicApi.greg.Demand.ResetMemory();
        }
        catch { }
        _scanTimer = 1e9f;
        try
        {
            greg.Mods.EconomyEngine.UI.MarketplaceUI.PushToast("Neuer Mitspieler: Demand wird neu abgeglichen.");
        }
        catch { }
    }

    private static void PushToast(string message)
    {
        try
        {
            if (ReadFlag(ToastsEnabledEntry, true))
                greg.Mods.EconomyEngine.UI.MarketplaceUI.PushToast(message);
        }
        catch { /* UI must never break the integration */ }
    }

    private static bool ReadFlag(MelonPreferences_Entry<bool> entry, bool fallback)
    {
        try { return entry != null ? entry.Value : fallback; }
        catch { return fallback; }
    }

    private static void NoteCoreMissing(Exception ex = null)
    {
        if (_coreMissingLogged) return;
        _coreMissingLogged = true;
        string detail = ex != null ? ": " + ex.GetBaseException().Message : "";
        MelonLogger.Warning("[Economy][Demand] gregCore nicht bereit - Demand-Automatisierung pausiert. " +
            "gregCore.dll muss im Mods-Ordner liegen" + detail);
        try
        {
            greg.Mods.EconomyEngine.UI.MarketplaceUI.PushToast(
                "Demand-Automatisierung pausiert: gregCore fehlt.");
        }
        catch { }
    }
}
