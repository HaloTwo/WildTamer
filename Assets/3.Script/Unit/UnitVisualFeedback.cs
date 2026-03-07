using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitVisualFeedback : MonoBehaviour
{
    [SerializeField] CombatAgent combat;
    [SerializeField] UnitStateController unitState;

    [SerializeField] List<SpriteRenderer> renderers = new();

    [Header("Hit Flash")]
    [SerializeField] float hitFlashDuration = 0.1f;
    [SerializeField] Color hitFlashColor;

    [Header("Fade Out")]
    [SerializeField] float fadeDuration = 0.6f;
    [SerializeField] float fadeDownDistance = 0.15f;
    [SerializeField] float fadeScaleMultiplier = 0.95f;

    readonly Dictionary<SpriteRenderer, Color> baseColors = new();

    Coroutine flashRoutine;
    Coroutine fadeRoutine;

    bool isFading;
    Vector3 originScale;

    void Reset()
    {
        AutoBind();
    }

    void Awake()
    {
        originScale = transform.localScale;
        RefreshBaseColors();
    }

    void OnEnable()
    {
        if (combat != null)
            combat.OnDamaged += HandleDamaged;

        if (unitState != null)
            unitState.OnRequestFadeOutAndRelease += HandleFade;

        CancelAllEffectsAndRestore();
    }

    void OnDisable()
    {
        if (combat != null)
            combat.OnDamaged -= HandleDamaged;

        if (unitState != null)
            unitState.OnRequestFadeOutAndRelease -= HandleFade;

        CancelAllEffectsAndRestore();
    }

    void AutoBind()
    {
        if (combat == null)
            combat = GetComponent<CombatAgent>();

        if (unitState == null)
            unitState = GetComponent<UnitStateController>();

        if (renderers == null)
            renderers = new List<SpriteRenderer>();
        else
            renderers.Clear();

        SpriteRenderer[] found = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < found.Length; i++)
        {
            SpriteRenderer sr = found[i];
            if (sr == null) continue;

            // UI/Canvas 밑 SpriteRenderer 제외하고 싶으면 이 조건 유지
            if (sr.GetComponentInParent<Canvas>() != null)
                continue;

            renderers.Add(sr);
        }
    }

    public void RefreshBaseColors()
    {
        baseColors.Clear();

        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            Color c = sr.color;
            c.a = 1f; // 기본 저장은 항상 불투명 상태 기준
            baseColors[sr] = c;
        }

        RestoreVisuals();
    }

    public void CancelAllEffectsAndRestore()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        isFading = false;
        RestoreVisuals();
    }

    void HandleDamaged(CombatAgent _, float __)
    {
        if (!gameObject.activeInHierarchy || isFading)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(CoFlash());
    }

    void HandleFade()
    {
        if (!gameObject.activeInHierarchy || isFading)
            return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(CoFade());
    }

    IEnumerator CoFlash()
    {
        SetFlashColor(hitFlashColor);

        yield return new WaitForSeconds(hitFlashDuration);

        RestoreColorsOnly();
        flashRoutine = null;
    }

    IEnumerator CoFade()
    {
        isFading = true;

        float t = 0f;
        Vector3 startLocalPos = transform.localPosition;
        Vector3 endLocalPos = startLocalPos + Vector3.down * fadeDownDistance;

        Vector3 startScale = transform.localScale;
        Vector3 endScale = originScale * fadeScaleMultiplier;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float ratio = Mathf.Clamp01(t / fadeDuration);

            SetAllAlpha(Mathf.Lerp(1f, 0f, ratio));
            transform.localPosition = Vector3.Lerp(startLocalPos, endLocalPos, ratio);
            transform.localScale = Vector3.Lerp(startScale, endScale, ratio);

            yield return null;
        }

        SetAllAlpha(0f);

        fadeRoutine = null;
        isFading = false;

        unitState.ReleaseToPoolExternally();
    }

    void SetFlashColor(Color flashColor)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            Color cur = sr.color;
            cur.r = flashColor.r;
            cur.g = flashColor.g;
            cur.b = flashColor.b;
            cur.a = 1f;
            sr.color = cur;
        }
    }

    void SetAllAlpha(float alpha)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    void RestoreColorsOnly()
    {
        foreach (var pair in baseColors)
        {
            if (pair.Key == null) continue;

            Color current = pair.Key.color;
            Color baseColor = pair.Value;

            current.r = baseColor.r;
            current.g = baseColor.g;
            current.b = baseColor.b;
            current.a = 1f;

            pair.Key.color = current;
        }
    }

    void RestoreVisuals()
    {
        foreach (var pair in baseColors)
        {
            if (pair.Key == null) continue;

            Color baseColor = pair.Value;
            baseColor.a = 1f;
            pair.Key.color = baseColor;
        }

        transform.localScale = originScale;
    }
}