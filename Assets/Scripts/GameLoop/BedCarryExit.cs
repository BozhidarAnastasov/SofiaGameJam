using UnityEngine;

/// Dr. Mundo's cabinet (Patient Room): drag the bed (hold A) to the exit and she leaves the room
/// together with it. On the second floor the bed comes out first, she is behind it, still holding it.
/// Once the bed is out (StoryFlag.BedTakenOut) it is no longer in this room.
public class BedCarryExit : MonoBehaviour
{
    [SerializeField] private PushableBox bed;
    [SerializeField] private Transform player;
    [Tooltip("The room's exit (its position is what counts).")]
    [SerializeField] private Transform exit;
    [Tooltip("How close to the exit she has to drag it.")]
    [SerializeField] private float reach = 1.3f;
    [SerializeField] private string sceneName = "Floor2";
    [SerializeField] private string entryId = "Floor2FromPatients";

    private bool leaving;

    void Awake()
    {
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        if (bed != null && GameProgress.Get(StoryFlag.BedTakenOut))
        {
            Debug.Log("[BedCarryExit] The bed was already taken out of this room.", this);
            bed.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (leaving || bed == null || !bed.isActiveAndEnabled || !bed.IsGrabbed || player == null || exit == null) return;
        if (RoomTransition.IsBusy) return;
        if (Vector2.Distance(player.position, exit.position) > reach) return;

        leaving = true;
        Debug.Log("[BedCarryExit] She drags the bed out of the room -> " + sceneName, this);
        GameProgress.Set(StoryFlag.BedTakenOut);
        RoomSpawnPoint.NextEntry = entryId;
        RoomTransition.FaintAndLoad(sceneName, player);
    }

    void OnDrawGizmosSelected()
    {
        if (exit == null) return;
        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawWireSphere(exit.position, reach);
    }
}
