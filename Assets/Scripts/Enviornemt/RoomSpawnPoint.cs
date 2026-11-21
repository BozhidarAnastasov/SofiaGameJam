using UnityEngine;

/// An arrival point in a room. When another room's door loads this scene with the same Entry Id,
/// the girl is placed here (facing Face Direction) instead of the room's default spawn.
public class RoomSpawnPoint : MonoBehaviour
{
    /// Set by the door that is leaving the previous room; consumed by the next room.
    public static string NextEntry;

    [Tooltip("Must match 'Entry Id' on the DoorExit in the room she comes from.")]
    [SerializeField] private string entryId = "FromElectrical";
    [SerializeField] private Vector2 faceDirection = Vector2.right;

    public string EntryId => entryId;

    void Start()
    {
        Debug.Log($"[RoomSpawnPoint] '{name}' (entry '{entryId}') Start: pending entry = '{NextEntry ?? "none"}'.", this);
        if (!string.IsNullOrEmpty(NextEntry) && NextEntry == entryId) PlaceNow();
    }

    /// Put the girl here (called by RoomTransition after the scene loads, or by Start).
    public void PlaceNow()
    {
        NextEntry = null;
        var player = FindFirstObjectByType<TopDownMovement>();
        if (player == null) { Debug.LogWarning($"[RoomSpawnPoint] '{entryId}': no Player (TopDownMovement) in this scene!", this); return; }

        Vector3 before = player.transform.position;
        var rb = player.GetComponent<Rigidbody2D>();
        player.transform.position = new Vector3(transform.position.x, transform.position.y, player.transform.position.z);
        if (rb != null) { rb.position = transform.position; rb.linearVelocity = Vector2.zero; }
        Physics2D.SyncTransforms();
        var anim = player.GetComponentInChildren<DirectionalSpriteAnimator>();
        if (anim != null) anim.FaceTowards((Vector2)transform.position + faceDirection);
        var cam = Camera.main;
        if (cam != null) cam.transform.position = new Vector3(transform.position.x, transform.position.y + 1f, cam.transform.position.z);
        Debug.Log($"[RoomSpawnPoint] Placed her at '{name}' (entry '{entryId}'): {before} -> {player.transform.position}.", this);
    }

    /// Find the arrival point for the pending entry in the loaded scene and use it.
    public static bool PlacePending()
    {
        if (string.IsNullOrEmpty(NextEntry)) { Debug.Log("[RoomSpawnPoint] No pending entry left (already placed by the arrival point, or she didn't come through a door)."); return false; }
        var all = FindObjectsByType<RoomSpawnPoint>(FindObjectsSortMode.None);
        foreach (var p in all)
            if (p.entryId == NextEntry) { p.PlaceNow(); return true; }
        Debug.LogWarning($"[RoomSpawnPoint] No arrival point with entry '{NextEntry}' in this scene. Found: " +
                         (all.Length == 0 ? "none" : string.Join(", ", System.Array.ConvertAll(all, a => a.entryId))) + ". Run Tools > Horror Room > 15.");
        NextEntry = null;
        return false;
    }
}
