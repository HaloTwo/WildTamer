using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_Encyclopedia_UnitSlot : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI unitSlotName;
    public TextMeshProUGUI unitSlotType;
    public Button infoBtn;

    private void Awake()
    {
        TryGetComponent(out infoBtn);
    }

    public void SetUnlocked(UnitKey unitKey)
    {
        icon.color = Color.white;
    }

    public void SetLocked(UnitKey unitKey)
    {
        icon.color = Color.black;
    }
}
