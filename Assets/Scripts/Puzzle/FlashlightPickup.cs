using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.InputSystem;

/// The flashlight lying on the red chair behind the Lobby's information desk.
/// Stand next to it and press Interact (Arcade A / U) to take it; from then on D switches it on/off
/// (in every room). Also works with the team's Interactable / Inventory system: if an Inventory is in
/// the scene, the flashlight's icon goes into the hotbar.
public class FlashlightPickup : MonoBehaviour, Interactable
{
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private float reach = 1.0f;
    [SerializeField] private string pickupMessage = "A flashlight! (D to switch it on/off)";

    [Header("Glint so players notice it")]
    [SerializeField] private float glintSpeed = 2.5f;
    [SerializeField] private float glintAmount = 0.35f;

    private SpriteRenderer sr;
    private InteractPrompt prompt;
    private InputAction fallback;
    private bool taken;

    InputAction Interact_ => interactAction != null && interactAction.action != null ? interactAction.action : fallback;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        if (PlayerFlashlight.HasFlashlight)            // already picked up earlier (came back to the Lobby)
        {
            Debug.Log("[FlashlightPickup] Already taken earlier: removing it from the chair.", this);
            taken = true; gameObject.SetActive(false); return;
        }
        Debug.Log($"[FlashlightPickup] On the chair at {transform.position}, player {(player != null ? "found" : "MISSING")}.", this);
    }

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null)
        {
            fallback = new InputAction("PickupInteract", InputActionType.Button);
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
        if (sr != null)                                  // soft pulse so it catches the eye in the dark
        {
            float g = 1f + Mathf.Sin(Time.time * glintSpeed) * glintAmount;
            sr.color = new Color(Mathf.Min(1f, g), Mathf.Min(1f, g), Mathf.Min(1f, g * 0.95f), 1f);
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

        var flash = player != null ? player.GetComponentInChildren<PlayerFlashlight>() : FindFirstObjectByType<PlayerFlashlight>();
        if (flash != null) flash.Give();
        else Debug.LogWarning("[FlashlightPickup] The player has no PlayerFlashlight component!", this);

        var inventory = FindFirstObjectByType<Inventory>();
        if (inventory != null) inventory.AddItem(gameObject);           // icon in the team's hotbar, if it's in the scene

        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(pickupMessage);
        Debug.Log("[FlashlightPickup] Picked up the flashlight.", this);
        gameObject.SetActive(false);
    }

    void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, reach); }
}
