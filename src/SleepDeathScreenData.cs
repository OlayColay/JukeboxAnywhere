using Expedition;
using Menu;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace JukeboxAnywhere;

public class SleepDeathScreenData
{
    public JukeboxAnywhereButton jukeboxButton;
}

public static class SleepDeathScreenExtension
{
    private static readonly ConditionalWeakTable<SleepAndDeathScreen, SleepDeathScreenData> cwt = new();

    public static SleepDeathScreenData JA(this SleepAndDeathScreen sleepDeathScreen) => cwt.GetValue(sleepDeathScreen, _ => new SleepDeathScreenData());
}