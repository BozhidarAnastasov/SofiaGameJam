using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slot : MonoBehaviour, IPointerClickHandler
{
    public GameObject currentItem;

    private Inventory inventory;
    private int index;
    private Outline selectionOutline;

    public void Initialize(Inventory owner, int slotIndex)
    {
        inventory = owner;
        index = slotIndex;
        selectionOutline = gameObject.AddComponent<Outline>();
        selectionOutline.effectColor = new Color(1f, 0.8f, 0.2f);
        selectionOutline.effectDistance = new Vector2(3f, -3f);
        selectionOutline.useGraphicAlpha = false;
    }

    public void SetSelected(bool selected)
    {
        selectionOutline.enabled = selected;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            inventory.SelectSlot(index);
    }
}
