using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Leave the room through the door: stand near it and press Interact (Arcade A / U).
/// Loads the next scene (the information room). While the door is still shut it just says so.
public class DoorExit : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("How close she has to be to the door.")]
    [SerializeField] private float reachDistance = 1.3f;

    [Header("Next scene")]
    [Tooltip("Exact scene name; it must be added to File > Build Profiles (Build Settings) to load.")]
    [SerializeField] private string sceneName = "Lobby";
    [Tooltip("Fade to black and back between the rooms.")]
    [SerializeField] private bool fainting = true;
    [Tooltip("Which arrival point she uses in the next scene (a RoomSpawnPoint with the same Entry Id).")]
    [SerializeField] private string entryId = "FromElectrical";

    [Header("Door state")]
    [Tooltip("The door's lock needs power: she can only leave after pulling the lever.")]
    [SerializeField] private bool requireLeverPulled = true;
    [SerializeField] private LeverSwitch lever;
    [Tooltip("Optional extra rule: only once the psycho has kicked the door in.")]
    [SerializeField] private bool requireKickedOpen = false;
    [Tooltip("The open doorway sprite (enabled when the psycho kicks the door in).")]
    [SerializeField] private SpriteRenderer doorOpen;
    [SerializeField] private string lockedMessage = "It won't open... the lock has no power.";

    public UnityEvent onExit;

    private TopDownMovement move;
    private InteractPrompt prompt;
    private bool leaving;
    private bool wasInRange, warnedNoAction;

    bool DoorIsOpen =>
        (!requireLeverPulled || LeverSwitch.PowerRestored || (lever != null && lever.IsPulled)) &&
        (!requireKickedOpen || (doorOpen != null && doorOpen.enabled));

    void Awake()
    {
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        Debug.Log($"[DoorExit] '{name}' ready -> '{sceneName}' (entry '{entryId}'), player {(player != null ? "found" : "MISSING")}, " +
                  $"in Build Settings: {Application.CanStreamedLevelBeLoaded(sceneName)}.", this);
        if (player != null) move = player.GetComponent<TopDownMovement>();
        if (lever == null) lever = FindFirstObjectByType<LeverSwitch>();
    }
    // used when no Interact Action is assigned: the same bindings as the arcade's Button A
    private InputAction fallbackAction;
    InputAction Interact => interactAction != null && interactAction.action != null ? interactAction.action : fallbackAction;

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null)
        {
            fallbackAction = new InputAction("DoorInteract", InputActionType.Button);
            fallbackAction.AddBinding("<Keyboard>/u");
            fallbackAction.AddBinding("<Gamepad>/buttonNorth");
            fallbackAction.AddBinding("<Joystick>/trigger");
            Debug.LogWarning($"[DoorExit] '{name}': no Interact Action assigned, using built-in A bindings (U / gamepad Y / joystick trigger).", this);
        }
        Interact.Enable();
    }

    void OnDisable() { if (fallbackAction != null) { fallbackAction.Disable(); fallbackAction.Dispose(); fallbackAction = null; } }

    void Update()
    {
        if (player == null || leaving) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);

        bool canAct = move == null || (move.enabled && move.CanMove);   // not while climbing or caught
        float dist = Vector2.Distance(player.position, transform.position);
        bool inRange = canAct && dist <= reachDistance;
        if (inRange != wasInRange)
        {
            Debug.Log($"[DoorExit] '{name}' -> '{sceneName}': she {(inRange ? "is at the door, press A" : "walked away")} (distance {dist:0.00}, reach {reachDistance}).", this);
            wasInRange = inRange;
        }
        if (prompt != null) prompt.Request(this, inRange);
        if (Interact == null) { if (!warnedNoAction) { Debug.LogWarning($"[DoorExit] '{name}': no Interact input at all!", this); warnedNoAction = true; } return; }
        if (!Interact.WasPressedThisFrame()) return;
        if (!inRange)
        {
            if (dist <= reachDistance * 2.5f)
                Debug.Log($"[DoorExit] '{name}': A pressed but she's too far (distance {dist:0.00} > reach {reachDistance})" + (canAct ? "." : " or can't act right now."), this);
            return;
        }
        Debug.Log($"[DoorExit] '{name}': A pressed at the door.", this);

        if (!DoorIsOpen)
        {
            Debug.Log($"[DoorExit] '{name}': door is still locked - pull the lever first ({lockedMessage})", this);
            if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(lockedMessage);
            return;
        }
        Exit();
    }

    void Exit()
    {
        if (RoomTransition.IsBusy) return;
        leaving = true;
        if (prompt != null) prompt.Request(this, false);
        if (move != null) move.CanMove = false;
        onExit.Invoke();

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            RoomSpawnPoint.NextEntry = entryId;
            Debug.Log($"[DoorExit] She left the room -> loading scene '{sceneName}' (arrives at '{entryId}').", this);
            if (fainting) RoomTransition.FaintAndLoad(sceneName, player);   // fade out, load, fade in
            else
            {
                FindFirstObjectByType<Inventory>()?.SaveForRoom(sceneName);
                SceneManager.LoadScene(sceneName);
            }
        }
        else
        {
            Debug.Log($"[DoorExit] She left the room -> would load scene '{sceneName}', but it isn't in the Build Settings yet " +
                      "(File > Build Profiles > Scenes). Exit works.", this);
            if (move != null) move.CanMove = true;     // let her keep playing while the scene doesn't exist
            leaving = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }
}
