using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 6.0f;
    [SerializeField] private float crouchSpeed = 1.8f;
    [SerializeField] private bool toggleCrouch = false; // True = Tap ButtonD to toggle, False = Hold ButtonD

    [Header("Interaction Settings (ButtonA)")]
    [SerializeField] private float interactRadius = 1.2f;
    [SerializeField] private LayerMask interactableLayer;

    [Header("3-Slot Inventory (ButtonE = Left, ButtonF = Right)")]
    [SerializeField] private int totalSlots = 3;
    [SerializeField] private int currentSlot = 0;

    // Public properties & event for InventoryHUD and future Stamina UI
    public bool IsSprinting { get; private set; }
    public bool IsCrouching { get; private set; }
    public int CurrentSlot => currentSlot;
    public event Action<int> OnSlotChanged;

    private Rigidbody2D rb;
    private ArcadeControls controls;
    private Vector2 moveInput;
    private bool sprintHeld;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new ArcadeControls();

        // 1. MOVE: Arcade Stick / WASD / Arrows / Gamepad Stick
        controls.Arcade.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Arcade.Move.canceled += _ => moveInput = Vector2.zero;

        // 2. INTERACT: ButtonA (Arcade Btn 1 / Keyboard 'E' / Gamepad North)
        controls.Arcade.ButtonA.performed += _ => TryInteract();

        // 3. USE ITEM IN HAND: ButtonB (Arcade Btn 2 / Keyboard 'I' / Gamepad East)
        controls.Arcade.ButtonB.performed += _ => UseItemInHand();

        // 4. SPRINT (Hold): ButtonC (Arcade Btn 3 / Keyboard 'O' / Gamepad South)
        controls.Arcade.ButtonC.performed += _ => sprintHeld = true;
        controls.Arcade.ButtonC.canceled += _ => sprintHeld = false;

        // 5. CROUCH: ButtonD (Arcade Btn 4 / Keyboard 'J' / Gamepad West)
        controls.Arcade.ButtonD.performed += _ =>
        {
            if (toggleCrouch)
                IsCrouching = !IsCrouching;
            else
                IsCrouching = true;
        };
        controls.Arcade.ButtonD.canceled += _ =>
        {
            if (!toggleCrouch)
                IsCrouching = false;
        };

        // 6. INVENTORY SWAP LEFT & RIGHT: ButtonE ('K' / LB) & ButtonF ('L' / RB)
        controls.Arcade.ButtonE.performed += _ => ChangeSlot(-1); // Right-to-Left
        controls.Arcade.ButtonF.performed += _ => ChangeSlot(1);  // Left-to-Right
    }

    private void OnEnable()
    {
        controls.Arcade.Enable();
    }

    private void OnDisable()
    {
        controls.Arcade.Disable();
    }

    private void OnDestroy()
    {
        // Required by ArcadeControls (~ArcadeControls assert check) to prevent memory leaks!
        controls.Dispose();
    }

    private void Update()
    {
        // Sprint only activates while holding ButtonC, not crouching, and moving
        IsSprinting = sprintHeld && !IsCrouching && moveInput.sqrMagnitude > 0.01f;
    }

    private void FixedUpdate()
    {
        float activeSpeed = walkSpeed;

        if (IsCrouching)
            activeSpeed = crouchSpeed;
        else if (IsSprinting)
            activeSpeed = sprintSpeed;

        rb.linearVelocity = moveInput.normalized * activeSpeed;
    }

    private void TryInteract()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, interactRadius, interactableLayer);
        if (hit != null)
        {
            Debug.Log($"[ButtonA] Interacted with: {hit.name}");
        }
        else
        {
            Debug.Log("[ButtonA] Nothing in range to interact with.");
        }
    }

    private void UseItemInHand()
    {
        Debug.Log($"[ButtonB] Used item in Slot [{currentSlot + 1}]");
    }

    private void ChangeSlot(int direction)
    {
        currentSlot = (currentSlot + direction + totalSlots) % totalSlots;
        OnSlotChanged?.Invoke(currentSlot);
        Debug.Log($"Swapped Inventory to Slot [{currentSlot + 1}]");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}