using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum UnitState
{
    Default,
    EnemyAlive,
    Corpse,
    AllyAlive
}

public enum UnitType
{
    Normal,
    Elite,
    Boss
}

public enum UnitKey
{
    None = 0,

    Merchant,
    Peasant,
    Thief,
    Priest,
    Knight,
}

public enum Team
{
    Player,
    Ally,
    Enemy
}

public class GameManager : Singleton<GameManager>
{
    [Header("Ref")]
    public PlayerSquadController playerSquad;
    CombatAgent playerCombat;
    [SerializeField] UnitDataSO[] unitDatas;


    UserData userData = new UserData();
    public UserData UserData => userData;


    Dictionary<UnitKey, UnitDataSO> unitDic = new();


    public TextMeshProUGUI fpsText;

    float deltaTime;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        float fps = 1f / deltaTime;
        fpsText.text = "FPS : " + Mathf.Ceil(fps);
    }

    protected override void Awake()
    {
        base.Awake();
        Application.targetFrameRate = 60;

        playerSquad.TryGetComponent(out playerCombat);


        foreach (var data in unitDatas)
        {
            unitDic.Add(data.UnitKey, data);
        }
    }


    private void Start()
    {
        LoadGame();

        playerCombat.PlayerDataSet();
        playerSquad.RestoreSavedAllies(userData.currentAllies);
        UIManager.Instance.SetCoinText(userData.coin);
    }


    public UnitDataSO GetUnitData(UnitKey key)
    {
        unitDic.TryGetValue(key, out UnitDataSO data);
        return data;
    }

    #region Coin

    /// <summary>
    /// 현재 코인 반환
    /// </summary>
    public int GetCoin()
    {
        return userData.coin;
    }

    /// <summary>
    /// 코인 추가
    /// </summary>
    public void AddCoin(int amount)
    {
        if (amount <= 0)
            return;

        userData.coin += amount;

        // 필요하면 UI 갱신
        UIManager.Instance?.SetCoinText(userData.coin);
    }


    /// <summary>
    /// 코인 직접 세팅
    /// </summary>
    public void SetCoin(int amount)
    {
        userData.coin = Mathf.Max(0, amount);

        // 필요하면 UI 갱신
        UIManager.Instance?.SetCoinText(userData.coin);
    }

    #endregion


    #region Unlock

    public void UnlockUnit(UnitKey unitKey)
    {
        if (unitKey == UnitKey.None)
            return;

        if (!userData.unlockedUnits.Contains(unitKey))
            userData.unlockedUnits.Add(unitKey);
    }

    public bool IsUnlocked(UnitKey unitKey)
    {
        return userData.unlockedUnits.Contains(unitKey);
    }

    #endregion


    public void SaveGame()
    {
        // 저장 직전에 현재 아군 목록 스냅샷 생성
        RebuildCurrentAlliesSnapshot();

        SaveManager.Instance.SaveJson(SaveName.PlayerData, userData);
        Debug.Log("Game Saved");
    }

    public void LoadGame()
    {
        userData = SaveManager.Instance.LoadJson<UserData>(SaveName.PlayerData);
    }

    public void ResetGameData()
    {
        SaveManager.Instance.DeleteSave(SaveName.PlayerData);
        SaveManager.Instance.DeleteSave(SaveName.MapFogData);
    }

    void RebuildCurrentAlliesSnapshot()
    {
        userData.currentAllies.Clear();

        for (int i = 0; i < playerSquad.allies.Count; i++)
        {
            AllyBrain ally = playerSquad.allies[i];
            if (ally == null) continue;

            UnitStateController usc = ally.GetComponent<UnitStateController>();
            if (usc == null) continue;

            userData.currentAllies.Add(usc.UnitKey);
        }
    }



    void OnApplicationQuit()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        SaveGame();
#endif
    }

    void OnApplicationPause(bool pause)
    {
#if UNITY_ANDROID || UNITY_IOS
        if (pause)
        {
            SaveGame();
        }
#endif
    }

    void OnApplicationFocus(bool focus)
    {
#if UNITY_ANDROID || UNITY_IOS
        if (!focus)
        {
            SaveGame();
        }
#endif
    }
}
