using System;
using BepInEx;
using Menu;
using UnityEngine;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Jukebox Anywhere", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "olaycolay.jukeboxanywhere";

        // Add hooks
        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // Put your custom hooks here!
            On.Menu.PauseMenu.SpawnExitContinueButtons += PauseMenu_SpawnExitContinueButtons;
            On.Menu.PauseMenu.Singal += PauseMenu_Singal;
        }

        private void PauseMenu_SpawnExitContinueButtons(On.Menu.PauseMenu.orig_SpawnExitContinueButtons orig, Menu.PauseMenu self)
        {
            orig(self);

            RWCustom.Custom.Log("Spawning jukebox button");
            SymbolButton jukeBoxButton = new(self, self.pages[0], "musicSymbol", "JUKEBOX", new Vector2(100f, self.continueButton.pos.y));
            jukeBoxButton.roundedRect.size = new(50f, 50f);
            jukeBoxButton.size = jukeBoxButton.roundedRect.size;
            self.pages[0].subObjects.Add(jukeBoxButton);
        }

        private void PauseMenu_Singal(On.Menu.PauseMenu.orig_Singal orig, PauseMenu self, MenuObject sender, string message)
        {
            if (message == "JUKEBOX")
            {
                self.manager.RequestMainProcessSwitch(Expedition.ExpeditionEnums.ProcessID.ExpeditionJukebox);
                self.PlaySound(SoundID.MENU_Switch_Page_Out);
            }
            else
            {
                orig(self, sender, message);
            }
        }

        // Load any resources, such as sprites or sounds
        private void LoadResources(RainWorld rainWorld)
        {
        }
    }
}