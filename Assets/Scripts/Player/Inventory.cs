using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Inventory : MonoBehaviour
{
    public GameObject inventoryPanel;
    public GameObject slotPrefab;
    [Min(0)] public int slotCount;
    public GameObject[] itemPrefabs;
    [SerializeField] private InputActionReference nextSlotAction;

    public int SelectedSlotIndex { get; private set; }
    public Slot SelectedSlot => slots.Count > 0 ? slots[SelectedSlotIndex] : null;

    private readonly List<Slot> slots = new List<Slot>();
    private bool enabledNextSlotAction;

    private void Awake()
    {
        if (inventoryPanel == null || slotPrefab == null || slotPrefab.GetComponent<Slot>() == null)
        {
            Debug.LogError("Assign an inventory panel and a slot prefab with a Slot component.", this);
            return;
        }

        for (int i = 0; i < slotCount; i++)
        {
            Slot slot = Instantiate(slotPrefab, inventoryPanel.transform).GetComponent<Slot>();
            slot.Initialize(this, i);
            slots.Add(slot);
        }
        SelectSlot(0);

        if (itemPrefabs == null) return;
        for (int i = 0; i < itemPrefabs.Length && i < slotCount; i++)
            if (itemPrefabs[i] != null) AddItem(itemPrefabs[i]);
    }

    private void OnEnable()
    {
        if (nextSlotAction == null || nextSlotAction.action == null) return;
        enabledNextSlotAction = !nextSlotAction.action.enabled;
        if (enabledNextSlotAction) nextSlotAction.action.Enable();
    }

    private void OnDisable()
    {
        if (enabledNextSlotAction && nextSlotAction != null && nextSlotAction.action != null)
            nextSlotAction.action.Disable();
    }

    private void Update()
    {
        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (scroll != 0f) SelectSlot(SelectedSlotIndex + (scroll > 0f ? -1 : 1));
        if (nextSlotAction != null && nextSlotAction.action != null
            && nextSlotAction.action.WasPressedThisFrame())
            SelectSlot(SelectedSlotIndex + 1);
    }

    public void SelectSlot(int index)
    {
        if (slots.Count == 0) return;
        SelectedSlotIndex = (index % slots.Count + slots.Count) % slots.Count;
        for (int i = 0; i < slots.Count; i++)
            slots[i].SetSelected(i == SelectedSlotIndex);
    }

    public bool AddItem(GameObject itemPrefab)
    {
        if (inventoryPanel == null || itemPrefab == null) return false;
        SpriteRenderer spriteRenderer = itemPrefab.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null) return false;

        foreach (Transform slotTransform in inventoryPanel.transform)
        {
            Slot slot = slotTransform.GetComponent<Slot>();
            if (slot != null && slot.currentItem == null)
            {
                // Only put an icon in the UI, not a copy of the world object's physics/scripts.
                GameObject newItem = new GameObject(itemPrefab.name, typeof(RectTransform), typeof(Image));
                newItem.transform.SetParent(slotTransform, false);
                Image icon = newItem.GetComponent<Image>();
                icon.sprite = spriteRenderer.sprite;
                icon.color = spriteRenderer.color;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform rect = icon.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(6f, 6f);
                rect.offsetMax = new Vector2(-6f, -6f);
                slot.currentItem = newItem;
                return true;
            }
        }
        Debug.Log("HotBar is full.", this);
        return false;
    }
}
