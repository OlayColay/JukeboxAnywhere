using System;
using System.Collections.Generic;
using System.Globalization;
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
    [BepInPlugin(MOD_ID, "Jukebox Anywhere", "1.1.1")]
    class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "olaycolay.jukeboxanywhere";
        public static ManualLogSource JLogger;

        public static string[] modSongNames;
        public static HashSet<string> regionAcronyms;

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

            On.Menu.MusicTrackContainer.ctor += MusicTrackContainer_ctor;

            On.Music.MultiplayerDJ.PlayNext += MultiplayerDJ_PlayNext;

            On.Expedition.ExpeditionProgression.GetUnlockedSongs += ExpeditionProgression_GetUnlockedSongs;
            On.Expedition.ExpeditionProgression.TrackName += ExpeditionProgression_TrackName;

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

        private string ExpeditionProgression_TrackName(On.Expedition.ExpeditionProgression.orig_TrackName orig, string filename)
        {
            string text = orig(filename);

            if (JukeboxConfig.CleanSongNames.Value)
            {
                return ConvertToTitleCase(text);
            }
            return text;
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
                self.manager.sideProcesses.Add(new JukeboxAnywhere(self.manager));
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
                self.manager.sideProcesses.OfType<JukeboxAnywhere>().FirstOrDefault()?.Singal(self.pages[0], "BACK MUTED");
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
            else if (self.menu is not JukeboxAnywhere || JukeboxConfig.RequireExpeditionUnlocks.Value)
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
            if (self.menu is JukeboxAnywhere && !JukeboxConfig.RequireExpeditionUnlocks.Value)
            {
                self.unlocked = true;
            }
            orig(self, timeStacker);

            if (self.menu is not JukeboxAnywhere)
            {
                return;
            }
            JukeboxAnywhere j = self.menu as JukeboxAnywhere;

            if (j.opening || j.closing)
            {
                self.sprite.y = (self.owner as MusicTrackContainer).pos.y + self.pos.y + self.menu.pages[0].pos.y + 25f;
            }
        }

        private void MusicTrackContainer_ctor(On.Menu.MusicTrackContainer.orig_ctor orig, MusicTrackContainer self, Menu.Menu menu, MenuObject owner, Vector2 pos, List<string> trackFilenames)
        {
            orig(self, menu, owner, pos, trackFilenames);

            if (self.trackList.Length > 0 && self.trackList.Length % 10 == 0)
            {
                // For some reason, having a multiple of 10 songs in the trackList creates an extra empty page. This prevents that
                self.maxPages--;
            }
        }

        private Dictionary<string, string> ExpeditionProgression_GetUnlockedSongs(On.Expedition.ExpeditionProgression.orig_GetUnlockedSongs orig)
        {
            Dictionary<string, string> songs = orig();

            if (JukeboxConfig.ModdedSongs.Value)
            {
                int initialCount = songs.Count + 1;
                for (int i = 0; i < modSongNames.Count(); i++) 
                {
                    if (!songs.ContainsValue(modSongNames[i]))
                    { 
                        songs["mus-" + Menu.Remix.ValueConverter.ConvertToString(i + initialCount)] = modSongNames[i];
                    }
                }
            }

            return songs;
        }

        // Un-randomize Arena song when playing from Jukebox
        private void MultiplayerDJ_PlayNext(On.Music.MultiplayerDJ.orig_PlayNext orig, Music.MultiplayerDJ self, float fadeInTime)
        {
            if (!self.musicPlayer.manager.sideProcesses.OfType<JukeboxAnywhere>().Any())
            {
                orig(self, fadeInTime);
            }
        }

        // Block controls on the pause menu when the Jukebox is showing
        private bool Page_get_Selected(Func<Page, bool> orig, Page self)
        {
            return orig(self) && (self.menu is not PauseMenu || !self.menu.manager.sideProcesses.OfType<JukeboxAnywhere>().Any());
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
                c.Emit(OpCodes.Isinst, typeof(JukeboxAnywhere)); // Check if 'this' is a JukeboxAnywhere.Jukebox
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
                c.Emit(OpCodes.Isinst, typeof(JukeboxAnywhere)); // Check if 'this' is a JukeboxAnywhere.Jukebox
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
            modSongNames = AssetManager.ListDirectory("music" + Path.DirectorySeparatorChar.ToString() + "songs", false, false, true)
                .Where(file => file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileNameWithoutExtension).ToArray();

            // Get region acronyms
            string text = AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar.ToString() + "regions.txt");
            if (File.Exists(text))
            {
                regionAcronyms = File.ReadAllLines(text).ToHashSet<string>();
            }
            else
            {
                regionAcronyms = ["No Regions"];
            }
        }

        // Define a list of words that should not be capitalized in a title
        readonly static HashSet<string> lowercaseWords = ["a", "and", "as", "at", "but", "by", "for", "from", "if", "in", "into", "nor", "of", "off",
            "on", "once", "onto", "or", "over", "so", "than", "that", "to", "upon", "when", "with", "yet"];
        public static string ConvertToTitleCase(string text)
        {
            if (text.IsNullOrWhiteSpace())
            {
                return text;
            }

            // Replace underscores with spaces and split the string into words
            string[] words = text.Replace('_', ' ').Split(' ');

            // Capitalize the first letter of each word, except the ones in the lowercaseWords list and region acronyms
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].IsNullOrWhiteSpace())
                {
                    continue;
                }
                
                if (regionAcronyms.Contains(words[i].ToUpper()))
                {
                    words[i] = words[i].ToUpper();
                }
                if (i == 0 || !lowercaseWords.Contains(words[i].ToLower()))
                {
                    char[] characters = words[i].ToCharArray();
                    // Check if the first character is a letter
                    if (char.IsLetter(characters[0]))
                    {
                        // Capitalize the first letter
                        characters[0] = char.ToUpper(characters[0]);
                    }
                    words[i] = new string(characters);
                }
            }

            // Reconstruct the string
            return string.Join(" ", words);
        }
    }
}
