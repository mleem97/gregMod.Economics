using UnityEngine;
using greg.Mods.EconomyEngine.Core;

namespace greg.Mods.EconomyEngine.UI;

public sealed class MarketplaceUI : MonoBehaviour
{
    private static MarketplaceUI _instance;
    private bool _visible;

    public static void Toggle()
    {
        if (_instance == null)
        {
            var go = new GameObject("EconomyEngineMarketplaceUI");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MarketplaceUI>();
        }
        _instance._visible = !_instance._visible;
    }

    private void OnGUI()
    {
        if (_visible)
        {
            var area = new Rect(Screen.width - 380, 80, 350, 150);
            GUI.Box(area, "Global Market");
            GUILayout.BeginArea(new Rect(area.x + 12, area.y + 28, area.width - 24, area.height - 36));
            DrawWindow();
            GUILayout.EndArea();
        }
    }

    private void DrawWindow()
    {
        GUILayout.Label($"Price: ${MarketplaceManager.MarketPricePerIops:F4} / IOPS");
        GUILayout.Label($"Demand: {MarketplaceManager.GlobalDemandMultiplier:P0}");
        GUILayout.Label("Automatic overcapacity sales run every 30 seconds.");
        if (GUILayout.Button("Close")) _visible = false;
    }
}
