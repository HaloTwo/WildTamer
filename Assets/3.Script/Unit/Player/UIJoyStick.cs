using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIJoyStick : Singleton<UIJoyStick>
{
    public bool BlockInput { get; set; }


    [SerializeField] RectTransform handle;
    [SerializeField] RectTransform bgGroundRect;
    [SerializeField] float handleRange = 25f;
    
    int activeFingerId = -1;
    public bool isDragging { get; private set; }
    public Vector2 Dir2D => handle.anchoredPosition.normalized;

    static readonly List<RaycastResult> _results = new(5);

    protected override void Awake()
    {
        base.Awake();
        Hide();
    }

    void Update()
    {
        if (BlockInput)
        {
            if (isDragging)
                EndDrag();
            return;
        }

#if UNITY_EDITOR || UNITY_STANDALONE

        // 마우스 테스트
        if (Input.GetMouseButtonDown(0))
        {
            if (IsTouchOverSelectableUI(Input.mousePosition, -1))
                return;

            BeginDragMouse(Input.mousePosition);
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            DragMouse(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }

#else

    // 모바일 터치
    if (Input.touchCount == 0)
    {
        if (isDragging) EndDrag();
        return;
    }

    if (activeFingerId == -1)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase != TouchPhase.Began) continue;

            if (IsTouchOverSelectableUI(t.position, t.fingerId))
                continue;

            BeginDrag(t);
            break;
        }
    }
    else
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.fingerId != activeFingerId) continue;

            if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                Drag(t);

            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                EndDrag();

            break;
        }
    }

#endif
    }

    void BeginDragMouse(Vector2 pos)
    {
        activeFingerId = 0;
        isDragging = true;

        bgGroundRect.position = pos;
        Show();
        handle.anchoredPosition = Vector2.zero;
    }

    void DragMouse(Vector2 pos)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bgGroundRect, pos, null, out Vector2 local))
        {
            handle.localPosition = (local.magnitude < handleRange)
                ? local
                : local.normalized * handleRange;
        }
    }

    void BeginDrag(Touch t)
    {
        activeFingerId = t.fingerId;
        isDragging = true;

        bgGroundRect.position = t.position;
        Show();
        handle.anchoredPosition = Vector2.zero;
    }

    void Drag(Touch t)
    {
        if (!isDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bgGroundRect, t.position, null, out Vector2 local))
        {
            handle.localPosition = (local.magnitude < handleRange)
                ? local
                : local.normalized * handleRange;
        }
    }

    void EndDrag()
    {
        activeFingerId = -1;
        isDragging = false;
        handle.anchoredPosition = Vector2.zero;
        Hide();
    }

    bool IsTouchOverSelectableUI(Vector2 screenPos, int fingerId)
    {
        var es = EventSystem.current;
        if (es == null) return false;

        var ped = new PointerEventData(es)
        {
            position = screenPos,
            pointerId = fingerId
        };

        _results.Clear();
        es.RaycastAll(ped, _results);

        for (int i = 0; i < _results.Count; i++)
        {
            var go = _results[i].gameObject;
            if (go == null) continue;

            // “클릭 가능한 UI” 위면 true
            if (go.GetComponentInParent<Selectable>() != null)
                return true;
        }
        return false;
    }

    void Show()
    {
        bgGroundRect.gameObject.SetActive(true);
        handle.gameObject.SetActive(true);
    }

    void Hide()
    {
        bgGroundRect.gameObject.SetActive(false);
        handle.gameObject.SetActive(false);
    }
}