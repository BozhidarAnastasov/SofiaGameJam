using UnityEngine;

/// Put this next to the Animator (on the object with the SpriteRenderer) of the girl or the psycho.
/// It reads how the character actually moves (works for player input and enemy AI alike)
/// and drives the controllers made by Tools > Horror Animation > Setup Everything.
[RequireComponent(typeof(Animator))]
public class DirectionalSpriteAnimator : MonoBehaviour
{
    public enum Locomotion { Idle = 0, Walk = 1, Run = 2, Crawl = 3 }

    [Tooltip("Leave empty to use the Rigidbody2D on this object or a parent.")]
    [SerializeField] private Rigidbody2D body;

    [Tooltip("Player only: run is shown only while its run button is held. Found automatically. " +
             "Without one (the psycho), running is decided by speed instead.")]
    [SerializeField] private TopDownMovement playerMovement;

    [Header("When does it walk / run (world units per second)")]
    [SerializeField] private float moveThreshold = 0.05f;
    [Tooltip("Only used when there is no TopDownMovement.")]
    [SerializeField] private float runThreshold = 3.5f;
    [Tooltip("Stay in walk/run this long after stopping, so a 1-frame physics hiccup never flashes the idle pose.")]
    [SerializeField] private float stopGraceTime = 0.08f;

    [Header("Speed at which each cycle looks right (no foot sliding)")]
    [SerializeField] private float walkReferenceSpeed = 1f;
    [SerializeField] private float runReferenceSpeed = 1.5f;
    [SerializeField] private float crawlReferenceSpeed = 0.8f;
    [SerializeField] private Vector2 animSpeedClamp = new Vector2(0.6f, 1.4f);

    [Tooltip("How much stronger the other axis must be before facing switches (stops up/side flicker on diagonals).")]
    [SerializeField] private float facingHysteresis = 1.25f;

    public bool Crawling { get; set; }

    /// While set, she keeps facing this direction whatever way she moves
    /// (e.g. facing the box while she walks backwards pulling it).
    public Vector2? FacingOverride { get; set; }
    public Vector2 Facing => facing;
    /// -1 = facing left, +1 = facing right (the side views, e.g. crawling).
    public float Side => side;

    static readonly int DirX = Animator.StringToHash("DirX");
    static readonly int DirY = Animator.StringToHash("DirY");
    static readonly int SideX = Animator.StringToHash("SideX");
    static readonly int StateId = Animator.StringToHash("State");
    static readonly int AnimSpeed = Animator.StringToHash("AnimSpeed");
    static readonly int PickUpId = Animator.StringToHash("PickUp");
    static readonly int GrabId = Animator.StringToHash("Grab");

