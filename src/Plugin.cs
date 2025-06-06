﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Logging;
using Expedition;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Music;
using UnityEngine;

namespace JukeboxAnywhere
{
    [BepInPlugin(MOD_ID, "Jukebox Anywhere", "1.8.0")]
    class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "olaycolay.jukeboxanywhere";
        public static ManualLogSource JLogger;

        public static string[] miscSongNames;
        public static string[] modSongNames;
        public static HashSet<string> regionAcronyms;

        public static string mainMenuSong = "";

        public void OnEnable()
        {
            JLogger = Logger;

            On.RainWorld.OnModsInit += Extras.WrapInit(LoadResources);

            // Put your custom hooks here!
            new Hook(typeof(Menu.Menu).GetProperty(nameof(Menu.Menu.FreezeMenuFunctions), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetGetMethod(true), Menu_get_FreezeMenuFunctions);

            On.Expedition.ExpeditionCoreFile.ToString += ExpeditionCoreFile_ToString;
            On.Expedition.ExpeditionCoreFile.FromString += ExpeditionCoreFile_FromString;

            On.Menu.PauseMenu.SpawnExitContinueButtons += PauseMenu_SpawnExitContinueButtons;
            On.Menu.PauseMenu.Singal += PauseMenu_Singal;
            On.Menu.PauseMenu.Update += PauseMenu_Update;

            new Hook(typeof(SleepAndDeathScreen).GetProperty(nameof(SleepAndDeathScreen.RevealMap), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetGetMethod(), SleepAndDeathScreen_get_RevealMap);
            On.Menu.SleepAndDeathScreen.AddSubObjects += SleepAndDeathScreen_AddSubObjects;
            On.Menu.SleepAndDeathScreen.Singal += SleepAndDeathScreen_Singal;
            On.Menu.SleepAndDeathScreen.Update += SleepAndDeathScreen_Update;

            On.Menu.MainMenu.ctor += MainMenu_ctor;
            On.Menu.MainMenu.Singal += MainMenu_Singal;

            On.Menu.MusicTrackButton.ctor += MusicTrackButton_ctor;
            On.Menu.MusicTrackButton.GrafUpdate += MusicTrackButton_GrafUpdate;

            On.Menu.MusicTrackContainer.ctor += MusicTrackContainer_ctor;
            On.Menu.MusicTrackContainer.SwitchPage += MusicTrackContainer_SwitchPage;

            On.Music.IntroRollMusic.ctor += IntroRollMusic_ctor;
            On.Music.IntroRollMusic.StartPlaying += IntroRollMusic_StartPlaying;
            On.Music.IntroRollMusic.StartMusic += IntroRollMusic_StartMusic;
            On.Music.IntroRollMusic.Update += IntroRollMusic_Update;

            On.Music.MultiplayerDJ.PlayNext += MultiplayerDJ_PlayNext;

            On.Expedition.ExpeditionProgression.GetUnlockedSongs += ExpeditionProgression_GetUnlockedSongs;
            On.Expedition.ExpeditionProgression.TrackName += ExpeditionProgression_TrackName;

            new Hook(typeof(Page).GetProperty(nameof(Page.Selected), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetGetMethod(), Page_get_Selected);

            try
            {
                IL.Menu.ExpeditionJukebox.ctor += ExpeditionJukebox_ctor;
                IL.Menu.MainMenu.AddMainMenuButton += MainMenu_AddMainMenuButton;
            }
            catch (Exception ex)
            {
                Debug.LogError("JukeboxAnywhere: Could not apply IL Hooks!\n" + ex.Message);
            }
        }

        private delegate bool orig_Menu_FreezeMenuFunctions(Menu.Menu self);
        private bool Menu_get_FreezeMenuFunctions(orig_Menu_FreezeMenuFunctions orig, Menu.Menu self)
        {
            return orig(self) || ((self is PauseMenu or SleepAndDeathScreen or MainMenu) && self.manager.sideProcesses.OfType<JukeboxAnywhere>().Any());
        }

        private void ExpeditionCoreFile_FromString(On.Expedition.ExpeditionCoreFile.orig_FromString orig, ExpeditionCoreFile self, string saveString)
        {
            orig(self, saveString);

            string[] array = Regex.Split(saveString, "<expC>");
            foreach (string s in array)
            {
                if (s.StartsWith("MAINMENUSONG:"))
                {
                    mainMenuSong = Regex.Split(s, ":")[1];
                }
            }
        }

        private string ExpeditionCoreFile_ToString(On.Expedition.ExpeditionCoreFile.orig_ToString orig, ExpeditionCoreFile self)
        {
            return orig(self) + (mainMenuSong.IsNullOrWhiteSpace() ? "" : ("<expC>MAINMENUSONG:" + mainMenuSong));
        }

        private void PauseMenu_SpawnExitContinueButtons(On.Menu.PauseMenu.orig_SpawnExitContinueButtons orig, PauseMenu self)
        {
            orig(self);

            JLogger.LogInfo("JukeboxAnywhere: Spawning jukebox button");
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

        public delegate bool orig_SleepAndDeathScreen_RevealMap(SleepAndDeathScreen self);
        private static bool SleepAndDeathScreen_get_RevealMap(orig_SleepAndDeathScreen_RevealMap orig, SleepAndDeathScreen self)
        {
            return orig(self) && !self.manager.sideProcesses.OfType<JukeboxAnywhere>().Any();
        }

        private void SleepAndDeathScreen_AddSubObjects(On.Menu.SleepAndDeathScreen.orig_AddSubObjects orig, SleepAndDeathScreen self)
        {
            orig(self);

            if (!JukeboxConfig.JukeboxInSleepScreen.Value)
            {
                return;
            }

            JLogger.LogInfo("JukeboxAnywhere: Spawning jukebox button");
            JukeboxAnywhereButton jukeboxButton = new(self, self.pages[0], new Vector2(
                self.LeftHandButtonsPosXAdd + self.manager.rainWorld.options.SafeScreenOffset.x, 
                Mathf.Max(self.manager.rainWorld.options.SafeScreenOffset.y, 15f) + self.continueButton.size.y + 15f
            ));
            self.pages[0].subObjects.Add(jukeboxButton);
        }

        private void SleepAndDeathScreen_Singal(On.Menu.SleepAndDeathScreen.orig_Singal orig, SleepAndDeathScreen self, MenuObject sender, string message)
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

        private void SleepAndDeathScreen_Update(On.Menu.SleepAndDeathScreen.orig_Update orig, SleepAndDeathScreen self)
        {
            orig(self);

            if (self.JA().jukeboxButton is JukeboxAnywhereButton jukeboxButton)
            {
                jukeboxButton.buttonBehav.greyedOut = self.ButtonsGreyedOut;
            }
        }

        private void MainMenu_ctor(On.Menu.MainMenu.orig_ctor orig, MainMenu self, ProcessManager manager, bool showRegionSpecificBkg)
        {
            orig(self, manager, showRegionSpecificBkg);

            if (JukeboxConfig.JukeboxInMainMenu.Value)
            {
                JLogger.LogInfo("JukeboxAnywhere: Spawning MainMenu jukebox button");
                float buttonWidth = MainMenu.GetButtonWidth(self.CurrLang);
                Vector2 pos = new(683f - buttonWidth / 2f, 0f);
                Vector2 size = new(buttonWidth, 30f);
                self.AddMainMenuButton(new(self, self.pages[0], "JUKEBOX", "JUKEBOX", pos, size), () =>
                {
                    self.PlaySound(SoundID.MENU_Switch_Page_Out);
                    self.manager.sideProcesses.Add(new JukeboxAnywhere(self.manager));
                }, 2);
            }
        }

        private void MainMenu_Singal(On.Menu.MainMenu.orig_Singal orig, MainMenu self, MenuObject sender, string message)
        {
            if (message == "JUKEBOX")
            {
                self.PlaySound(SoundID.MENU_Switch_Page_In);
                self.manager.sideProcesses.Add(new JukeboxAnywhere(self.manager));
            }
            else
            {
                orig(self, sender, message);
            }
        }

        private void MainMenu_AddMainMenuButton(ILContext il)
        {
            ILCursor c = new(il);

            try
            {
                // Move cursor after int num = 8
                if (!c.TryGotoNext(MoveType.After, i => i.MatchLdcI4(8)))
                {
                    throw new Exception("Failed to match IL for MainMenu_ctor1!");
                }

                c.MoveAfterLabels();
                c.EmitDelegate((int _) => 12);
            }
            catch (Exception ex)
            {
                Debug.LogError("JukeboxAnywhere: Could not emit MainMenu_AddMainMenuButton ILs!\n" + ex.Message);
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

            // Vanilla fix: If the game is 16:10 aspect ratio, adjust the record icon's position
            if (menu.manager.rainWorld.options.resolution == 4)
            {
                self.sprite.x = (owner as MusicTrackContainer).pos.x + self.pos.x - 43f;
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

            // Vanilla fix: If the game is 16:10 aspect ratio, adjust the record icon's position
            if (self.menu.manager.rainWorld.options.resolution == 4)
            {
                self.sprite.x = (self.owner as MusicTrackContainer).pos.x + self.pos.x - 43f;
            }

            // Don't show spinning record if the playing song isn't in the Jukebox's list
            if (self.sprite.element.name == "mediadisc" && self.menu.manager.musicPlayer.song?.name is string name &&
                (self.owner as MusicTrackContainer).JA().unlockedSongs.FirstOrDefault(e => e.Value.ToLowerInvariant() == name.ToLowerInvariant()).Key.IsNullOrWhiteSpace())
            {
                self.sprite.rotation = 0f;
                self.sprite.SetElementByName("musicSymbol");

                (self.menu as ExpeditionJukebox).currentSong.label.text = name;
            }

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

            // For some reason, having a multiple of 10 songs in the trackList creates an extra empty page. This prevents that
            if (self.trackList.Length > 0 && self.trackList.Length % 10 == 0)
            {
                self.maxPages--;
            }

            // Vanilla fix: If the game is 16:10 aspect ratio, adjust the background's position
            if (self.menu.manager.rainWorld.options.resolution == 4)
            {
                FNode bgSprite = self.Container._childNodes.First(sprite => sprite.alpha == 0.55f);
                if (bgSprite != null)
                {
                    bgSprite.x = pos.x - 83f;
                }
            }

            // Option to sort tracks alphabetically
            if (JukeboxConfig.AlphabeticalOrder.Value)
            {
                self.trackList = [.. self.trackList.OrderByDescending(track => track.unlocked).ThenBy(track => track.trackName.myText)];
                self.SwitchPage();
            }
        }

        private void MusicTrackContainer_SwitchPage(On.Menu.MusicTrackContainer.orig_SwitchPage orig, MusicTrackContainer self)
        {
            orig(self);

            if (self.trackList == null)
            {
                return;
            }

            // Vanilla fix: Give the top and bottom trackButtons of each page nextSelectables that make sense
            int firstTrackOfPage = self.currentPage * 10;
            if (self.menu is ExpeditionJukebox jukebox && jukebox.backButton != null)
            {
                self.trackList[firstTrackOfPage].nextSelectable[1] = jukebox.backButton;
                jukebox.backButton.nextSelectable[3] = self.trackList[firstTrackOfPage];
                if (self.menu is JukeboxAnywhere ja && ja.threatButton != null)
                {
                    ja.threatButton.nextSelectable[3] = self.trackList[firstTrackOfPage];
                }
            }
            if (firstTrackOfPage + 10 >= self.trackList.Length)
            {
                self.trackList[self.trackList.Length - 1].nextSelectable[3] = self.backPage;
                self.backPage.nextSelectable[1] = self.forwardPage.nextSelectable[1] = self.trackList[self.trackList.Length - 1];
            }
            else
            {
                self.trackList[firstTrackOfPage + 9].nextSelectable[3] = self.backPage;
                self.backPage.nextSelectable[1] = self.forwardPage.nextSelectable[1] = self.trackList[firstTrackOfPage + 9];
            }

            // Move inactive tracks to further below menu so they don't show during opening/closing
            for (int i = 0; i < self.trackList.Length; i++)
            {
                if (Mathf.FloorToInt((float)(i / 10)) != self.currentPage)
                {
                    self.trackList[i].pos.y = -20000f;
                }
            }

            if (JukeboxConfig.RequireExpeditionUnlocks.Value)
            {
                return;
            }

            for (int i = 0; i < self.trackList.Length; i++)
            {
                if (Mathf.FloorToInt((float)(i / 10)) == self.currentPage)
                {
                    self.trackList[i].trackColor = new HSLColor(Mathf.InverseLerp(0f, (float)self.trackList.Length, (float)i), 1f, 0.7f).rgb;
                }
            }
        }

        private Dictionary<string, string> ExpeditionProgression_GetUnlockedSongs(On.Expedition.ExpeditionProgression.orig_GetUnlockedSongs orig)
        {
            Dictionary<string, string> songs = orig();
            var songNamesLower = songs.Values.Select(s => s.ToLowerInvariant());

            if (JukeboxConfig.MiscSongs.Value && ModManager.Watcher)
            {
                int initialCount = songs.Count + 1;
                int i = 0;
                foreach (string songName in miscSongNames)
                {
                    //JLogger.LogInfo(miscSongNames[i] + ": " + songs.ContainsValue(miscSongNames[i]));
                    if (!songNamesLower.Contains(songName.ToLowerInvariant()))
                    {
                        songs["mus-" + (i + initialCount)] = songName;
                        //JLogger.LogInfo(i + ": " + (i + initialCount) + ": " + songs["mus-" + (i + initialCount)]);
                        i++;
                    }
                }
            }

            if (JukeboxConfig.ModdedSongs.Value)
            {
                int initialCount = songs.Count + 1;
                int i = 0;
                foreach (string songName in modSongNames) 
                {
                    //JLogger.LogInfo(modSongNames[i] + ": " + songs.ContainsValue(modSongNames[i]));
                    if (!songNamesLower.Contains(songName.ToLowerInvariant()))
                    { 
                        songs["mus-" + (i + initialCount)] = songName;
                        //JLogger.LogInfo(i + ": " + (i + initialCount) + ": " + songs["mus-" + (i + initialCount)]);
                        i++;
                    }
                }
            }

            return songs;
        }

        private string ExpeditionProgression_TrackName(On.Expedition.ExpeditionProgression.orig_TrackName orig, string filename)
        {
            string text = orig(filename);

            if (JukeboxConfig.CleanSongNames.Value)
            {
                if (miscSongNames.Contains(text))
                {
                    return text.ToUpperInvariant();
                }
                return ConvertToTitleCase(text);
            }
            return text;
        }

        // Much of this crap is just for getting the currentSong text and visualizer working from Main Menu
        // Since they only look at the first subtrack, which is normally TITLEROLLRAIN. We switch that here
        private void IntroRollMusic_ctor(On.Music.IntroRollMusic.orig_ctor orig, IntroRollMusic self, MusicPlayer musicPlayer)
        {
            orig(self, musicPlayer);

            self.subTracks.Reverse();
            string mainMenuSongLower = mainMenuSong?.ToLowerInvariant();
            bool songExists = !mainMenuSong.IsNullOrWhiteSpace() && 
                ExpeditionProgression.GetUnlockedSongs().Any(pair => pair.Value.ToLowerInvariant().Contains(mainMenuSongLower));
            self.name = self.subTracks[0].trackName = songExists ? mainMenuSong : "RW_8 - Sundown";
        }

        public void IntroRollMusic_StartPlaying(On.Music.IntroRollMusic.orig_StartPlaying orig, IntroRollMusic self)
        {
            self.startedPlaying = true;
            self.subTracks[1].StartPlaying();
        }

        public void IntroRollMusic_StartMusic(On.Music.IntroRollMusic.orig_StartMusic orig, IntroRollMusic self)
        {
            if (self.musicTrackStarted)
            {
                return;
            }
            self.musicTrackStarted = true;
            self.subTracks[0].StartPlaying();
        }

        public void IntroRollMusic_Update(On.Music.IntroRollMusic.orig_Update orig, IntroRollMusic self)
        {
            orig(self);

            self.subTracks[0].volume = 1f;
            self.subTracks[1].volume = self.rainVol;
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

        private void ExpeditionJukebox_ctor(ILContext il)
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
            modSongNames = [.. AssetManager.ListDirectory("music" + Path.DirectorySeparatorChar.ToString() + "songs", false, false, true)
                .Where(file => file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileNameWithoutExtension).Distinct()];
            //JLogger.LogInfo("Mod song names: " + string.Join(", ", modSongNames));
            miscSongNames = [.. AssetManager.ListDirectory("music" + Path.DirectorySeparatorChar.ToString() + "songs")
                .Where(file => file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileNameWithoutExtension).Distinct().Where(name => !modSongNames.Contains(name))];

            // Get region acronyms
            string text = AssetManager.ResolveFilePath("World" + Path.DirectorySeparatorChar.ToString() + "regions.txt");
            if (File.Exists(text))
            {
                regionAcronyms = [.. File.ReadAllLines(text)];
            }
            else
            {
                regionAcronyms = ["No Regions"];
            }

            // Load atlas
            Futile.atlasManager.LoadAtlas("atlases/jukeboxanywhere");
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
