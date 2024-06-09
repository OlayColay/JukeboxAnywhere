using Expedition;
using Menu;
using System;
using System.Linq;
using UnityEngine;

namespace JukeboxAnywhere;
public class Jukebox : ExpeditionJukebox
{
    public bool opening;
    public bool closing;

    public float lastAlpha;
    public float currentAlpha;

    public float targetAlpha;
    public float uAlpha;

    protected FSprite darkSprite;

    public Jukebox(ProcessManager manager) : base(manager)
    {
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

        this.pages[0].RemoveSubObject(base.scene);
        base.scene.UnloadImages();
        base.scene = null;

        opening = true;
        targetAlpha = 1f;

        // Re-select playing track if one is already playing
        Music.Song currentSong = manager.musicPlayer.song;
        if (currentSong != null)
        {
            //RWCustom.Custom.Log("Currently playing song: " + currentSong.name);
            //RWCustom.Custom.Log("Songs:" + string.Join(", ", ExpeditionProgression.GetUnlockedSongs().Select(kv => $"\n{kv.Key}: {kv.Value}")));
            string key = ExpeditionProgression.GetUnlockedSongs().FirstOrDefault(e => e.Value == currentSong.name).Key;
            if (key != "" && !int.TryParse(key.Substring(key.IndexOf('-')+1), out selectedTrack))
            {
                Debug.LogError("JukeboxAnywhere: currently playing track has invalid code (ie. mus-xx)!");
            }
            selectedTrack--;
            this.trackContainer.GoToPlayingTrackPage();
        }
    }

    public override void Update()
    {
        base.Update();

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
            closing = false;
        }

        if (opening)
        {
            return;
        }
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        if (opening || closing)
        {
            uAlpha = Mathf.Pow(Mathf.Max(0f, Mathf.Lerp(lastAlpha, currentAlpha, timeStacker)), 1.5f);
            darkSprite.alpha = uAlpha * 0.3f;
        }
        pages[0].pos.y = Mathf.Lerp(manager.rainWorld.options.ScreenSize.y + 100f, 0.01f, (uAlpha < 0.999f) ? uAlpha : 1f);
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
            return;
        }
        base.Singal(sender, message);
    }
}
