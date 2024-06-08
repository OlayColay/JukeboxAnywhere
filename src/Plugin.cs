using System;
using System.Linq;
using BepInEx;
using Menu;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace SlugTemplate
{
    [BepInPlugin(MOD_ID, "Jukebox Anywhere", "0.1.0")]
    class Plugin : BaseUnityPlugin
    {
        private const string MOD_ID = "olaycolay.jukeboxanywhere";

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // Put your custom hooks here!
            On.Menu.PauseMenu.SpawnExitContinueButtons += PauseMenu_SpawnExitContinueButtons;
            On.Menu.PauseMenu.Singal += PauseMenu_Singal;

            new Hook(typeof(Page).GetProperty(nameof(Page.Selected), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetGetMethod(), Page_get_Selected);
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
                self.PlaySound(SoundID.MENU_Switch_Page_Out);
                self.pages[0].toggled = false;
                self.manager.sideProcesses.Add(new Jukebox(self.manager));
            }
            else
            {
                orig(self, sender, message);
            }
        }

        private bool Page_get_Selected(Func<Page, bool> orig, Page self)
        {
            return orig(self) && (self.menu is not PauseMenu || !self.menu.manager.sideProcesses.OfType<Jukebox>().Any());
        }

        // Load any resources, such as sprites or sounds
        private void LoadResources(RainWorld rainWorld)
        {
        }
    }
}