    private Animator animator;
    private Transform groundShadow;
    private Vector3 restingShadowScale;
    [Tooltip("Seconds for the shadow to grow or shrink when entering or leaving a diagonal crawl.")]
    [SerializeField, Min(0f)] private float shadowTransitionTime = 0.15f;
    private float shadowBlend;
    private Vector2 lastPos, velocity, facing = Vector2.down;
    private Vector2 eightWayFacing = Vector2.down;
    private float side = 1f, stoppedFor;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (body == null) body = GetComponentInParent<Rigidbody2D>();
        if (playerMovement == null) playerMovement = GetComponentInParent<TopDownMovement>();
        if (playerMovement != null)
        {
            groundShadow = playerMovement.transform.Find("Shadow");
            if (groundShadow != null) restingShadowScale = groundShadow.localScale;
        }
        lastPos = Position();
    }

    Vector2 Position() => body != null ? body.position : (Vector2)transform.position;

    void FixedUpdate()
    {
        Vector2 p = Position();
        velocity = (p - lastPos) / Time.fixedDeltaTime;
        lastPos = p;
    }

    void Update()
    {
        float speed = velocity.magnitude;
        bool moving = speed > moveThreshold;
        stoppedFor = moving ? 0f : stoppedFor + Time.deltaTime;

        if (FacingOverride.HasValue)
        {
            Vector2 f = FacingOverride.Value;
            facing = Mathf.Abs(f.x) >= Mathf.Abs(f.y) ? new Vector2(Mathf.Sign(f.x), 0f) : new Vector2(0f, Mathf.Sign(f.y));
            if (Mathf.Abs(f.x) > 0.01f) side = Mathf.Sign(f.x);
        }
        else if (moving)
        {
            facing = ToCardinal(velocity);
            if (Mathf.Abs(velocity.x) > moveThreshold) side = Mathf.Sign(velocity.x);
        }

        bool crawling = Crawling || (playerMovement != null && playerMovement.IsCrawling);
        if (FacingOverride.HasValue || moving)
        {
            Vector2 direction = FacingOverride ?? velocity;
            // Round to the nearest 45-degree sector for both keys and analog sticks.
            Vector2 unit = direction.normalized;
            const float boundary = 0.38268343f; // sin(22.5 degrees)
            eightWayFacing = new Vector2(Mathf.Abs(unit.x) > boundary ? Mathf.Sign(unit.x) : 0f,
                                      Mathf.Abs(unit.y) > boundary ? Mathf.Sign(unit.y) : 0f);
        }

        Locomotion state;
        float reference;
        if (crawling) { state = Locomotion.Crawl; reference = crawlReferenceSpeed; }
        else if (!moving && stoppedFor >= stopGraceTime) { state = Locomotion.Idle; reference = 1f; }
        else if (WantsRun(speed)) { state = Locomotion.Run; reference = runReferenceSpeed; }
        else { state = Locomotion.Walk; reference = walkReferenceSpeed; }

        // Cycle speed follows movement speed so feet don't slide. A crawler that stops just freezes.
        float animSpeed = state == Locomotion.Idle ? 1f
            : state == Locomotion.Crawl && !moving ? 0f
            : Mathf.Clamp(speed / reference, animSpeedClamp.x, animSpeedClamp.y);

        bool eightWayMovement = playerMovement != null && (state == Locomotion.Walk || state == Locomotion.Run);
        Vector2 animationFacing = crawling || eightWayMovement ? eightWayFacing : facing;
        SetDiagonalShadow(crawling && eightWayFacing.x != 0f && eightWayFacing.y != 0f);
        animator.SetFloat(DirX, animationFacing.x);
        animator.SetFloat(DirY, animationFacing.y);
        animator.SetFloat(SideX, side);
        animator.SetInteger(StateId, (int)state);
        animator.SetFloat(AnimSpeed, animSpeed);
    }

    void SetDiagonalShadow(bool enlarged)
    {
        float target = enlarged ? 1f : 0f;
        if (groundShadow == null || shadowBlend == target) return;
        shadowBlend = shadowTransitionTime <= 0f ? target
            : Mathf.MoveTowards(shadowBlend, target, Time.deltaTime / shadowTransitionTime);
        float scale = Mathf.SmoothStep(1f, 1.25f, shadowBlend);
        groundShadow.localScale = Vector3.Scale(restingShadowScale, new Vector3(scale, scale, 1f));
    }

    void OnDisable()
    {
        shadowBlend = 0f;
        if (groundShadow != null) groundShadow.localScale = restingShadowScale;
    }

    // Player: run button held + movement input. Others (psycho): fast enough.
    bool WantsRun(float speed) =>
        playerMovement != null ? playerMovement.IsRunning : speed >= runThreshold;

    // Always exactly one of the 4 directions. Keeps the current axis until the other one clearly wins.
    Vector2 ToCardinal(Vector2 v)
    {
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y);
        bool horizontal = facing.x != 0f ? ax * facingHysteresis >= ay : ax > ay * facingHysteresis;
        return horizontal ? new Vector2(Mathf.Sign(v.x), 0f) : new Vector2(0f, Mathf.Sign(v.y));
    }

    /// Turn the character to look somewhere without moving (e.g. psycho turning to the girl).
    public void FaceTowards(Vector2 worldPoint)
    {
        Vector2 d = worldPoint - Position();
        if (d.sqrMagnitude < 0.0001f) return;
        facing = ToCardinal(d);
        eightWayFacing = facing;
        if (Mathf.Abs(d.x) > 0.01f) side = Mathf.Sign(d.x);
    }

    public void PlayPickUp() => animator.SetTrigger(PickUpId);   // girl
    public void PlayGrab() => animator.SetTrigger(GrabId);       // psycho
}
