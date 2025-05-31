using BepInEx;
using Expedition;
using Menu;
using System;
using System.Linq;
using UnityEngine;

namespace JukeboxAnywhere;
public class JukeboxAnywhere : ExpeditionJukebox
{
    public bool opening;
    public bool closing;

    public float lastAlpha;
    public float currentAlpha;

    public float targetAlpha;
    public float uAlpha;

    public SimpleButton threatButton;
    public SymbolButton favouriteButtonMainMenu;

    protected FSprite darkSprite;

    public JukeboxAnywhere(ProcessManager manager) : base(manager)
    {
        // Create new JukeboxManager
        manager.sideProcesses.Add(new JukeboxManager(manager, this.songList));
        this.repeat = JukeboxManager.repeatAnywhere;
        this.shuffle = JukeboxManager.shuffleAnywhere;

        // Black background
        this.darkSprite = new FSprite("pixel", true)
        {
            color = new Color(0f, 0f, 0f),
            anchorX = 0f,
            anchorY = 0f,
            scaleX = manager.rainWorld.screenSize.x + 2f,
            scaleY = manager.rainWorld.screenSize.x + 2f,
            x = -1f,
            y = -1f,
            alpha = 0f
        };
        this.pages[0].Container.AddChildAtIndex(this.darkSprite, 0);

        opening = true;
        targetAlpha = 1f;

        // Re-select playing track if one is already playing
        if (manager.musicPlayer.song != null)
        {
            string currentSong = (manager.currentMainLoop.ID == ProcessManager.ProcessID.MainMenu && manager.musicPlayer.song.name == "TitleRollRain")
                ? (Plugin.mainMenuSong == "" ? "RW_8 - Sundown" : Plugin.mainMenuSong) : manager.musicPlayer.song.name;

            Plugin.JLogger.LogInfo("Currently playing song: " + currentSong);
            //Plugin.JLogger.LogInfo("Songs:" + string.Join(", ", ExpeditionProgression.GetUnlockedSongs().Select(kv => $"\n{kv.Key}: {kv.Value}")));
            string key = this.trackContainer.JA().unlockedSongs.FirstOrDefault(e => e.Value.ToLowerInvariant() == currentSong.ToLowerInvariant()).Key;
            if (key.IsNullOrWhiteSpace())
            {
                selectedTrack = 0;
            }
            else if (!int.TryParse(key.Substring(key.IndexOf('-') + 1), out selectedTrack))
            {
                Plugin.JLogger.LogWarning("JukeboxAnywhere: currently playing track has invalid code (ie. mus-xx)!");
            }
            else
            {
                selectedTrack--;
            }
            this.trackContainer.GoToPlayingTrackPage();
        }

        pages[0].pos.y = manager.rainWorld.options.ScreenSize.y + 100f;

        // Threat Themes webapp button
        if (JukeboxConfig.ThreatThemesButton.Value)
        {
            threatButton = new(this, pages[0], this.Translate("THREAT THEMES"), "THREAT", this.backButton.pos + new Vector2(45f, 0f), new Vector2(110f, 30f));
            threatButton.nextSelectable[0] = this.backButton;
            threatButton.nextSelectable[1] = threatButton.nextSelectable[2] = this.trackContainer.forwardPage;
            threatButton.nextSelectable[3] = this.trackContainer.trackList[this.trackContainer.currentPage * 10];
            pages[0].subObjects.Add(threatButton);

            this.backButton.pos.x -= 80f;
            this.backButton.SetSize(new Vector2(110f, 30f));
            this.backButton.nextSelectable[1] = this.trackContainer.backPage;
            this.backButton.nextSelectable[2] = threatButton;
        }

        // New favorite buttons
        this.favouriteButtonMainMenu = new(this, pages[0], "mediafavmainmenu", "FAVOURITEMAINMENU", this.favouriteButton.pos);
        this.favouriteButtonMainMenu.size = this.favouriteButtonMainMenu.roundedRect.size = this.favouriteButton.size;
        this.favouriteButtonMainMenu.nextSelectable[0] = this.favouriteButton;
        this.favouriteButtonMainMenu.nextSelectable[1] = this.playbackSlider;
        this.favouriteButtonMainMenu.nextSelectable[2] = this.volumeSlider;
        this.favouriteButtonMainMenu.nextSelectable[3] = this.backButton;
        pages[0].subObjects.Add(this.favouriteButtonMainMenu);
        this.repeatButton.pos.x -= 80f;
        this.shuffleButton.pos.x -= 80f;
        this.favouriteButton.pos.x -= 80f;
        this.favouriteButton.symbolSprite.element = Futile.atlasManager.GetElementWithName("mediafavexpedition");
        this.favouriteButton.nextSelectable[2] = this.favouriteButtonMainMenu;
        this.volumeSlider.nextSelectable[0] = this.favouriteButtonMainMenu;

        // Other button navigation fixes for the first loaded page
        this.backButton.nextSelectable[3] = this.trackContainer.trackList[this.trackContainer.currentPage * 10];
        this.trackContainer.forwardPage.nextSelectable[3] = threatButton ?? backButton;
        this.trackContainer.trackList[this.trackContainer.currentPage * 10].nextSelectable[1] = this.backButton;
    }

