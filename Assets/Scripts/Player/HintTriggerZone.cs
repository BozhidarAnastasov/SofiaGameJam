using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class HintTriggerZone : MonoBehaviour
{
    [Header("Bubble Settings")]
    [Tooltip("Which item icon inside ItemHintPopup should appear when entering this area?")]
    [SerializeField] private HintItemType itemToDisplay = HintItemType.Keycard;

    [Header("State")]
    [Tooltip("Check this (or call SolveArea() via UnityEvent) once the player unlocks/completes this obstacle.")]
    [SerializeField] private bool isSolved = false;

    private void Reset()
    {
        // Automatically turns the collider into a Trigger when you attach the script
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(2.5f, 2.5f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isSolved) return;

        if (IsPlayer(other) && PlayerItemHintPopup.Instance != null)
        {
            PlayerItemHintPopup.Instance.ShowHint(itemToDisplay, this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other) && PlayerItemHintPopup.Instance != null)
        {
            PlayerItemHintPopup.Instance.HideHint(this);
        }
    }

    private void OnDisable()
    {
        if (PlayerItemHintPopup.Instance != null)
        {
            PlayerItemHintPopup.Instance.HideHint(this);
        }
    }

    /// Connect this to a door/lever UnityEvent so the bubble stops popping up after the player solves it!
    public void SolveArea()
    {
        isSolved = true;
        if (PlayerItemHintPopup.Instance != null)
        {
            PlayerItemHintPopup.Instance.HideHint(this);
        }
    }

    private bool IsPlayer(Collider2D col)
    {
        return col.CompareTag("Player") || col.transform.root.CompareTag("Player") || col.name.Contains("Player");
    }

    // Draws a visible green zone in Scene View so you can see all your room trigger areas easily
    private void OnDrawGizmos()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = isSolved ? new Color(0.5f, 0.5f, 0.5f, 0.15f) : new Color(0.2f, 0.9f, 0.4f, 0.22f);
        Gizmos.DrawCube(box.offset, box.size);

        Gizmos.color = isSolved ? Color.gray : new Color(0.2f, 0.9f, 0.4f, 0.85f);
        Gizmos.DrawWireCube(box.offset, box.size);
    }
}