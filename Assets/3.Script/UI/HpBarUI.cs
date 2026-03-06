using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HpBarUI : MonoBehaviour
{
    [Header("텍스트")]
    [SerializeField] TextMeshProUGUI _currentHp_txt;
    [SerializeField] TextMeshProUGUI _maxHp_txt;

    [Header("바")]    
    [SerializeField] Image _Hpbar;

    [Header("연출")]   
    [SerializeField] float _fillDuration = 0.15f;

    Tween _hpTween;

    public void SetHp(float currentHp, float maxHp)
    {
        float gauge = 0f;
        if (maxHp > 0f)
            gauge = Mathf.Clamp01(currentHp / maxHp);

        _hpTween?.Kill();

        _hpTween = _Hpbar.DOFillAmount(gauge, _fillDuration);

        if (_currentHp_txt != null)
            _currentHp_txt.text = Mathf.CeilToInt(currentHp).ToString();

        if (_maxHp_txt != null)
            _maxHp_txt.text = Mathf.CeilToInt(maxHp).ToString();
    }


    private void OnDisable()
    {
        _hpTween?.Kill();
    }
}