using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [SerializeField] private Inventory InventoryController;

    private void Awake()
    {
        if (InventoryController == null)
            InventoryController = FindFirstObjectByType<Inventory>();
    }

    public void Pickup(Item item)
    {
        if (item == null || !item.CanInteract() || InventoryController == null) return;
        if (!InventoryController.AddItem(item.gameObject)) return;

        if (TryGetComponent(out DirectionalSpriteAnimator animationDriver))
            animationDriver.PlayPickUp();

        item.gameObject.SetActive(false);
        Destroy(item.gameObject);
    }
}
