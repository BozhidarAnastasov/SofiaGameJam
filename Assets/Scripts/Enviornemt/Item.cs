using UnityEngine;

public class Item : MonoBehaviour, Interactable
{
    private ItemPickup pickup;

    private void Awake()
    {
        pickup = FindFirstObjectByType<ItemPickup>();
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
