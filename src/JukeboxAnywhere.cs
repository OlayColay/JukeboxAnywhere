using BepInEx;
using Expedition;
using Menu;
using System;
using System.Collections.Generic;
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

    public FSprite trackContainerBackground;

    public JukeboxAnywhere(ProcessManager manager) : base(manager)
    {
        // Create new JukeboxManager
        manager.sideProcesses.Add(new JukeboxManager(manager, this.songList));
        this.repeat = JukeboxManager.repeatAnywhere;
        this.shuffle = JukeboxManager.shuffleAnywhere;

        opening = true;
        targetAlpha = 1f;

        FNode trackContainerBg = this.trackContainer.Container._childNodes.First(sprite => sprite.alpha == 0.55f);
        if (trackContainerBg != null)
        {
            this.trackContainerBackground = trackContainerBg as FSprite;
            this.trackContainerBackground.y = this.trackContainer.pos.y - 680f;
        }

        this.trackContainer.borderRect.pos.y = -680f;

        // Re-select playing track if one is already playing
        if (manager.musicPlayer.song != null)
        {
            string currentSong = manager.musicPlayer.song.name;

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
                this.currentSong.label.text = ExpeditionProgression.TrackName(this.songList[this.selectedTrack]);
            }
            this.trackContainer.GoToPlayingTrackPage();
        }

        pages[0].pos.y = manager.rainWorld.options.ScreenSize.y + 300f;

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
            jukeboxLogo.y = pages[0].pos.y + 618f;
            this.shade.alpha = uAlpha * 0.8f;
            if (this.trackContainerBackground != null)
            {
                this.trackContainerBackground.alpha = uAlpha * 0.55f;
                this.trackContainerBackground.y = this.pages[0].pos.y - 15f;
            }
        }
        pages[0].pos.y = Mathf.Lerp(manager.rainWorld.options.ScreenSize.y + 300f, 0.01f, (uAlpha < 0.999f) ? uAlpha : 1f);

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
        if (message == "PREV" && JukeboxConfig.AlphabeticalOrder.Value)
        {
            this.manager.musicPlayer.FadeOutAllSongs(0f);
            this.PreviousTrack(this.shuffle);
            this.seekBar.SetProgress(0f);
            this.currentSong.label.text = ExpeditionProgression.TrackName(this.songList[this.selectedTrack]);
            this.pendingSong = 1;
            this.trackContainer.GoToPlayingTrackPage();
            base.PlaySound(SoundID.MENU_Button_Press_Init);
            return;
        }
        if (message == "NEXT" && JukeboxConfig.AlphabeticalOrder.Value)
        {
            this.manager.musicPlayer.FadeOutAllSongs(0f);
            this.NextTrack(this.shuffle);
            this.seekBar.SetProgress(0f);
            this.currentSong.label.text = ExpeditionProgression.TrackName(this.songList[this.selectedTrack]);
            this.pendingSong = 1;
            this.trackContainer.GoToPlayingTrackPage();
            base.PlaySound(SoundID.MENU_Button_Press_Init);
            return;
        }

        base.Singal(sender, message);
    }

    public new void NextTrack(bool shuffle)
    {
        MusicTrackButton[] trackList = this.trackContainer.trackList;
        int num = -1;
        for (int i = 0; i < trackList.Length; i++)
        {
            if (trackList[i].AmISelected)
            {
                num = i;
                break;
            }
        }
        if (shuffle)
        {
            List<int> list = [];
            for (int i = 0; i < trackList.Length; i++)
            {
                if (trackList[i].unlocked && i != num)
                {
                    list.Add(i);
                }
            }
            num = ((list.Count > 0) ? list[UnityEngine.Random.Range(0, list.Count)] : num);
            this.selectedTrack = this.songList.Select((value, idx) => new { value, idx })
                .FirstOrDefault(songName => ExpeditionProgression.TrackName(songName.value) == trackList[num].trackName.text)?.idx ?? -1;
            return;
        }
        for (int j = num + 1; j < trackList.Length; j++)
        {
            if (trackList[j].unlocked)
            {
                this.selectedTrack = this.songList.Select((value, idx) => new { value, idx })
                    .FirstOrDefault(songName => ExpeditionProgression.TrackName(songName.value) == trackList[j].trackName.text)?.idx ?? -1;
                return;
            }
        }
        for (int k = 0; k < num; k++)
        {
            if (trackList[k].unlocked)
            {
                this.selectedTrack = this.songList.Select((value, idx) => new { value, idx })
                    .FirstOrDefault(songName => ExpeditionProgression.TrackName(songName.value) == trackList[k].trackName.text)?.idx ?? -1;
                return;
            }
        }
        this.selectedTrack = num;
    }

    public new void PreviousTrack(bool shuffle)
    {
        MusicTrackButton[] trackList = this.trackContainer.trackList;
        int num = -1;
        for (int i = 0; i < trackList.Length; i++)
        {
            if (trackList[i].AmISelected)
            {
                num = i;
                break;
            }
        }
        if (shuffle)
        {
            List<int> list = [];
            for (int i = 0; i < trackList.Length; i++)
            {
                if (trackList[i].unlocked && i != num)
                {
                    list.Add(i);
                }
            }
            num = ((list.Count > 0) ? list[UnityEngine.Random.Range(0, list.Count)] : num);
            this.selectedTrack = this.songList.Select((value, idx) => new { value, idx })
                .FirstOrDefault(songName => ExpeditionProgression.TrackName(songName.value) == trackList[num].trackName.text)?.idx ?? -1;
            return;
        }
        for (int j = num - 1; j > 0; j--)
        {
            if (trackList[j].unlocked)
            {
                this.selectedTrack = this.songList.Select((value, idx) => new { value, idx })
                    .FirstOrDefault(songName => ExpeditionProgression.TrackName(songName.value) == trackList[j].trackName.text)?.idx ?? -1;
                return;
            }
        }
        for (int k = trackList.Length - 1; k > num; k--)
        {
            if (trackList[k].unlocked)
            {
                this.selectedTrack = this.songList.Select((value, idx) => new { value, idx })
                    .FirstOrDefault(songName => ExpeditionProgression.TrackName(songName.value) == trackList[k].trackName.text)?.idx ?? -1;
                return;
            }
        }
        this.selectedTrack = num;
    }
}
