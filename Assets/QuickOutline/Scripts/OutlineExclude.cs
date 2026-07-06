/// <summary>
/// Add this component to any child GameObject whose renderer should be excluded
/// from the parent's Outline effect.
///
/// Usage — wolf ring example:
///   1. Open the Maiwolf prefab.
///   2. Select the ring/wheel child object (the one with the torus mesh).
///   3. Add Component → OutlineExclude.
///   4. The ring will be silently skipped when Outline.Awake collects renderers.
/// </summary>
public class OutlineExclude : UnityEngine.MonoBehaviour { }
