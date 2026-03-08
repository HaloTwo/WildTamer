using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class UserData
{
    public int coin;

    // 저장 시점의 현재 동행 아군 스냅샷
    public List<UnitKey> currentAllies = new();

    // 도감 해금
    public List<UnitKey> unlockedUnits = new();
}

public enum SaveName
{
    MapFogData,
    PlayerData,
}

public class SaveManager
{
    private static readonly Lazy<SaveManager> instance = new(() => new SaveManager());
    public static SaveManager Instance => instance.Value;

    private SaveManager() { }

    private string GetPath(SaveName saveName)
    {
        string fileName = saveName switch
        {
            SaveName.PlayerData => "player.json",
            SaveName.MapFogData => "fog.dat",
            _ => "save.json"
        };

        //Debug.Log($"Save path: {Path.Combine(Application.persistentDataPath, fileName)}");

        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public void SaveJson<T>(SaveName saveName, T data)
    {
        string path = GetPath(saveName);
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
    }

    public T LoadJson<T>(SaveName saveName) where T : new()
    {
        string path = GetPath(saveName);

        if (!File.Exists(path))
            return new T();

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<T>(json);
    }

    public void SaveBytes(SaveName saveName, byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        string path = GetPath(saveName);
        File.WriteAllBytes(path, data);
    }

    public byte[] LoadBytes(SaveName saveName)
    {
        string path = GetPath(saveName);

        if (!File.Exists(path))
            return null;

        return File.ReadAllBytes(path);
    }

    public bool HasSave(SaveName saveName)
    {
        return File.Exists(GetPath(saveName));
    }

    public void DeleteSave(SaveName saveName)
    {
        string path = GetPath(saveName);

        if (File.Exists(path))
            File.Delete(path);
    }
}