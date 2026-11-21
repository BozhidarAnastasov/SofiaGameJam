
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownMovement : MonoBehaviour
{
    [Tooltip("Normal speed. Matches the walk animation's Walk Reference Speed so her feet don't slide.")]
    [SerializeField] private float walkSpeed = 2f;
    [Tooltip("Speed while the run button is held. Matches the Run Reference Speed of the animator.")]
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float crawlSpeed = 0.8f;
    [Tooltip("Stick values below this count as no input.")]
    [SerializeField] private float inputDeadZone = 0.2f;

    [SerializeField] private InputActionReference moveAction;
    [Tooltip("Hold this button to run (Arcade B). Without it she always walks.")]
    [SerializeField] private InputActionReference sprintAction;

    private InputAction crouchAction;
    private Rigidbody2D rb;
    private Vector2 moveInput;

    /// True only while the run button is held AND there is movement input.
    public bool IsRunning { get; private set; }

    public bool IsCrawling { get; private set; }

    /// Freeze movement, e.g. during the pick-up animation or a cutscene.
    public bool CanMove { get; set; } = true;

    /// Slows her down, e.g. while she drags the heavy box (1 = normal).
    public float SpeedMultiplier { get; set; } = 1f;

    /// Turn running off temporarily (while she holds the box).
    public bool AllowRun { get; set; } = true;

    /// Turn crawling off temporarily (while she holds the box, climbs...).
    public bool AllowCrawl { get; set; } = true;

    /// Her normal walking speed.
    public float WalkSpeed => walkSpeed;

    /// How fast she is moving right now (0 when standing still).
    public float CurrentSpeed { get; private set; }

    /// Make her stand up right away.
    public void StandUp(){ IsCrawling = false;}

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (moveAction == null || sprintAction == null)
        {
            Debug.LogError($"{name}: assign Move Action and Sprint Action on TopDownMovement.", this);
            enabled = false;
            return;
        }
        moveAction.action.Enable();
        sprintAction.action.Enable();
        crouchAction = sprintAction.action.actionMap.FindAction("Crouch");
        crouchAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.action.Disable();
        sprintAction?.action.Disable();
        crouchAction?.Disable();
        IsCrawling = false;
        IsRunning = false;
        moveInput = Vector2.zero;
    }

    void Update()
    {
        moveInput = CanMove ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        if (moveInput.magnitude < inputDeadZone) moveInput = Vector2.zero;

        bool hasInput = moveInput != Vector2.zero;
        IsCrawling = CanMove && crouchAction != null && crouchAction.IsPressed();
        IsRunning = hasInput && !IsCrawling && sprintAction.action.IsPressed();
    }

    void FixedUpdate()
    {
        if (moveInput == Vector2.zero) { CurrentSpeed = 0f; return; }

        float speed = IsCrawling ? crawlSpeed : IsRunning ? runSpeed : walkSpeed;
        rb.MovePosition(rb.position + moveInput.normalized * speed * Time.fixedDeltaTime);
    }
}
