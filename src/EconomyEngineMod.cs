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
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Economy Engine v1.0.1 initialized.");
    }

    public override void OnUpdate()
    {
        MarketplaceManager.Update(Time.deltaTime);

        if (Keyboard.current != null && Keyboard.current.f11Key.wasPressedThisFrame)
        {
            MarketplaceUI.Toggle();
        }
    }
}
