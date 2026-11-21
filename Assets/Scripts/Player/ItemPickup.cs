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
        if (item == null || !item.isActiveAndEnabled || InventoryController == null) return;
        if (!InventoryController.AddItem(item.gameObject)) return;

        // Hide immediately so another interaction cannot collect it again before Destroy runs.
        item.gameObject.SetActive(false);
        Destroy(item.gameObject);
    }
}
