#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class GameManagerWindow : EditorWindow
{
    [MenuItem("Tools/Game Manager")]
    public static void Open()
    {
        GetWindow<GameManagerWindow>("Game Manager");
    }

    void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Game Manager Tools", EditorStyles.boldLabel);

        GUILayout.Space(10);

        if (GUILayout.Button("Reset Save Data"))
        {
            if (EditorUtility.DisplayDialog(
                "Reset Save",
                "세이브 데이터를 삭제하시겠습니까?",
                "Yes",
                "Cancel"))
            {
                SaveManager.Instance.DeleteSave(SaveName.PlayerData);
                SaveManager.Instance.DeleteSave(SaveName.MapFogData);

                Debug.Log("Save Reset Complete");
            }
        }
    }
}
#endif