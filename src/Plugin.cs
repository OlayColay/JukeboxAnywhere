using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;

namespace JukeboxAnywhere
{
    [BepInPlugin(MOD_ID, "Jukebox Anywhere", "0.3.0")]
    class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "olaycolay.jukeboxanywhere";
        public static ManualLogSource JLogger;

        public static string[] songNames;

        public void OnEnable()
        {
            JLogger = Logger;

            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // Put your custom hooks here!
            On.Menu.PauseMenu.SpawnExitContinueButtons += PauseMenu_SpawnExitContinueButtons;
            On.Menu.PauseMenu.Singal += PauseMenu_Singal;
            On.Menu.PauseMenu.Update += PauseMenu_Update;

            On.Menu.MusicTrackButton.ctor += MusicTrackButton_ctor;
            On.Menu.MusicTrackButton.GrafUpdate += MusicTrackButton_GrafUpdate;

            On.Music.MultiplayerDJ.PlayNext += MultiplayerDJ_PlayNext;

            On.Expedition.ExpeditionProgression.GetUnlockedSongs += ExpeditionProgression_GetUnlockedSongs;

            new Hook(typeof(Page).GetProperty(nameof(Page.Selected), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetGetMethod(), Page_get_Selected);

            try
            {
                IL.Menu.ExpeditionJukebox.ctor += ExpeditionJukebox_ctor;
            }
            catch (Exception ex)
            {
                Debug.LogError("JukeboxAnywhere: Could not apply ExpeditionJukebox_ctor IL Hook!\n" + ex.Message);
            }
        }

        private void PauseMenu_SpawnExitContinueButtons(On.Menu.PauseMenu.orig_SpawnExitContinueButtons orig, PauseMenu self)
        {
            orig(self);

            RWCustom.Custom.Log("JukeboxAnywhere: Spawning jukebox button");
            JukeboxAnywhereButton jukeBoxButton = new(self, self.pages[0], new Vector2(100f, self.continueButton.pos.y));
            self.pages[0].subObjects.Add(jukeBoxButton);
        }

        private void PauseMenu_Singal(On.Menu.PauseMenu.orig_Singal orig, PauseMenu self, MenuObject sender, string message)
        {
            if (message == "JUKEBOX")
            {
                self.PlaySound(SoundID.MENU_Switch_Page_Out);
                self.manager.sideProcesses.Add(new Jukebox(self.manager));
            }
            else
            {
                orig(self, sender, message);
            }
        }

        private void PauseMenu_Update(On.Menu.PauseMenu.orig_Update orig, PauseMenu self)
        {
            orig(self);
            if (self.wantToContinue)
            {
                self.manager.sideProcesses.OfType<Jukebox>().FirstOrDefault()?.Singal(self.pages[0], "BACK MUTED");
            }
        }

        private void MusicTrackButton_ctor(On.Menu.MusicTrackButton.orig_ctor orig, MusicTrackButton self, Menu.Menu menu, MenuObject owner, string displayText, string singalText, Vector2 pos, Vector2 size, SelectOneButton[] buttonArray, int index)
        {
            orig(self, menu, owner, displayText, singalText, pos, size, buttonArray, index);

            int vanillaSongCount = ModManager.MSC ? 124 : 81;
            // If this is a modded song it should be unlocked
            if (self.buttonArrayIndex >= vanillaSongCount)
            {
                self.unlocked = true;
            }
            else if (self.menu is not Jukebox || JukeboxConfig.RequireExpeditionUnlocks.Value)
            {
                return;
            }

            // Act like an unlocked song
            self.buttonBehav.greyedOut = false;
            self.trackName.text = Expedition.ExpeditionProgression.TrackName(displayText);
            self.trackName.label.color = new Color(0.8f, 0.8f, 0.8f);
            self.sprite.color = new Color(0.8f, 0.8f, 0.8f);
            for (int i = 9; i < self.roundedRect.sprites.Length; i++)
            {
                self.roundedRect.sprites[i].shader = menu.manager.rainWorld.Shaders["MenuTextCustom"];
            }
        }

        private void MusicTrackButton_GrafUpdate(On.Menu.MusicTrackButton.orig_GrafUpdate orig, MusicTrackButton self, float timeStacker)
        {
            // Reason we don't make unlocked true during the constructor is so that the menu's unlocked counter remains accurate
            if (self.menu is Jukebox && !JukeboxConfig.RequireExpeditionUnlocks.Value)
            {
                self.unlocked = true;
            }
            orig(self, timeStacker);

            if (self.menu is not Jukebox)
            {
                return;
            }
            Jukebox j = self.menu as Jukebox;

            if (j.opening || j.closing)
            {
                self.sprite.y = (self.owner as MusicTrackContainer).pos.y + self.pos.y + self.menu.pages[0].pos.y + 25f;
            }
        }

        private Dictionary<string, string> ExpeditionProgression_GetUnlockedSongs(On.Expedition.ExpeditionProgression.orig_GetUnlockedSongs orig)
        {
            Dictionary<string, string> songs = orig();

            if (JukeboxConfig.ModdedSongs.Value)
            {
                int initialCount = songs.Count + 1;
                for (int i = 0; i < songNames.Count(); i++) 
                {
                    if (!songs.ContainsValue(songNames[i]))
                    { 
                        songs["mus-" + Menu.Remix.ValueConverter.ConvertToString(i + initialCount)] = songNames[i];
                    }
                }
            }

            return songs;
        }

        // Un-randomize Arena song when playing from Jukebox
        private void MultiplayerDJ_PlayNext(On.Music.MultiplayerDJ.orig_PlayNext orig, Music.MultiplayerDJ self, float fadeInTime)
        {
            if (!self.musicPlayer.manager.sideProcesses.OfType<Jukebox>().Any())
            {
                orig(self, fadeInTime);
            }
        }

        // Block controls on the pause menu when the Jukebox is showing
        private bool Page_get_Selected(Func<Page, bool> orig, Page self)
        {
            return orig(self) && (self.menu is not PauseMenu || !self.menu.manager.sideProcesses.OfType<Jukebox>().Any());
        }

        private void ExpeditionJukebox_ctor(MonoMod.Cil.ILContext il)
        {
            ILCursor c = new(il);
            ILLabel brSLabel = null;

            try
            {
                // Move cursor before manager.musicPlayer.FadeOutAllSongs(1f)
                c.GotoNext(
                    x => x.MatchLdarg(1),
                    x => x.MatchLdfld<ProcessManager>(nameof(ProcessManager.musicPlayer)),
                    x => x.MatchLdcR4(1),
                    x => x.MatchCallvirt<Music.MusicPlayer>("FadeOutAllSongs")
                );
                new ILCursor(c).GotoNext(x => x.MatchBr(out brSLabel));
                c.Emit(OpCodes.Ldarg_0); // Load 'this'
                c.Emit(OpCodes.Isinst, typeof(Jukebox)); // Check if 'this' is a JukeboxAnywhere.Jukebox
                c.Emit(OpCodes.Brtrue_S, brSLabel); // If 'this' is a JukeboxAnywhere.Jukebox, skip the FadeOutAllSongs call

                // Move cursor before GetRandomJukeboxScene()
                c.GotoNext(
                    x => x.MatchLdarg(0),
                    x => x.MatchCall<ExpeditionJukebox>("GetRandomJukeboxScene")
                );
                ILCursor jumpCursor = new ILCursor(c).GotoNext(
                    x => x.MatchLdarg(0),
                    x => x.MatchLdstr("Futile_White")
                );
                ILLabel jumpLabel = jumpCursor.DefineLabel();
                jumpCursor.MarkLabel(jumpLabel);
                c.Emit(OpCodes.Ldarg_0); // Load 'this'
                c.Emit(OpCodes.Isinst, typeof(Jukebox)); // Check if 'this' is a JukeboxAnywhere.Jukebox
                c.Emit(OpCodes.Brtrue_S, jumpLabel); // If 'this' is a JukeboxAnywhere.Jukebox, skip the FadeOutAllSongs call
            }
            catch (Exception ex)
            {
                Debug.LogError("JukeboxAnywhere: Could not emit ExpeditionJukebox_ctor ILs!\n" + ex.Message);
            }
        }

        // Load any resources, such as sprites or sounds
        private void LoadResources(RainWorld rainWorld)
        {
            // Remix menu config
            JukeboxConfig.RegisterOI();

            // Load list of songs in music folder
            songNames = AssetManager.ListDirectory("music" + Path.DirectorySeparatorChar.ToString() + "songs", false, false, true).Select(Path.GetFileNameWithoutExtension).ToArray();
        }
    }
}
