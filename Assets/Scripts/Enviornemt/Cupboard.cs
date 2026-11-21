using UnityEngine;

public class Cupboard : MonoBehaviour, Interactable
{
    public bool IsOpened { get; private set; }
    public string CupboardID { get; private set; }
    public GameObject itemPrefab;
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
            GameObject droppedItem = Instantiate(itemPrefab, transform.position + Vector3.down, Quaternion.identity);
            BounceEffect bounce = droppedItem.GetComponent<BounceEffect>();
            if (bounce == null) bounce = droppedItem.AddComponent<BounceEffect>();
            bounce.StartBounce();
        }
    }

    public void SetOpened(bool opened)
    {
        IsOpened = opened;
    }
}
