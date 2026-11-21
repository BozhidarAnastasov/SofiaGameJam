using UnityEngine;

public class Item : MonoBehaviour, Interactable
{
    private ItemPickup pickup;
    internal string PickupId { get; set; }

    private void Awake()
    {
        pickup = FindFirstObjectByType<ItemPickup>();
        PickupId = GlobalHelper.GenerateUniqueID(gameObject);
    }

    private void Start()
    {
        if (pickup != null && pickup.HasCollected(this)) gameObject.SetActive(false);
    }

    public bool CanInteract()
    {
        return isActiveAndEnabled && pickup != null;
    }

    public void Interact()
    {
        if (CanInteract()) pickup.Pickup(this);
    }
}
