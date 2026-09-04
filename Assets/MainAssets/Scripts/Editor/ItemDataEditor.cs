#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ItemData))]
public class ItemDataEditor : Editor
{
    // Always-shown properties
    private SerializedProperty _id, _displayName, _description, _icon;
    private SerializedProperty _archetype, _usageContext, _apCost, _maxHeld;

    // Healing
    private SerializedProperty _healMode, _healAmount, _healPercent, _aoeRadius, _clearsStatusEffects;

    // Revival
    private SerializedProperty _revivePercent;

    // BuffDebuff
    private SerializedProperty _statusEffect, _statusDuration, _isDebuff;

    // APAffecting
    private SerializedProperty _apDelta;

    // AcceptanceRateEnhancing
    private SerializedProperty _acceptanceBonus, _appliesToEntireAttempt;

    // DialogAssist
    private SerializedProperty _assistType, _usesPerSession;

    private void OnEnable()
    {
        _id          = serializedObject.FindProperty("id");
        _displayName = serializedObject.FindProperty("displayName");
        _description = serializedObject.FindProperty("description");
        _icon        = serializedObject.FindProperty("icon");

        _archetype    = serializedObject.FindProperty("archetype");
        _usageContext = serializedObject.FindProperty("usageContext");
        _apCost       = serializedObject.FindProperty("apCost");
        _maxHeld      = serializedObject.FindProperty("maxHeld");

        _healMode           = serializedObject.FindProperty("healMode");
        _healAmount         = serializedObject.FindProperty("healAmount");
        _healPercent        = serializedObject.FindProperty("healPercent");
        _aoeRadius          = serializedObject.FindProperty("aoeRadius");
        _clearsStatusEffects= serializedObject.FindProperty("clearsStatusEffects");

        _revivePercent = serializedObject.FindProperty("revivePercent");

        _statusEffect  = serializedObject.FindProperty("statusEffect");
        _statusDuration= serializedObject.FindProperty("statusDuration");
        _isDebuff      = serializedObject.FindProperty("isDebuff");

        _apDelta = serializedObject.FindProperty("apDelta");

        _acceptanceBonus       = serializedObject.FindProperty("acceptanceBonus");
        _appliesToEntireAttempt= serializedObject.FindProperty("appliesToEntireAttempt");

        _assistType    = serializedObject.FindProperty("assistType");
        _usesPerSession= serializedObject.FindProperty("usesPerSession");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Identity ─────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_id,          new GUIContent("ID (stable, never rename)"));
        EditorGUILayout.PropertyField(_displayName, new GUIContent("Display Name"));
        EditorGUILayout.PropertyField(_description, new GUIContent("Description"));
        EditorGUILayout.PropertyField(_icon,        new GUIContent("Icon"));
        EditorGUILayout.Space(6);

        // ── Usage ─────────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Usage", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_archetype,    new GUIContent("Archetype"));
        EditorGUILayout.PropertyField(_usageContext, new GUIContent("Usage Context"));
        EditorGUILayout.PropertyField(_apCost,       new GUIContent("AP Cost"));
        EditorGUILayout.PropertyField(_maxHeld,      new GUIContent("Max Held"));
        EditorGUILayout.Space(6);

        // ── Archetype-specific ────────────────────────────────────────────────
        var archetype = (ItemEnum.Archetype)_archetype.enumValueIndex;

