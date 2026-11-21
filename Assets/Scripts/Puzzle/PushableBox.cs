using UnityEngine;
using UnityEngine.InputSystem;

/// A heavy box. Stand next to it and HOLD Interact (U / Button A) to grab it; while held, she
/// pushes or pulls it by walking, slower than normal and always facing it. Let go of the button
/// to drop it. Without the button she can't move it at all.
[RequireComponent(typeof(Rigidbody2D))]
public class PushableBox : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("The lever, so pressing U next to it pulls the lever instead of grabbing the box.")]
    [SerializeField] private LeverSwitch lever;

    [Tooltip("How close (from her feet to the box's edge) she must be to grab it.")]
    [SerializeField] private float grabDistance = 0.6f;
    [Tooltip("Her speed while dragging the box (1 = normal).")]
    [SerializeField] private float dragSpeed = 0.55f;
    [Tooltip("If the box gets stuck (wall) this far behind her, she lets go.")]
    [SerializeField] private float stuckDistance = 0.35f;
    [Tooltip("Where her feet go when she climbs on top (relative to the box's pivot).")]
    [SerializeField] private Vector2 standOffset = new Vector2(0.05f, 0.55f);

    private Rigidbody2D rb;
    private Collider2D boxCol;
    private Rigidbody2D playerRb;
    private Collider2D playerCol;
    private TopDownMovement playerMove;
    private DirectionalSpriteAnimator playerAnim;
    private InteractPrompt prompt;
    private Vector2 grabOffset;
    private bool locked;

    public bool IsGrabbed { get; private set; }
    public Vector3 StandPoint => transform.position + (Vector3)standOffset;
    public SpriteRenderer Renderer { get; private set; }
    public Rigidbody2D Body => rb;
    public bool IsMoving => rb.linearVelocity.sqrMagnitude > 0.01f;

    /// True while she stands close enough to grab it (the lever uses this to stay quiet).
    public bool PlayerInGrabRange
    {
        get
        {
            if (player == null || boxCol == null || locked) return false;
            Vector2 feet = player.position;
            return Vector2.Distance(boxCol.ClosestPoint(feet), feet) <= grabDistance;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCol = GetComponent<Collider2D>();
        Renderer = GetComponentInChildren<SpriteRenderer>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.bodyType = RigidbodyType2D.Kinematic;        // can't be shoved by walking into it
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody2D>();
            playerCol = player.GetComponent<Collider2D>();
            playerMove = player.GetComponent<TopDownMovement>();
            playerAnim = player.GetComponentInChildren<DirectionalSpriteAnimator>();
        }
    }

    void OnEnable() { if (interactAction != null) interactAction.action.Enable(); }
    void OnDisable() { Release(); }

    void Update()
    {
        if (player == null || interactAction == null) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);

        bool leverWantsU = lever != null && lever.CanPullNow;
        bool canGrab = !locked && !IsGrabbed && !leverWantsU && (playerMove == null || playerMove.CanMove) && PlayerInGrabRange;
        if (prompt != null) prompt.Request(this, canGrab);

        if (!IsGrabbed && canGrab && interactAction.action.WasPressedThisFrame()) Grab();
        else if (IsGrabbed && !interactAction.action.IsPressed()) Release();
    }

    void FixedUpdate()
    {
        if (!IsGrabbed) return;
        Vector2 playerPos = playerRb != null ? playerRb.position : (Vector2)player.position;
        Vector2 target = playerPos + grabOffset;
        Vector2 gap = target - rb.position;
        if (gap.magnitude > stuckDistance) { Release(); return; }      // hit a wall: let go
        rb.linearVelocity = gap / Time.fixedDeltaTime;
    }

    void Grab()
    {
        IsGrabbed = true;
        grabOffset = rb.position - (playerRb != null ? playerRb.position : (Vector2)player.position);
        rb.bodyType = RigidbodyType2D.Dynamic;          // dynamic so walls stop it
        rb.linearDamping = 0f;
        if (playerCol != null && boxCol != null) Physics2D.IgnoreCollision(playerCol, boxCol, true);
        if (playerMove != null) { playerMove.SpeedMultiplier = dragSpeed; playerMove.AllowRun = false; }
        if (playerAnim != null) playerAnim.FacingOverride = grabOffset;   // keep facing the box
    }

    public void Release()
    {
        if (!IsGrabbed) return;
        IsGrabbed = false;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }
        if (playerCol != null && boxCol != null) Physics2D.IgnoreCollision(playerCol, boxCol, false);
        if (playerMove != null) { playerMove.SpeedMultiplier = 1f; playerMove.AllowRun = true; }
        if (playerAnim != null) playerAnim.FacingOverride = null;
    }

    /// Stop the box for good (she stands on it / the puzzle is solved).
    public void Lock()
    {
        Release();
        locked = true;
        if (prompt != null) prompt.Request(this, false);
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    public void Unlock() => locked = false;
}
