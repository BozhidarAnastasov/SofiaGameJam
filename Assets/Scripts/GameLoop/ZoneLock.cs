using ArcadesTemplate.Core;
using UnityEngine;

/// An invisible barrier across a passage that opens once a story flag is set.
/// SurgicalWing: the operating room stays blocked until she has the flashlight ("It's too dark...").
/// Needs a (non-trigger) Collider2D on the same GameObject; it is switched off when the flag is set.
[RequireComponent(typeof(Collider2D))]
public class ZoneLock : MonoBehaviour
{
    [SerializeField] private StoryFlag flag = StoryFlag.HasFlashlight;
    [SerializeField] private string lockedMessage = "It's pitch black in there... I need a light.";
    [Tooltip("Seconds before the message can show again.")]
    [SerializeField] private float messageCooldown = 3f;

    private Collider2D col;
    private float nextMessage;

    void Awake() { col = GetComponent<Collider2D>(); Refresh(); }

    void Update() => Refresh();

    void Refresh()
    {
        bool open = GameProgress.Get(flag);
        if (col.enabled == open)
        {
            col.enabled = !open;
            Debug.Log($"[ZoneLock] '{name}': {(open ? "open" : "blocked")} ({flag}).", this);
        }
    }

    void OnCollisionEnter2D(Collision2D c) => Bump(c.collider);
    void OnCollisionStay2D(Collision2D c) => Bump(c.collider);

    void Bump(Collider2D other)
    {
        if (Time.time < nextMessage || other.GetComponentInParent<TopDownMovement>() == null) return;
        nextMessage = Time.time + messageCooldown;
        Debug.Log($"[ZoneLock] '{name}': she bumped into the lock ({lockedMessage}).", this);
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(lockedMessage);
    }
}
