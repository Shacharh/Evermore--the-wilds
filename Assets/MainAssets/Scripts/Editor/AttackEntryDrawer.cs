using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AttackEntry))]
public class AttackEntryDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, GameObject> _rootPicker  = new();
    private static readonly Dictionary<string, bool>       _vfxExpanded = new();
    private static readonly Dictionary<string, Object>     _lastTarget  = new();

    private const float LineH = 18f;
    private const float Pad   = 2f;

    private const int StandardFieldCount = 4; // attack, levelLearned, AnimationTrigger, vfxSpawnOffset
    private const int VFXCollapsedLines  = 1; // toggle button only
    private const int VFXExpandedLines   = 4; // button + root picker + dropdown + path readout

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        if (!prop.isExpanded) return LineH;
        bool vfxOpen = IsVFXExpanded(prop);
        int total = 1 + StandardFieldCount + (vfxOpen ? VFXExpandedLines : VFXCollapsedLines);
        return total * (LineH + Pad);
    }

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);

        var rect = new Rect(pos.x, pos.y, pos.width, LineH);
        prop.isExpanded = EditorGUI.Foldout(rect, prop.isExpanded, label, true);

        if (prop.isExpanded)
        {
            EditorGUI.indentLevel++;

            rect = NextLine(rect);
            EditorGUI.PropertyField(rect, prop.FindPropertyRelative("attack"));

            rect = NextLine(rect);
            EditorGUI.PropertyField(rect, prop.FindPropertyRelative("levelLearned"));

            rect = NextLine(rect);
            EditorGUI.PropertyField(rect, prop.FindPropertyRelative("AnimationTrigger"));

            rect = NextLine(rect);
            EditorGUI.PropertyField(rect, prop.FindPropertyRelative("vfxSpawnOffset"));

            // ── VFX Spawn Point toggle ─────────────────────────────────────────
            string key     = prop.propertyPath;
            bool   vfxOpen = IsVFXExpanded(prop);

            rect = NextLine(rect);
            string currentPath = prop.FindPropertyRelative("vfxSpawnPointPath").stringValue;
            string pathSuffix  = string.IsNullOrEmpty(currentPath) ? "" : $"  ({currentPath})";
            string btnLabel    = (vfxOpen ? "▼" : "▶") + "  VFX Spawn Point" + pathSuffix;
            if (GUI.Button(EditorGUI.IndentedRect(rect), btnLabel, EditorStyles.miniButton))
            {
                _vfxExpanded[key] = !vfxOpen;
                vfxOpen = _vfxExpanded[key];
                if (!vfxOpen)
                    _rootPicker[key] = null; // clear picker on hide
            }

            // ── VFX fields (hidden until toggled open) ─────────────────────────
            if (vfxOpen)
            {
                if (!_rootPicker.ContainsKey(key))
                    _rootPicker[key] = null;

                rect = NextLine(rect);
                _rootPicker[key] = (GameObject)EditorGUI.ObjectField(
                    rect, "Monster Root", _rootPicker[key], typeof(GameObject), true);

                var pathProp = prop.FindPropertyRelative("vfxSpawnPointPath");

                rect = NextLine(rect);
                var root = _rootPicker[key];
                if (root != null)
                {
                    List<Transform> children = GetAllDescendants(root.transform);
                    var options = new string[children.Count + 1];
                    options[0] = "(none — use root position)";
                    for (int i = 0; i < children.Count; i++)
                        options[i + 1] = GetRelativePath(root.transform, children[i]);

                    int currentIndex = 0;
                    for (int i = 1; i < options.Length; i++)
                        if (options[i] == pathProp.stringValue) { currentIndex = i; break; }

                    EditorGUI.BeginChangeCheck();
                    int newIndex = EditorGUI.Popup(rect, "Spawn Point", currentIndex, options);
                    if (EditorGUI.EndChangeCheck())
                        pathProp.stringValue = newIndex == 0 ? "" : options[newIndex];
                }
                else
                {
                    EditorGUI.LabelField(rect, "Spawn Point", "← drag the monster prefab root above");
                }

                rect = NextLine(rect);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.PropertyField(rect, pathProp, new GUIContent("Path (read-only)"));
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    // ── State ─────────────────────────────────────────────────────────────────

    // Resets the section (collapsed + picker cleared) whenever the user
    // clicks away to a different asset and then back.
    private static bool IsVFXExpanded(SerializedProperty prop)
    {
        string key           = prop.propertyPath;
        Object currentTarget = prop.serializedObject.targetObject;

        if (_lastTarget.TryGetValue(key, out Object prev) && prev != currentTarget)
        {
            _vfxExpanded[key] = false;
            _rootPicker[key]  = null;
        }
        _lastTarget[key] = currentTarget;

        if (!_vfxExpanded.ContainsKey(key))
            _vfxExpanded[key] = false;

        return _vfxExpanded[key];
    }

    // ── Layout ────────────────────────────────────────────────────────────────

    private static Rect NextLine(Rect r) =>
        new Rect(r.x, r.y + LineH + Pad, r.width, LineH);

    // ── Transform helpers ─────────────────────────────────────────────────────

    private static List<Transform> GetAllDescendants(Transform root)
    {
        var list = new List<Transform>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            if (t != root) list.Add(t);
        return list;
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        var parts   = new List<string>();
        var current = target;
        while (current != null && current != root)
        {
            parts.Insert(0, current.name);
            current = current.parent;
        }
        return string.Join("/", parts);
    }
}
