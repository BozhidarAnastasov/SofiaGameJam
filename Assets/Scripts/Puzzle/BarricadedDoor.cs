using System.Collections;
using System.Collections.Generic;
using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.InputSystem;

/// Planks nailed over a door. Press Interact (Arcade A / U) next to it with the crowbar selected in the
/// hotbar to pry them off; then the door (DoorExit on the same object) works. Without the crowbar she
/// says it's boarded up. Removed planks stay removed when she comes back to the room.
public class BarricadedDoor : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private SpriteRenderer planks;
    [Tooltip("The door that works once the planks are off.")]
    [SerializeField] private DoorExit door;
    [SerializeField] private float reach = 1.3f;
    [Tooltip("The hotbar item that pries the planks off (matched by name).")]
    [SerializeField] private string toolName = "Crowbar";
    [SerializeField] private string boardedMessage = "It's boarded up... I need something to pry the planks off.";
    [SerializeField] private string wrongItemMessage = "That won't work. Maybe the crowbar...";

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pryClip;

    static readonly HashSet<string> removed = new HashSet<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() => removed.Clear();

    string Id => gameObject.scene.name + "/" + name;

    private InteractPrompt prompt;
    private InputAction fallback;
    private bool open, busy;

    InputAction Interact => interactAction != null && interactAction.action != null ? interactAction.action : fallback;

    void Awake()
    {
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        if (door == null) door = GetComponent<DoorExit>();
        open = removed.Contains(Id);
        if (planks != null) planks.enabled = !open;
        if (door != null) door.enabled = open;
        Debug.Log($"[BarricadedDoor] '{name}': {(open ? "planks already removed, door works" : "boarded up, needs the " + toolName)}.", this);
    }

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null)
        {
            fallback = new InputAction("BarricadeInteract", InputActionType.Button);
            fallback.AddBinding("<Keyboard>/u"); fallback.AddBinding("<Gamepad>/buttonNorth"); fallback.AddBinding("<Joystick>/trigger");
        }
        Interact.Enable();
    }

    void OnDisable() { if (fallback != null) { fallback.Disable(); fallback.Dispose(); fallback = null; } }

    void Update()
    {
        if (open || busy || player == null) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);
        var move = player.GetComponent<TopDownMovement>();
        bool inRange = (move == null || (move.enabled && move.CanMove)) && Vector2.Distance(player.position, transform.position) <= reach;
        if (prompt != null) prompt.Request(this, inRange);
        if (!inRange || !Interact.WasPressedThisFrame()) return;

        string held = SelectedItemName();
        Debug.Log($"[BarricadedDoor] A pressed at '{name}', selected hotbar item: '{held ?? "none"}'.", this);
        if (held != null && held.ToLowerInvariant().Contains(toolName.ToLowerInvariant())) StartCoroutine(PryOff());
        else Say(held == null ? boardedMessage : wrongItemMessage);
    }

    // the team's Inventory names each hotbar icon after the item it came from (e.g. "Crowbar")
    static string SelectedItemName()
    {
        var inv = FindFirstObjectByType<Inventory>();
        var slot = inv != null ? inv.SelectedSlot : null;
        return slot != null && slot.currentItem != null ? slot.currentItem.name : null;
    }

    void Say(string msg)
    {
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(msg);
        Debug.Log("[BarricadedDoor] " + msg, this);
    }

    IEnumerator PryOff()
    {
        busy = true;
        if (prompt != null) prompt.Request(this, false);
        var move = player.GetComponent<TopDownMovement>();
        if (move != null) move.CanMove = false;
        if (audioSource != null && pryClip != null) audioSource.PlayOneShot(pryClip);

        if (planks != null)
        {
            Transform t = planks.transform; Vector3 home = t.localPosition; Quaternion rot = t.localRotation;
            for (int i = 0; i < 3; i++)                          // three hard tugs: the planks creak and jolt
            {
                for (float e = 0f; e < 0.18f; e += Time.deltaTime)
                {
                    t.localPosition = home + (Vector3)(Random.insideUnitCircle * 0.04f);
                    yield return null;
                }
                t.localPosition = home; yield return new WaitForSeconds(0.12f);
            }
            for (float e = 0f; e < 0.45f; e += Time.deltaTime)  // they come off and drop to the floor
            {
                float k = e / 0.45f;
                t.localPosition = home + new Vector3(0.35f * k, -0.9f * k * k, 0f);
                t.localRotation = rot * Quaternion.Euler(0f, 0f, -35f * k);
                var c = planks.color; c.a = 1f - k * k; planks.color = c;
                yield return null;
            }
            planks.enabled = false;
            t.localPosition = home; t.localRotation = rot;
        }
        open = true;
        removed.Add(Id);
        if (door != null) door.enabled = true;
        if (move != null) move.CanMove = true;
        Debug.Log($"[BarricadedDoor] Planks pried off '{name}': the door works now.", this);
        busy = false;
    }

    void OnDrawGizmosSelected() { Gizmos.color = new Color(0.7f, 0.45f, 0.2f); Gizmos.DrawWireSphere(transform.position, reach); }
}
