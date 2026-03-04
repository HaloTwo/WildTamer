using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIJoyStick : Singleton<UIJoyStick>, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] RectTransform handle;
    [SerializeField] RectTransform bgGroundRect;

    private float handleRange = 25f;
    public bool isDragging { get; private set; }
    private bool isjoyStickMove = true;

    public Vector2 Dir2D => handle.anchoredPosition.normalized;   

    protected override void Awake()
    {
        base.Awake();
        Hide();                            // 시작은 숨김
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isjoyStickMove) return;

        // 조이스틱을 터치 위치로 옮기고 보여줌
        bgGroundRect.transform.position = eventData.position;
        Show();

        isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isjoyStickMove) return;

        isDragging = true;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            bgGroundRect, eventData.position, eventData.pressEventCamera, out Vector2 localVector))
        {
            handle.localPosition = (localVector.magnitude < handleRange)
                ? localVector
                : localVector.normalized * handleRange;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isjoyStickMove) return;

        isDragging = false;
        handle.anchoredPosition = Vector2.zero;

        Hide();
    }

    public void MoveStick(bool onOff)
    {
        isjoyStickMove = onOff;

        isDragging = false;
        handle.anchoredPosition = Vector2.zero;

        if (onOff) Hide();  // 켜져있어도 평소엔 숨김
        else Hide();
    }

    private void Show()
    {
        if (bgGroundRect != null) bgGroundRect.gameObject.SetActive(true);
        if (handle != null) handle.gameObject.SetActive(true);
    }

    private void Hide()
    {
        if (bgGroundRect != null) bgGroundRect.gameObject.SetActive(false);
        if (handle != null) handle.gameObject.SetActive(false);
    }
}