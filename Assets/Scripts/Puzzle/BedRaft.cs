using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// The hospital bed over the Lobby's hole, used to get across it. Stand next to the bed and press
/// Interact (Arcade A / U): she climbs onto it, pushes off and rolls across the hole to the far edge,
/// then steps off on the other side. The bed stays where it was pushed to; from that side she can
/// ride it back the other way.
public class BedRaft : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;

    [Header("Where the bed can rest (world x of the bed's pivot; y stays as placed)")]
    [SerializeField] private float leftRestX = 9.2f;
    [SerializeField] private float rightRestX = 13.9f;

    [Header("Riding")]
    [Tooltip("How close she must stand to the bed's side to climb on.")]
    [SerializeField] private float reach = 0.9f;
    [Tooltip("Where her feet go on the bed, relative to the bed's pivot (its foot end).")]
    [SerializeField] private Vector2 standOffset = new Vector2(0f, 2.0f);
    [Tooltip("How far beyond the bed's side she steps off.")]
    [SerializeField] private float stepOffGap = 0.55f;
    [SerializeField] private float stepOffY = 0.6f;
    [SerializeField] private float rollSpeed = 2.2f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rollClip;

    private Collider2D bedCol;
    private SpriteRenderer bedSr;
    private InteractPrompt prompt;
    private InputAction fallback;
    private bool busy;

    InputAction Interact => interactAction != null && interactAction.action != null ? interactAction.action : fallback;
    float HalfWidth => bedCol != null ? bedCol.bounds.extents.x : 1.375f;

    void Awake()
    {
        bedCol = GetComponent<Collider2D>();
        bedSr = GetComponent<SpriteRenderer>();
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        Debug.Log($"[BedRaft] Ready at x {transform.position.x:0.##} (rests {leftRestX} / {rightRestX}), player {(player != null ? "found" : "MISSING")}.", this);
    }

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null)
        {
            fallback = new InputAction("BedInteract", InputActionType.Button);
            fallback.AddBinding("<Keyboard>/u"); fallback.AddBinding("<Gamepad>/buttonNorth"); fallback.AddBinding("<Joystick>/trigger");
        }
        Interact.Enable();
    }

    void OnDisable() { if (fallback != null) { fallback.Disable(); fallback.Dispose(); fallback = null; } }

    void Update()
    {
        if (player == null || busy) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);
        var move = player.GetComponent<TopDownMovement>();
        bool canAct = move == null || (move.enabled && move.CanMove);

        // she has to stand beside the bed, on the side it can be pushed away from
        Vector2 feet = player.position;
        bool near = bedCol != null && Vector2.Distance(bedCol.ClosestPoint(feet), feet) <= reach;
        bool fromLeft = feet.x < transform.position.x;
        float target = fromLeft ? rightRestX : leftRestX;
        bool canRide = canAct && near && Mathf.Abs(target - transform.position.x) > 0.2f;
        if (prompt != null) prompt.Request(this, canRide);

        if (canRide && Interact.WasPressedThisFrame())
        {
            Debug.Log($"[BedRaft] A pressed: she gets on from the {(fromLeft ? "left" : "right")}, rolling to x {target}.", this);
            StartCoroutine(Ride(target, fromLeft));
        }
    }

    IEnumerator Ride(float targetX, bool goingRight)
    {
        busy = true;
        if (prompt != null) prompt.Request(this, false);
        var move = player.GetComponent<TopDownMovement>();
        var rb = player.GetComponent<Rigidbody2D>();
        var psr = player.GetComponentInChildren<SpriteRenderer>();
        var anim = player.GetComponentInChildren<DirectionalSpriteAnimator>();
        if (move != null) { move.StandUp(); move.CanMove = false; }
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.simulated = false; }
        int order = psr != null ? psr.sortingOrder : 0;
        if (psr != null && bedSr != null) psr.sortingOrder = bedSr.sortingOrder + 1;

        // climb on
        yield return Hop(player, player.position, transform.position + (Vector3)standOffset, 0.3f, 0.3f, rb);
        if (anim != null) anim.FaceTowards(player.position + (goingRight ? Vector3.right : Vector3.left));
        yield return new WaitForSeconds(0.15f);

        // push off and roll across (slow start, slow stop)
        if (audioSource != null && rollClip != null) audioSource.PlayOneShot(rollClip);
        float startX = transform.position.x, dist = Mathf.Abs(targetX - startX), time = Mathf.Max(0.4f, dist / rollSpeed);
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / time);
            float x = Mathf.Lerp(startX, targetX, k);
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            Vector3 p = transform.position + (Vector3)standOffset + Vector3.up * Mathf.Abs(Mathf.Sin(e * 14f)) * 0.03f;   // rattling
            player.position = p; if (rb != null) rb.position = p;
            yield return null;
        }
        transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
        Physics2D.SyncTransforms();
        Debug.Log($"[BedRaft] The bed stopped at x {targetX:0.##}.", this);

        // step off on the far side
        float offX = targetX + (goingRight ? 1f : -1f) * (HalfWidth + stepOffGap);
        Vector3 off = new Vector3(offX, stepOffY, player.position.z);
        yield return Hop(player, player.position, off, 0.3f, 0.25f, rb);

        if (psr != null) psr.sortingOrder = order;
        if (rb != null) { rb.position = off; rb.simulated = true; }
        if (move != null) move.CanMove = true;
        Debug.Log($"[BedRaft] She stepped off at {off}.", this);
        busy = false;
    }

    static IEnumerator Hop(Transform t, Vector3 a, Vector3 b, float time, float arc, Rigidbody2D rb)
    {
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = e / time;
            Vector3 p = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
            t.position = p; if (rb != null) rb.position = p;
            yield return null;
        }
        t.position = b; if (rb != null) rb.position = b;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 p = transform.position;
        Gizmos.DrawWireSphere(new Vector3(leftRestX, p.y, p.z), 0.25f);
        Gizmos.DrawWireSphere(new Vector3(rightRestX, p.y, p.z), 0.25f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p + (Vector3)standOffset, 0.2f);
    }
}
