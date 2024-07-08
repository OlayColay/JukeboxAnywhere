using System.Collections.Generic;
using UnityEngine;
using Expedition;
using Menu;
using RWCustom;
using Music;
using BepInEx;
using System.Linq;

namespace JukeboxAnywhere;
public class JukeboxAnywhereButton : SimpleButton
{
    string[] songList;
    Song currentSong;
    int songNum;
    Color nameColor = Color.white;
    Color trackColor = Color.white;
    MenuLabel trackName;
    FSprite sprite = new("Futile_White", true);

	public JukeboxAnywhereButton(Menu.Menu menu, MenuObject owner, Vector2 pos)
		: base(menu, owner, "", "JUKEBOX", pos, new(50f, 50f))
	{
        // Get currently playing song
        this.songList = [.. ExpeditionProgression.GetUnlockedSongs().Values];
        UpdateCurrentSong();

        this.menuLabel.label.alignment = FLabelAlignment.Left;
        this.menuLabel.pos = new Vector2(-70f, 10f);
        this.menuLabel.label.color = this.trackColor;
        this.trackName = new MenuLabel(menu, this, (currentSong != null && songNum > 0) ? ExpeditionProgression.TrackName(songList[songNum]) : "", new Vector2(53f, 16f), default, false, null);
        this.trackName.label.alignment = FLabelAlignment.Left;
        this.trackName.label.color = this.nameColor;
        this.subObjects.Add(this.trackName);
        this.sprite.SetAnchor(0.5f, 0.5f);
        this.sprite.x = this.pos.x + 25f;
        this.sprite.y = this.pos.y + 25f;
        this.Container.AddChild(this.sprite);
        this.trackName.label.color = new Color(0.8f, 0.8f, 0.8f);
        this.sprite.color = new Color(0.8f, 0.8f, 0.8f);
        for (int i = 9; i < this.roundedRect.sprites.Length; i++)
        {
            this.roundedRect.sprites[i].shader = menu.manager.rainWorld.Shaders["MenuTextCustom"];
        }

        if (currentSong != null)
        {
            SetSize(new Vector2(240f, 50f));
        }
    }

    public override void Update()
    {
        base.Update();
        UpdateCurrentSong();
        if (currentSong != null && songNum >= 0)
        {
            SetSize(new Vector2(240f, 50f));
            trackName.text = ExpeditionProgression.TrackName(songList[songNum]);
        }
        else
        {
            SetSize(new Vector2(50f, 50f));
            menuLabel.text = "";
            trackName.text = "";
        }
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);
        this.sprite.x = this.pos.x + 25f;
        this.sprite.y = this.pos.y + 27f;
        for (int i = 0; i < 8; i++)
        {
            this.selectRect.sprites[i].color = this.MyColor(timeStacker);
            this.selectRect.sprites[i].shader = this.menu.manager.rainWorld.Shaders["MenuText"];
        }

        if (this.currentSong != null)
        {
            // spin disc even if the song playing doesn't have a display name (like the short atmoshperic songs)
            this.sprite.rotation += 150f * Time.deltaTime;
            if (this.sprite.element.name != "mediadisc")
            {
                this.sprite.SetElementByName("mediadisc");
            }
        }
        else
        {
            this.sprite.rotation = 0f;
            if (this.sprite.element.name != "musicSymbol")
            {
                this.sprite.SetElementByName("musicSymbol");
            }
        }
        this.sprite.alpha = 1f;
        this.menuLabel.pos.x = -70f;
        this.trackName.pos.x = 52f;
        this.sprite.shader = this.menu.manager.rainWorld.Shaders["MenuText"];
        this.menuLabel.label.color = this.trackColor;
        this.trackName.label.color = this.nameColor;
    }

    public void UpdateCurrentSong()
    {
        currentSong = menu.manager.musicPlayer.song;
        if (currentSong != null)
        {
            string key = ExpeditionProgression.GetUnlockedSongs().FirstOrDefault(e => e.Value == currentSong.name).Key;
            int selectedTrack = 0;
            if (!key.IsNullOrWhiteSpace() && !int.TryParse(key.Substring(key.IndexOf('-') + 1), out selectedTrack))
            {
                Custom.LogImportant("JukeboxAnywhere: currently playing track has invalid code (ie. mus-xx)!");
            }

            if (selectedTrack > 0)
            {
                this.menuLabel.label.text = menu.Translate("Track:") + " " + selectedTrack.ToString();
            }

            songNum = selectedTrack - 1;
        }
    }
}
