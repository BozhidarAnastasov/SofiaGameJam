using UnityEngine;

public class HintAreas : MonoBehaviour
{
    [Header("Which Popup Child Should Appear?")]
    [SerializeField] private HintItemType neededItem = HintItemType.Keycard;

    [Header("Proximity Settings")]
    [Tooltip("How close the player needs to walk before the thought bubble pops up.")]
    [SerializeField] private float triggerRadius = 2.0f;

    [Tooltip("Optional: Drag Player here, or leave empty to auto-find Player automatically.")]
    [SerializeField] private Transform player;

    [Header("State")]
    [Tooltip("Check this or call DisableHintPermanently() once the player unlocks/solves this obstacle.")]
    [SerializeField] private bool hintSolvedOrDisabled = false;

    private bool playerInside;

    private void Start()
    {
        FindPlayerIfMissing();
    }

    private void Update()
    {
        if (hintSolvedOrDisabled)
        {
            if (playerInside) ExitArea();
            return;
        }

        if (player == null)
        {
            FindPlayerIfMissing();
            if (player == null) return;
        }

        float sqrDist = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude;
        bool isNear = sqrDist <= (triggerRadius * triggerRadius);

        if (isNear && !playerInside)
        {
            playerInside = true;
            if (PlayerItemPopupHint.Instance != null)
            {
                PlayerItemPopupHint.Instance.ShowHint(neededItem, this);
            }
        }
        else if (!isNear && playerInside)
        {
            ExitArea();
        }
    }

    private void OnDisable()
    {
        if (playerInside) ExitArea();
    }

    private void ExitArea()
    {
        playerInside = false;
        if (PlayerItemPopupHint.Instance != null)
        {
            PlayerItemPopupHint.Instance.HideHint(this);
        }
    }

    /// Hook this up to a UnityEvent (e.g., when door unlocks, rubble breaks, or item is used) so the '?' stops appearing!
    public void DisableHintPermanently()
    {
        hintSolvedOrDisabled = true;
        if (playerInside) ExitArea();
    }

    private void FindPlayerIfMissing()
    {
        if (player != null) return;

        // Grab player straight from the popup's parent if present
        if (PlayerItemPopupHint.Instance != null && PlayerItemPopupHint.Instance.transform.parent != null)
        {
            player = PlayerItemPopupHint.Instance.transform.parent;
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    // Draws the activation circle in Unity's Scene View so you can size each room trigger visually!
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}