    public override void Update()
    {
        bool newSong = false;
        if (this.pendingSong == 0)
        {
            newSong = true;
        }

        base.Update();

        // Ensure song continues playing after death/cycle
        if (newSong && this.manager.musicPlayer.song != null)
        {
            this.manager.musicPlayer.song.context = Music.MusicPlayer.MusicContext.StoryMode;
        }

        // Update alpha and opening/closing status
        lastAlpha = currentAlpha;
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, 0.2f);
        if (opening && pages[0].pos.y <= 0.01f)
        {
            opening = false;
        }
        if (closing && Math.Abs(currentAlpha - targetAlpha) < 0.09f)
        {
            manager.StopSideProcess(this);
            Expedition.Expedition.coreFile.Save(false);
            closing = false;
        }

        if (this.threatButton != null && (this.threatButton.Selected || this.threatButton.MouseOver))
        {
            this.infoLabel.text = base.Translate("Opens Rotwall's Threatmixer in a web browser");
            this.infoLabelFade = 1f;
        }
        if (this.favouriteButton.Selected || this.favouriteButton.MouseOver)
        {
            this.infoLabel.text = base.Translate("Set a track to play as the Expedition menu theme");
            this.infoLabelFade = 1f;
        }
        if (this.favouriteButtonMainMenu.Selected || this.favouriteButtonMainMenu.MouseOver)
        {
            this.infoLabel.text = base.Translate("Set a track to play as the main menu theme");
            this.infoLabelFade = 1f;
        }
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        if (opening || closing)
        {
            uAlpha = Mathf.Pow(Mathf.Max(0f, Mathf.Lerp(lastAlpha, currentAlpha, timeStacker)), 1.5f);
            darkSprite.alpha = uAlpha * 0.3f;
            jukeboxLogo.y = pages[0].pos.y + 618f;
        }
        pages[0].pos.y = Mathf.Lerp(manager.rainWorld.options.ScreenSize.y + 100f, 0.01f, (uAlpha < 0.999f) ? uAlpha : 1f);

        if (Plugin.mainMenuSong == this.songList[this.selectedTrack])
        {
            this.favouriteButtonMainMenu.symbolSprite.color = new Color(1f, 0.7f, 0f);
            this.favouriteButtonMainMenu.symbolSprite.shader = this.manager.rainWorld.Shaders["MenuTextCustom"];
            return;
        }
        this.favouriteButtonMainMenu.symbolSprite.color = MenuRGB(MenuColors.VeryDarkGrey);
        this.favouriteButtonMainMenu.symbolSprite.shader = this.manager.rainWorld.Shaders["Basic"];
    }

    public override void Singal(MenuObject sender, string message)
    {
        if (message.StartsWith("BACK"))
        {
            this.closing = true;
            this.targetAlpha = 0f;
            if (message == "BACK")
            {
                this.PlaySound(SoundID.MENU_Switch_Page_Out);
            }
            Expedition.Expedition.coreFile.Save(false);
            return;
        }
        else if (message == "REPEAT")
        {
            JukeboxManager.repeatAnywhere = !JukeboxManager.repeatAnywhere;
        }
        else if (message == "SHUFFLE")
        {
            JukeboxManager.shuffleAnywhere = !JukeboxManager.shuffleAnywhere;
        }
        else if (message == "THREAT")
        {
            System.Diagnostics.Process.Start("https://threatmixer.netlify.app/");
        }
        else if (message == "FAVOURITEMAINMENU")
        {
            if (Plugin.mainMenuSong != this.songList[this.selectedTrack])
            {
                Plugin.mainMenuSong = this.songList[this.selectedTrack];
                this.PlaySound(SoundID.MENU_Player_Join_Game);
                return;
            }
            Plugin.mainMenuSong = "";
            this.PlaySound(SoundID.MENU_Button_Press_Init);
        }

        base.Singal(sender, message);
    }
}
