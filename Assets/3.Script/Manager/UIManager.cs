using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : Singleton<UIManager>
{

    [SerializeField] HpBarUI hpBarUI;
    [SerializeField] TextMeshProUGUI UnitCountTxt;
    [SerializeField] TextMeshProUGUI CoinTxt;


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

    public void SetCoinText(int coin)
    {
        CoinTxt.text = coin.ToString();
    }

    public void ShowEncyclopedia()
    {
        GameObject go = ObjectPool.Instance.Get("UI_Encyclopedia", parent: transform);
        SetupUI(go);
    }


    public void ShowGameOverUI()
    {
        GameObject go = ObjectPool.Instance.Get("UI_GameOver", parent: transform);
        SetupUI(go);
    }



    void SetupUI(GameObject uiObj)
    {
        UIJoyStick.Instance.BlockInput = true;

        RectTransform rect = uiObj.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
        rect.anchoredPosition = Vector2.zero;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();
    }

}
