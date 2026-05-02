using UnityEngine;

/// <summary>
/// ScriptableObject that maps each ElementType to a sprite icon.
///
/// HOW TO SET UP:
///   1. In the Project window: right-click → Create → Evermore → Type Icon Library
///   2. Name the asset exactly "TypeIconLibrary" and place it in a "Resources" folder
///      (e.g. Assets/Resources/TypeIconLibrary.asset).
///   3. In the Inspector, assign a sprite for each element type from the Unit_Markers folder.
///
/// The MonsterInfoPanel loads this automatically at runtime via Resources.Load.
/// </summary>
[CreateAssetMenu(menuName = "Evermore/Type Icon Library", fileName = "TypeIconLibrary")]
public class TypeIconLibrary : ScriptableObject
{
    [System.Serializable]
    public struct TypeIcon
    {
        public AttackEnum.ElementType elementType;
        public Sprite                 icon;
    }

    [Tooltip("Assign one entry per ElementType with its corresponding sprite.")]
    public TypeIcon[] icons;

    // ── Cached instance (auto-loaded from Resources) ──────────────────────────

    private static TypeIconLibrary _instance;

    public static TypeIconLibrary Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<TypeIconLibrary>("TypeIconLibrary");
            return _instance;
        }
    }

    /// <summary>
    /// Returns the sprite for the given element type, or null if not configured.
    /// </summary>
    public Sprite GetIcon(AttackEnum.ElementType type)
    {
        if (icons == null) return null;
        foreach (var entry in icons)
            if (entry.elementType == type) return entry.icon;
        return null;
    }
}
