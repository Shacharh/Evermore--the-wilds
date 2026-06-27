using UnityEngine;

/// <summary>
/// Drop this onto any GameObject in your main-menu scene.
/// It starts the menu music loop automatically when the scene loads,
/// and stops it when the scene is unloaded.
///
/// Setup:
///   1. Open your SFXConfig asset and assign an AudioClip to "Menu Music Clip".
///   2. Add this component to any GameObject in the menu scene (e.g. the Canvas or a dedicated Audio object).
/// </summary>
public class MenuSceneAudio : MonoBehaviour
{
    private void Start()  => AudioManager.PlayMenuMusic();
    private void OnDestroy() => AudioManager.StopMusic();
}
