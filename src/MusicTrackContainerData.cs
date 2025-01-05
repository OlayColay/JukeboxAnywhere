using Expedition;
using Menu;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace JukeboxAnywhere;

public class MusicTrackContainerData
{
    public Dictionary<string, string> unlockedSongs = ExpeditionProgression.GetUnlockedSongs();
}

public static class MusicTrackContainerExtension
{
    private static readonly ConditionalWeakTable<MusicTrackContainer, MusicTrackContainerData> cwt = new();

    public static MusicTrackContainerData JA(this MusicTrackContainer musicTrackContainer) => cwt.GetValue(musicTrackContainer, _ => new MusicTrackContainerData());
}