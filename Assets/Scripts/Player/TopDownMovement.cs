
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    [SerializeField, Min(0.1f)] private float sprintDuration = 3f;
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 3f;
    [SerializeField, Min(0.1f)] private float staminaRecoveryDuration = 4f;

    [SerializeField] private InputActionReference moveAction;
    [Tooltip("Hold this button to run (Arcade B). Without it she always walks.")]
    [SerializeField] private InputActionReference sprintAction;
    [Tooltip("Hold this button to crawl (Arcade C). The Player prefab already has it set to ButtonC.")]
    [SerializeField] private InputActionReference crawlAction;

    private InputAction crouchAction;
    private Rigidbody2D rb;
    private Animator animator;
    private static readonly int PickupState = Animator.StringToHash("Base Layer.PickUp");
    private bool movementAllowed = true;
    private Vector2 moveInput;
    private float stamina = 1f;
    private float staminaRecoveryStartsAt;
    private bool exhausted;
    private RectTransform staminaFill;

    /// True while moving with the run button held and stamina available.
    public bool IsRunning { get; private set; }

    public bool IsCrawling { get; private set; }

    /// Freeze movement, e.g. during the pick-up animation or a cutscene.
    public bool CanMove
    {
        get => movementAllowed && !IsPlayingPickup();
        set => movementAllowed = value;
    }

    private bool IsPlayingPickup()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            return false;
        return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == PickupState ||
               (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == PickupState);
    }

    /// Slows her down, e.g. while she drags the heavy box (1 = normal).
    public float SpeedMultiplier { get; set; } = 1f;

    /// Turn running off temporarily (while she holds the box).
    public bool AllowRun { get; set; } = true;

    /// Turn crawling off temporarily (while she holds the box, climbs...).
    public bool AllowCrawl { get; set; } = true;

    /// Her normal walking speed.
    public float WalkSpeed => walkSpeed * RoomPace.Factor;

    /// How fast she is moving right now (0 when standing still).
    public float CurrentSpeed { get; private set; }

    /// Make her stand up right away.
    public void StandUp(){ IsCrawling = false;}

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
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
        // the crawl button (C); fall back to an action called "Crouch" or "ButtonC" in the same map
        crouchAction = crawlAction != null ? crawlAction.action : null;
        if (crouchAction == null && sprintAction.action.actionMap != null)
            crouchAction = sprintAction.action.actionMap.FindAction("Crouch") ?? sprintAction.action.actionMap.FindAction("ButtonC");
        if (crouchAction == null) Debug.LogWarning($"{name}: no crawl action found, so she can't crawl. Assign Crawl Action on TopDownMovement.", this);
        crouchAction?.Enable();
    }

    void Start()
    {
        var inventory = FindFirstObjectByType<Inventory>();
        var canvas = inventory != null ? inventory.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;

        RectTransform background = CreateBarImage("Stamina", canvas.transform, new Color(0.08f, 0.08f, 0.08f, 0.9f));
        background.anchorMin = background.anchorMax = background.pivot = Vector2.zero;
        background.anchoredPosition = new Vector2(20f, 24f);
        background.sizeDelta = new Vector2(140f, 12f);

        staminaFill = CreateBarImage("Fill", background, new Color(0.4f, 0.8f, 0.6f));
        staminaFill.anchorMin = staminaFill.anchorMax = staminaFill.pivot = Vector2.zero;
        staminaFill.anchoredPosition = new Vector2(2f, 2f);
        staminaFill.sizeDelta = new Vector2(136f, 8f);
    }

    private static RectTransform CreateBarImage(string objectName, Transform parent, Color color)
    {
        var bar = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        var image = bar.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
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
        IsCrawling = CanMove && AllowCrawl && crouchAction != null && crouchAction.IsPressed();
        bool sprintHeld = sprintAction.action.IsPressed();
        if (exhausted && !sprintHeld && stamina >= 0.25f) exhausted = false;

        IsRunning = hasInput && AllowRun && !IsCrawling && sprintHeld && !exhausted;
        if (IsRunning)
        {
            stamina = Mathf.MoveTowards(stamina, 0f, Time.deltaTime / sprintDuration);
            staminaRecoveryStartsAt = Time.time + staminaRecoveryDelay;
            exhausted = stamina == 0f;
        }
        else if (Time.time >= staminaRecoveryStartsAt)
        {
            stamina = Mathf.MoveTowards(stamina, 1f, Time.deltaTime / staminaRecoveryDuration);
        }

        if (staminaFill != null) staminaFill.localScale = new Vector3(stamina, 1f, 1f);
    }

    void FixedUpdate()
    {
        if (!CanMove || moveInput == Vector2.zero) { CurrentSpeed = 0f; return; }

        float speed = (IsCrawling ? crawlSpeed : IsRunning ? runSpeed : walkSpeed) * SpeedMultiplier * RoomPace.Factor;   // RoomPace: rooms built at another scale
        CurrentSpeed = speed;
        rb.MovePosition(rb.position + moveInput.normalized * speed * Time.fixedDeltaTime);
    }
}
