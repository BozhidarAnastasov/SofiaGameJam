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
    private HashSet<string> collectedItems = new HashSet<string>();
    private bool enabledNextSlotAction;

    private struct ItemIcon
    {
        public string Name;
        public Sprite Sprite;
        public Color Color;
    }

    private static string destinationRoom;
    private static ItemIcon[] savedSlots;
    private static int savedSelection;
    private static HashSet<string> savedCollectedItems;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ClearRoomSave()
    {
        destinationRoom = null;
        savedSlots = null;
        savedCollectedItems = null;
    }

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

        if (savedSlots != null && gameObject.scene.name == destinationRoom)
        {
            for (int i = 0; i < slots.Count && i < savedSlots.Length; i++)
                if (savedSlots[i].Sprite != null) CreateIcon(slots[i], savedSlots[i]);

            SelectSlot(savedSelection);
            collectedItems = savedCollectedItems;
            ClearRoomSave();
            return;
        }

        if (itemPrefabs == null) return;
        for (int i = 0; i < itemPrefabs.Length && i < slotCount; i++)
            if (itemPrefabs[i] != null) AddItem(itemPrefabs[i]);
    }

    internal void SaveForRoom(string roomName)
    {
        destinationRoom = roomName;
        savedSelection = SelectedSlotIndex;
        savedCollectedItems = new HashSet<string>(collectedItems);
        savedSlots = new ItemIcon[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            GameObject item = slots[i].currentItem;
            if (item != null && item.TryGetComponent(out Image icon))
                savedSlots[i] = new ItemIcon { Name = item.name, Sprite = icon.sprite, Color = icon.color };
        }
    }

    internal bool HasCollected(string id) => collectedItems.Contains(id);

    internal void MarkCollected(string id) => collectedItems.Add(id);

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
                CreateIcon(slot, new ItemIcon
                {
                    Name = itemPrefab.name,
                    Sprite = spriteRenderer.sprite,
                    Color = spriteRenderer.color
                });
                return true;
            }
        }
        Debug.Log("HotBar is full.", this);
        return false;
    }

    private static void CreateIcon(Slot slot, ItemIcon item)
    {
        // The hotbar stores icons without copying the world object's physics or scripts.
        var iconObject = new GameObject(item.Name, typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slot.transform, false);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = item.Sprite;
        icon.color = item.Color;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform rect = icon.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 6f);
        rect.offsetMax = new Vector2(-6f, -6f);
        slot.currentItem = iconObject;
    }
}
