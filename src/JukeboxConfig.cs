﻿using Menu;
using Menu.Remix.MixedUI;
using System.IO;
using System.Linq;
using UnityEngine;

namespace JukeboxAnywhere
{
    public class JukeboxConfig : OptionInterface
    {
        public static JukeboxConfig Instance { get; } = new();
        public static Configurable<bool> RequireExpeditionUnlocks;
        public static Configurable<bool> AlphabeticalOrder;
        public static Configurable<bool> MiscSongs;
        public static Configurable<bool> ModdedSongs;
        public static Configurable<bool> CleanSongNames;
        public static Configurable<bool> JukeboxInSleepScreen;
        public static Configurable<bool> JukeboxInMainMenu;
        public static Configurable<bool> ThreatThemesButton;

        public JukeboxConfig()
        {
            RequireExpeditionUnlocks = config.Bind("requireExpeditionUnlocks", true, new ConfigurableInfo("Only access songs that are unlocked from Expedition goals.", tags:
            [
                "Require Expedition Unlocks for Songs"
            ]));
            AlphabeticalOrder = config.Bind("alphabeticalOrder", false, new ConfigurableInfo("Sort the Jukebox's tracks by alphabetical order.", tags:
            [
                "Sort Tracks Alphabetically"
            ]));
            MiscSongs = config.Bind("miscSongs", true, new ConfigurableInfo("Enable Watcher songs and misc. vanilla songs that aren't in the vanilla Jukebox (such as ambient pieces and region intros) to be playable from the Jukebox. Requries the Watcher DLC.", tags:
            [
                "Enable Watcher and Misc. Songs"
            ]));
            ModdedSongs = config.Bind("moddedSongs", true, new ConfigurableInfo("Enable modded songs to be playable from the Jukebox.", tags:
            [
                "Enable Modded Songs"
            ]));
            CleanSongNames = config.Bind("cleanSongNames", true, new ConfigurableInfo("Attempt to fix capitalization and remove underscores from song names on the Jukebox", tags:
            [
                "Clean Song Names"
            ]));
            JukeboxInSleepScreen = config.Bind("jukeboxInSleepScreen", true, new ConfigurableInfo("Enable Jukebox in Sleep and Death menus", tags:
            [
                "Jukebox in Sleep/Death Menus"
            ]));
            JukeboxInMainMenu = config.Bind("jukeboxInMainMenu", true, new ConfigurableInfo("Enable Jukebox in main menu", tags:
            [
                "Jukebox in Main Menu"
            ]));
            ThreatThemesButton = config.Bind("threatThemesButton", true, new ConfigurableInfo("Enable Threat Themes Button that opens Rotwall's webapp in your browser", tags:
            [
                "Enable Threat Themes Webapp Button"
            ]));
        }

        public static void RegisterOI()
        {
            if (MachineConnector.GetRegisteredOI(Plugin.MOD_ID) != Instance)
                MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Instance);
        }

        // Called when the config menu is opened by the player.
        public override void Initialize()
        {
            base.Initialize();
            Tabs =
            [
                new OpTab(this, "Options"),
            ];

            // Options tab
            AddDivider(593f);
            AddTitle(0);
            AddDivider(557f);
            AddCheckbox(RequireExpeditionUnlocks, 520f);
            AddCheckbox(AlphabeticalOrder, 480f);
            AddCheckbox(MiscSongs, 440f);
            AddCheckbox(ModdedSongs, 400f);
            AddCheckbox(CleanSongNames, 360f);
            AddCheckbox(JukeboxInSleepScreen, 320f);
            AddCheckbox(JukeboxInMainMenu, 280f);
            AddCheckbox(ThreatThemesButton, 240f);

            if (!ModManager.Watcher)
            {
                MiscSongs.BoundUIconfig.Hide();
            }
        }

        // Combines two flipped 'LinearGradient200's together to make a fancy looking divider.
        private void AddDivider(float y, int tab = 0)
        {
            OpImage dividerLeft = new(new Vector2(300f, y), "LinearGradient200");
            dividerLeft.sprite.SetAnchor(0.5f, 0f);
            dividerLeft.sprite.rotation = 270f;

            OpImage dividerRight = new(new Vector2(300f, y), "LinearGradient200");
            dividerRight.sprite.SetAnchor(0.5f, 0f);
            dividerRight.sprite.rotation = 90f;

            Tabs[tab].AddItems(
            [
                dividerLeft,
                dividerRight
            ]);
        }

        // Adds the mod name to the interface.
        private void AddTitle(int tab, string text = "Jukebox Anywhere", float yPos = 560f)
        {
            OpLabel title = new(new Vector2(150f, yPos), new Vector2(300f, 30f), text, bigText: true);

            Tabs[tab].AddItems(
            [
                title
            ]);
        }

        // Adds a checkbox tied to the config setting passed through `optionText`, as well as a label next to it with a description.
        private void AddCheckbox(Configurable<bool> optionText, float y)
        {
            OpCheckBox checkbox = new(optionText, new Vector2(150f, y))
            {
                description = optionText.info.description
            };

            OpLabel checkboxLabel = new(150f + 40f, y + 2f, optionText.info.Tags[0] as string)
            {
                description = optionText.info.description
            };

            Tabs[0].AddItems(
            [
                checkbox,
                checkboxLabel
            ]);
        }
    }
}