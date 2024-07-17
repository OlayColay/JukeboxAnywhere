using System;
using System.Linq;
using UnityEngine;

namespace JukeboxAnywhere;
public class JukeboxManager : MainLoopProcess
{
    public static JukeboxManager instance;

    public static bool repeatAnywhere = false;
    public static bool shuffleAnywhere = false;

    public JukeboxManager(ProcessManager manager) : base(manager, null)
    {
        if (instance != null)
        {
            manager.StopSideProcess(instance);
        }
        instance = this;
    }

    public override void Update()
    {
        if (manager.sideProcesses.Any((process) => process.ID == Expedition.ExpeditionEnums.ProcessID.ExpeditionJukebox))
        {
            return;
        }

		TimeSpan timeSpan = TimeSpan.FromSeconds((double)this.manager.musicPlayer.song.subTracks[0].source.time);
        TimeSpan timeSpan2 = TimeSpan.FromSeconds((double)this.manager.musicPlayer.song.subTracks[0].source.clip.length);
        float num = Mathf.InverseLerp(0f, (float)timeSpan2.TotalMilliseconds, (float)timeSpan.TotalMilliseconds) * 100f;
        if (num >= 99f)
        {
            if (repeatAnywhere)
            {
                this.manager.musicPlayer.song.subTracks[0].source.time = 0f;
            }
            else if (shuffleAnywhere)
            {
                //this.manager.musicPlayer.FadeOutAllSongs(0f);
                //this.NextTrack(true);
                //this.seekBar.SetProgress(0f);
                //this.currentSong.label.text = ExpeditionProgression.TrackName(this.songList[this.selectedTrack]);
                //this.pendingSong = 1;
                //this.trackContainer.GoToPlayingTrackPage();
            }
        }
    }
}
