#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public class GameManagerWindow : EditorWindow
{
    Vector2 scroll;

    [MenuItem("Tools/Game Manager")]
    public static void Open()
    {
        GetWindow<GameManagerWindow>("Game Manager");
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Game Manager Tools", EditorStyles.boldLabel);
        GUILayout.Space(8);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawSaveSection();
        GUILayout.Space(12);

        DrawPlayerSection();
        GUILayout.Space(12);

        DrawAllySpawnSection();

        EditorGUILayout.EndScrollView();
    }

    void DrawSaveSection()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Save", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset Save Data"))
        {
            SaveManager.Instance.DeleteSave(SaveName.PlayerData);
            SaveManager.Instance.DeleteSave(SaveName.MapFogData);

            Debug.Log("[GameManagerWindow] Save Reset Complete");
        }

        EditorGUILayout.EndVertical();
    }

    void DrawPlayerSection()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Player", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서만 체력 회복 버튼이 동작함.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        CombatAgent playerCombat = GetPlayerCombat();
        if (playerCombat == null)
        {
            EditorGUILayout.HelpBox("Player CombatAgent를 찾을 수 없음.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Heal +50"))
        {
            playerCombat.Heal(50f);
            Debug.Log("[GameManagerWindow] Player Heal +50");
        }

        if (GUILayout.Button("Full Heal"))
        {
            playerCombat.FullHeal();
            Debug.Log("[GameManagerWindow] Player Full Heal");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    void DrawAllySpawnSection()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Spawn Allies", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서만 아군 스폰 버튼이 동작함.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        PlayerSquadController squad = GetSquad();
        if (squad == null)
        {
            EditorGUILayout.HelpBox("PlayerSquadController를 찾을 수 없음.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        if (GUILayout.Button("Spawn All Allies Once"))
        {
            foreach (UnitKey key in Enum.GetValues(typeof(UnitKey)))
            {
                if (key == UnitKey.None) continue;
                SpawnAllyByKey(key);
            }
        }

        GUILayout.Space(6);

        int col = 3;
        int index = 0;

        EditorGUILayout.BeginHorizontal();
        foreach (UnitKey key in Enum.GetValues(typeof(UnitKey)))
        {
            if (key == UnitKey.None) continue;

            if (GUILayout.Button(key.ToString(), GUILayout.Height(28)))
            {
                SpawnAllyByKey(key);
            }

            index++;
            if (index % col == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    static PlayerSquadController GetSquad()
    {
        return GameManager.Instance != null ? GameManager.Instance.playerSquad : null;
    }

    static CombatAgent GetPlayerCombat()
    {
        PlayerSquadController squad = GetSquad();
        if (squad == null) return null;

        return squad.GetComponent<CombatAgent>();
    }

    static void SpawnAllyByKey(UnitKey unitKey)
    {
        if (!Application.isPlaying)
            return;

        if (unitKey == UnitKey.None)
            return;

        PlayerSquadController squad = GetSquad();
        if (squad == null)
        {
            Debug.LogWarning("[GameManagerWindow] PlayerSquadController 없음");
            return;
        }

        if (squad.IsFull)
        {
            Debug.LogWarning("[GameManagerWindow] 스쿼드가 가득 참");
            return;
        }

        string poolKey = unitKey.ToString();
        GameObject go = ObjectPool.Instance.Get(poolKey, squad.transform.position, Quaternion.identity);

        if (go == null)
        {
            Debug.LogWarning($"[GameManagerWindow] 풀에서 못 찾음: {poolKey}");
            return;
        }

        UnitStateController usc = go.GetComponent<UnitStateController>();
        if (usc == null)
        {
            Debug.LogWarning($"[GameManagerWindow] UnitStateController 없음: {poolKey}");
            ObjectPool.Instance.Release(go);
            return;
        }

        usc.SpawnAsAlly();

        Debug.Log($"[GameManagerWindow] Spawn Ally : {unitKey}");
    }
}
#endif