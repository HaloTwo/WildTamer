using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    public void ShowEncyclopedia()
    {
        GameObject go = ObjectPool.Instance.Get("UI_Encyclopedia", parent: transform);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
        rect.anchoredPosition = Vector2.zero;

        // UI 레이아웃 강제 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        Canvas.ForceUpdateCanvases();
    }


}
