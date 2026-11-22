using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [SerializeField] private Inventory InventoryController;

    private void Awake()
    {
        if (InventoryController == null)
            InventoryController = FindFirstObjectByType<Inventory>();
    }

    internal bool HasCollected(Item item) =>
        InventoryController != null && InventoryController.HasCollected(item.PickupId);

    public void Pickup(Item item)
    {
        if (item == null || !item.CanInteract() || InventoryController == null) return;
        if (!InventoryController.AddItem(item.gameObject)) return;
        InventoryController.MarkCollected(item.PickupId);

        // The pick-up sound is an animation event on Girl_PickUp_Left/Right, on the frame her hand
        // reaches the floor. Without that animation, play it straight away.
        if (TryGetComponent(out DirectionalSpriteAnimator animationDriver) && animationDriver.isActiveAndEnabled)
        {
            animationDriver.PlayPickUp();
        }
        else GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.pickUpItem : null, 1f, 0.04f, gameObject);
        item.gameObject.SetActive(false);
        Destroy(item.gameObject);
    }
}