        switch (archetype)
        {
            case ItemEnum.Archetype.Healing:
                DrawHealingSection();
                break;
            case ItemEnum.Archetype.Revival:
                DrawRevivalSection();
                break;
            case ItemEnum.Archetype.BuffDebuff:
                DrawBuffDebuffSection();
                break;
            case ItemEnum.Archetype.APAffecting:
                DrawAPSection();
                break;
            case ItemEnum.Archetype.AcceptanceRateEnhancing:
                DrawAcceptanceRateSection();
                break;
            case ItemEnum.Archetype.DialogAssist:
                DrawDialogAssistSection();
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHealingSection()
    {
        EditorGUILayout.LabelField("Healing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_healMode,   new GUIContent("Heal Mode"));

        var healMode = (ItemEnum.HealMode)_healMode.enumValueIndex;

        EditorGUILayout.PropertyField(_healAmount, new GUIContent("Flat Heal Amount"));
        EditorGUILayout.PropertyField(_healPercent,new GUIContent("Heal % of Max HP"));

        if (healMode == ItemEnum.HealMode.AreaHeal)
            EditorGUILayout.PropertyField(_aoeRadius, new GUIContent("AoE Radius (tiles)"));

        EditorGUILayout.PropertyField(_clearsStatusEffects, new GUIContent("Clears Status Effects"));

        float total = _healAmount.intValue + _healPercent.floatValue * 100f;
        if (total <= 0)
            EditorGUILayout.HelpBox("Set Flat Heal Amount and/or Heal % — both are 0.", MessageType.Warning);
    }

    private void DrawRevivalSection()
    {
        EditorGUILayout.LabelField("Revival", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_revivePercent, new GUIContent("HP Restored on Revive (0–1)"));
        EditorGUILayout.HelpBox(
            "Revival is not yet fully functional — it requires the Reaction System.\n" +
            "The item will appear in the inventory but use is blocked at runtime.",
            MessageType.Info);
    }

    private void DrawBuffDebuffSection()
    {
        EditorGUILayout.LabelField("Buff / Debuff", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_statusEffect,   new GUIContent("Status Effect"));
        EditorGUILayout.PropertyField(_statusDuration, new GUIContent("Duration (turns)"));
        EditorGUILayout.PropertyField(_isDebuff,       new GUIContent("Is Debuff (targets enemy)"));

        if (_statusEffect.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Assign a StatusEffectData asset.", MessageType.Warning);
    }

    private void DrawAPSection()
    {
        EditorGUILayout.LabelField("AP Affecting", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_apDelta, new GUIContent("AP Delta (positive = grant, negative = remove)"));

        int net = _apDelta.intValue - _apCost.intValue;
        string netStr = net >= 0 ? $"+{net}" : $"{net}";
        EditorGUILayout.HelpBox($"Net AP change for player: {netStr} (apDelta {_apDelta.intValue:+0;-0} − cost {_apCost.intValue}).", MessageType.None);
    }

    private void DrawAcceptanceRateSection()
    {
        EditorGUILayout.LabelField("Acceptance Rate Enhancing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_acceptanceBonus,
            new GUIContent("Acceptance Bonus (0–0.5)", "Added directly to the acceptance roll on the NEXT dialogue initiation."));
        EditorGUILayout.PropertyField(_appliesToEntireAttempt,
            new GUIContent("Applies to Entire Attempt", "If true the bonus persists for all questions in the session, not just the initiation roll."));

        if (_acceptanceBonus.floatValue <= 0f)
            EditorGUILayout.HelpBox("Acceptance Bonus is 0 — this item will have no effect.", MessageType.Warning);
    }

    private void DrawDialogAssistSection()
    {
        EditorGUILayout.LabelField("Dialog Assist", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_assistType, new GUIContent("Assist Type"));
        EditorGUILayout.PropertyField(_usesPerSession, new GUIContent("Uses Per Session",
            "How many times this assist can be applied in one dialogue session."));

        string desc = ((ItemEnum.DialogAssistType)_assistType.enumValueIndex) switch
        {
            ItemEnum.DialogAssistType.HintReveal     => "Marks the correct answer with a ✓ glyph.",
            ItemEnum.DialogAssistType.EliminateOption => "Removes one non-correct answer button.",
            ItemEnum.DialogAssistType.AllowRetry      => "Absorbs the next wrong/reallybad answer — retry for free.",
            _ => ""
        };
        if (!string.IsNullOrEmpty(desc))
            EditorGUILayout.HelpBox(desc, MessageType.Info);
    }
}
#endif
