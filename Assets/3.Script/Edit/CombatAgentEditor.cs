#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatAgent))]
public class CombatAgentEditor : Editor
{
    SerializedProperty skillType;
    SerializedProperty skillCooldown;
    SerializedProperty skillCastRange;

    SerializedProperty magicRainRadius;
    SerializedProperty magicRainMaxTargets;
    SerializedProperty magicRainDelay;
    SerializedProperty magicRainDamage;
    SerializedProperty magicRainProjectilePrefab;
    SerializedProperty magicRainFallHeight;
    SerializedProperty magicRainFlightTime;

    SerializedProperty shockwaveRadius;
    SerializedProperty shockwaveDelay;
    SerializedProperty shockwaveDamage;
    SerializedProperty shockwaveFxPrefab;

    void OnEnable()
    {
        skillType = serializedObject.FindProperty("skillType");
        skillCooldown = serializedObject.FindProperty("skillCooldown");
        skillCastRange = serializedObject.FindProperty("skillCastRange");

        magicRainRadius = serializedObject.FindProperty("magicRainRadius");
        magicRainMaxTargets = serializedObject.FindProperty("magicRainMaxTargets");
        magicRainDelay = serializedObject.FindProperty("magicRainDelay");
        magicRainDamage = serializedObject.FindProperty("magicRainDamage");
        magicRainProjectilePrefab = serializedObject.FindProperty("magicRainProjectilePrefab");
        magicRainFallHeight = serializedObject.FindProperty("magicRainFallHeight");
        magicRainFlightTime = serializedObject.FindProperty("magicRainFlightTime");

        shockwaveRadius = serializedObject.FindProperty("shockwaveRadius");
        shockwaveDelay = serializedObject.FindProperty("shockwaveDelay");
        shockwaveDamage = serializedObject.FindProperty("shockwaveDamage");
        shockwaveFxPrefab = serializedObject.FindProperty("shockwaveFxPrefab");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspectorExceptSkill();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Skill", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(skillType);

        SkillType type = (SkillType)skillType.enumValueIndex;

        if (type != SkillType.None)
        {
            EditorGUILayout.PropertyField(skillCooldown);
            EditorGUILayout.PropertyField(skillCastRange);
        }

        EditorGUILayout.Space();

        switch (type)
        {
            case SkillType.MagicRain:
                EditorGUILayout.LabelField("Magic Rain Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(magicRainRadius);
                EditorGUILayout.PropertyField(magicRainMaxTargets);
                EditorGUILayout.PropertyField(magicRainDelay);
                EditorGUILayout.PropertyField(magicRainDamage);
                EditorGUILayout.PropertyField(magicRainProjectilePrefab);
                EditorGUILayout.PropertyField(magicRainFallHeight);
                EditorGUILayout.PropertyField(magicRainFlightTime);
                break;

            case SkillType.Shockwave:
                EditorGUILayout.LabelField("Shockwave Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(shockwaveRadius);
                EditorGUILayout.PropertyField(shockwaveDelay);
                EditorGUILayout.PropertyField(shockwaveDamage);
                EditorGUILayout.PropertyField(shockwaveFxPrefab);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }

    void DrawDefaultInspectorExceptSkill()
    {
        DrawPropertiesExcluding(serializedObject,
            "skillType",
            "skillCooldown",
            "skillCastRange",

            "magicRainRadius",
            "magicRainMaxTargets",
            "magicRainDelay",
            "magicRainDamage",
            "magicRainProjectilePrefab",
            "magicRainFallHeight",
            "magicRainFlightTime",

            "shockwaveRadius",
            "shockwaveDelay",
            "shockwaveDamage",
            "shockwaveFxPrefab");
    }
}
#endif