using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace JukeboxAnywhere;
public class JukeboxManager : MainLoopProcess
{
    public static JukeboxManager instance;

    public static bool repeatAnywhere = false;
    public static bool shuffleAnywhere = false;
    public static string pendingSong;

    public static List<string> unlockedSongs;

    public JukeboxManager(ProcessManager manager, List<string> unlockedSongs) : base(manager, null)
    {
        if (instance != null)
        {
            manager.StopSideProcess(instance);
        }
        JukeboxManager.instance = this;
        JukeboxManager.unlockedSongs = unlockedSongs;
    }

    public override void Update()
    {
        if (instance.manager.musicPlayer == null ||
            manager.sideProcesses.Any((process) => process.ID == Expedition.ExpeditionEnums.ProcessID.ExpeditionJukebox))
        {
            return;
        }

        if (instance.manager.musicPlayer.song == null)
        {
            if (shuffleAnywhere && pendingSong != null)
            {
                instance.manager.musicPlayer.MenuRequestsSong(pendingSong, 1f, 0f);
                pendingSong = null;
            }
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
                NextShuffleTrack();
            }
        }
    }

    public static void NextShuffleTrack()
    {
        if (instance.manager.musicPlayer == null)
        {
            return;
        }

        string curSong = instance.manager.musicPlayer.song?.name;
        do
        {
            pendingSong = unlockedSongs[Random.Range(0, unlockedSongs.Count)];
        }
        while (pendingSong == curSong);

        instance.manager.musicPlayer.FadeOutAllSongs(0f);
        Plugin.JLogger.LogInfo("JukeboxAnywhere: Playing next shuffled song: " + pendingSong);
    }
}
