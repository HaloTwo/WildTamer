using TMPro;
using UnityEngine;

public class UIManager : Singleton<UIManager>
{

    [SerializeField] HpBarUI hpBarUI;
    [SerializeField] TextMeshProUGUI UnitCountTxt;


    protected override void Awake()
    {
        base.Awake();

    }


    public void SetHpBar(float currentHp, float maxHp)
    {
        hpBarUI.SetHp(currentHp, maxHp);
    }

    public void UnitCountTxtUpdate(int current, int max)
    {
        UnitCountTxt.text = $"{current} / {max}";
    }


}
