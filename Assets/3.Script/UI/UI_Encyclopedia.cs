using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_Encyclopedia : MonoBehaviour
{
    readonly Dictionary<UnitKey, UI_Encyclopedia_UnitSlot> unitSlotDic = new();

    [Header("Ref")]
    [SerializeField] private Button close_btn;
    [SerializeField] private Transform contentTrf;

    [Header("디테일 관련")]
    [SerializeField] Image detailImage;
    [SerializeField] TextMeshProUGUI detailNameTxt;
    [SerializeField] TextMeshProUGUI detailTypeTxt;
    [SerializeField] TextMeshProUGUI detailHealthTxt;
    [SerializeField] TextMeshProUGUI detailAttackTxt;
    [SerializeField] TextMeshProUGUI detailAttackSpeedTxt;
    [SerializeField] TextMeshProUGUI detailAttackRangeTxt;



    private void Awake()
    {
        close_btn.onClick.AddListener(Close);
    }

    private void Start()
    {
        InitSlots();
    }

    private void OnEnable()
    {
        RefreshSlots();
    }

    private void InitSlots()
    {
        foreach (UnitKey key in System.Enum.GetValues(typeof(UnitKey)))
        {
            if (key == UnitKey.None) continue;

            GameObject slotObj = ObjectPool.Instance.Get("UI_Encyclopedia_UnitSlot", parent: contentTrf);

            UI_Encyclopedia_UnitSlot slot = slotObj.GetComponent<UI_Encyclopedia_UnitSlot>();
            unitSlotDic.Add(key, slot);

            slot.icon.sprite = SpritePool.Instance.Get(key.ToString()+"_icon");
            slot.unitSlotType.text = GameManager.Instance.GetUnitData(key).UnitType.ToString();

            slot.infoBtn.onClick.AddListener(() => DetailInformationSet(key));
        }

        RefreshSlots();
    }

    private void Close()
    {
        UIJoyStick.Instance.BlockInput = false;
        ObjectPool.Instance.Release(gameObject);
    }

    public void RefreshSlots()
    {
        foreach (var pair in unitSlotDic)
        {
            UnitKey key = pair.Key;
            UI_Encyclopedia_UnitSlot slot = pair.Value;

            if (GameManager.Instance.UserData.unlockedUnits.Contains(key))
            {
                slot.SetUnlocked(key);      // 해금 처리
            }
            else
            {
                slot.SetLocked(key);        // 미해금 처리
            }

            slot.unitSlotName.text = key.ToString();
            slot.unitSlotType.text = GameManager.Instance.GetUnitData(key).UnitType.ToString();
        }
    }


    private void DetailInformationSet(UnitKey key)
    {
        bool isUnlocked = GameManager.Instance.UserData.unlockedUnits.Contains(key);
        UnitDataSO dataSO = GameManager.Instance.GetUnitData(key);

        detailImage.sprite = SpritePool.Instance.Get(key.ToString());
        detailImage.color = isUnlocked ? Color.white : Color.black;

        // 이름 / 타입은 항상 표시
        detailNameTxt.text = key.ToString();
        detailTypeTxt.text = dataSO.UnitType.ToString();

        if (!isUnlocked)
        {
            detailHealthTxt.text = "HP : ???";
            detailAttackTxt.text = "ATK : ???";
            detailAttackSpeedTxt.text = "ASP : ???";
            detailAttackRangeTxt.text = "RNG : ???";
            return;
        }

        // 해금 상태
        detailHealthTxt.text = $"HP : {dataSO.maxHP}";
        detailAttackTxt.text = $"ATK : {dataSO.attackDamage}";
        detailAttackSpeedTxt.text = $"ASP : {dataSO.attackCooldown}";
        detailAttackRangeTxt.text = $"RNG : {dataSO.attackRange}";
    }
}