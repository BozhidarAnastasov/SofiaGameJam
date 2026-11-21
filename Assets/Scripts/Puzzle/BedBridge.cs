using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// The hospital bed in the Lobby's hole as a bridge to the information desk side.
/// Up:   at the bed's foot (lobby side) press Interact (Arcade A / U): she hops on, walks up the bed
///       and climbs over the headboard to the information desk side.
/// Back: at the headboard (desk side) press Interact: she kicks the bed's top part so it tips down
///       towards the lobby, jumps on and slides down the slope back to the lobby floor.
///       The bed stays tipped afterwards.
public class BedBridge : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;

    [Header("Where she stands (world positions, set by Tools > Horror Room > 16)")]
    [Tooltip("Lobby side, below the bed's foot.")]
    [SerializeField] private Vector2 footSpot = new Vector2(11.57f, -1.6f);
    [Tooltip("Information desk side, just past the headboard.")]
    [SerializeField] private Vector2 topSpot = new Vector2(11.57f, 3.85f);
    [SerializeField] private float reach = 0.8f;

    [Header("On the bed (y relative to the bed's pivot, which is its foot)")]
    [SerializeField] private float bedFootY = 0.45f;
    [SerializeField] private float bedHeadY = 3.9f;
    [SerializeField] private float walkSpeed = 1.6f;
    [SerializeField] private float slideSpeed = 4f;

    [Header("Tipped bed (after the kick)")]
    [Tooltip("The bed's height shrinks to this as its head tips down into the hole.")]
    [SerializeField] private float tippedScaleY = 0.86f;
    [SerializeField] private Color tippedTint = new Color(0.82f, 0.82f, 0.86f);

    [Header("Poses (her own run frames, set by Tools > Horror Room > 16)")]
    [SerializeField] private Sprite stepUpPose;      // back view, stepping up        (run_up 2)
    [SerializeField] private Sprite climbPoseA;      // back view, left knee high     (run_up 4)
    [SerializeField] private Sprite climbPoseB;      // back view, right knee high    (run_up 9)
    [SerializeField] private Sprite kickWindUpPose;  // front view, knee chambered    (run_down 6)
    [SerializeField] private Sprite kickPose;        // front view, leg thrust out    (run_down 2)
    [SerializeField] private Sprite slidePose;       // front view, braced            (run_down 0)

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip kickClip;
    [SerializeField] private AudioClip creakClip;

    public bool IsTipped { get; private set; }

    private SpriteRenderer bedSr;
    private InteractPrompt prompt;
    private InputAction fallback;
    private bool busy;
    private Vector3 baseScale;

    InputAction Interact => interactAction != null && interactAction.action != null ? interactAction.action : fallback;

    void Awake()
    {
        bedSr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        Debug.Log($"[BedBridge] Ready. Lobby side {footSpot}, desk side {topSpot}, player {(player != null ? "found" : "MISSING")}.", this);
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
        Vector2 feet = player.position;
        bool atFoot = canAct && Vector2.Distance(feet, footSpot) <= reach;
        bool atTop = canAct && Vector2.Distance(feet, topSpot) <= reach;
        if (prompt != null) prompt.Request(this, atFoot || atTop);
        if (!(atFoot || atTop) || !Interact.WasPressedThisFrame()) return;

        Debug.Log($"[BedBridge] A pressed on the {(atFoot ? "lobby side: climbing up" : "desk side: " + (IsTipped ? "sliding down" : "kicking the bed"))}.", this);
        StartCoroutine(atFoot ? GoUp() : GoDown());
    }

    // ---------------------------------------------------------------- up: hop on, walk, climb over the headboard
    IEnumerator GoUp()
    {
        var c = Begin();
        Pose(c, stepUpPose);
        yield return Hop(Bed(bedFootY), 0.28f, 0.3f, true);
        Unpose(c);                                                   // normal walk cycle up the bed
        yield return Walk(Bed(bedHeadY - 0.5f), walkSpeed, Vector2.up);
        // climb over the headboard: grab, pull up one knee, then the other, swing over, land
        Pose(c, climbPoseA);
        yield return Squash(1f, 1f, 1.06f, 0.9f, 0.14f);
        yield return Nudge(c, new Vector2(0f, 0.12f), 0.14f);
        Pose(c, climbPoseB);
        yield return Squash(1.06f, 0.9f, 0.96f, 1.06f, 0.12f);
        yield return Nudge(c, new Vector2(0f, 0.1f), 0.12f);
        Pose(c, climbPoseA);
        yield return Hop(topSpot, 0.34f, 0.45f, false, climbPoseB, climbPoseA);
        Unpose(c);
        yield return Squash(1.06f, 0.92f, 1f, 1f, 0.12f);
        End(c, topSpot);
        Debug.Log($"[BedBridge] She climbed over the headboard to the desk side at {topSpot}.", this);
    }

    // ---------------------------------------------------------------- back: kick the top so it tips, slide down
    IEnumerator GoDown()
    {
        var c = Begin();
        if (c.anim != null) c.anim.FaceTowards((Vector2)player.position + Vector2.down);
        if (!IsTipped)
        {
            // wind up (knee chambered) and kick the headboard (leg thrust out, little lunge)
            Pose(c, kickWindUpPose);
            yield return Squash(1f, 1f, 0.95f, 1.05f, 0.2f);
            yield return Nudge(c, new Vector2(0f, 0.06f), 0.08f);
            Pose(c, kickPose);
            if (audioSource != null && kickClip != null) audioSource.PlayOneShot(kickClip);
            StartCoroutine(Shake(transform, 0.25f, 0.05f));
            yield return Nudge(c, new Vector2(0f, -0.2f), 0.06f);
            yield return Squash(1.06f, 0.94f, 1f, 1f, 0.12f);
            yield return new WaitForSeconds(0.12f);
            Pose(c, kickWindUpPose);
            yield return Nudge(c, new Vector2(0f, 0.14f), 0.12f);    // pull the leg back
            // the head end tips down into the hole: the bed looks shorter and darker, sloping to the lobby
            if (audioSource != null && creakClip != null) audioSource.PlayOneShot(creakClip);
            Color c0 = bedSr != null ? bedSr.color : Color.white;
            for (float e = 0f; e < 0.45f; e += Time.deltaTime)
            {
                float k = e / 0.45f; k = k * k;                      // falls faster as it goes
                transform.localScale = new Vector3(baseScale.x, baseScale.y * Mathf.Lerp(1f, tippedScaleY, k), baseScale.z);
                if (bedSr != null) bedSr.color = Color.Lerp(c0, tippedTint, k);
                yield return null;
            }
            transform.localScale = new Vector3(baseScale.x, baseScale.y * tippedScaleY, baseScale.z);
            StartCoroutine(Shake(transform, 0.15f, 0.03f));
            IsTipped = true;
            Physics2D.SyncTransforms();
            Debug.Log("[BedBridge] The bed tipped towards the lobby.", this);
            yield return new WaitForSeconds(0.2f);
        }
        // jump onto the slope, slide down braced, hop off onto the lobby floor
        Pose(c, slidePose);
        yield return Squash(1f, 1f, 1.06f, 0.9f, 0.1f);
        yield return Hop(Bed(HeadY() - 0.45f), 0.3f, 0.35f, true);
        yield return Squash(1.06f, 0.9f, 1f, 1f, 0.08f);
        yield return Slide(Bed(bedFootY), slideSpeed);
        yield return Hop(footSpot, 0.28f, 0.25f, false);
        Unpose(c);
        yield return Squash(1.06f, 0.92f, 1f, 1f, 0.12f);
        End(c, footSpot);
        Debug.Log($"[BedBridge] She slid down and is back in the lobby at {footSpot}.", this);
    }

    // ---------------------------------------------------------------- helpers
    float HeadY() => bedHeadY * (transform.localScale.y / baseScale.y);
    Vector2 Bed(float y) => new Vector2(transform.position.x, transform.position.y + y * (transform.localScale.y / baseScale.y));

    class Ctx { public TopDownMovement move; public Rigidbody2D rb; public SpriteRenderer sr; public DirectionalSpriteAnimator anim; public Animator animator; public int order; public Vector3 scale; }

    // show one of her frames (the Animator would overwrite it every frame, so it pauses meanwhile)
    void Pose(Ctx c, Sprite pose)
    {
        if (pose == null || c.sr == null) return;
        if (c.animator != null) c.animator.enabled = false;
        c.sr.sprite = pose;
    }

    void Unpose(Ctx c) { if (c.animator != null) c.animator.enabled = true; }

    // small body movement (a lunge / pull up) and back
    IEnumerator Nudge(Ctx c, Vector2 offset, float time)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        Vector3 a = player.position, b = a + (Vector3)offset;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            Vector3 p = Vector3.Lerp(a, b, Mathf.Sin(e / time * Mathf.PI));
            player.position = p; if (rb != null) rb.position = p;
            yield return null;
        }
        player.position = a; if (rb != null) rb.position = a;
    }

    // slide in the current pose (no walk cycle), slightly wobbling
    IEnumerator Slide(Vector2 to, float speed)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        float t = 0f;
        while (((Vector2)player.position - to).sqrMagnitude > 0.0004f)
        {
            t += Time.deltaTime;
            Vector3 p = Vector2.MoveTowards(player.position, to, speed * Time.deltaTime);
            p.z = player.position.z;
            player.position = p; if (rb != null) rb.position = p;
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 30f) * 3f);
            yield return null;
        }
        var s2 = player.GetComponentInChildren<SpriteRenderer>();
        if (s2 != null) s2.transform.localRotation = Quaternion.identity;
    }

    Ctx Begin()
    {
        busy = true;
        if (prompt != null) prompt.Request(this, false);
        var c = new Ctx
        {
            move = player.GetComponent<TopDownMovement>(), rb = player.GetComponent<Rigidbody2D>(),
            sr = player.GetComponentInChildren<SpriteRenderer>(), anim = player.GetComponentInChildren<DirectionalSpriteAnimator>(),
            animator = player.GetComponentInChildren<Animator>()
        };
        if (c.move != null) { c.move.StandUp(); c.move.CanMove = false; }
        if (c.rb != null) { c.rb.linearVelocity = Vector2.zero; c.rb.simulated = false; }
        if (c.sr != null) { c.order = c.sr.sortingOrder; c.scale = c.sr.transform.localScale; playerScale = c.scale; }
        return c;
    }

    void End(Ctx c, Vector2 at)
    {
        Vector3 p = new Vector3(at.x, at.y, player.position.z);
        player.position = p;
        if (c.sr != null) { c.sr.sortingOrder = c.order; c.sr.transform.localScale = c.scale; c.sr.transform.localRotation = Quaternion.identity; }
        if (c.animator != null) c.animator.enabled = true;
        if (c.rb != null) { c.rb.position = at; c.rb.simulated = true; }
        Physics2D.SyncTransforms();
        if (c.move != null) c.move.CanMove = true;
        busy = false;
    }

    // hop with an arc; drawn above the bed while on it
    IEnumerator Hop(Vector2 to, float time, float arc, bool onBed, Sprite poseRising = null, Sprite poseFalling = null)
    {
        var sr = player.GetComponentInChildren<SpriteRenderer>();
        var rb = player.GetComponent<Rigidbody2D>();
        if (onBed && sr != null && bedSr != null) sr.sortingOrder = bedSr.sortingOrder + 1;
        Vector3 a = player.position, b = new Vector3(to.x, to.y, a.z);
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = e / time;
            Vector3 p = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
            player.position = p; if (rb != null) rb.position = p;
            if (sr != null && poseRising != null) sr.sprite = k < 0.5f ? poseRising : (poseFalling != null ? poseFalling : poseRising);   // swing legs over
            yield return null;
        }
        player.position = b; if (rb != null) rb.position = b;
    }

    IEnumerator Walk(Vector2 to, float speed, Vector2 face)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        var anim = player.GetComponentInChildren<DirectionalSpriteAnimator>();
        if (anim != null) anim.FacingOverride = face;
        while (((Vector2)player.position - to).sqrMagnitude > 0.0004f)
        {
            Vector3 p = Vector2.MoveTowards(player.position, to, speed * Time.deltaTime);
            p.z = player.position.z;
            player.position = p; if (rb != null) rb.position = p;
            yield return null;
        }
        if (anim != null) anim.FacingOverride = null;
    }

    private Vector3 playerScale = Vector3.one;     // her normal scale, captured in Begin()

    IEnumerator Squash(float x0, float y0, float x1, float y1, float time)
    {
        var sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) yield break;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / time);
            sr.transform.localScale = new Vector3(playerScale.x * Mathf.Lerp(x0, x1, k), playerScale.y * Mathf.Lerp(y0, y1, k), playerScale.z);
            yield return null;
        }
        sr.transform.localScale = new Vector3(playerScale.x * x1, playerScale.y * y1, playerScale.z);
    }

    static IEnumerator Shake(Transform t, float time, float amount)
    {
        Vector3 home = t.position;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            t.position = home + (Vector3)(Random.insideUnitCircle * amount * (1f - e / time));
            yield return null;
        }
        t.position = home;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green; Gizmos.DrawWireSphere(footSpot, reach);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(topSpot, reach);
    }
}
