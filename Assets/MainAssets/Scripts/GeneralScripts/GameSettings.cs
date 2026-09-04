using UnityEngine;

/// <summary>
/// Static container for player-adjustable game settings.
/// Values are persisted across sessions via PlayerPrefs.
///
/// Usage:
///   GameSettings.EdgeScrollEnabled = false;
///   if (GameSettings.EdgeScrollEnabled) { ... }
/// </summary>
public static class GameSettings
{
    private const string KeyEdgeScroll = "Setting_EdgeScroll";

    /// <summary>
    /// Whether edge scrolling is active.  Default: true.
    /// Changing this takes effect immediately — no restart required.
    /// </summary>
    public static bool EdgeScrollEnabled
    {
        get => PlayerPrefs.GetInt(KeyEdgeScroll, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(KeyEdgeScroll, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
