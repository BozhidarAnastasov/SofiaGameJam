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
    [SerializeField] private string sceneName = "InformationRoom";

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

    bool DoorIsOpen =>
        (!requireLeverPulled || (lever != null && lever.IsPulled)) &&
        (!requireKickedOpen || (doorOpen != null && doorOpen.enabled));

    void Awake()
    {
        if (player != null) move = player.GetComponent<TopDownMovement>();
        if (lever == null) lever = FindFirstObjectByType<LeverSwitch>();
    }
    void OnEnable() { if (interactAction != null) interactAction.action.Enable(); }

    void Update()
    {
        if (player == null || leaving) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);

        bool canAct = move == null || (move.enabled && move.CanMove);   // not while climbing or caught
        bool inRange = canAct && Vector2.Distance(player.position, transform.position) <= reachDistance;
        if (prompt != null) prompt.Request(this, inRange);
        if (!inRange || interactAction == null || !interactAction.action.WasPressedThisFrame()) return;

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
        leaving = true;
        if (prompt != null) prompt.Request(this, false);
        if (move != null) move.CanMove = false;
        onExit.Invoke();

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.Log($"[DoorExit] She left the room -> loading scene '{sceneName}'.", this);
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.Log($"[DoorExit] She left the room -> would load scene '{sceneName}', but it isn't in the Build Settings yet " +
                      "(the information room is still in production). Exit works.", this);
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
