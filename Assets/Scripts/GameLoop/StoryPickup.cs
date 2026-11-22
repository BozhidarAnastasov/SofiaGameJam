using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.InputSystem;

/// A story item lying in a room (the coat, the key card...). Stand next to it and press A to take it.
/// It sets a story flag and puts its icon into the hotbar (the icon is named after this GameObject,
/// e.g. "Card", which is what the office door checks). Once taken it never comes back.
public class StoryPickup : MonoBehaviour, Interactable
{
    [SerializeField] private StoryFlag flag = StoryFlag.HasCard;
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private float reach = 1.1f;
    [SerializeField] private string pickupMessage = "";
    [Tooltip("Also put the icon into the hotbar.")]
    [SerializeField] private bool addToHotbar = true;

    [Header("Glint so players notice it")]
    [SerializeField] private float glintSpeed = 2.5f;
    [SerializeField] private float glintAmount = 0.25f;

    private SpriteRenderer sr;
    private InteractPrompt prompt;
    private InputAction fallback;
    private bool taken;

    InputAction Interact_ => interactAction != null && interactAction.action != null ? interactAction.action : fallback;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        if (GameProgress.Get(flag))
        {
            Debug.Log($"[StoryPickup] '{name}' already taken earlier ({flag}): removing it.", this);
            taken = true; gameObject.SetActive(false); return;
        }
        Debug.Log($"[StoryPickup] '{name}' waiting at {transform.position} ({flag}), player {(player != null ? "found" : "MISSING")}.", this);
    }

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null)
        {
            fallback = new InputAction("StoryPickupInteract", InputActionType.Button);
            fallback.AddBinding("<Keyboard>/u"); fallback.AddBinding("<Gamepad>/buttonNorth"); fallback.AddBinding("<Joystick>/trigger");
        }
        Interact_.Enable();
    }

    void OnDisable()
    {
        if (prompt != null) prompt.Request(this, false);
        if (fallback != null) { fallback.Disable(); fallback.Dispose(); fallback = null; }
    }

    void Update()
    {
        if (taken || player == null) return;
        if (sr != null && glintAmount > 0f)
        {
            float g = 1f + Mathf.Sin(Time.time * glintSpeed) * glintAmount;
            sr.color = new Color(Mathf.Min(1f, g), Mathf.Min(1f, g), Mathf.Min(1f, g), 1f);
        }
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);
        var move = player.GetComponent<TopDownMovement>();
        bool inRange = (move == null || (move.enabled && move.CanMove)) && Vector2.Distance(player.position, transform.position) <= reach;
        if (prompt != null) prompt.Request(this, inRange, Interact_);
        if (inRange && Interact_.WasPressedThisFrame()) Interact();
    }

    // ---- Interactable (team system)
    public bool CanInteract() => !taken && isActiveAndEnabled;

    public void Interact()
    {
        if (!CanInteract()) return;
        taken = true;
        if (prompt != null) prompt.Request(this, false);
        if (sr != null) sr.color = Color.white;               // the hotbar icon copies this color

        if (addToHotbar)
        {
            var inv = FindFirstObjectByType<Inventory>();
            if (inv == null) Debug.LogWarning($"[StoryPickup] No Inventory in this scene: '{name}' only sets {flag}.", this);
            else if (!inv.AddItem(gameObject)) Debug.LogWarning($"[StoryPickup] Hotbar full: '{name}' only sets {flag}.", this);
        }
        GameProgress.Set(flag);
        if (player != null && player.TryGetComponent(out DirectionalSpriteAnimator anim)) anim.PlayPickUp();
        if (!string.IsNullOrEmpty(pickupMessage) && ArcadeMessageService.Instance != null)
            ArcadeMessageService.Instance.ShowMessage(pickupMessage);
        Debug.Log($"[StoryPickup] Picked up '{name}' -> {flag}.", this);
        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, reach);
    }
}
