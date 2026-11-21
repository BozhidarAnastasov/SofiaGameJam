using UnityEngine;

public class Cupboard : MonoBehaviour, Interactable
{
    public bool IsOpened { get; private set; }
    public string CupboardID { get; private set; }
    public GameObject itemPrefab;
    [SerializeField] private Sprite openedSprite;
    void Start()
    {
        CupboardID ??= GlobalHelper.GenerateUniqueID(gameObject);
    }

    public bool CanInteract()
    {
        return !IsOpened;
    }

    public void Interact()
    {
        OpenChest();
    }

    public void OpenChest()
    {
        if (!CanInteract()) return;
        SetOpened(true);

        if (itemPrefab)
        {
            string pickupId = "Cupboard:" + CupboardID;
            var inventory = FindFirstObjectByType<Inventory>();
            if (inventory != null && inventory.HasCollected(pickupId)) return;

            GameObject droppedItem = Instantiate(itemPrefab, transform.position + Vector3.down, Quaternion.identity);
            if (droppedItem.TryGetComponent(out Item item)) item.PickupId = pickupId;
            BounceEffect bounce = droppedItem.GetComponent<BounceEffect>();
            if (bounce == null) bounce = droppedItem.AddComponent<BounceEffect>();
            bounce.StartBounce();
        }
    }

    public void SetOpened(bool opened)
    {
        IsOpened = opened;
        if (opened && openedSprite != null && TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            spriteRenderer.sprite = openedSprite;
        }
    }
}
