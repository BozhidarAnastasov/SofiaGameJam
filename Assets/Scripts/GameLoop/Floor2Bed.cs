using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;

/// Second floor: the bed she dragged out of Dr. Mundo's cabinet. It comes out of the door first,
/// with her behind it (still holding it if she keeps A held). Push it over the hole in the floor:
/// it tips in and crashes down into the Lobby (StoryFlag.BedDropped -> the bed appears across the
/// Lobby's hole, so she can climb over to the info desk).
public class Floor2Bed : MonoBehaviour
{
    [SerializeField] private PushableBox bed;
    [SerializeField] private Transform player;
    [Tooltip("The hole's solid collider: she can't walk in, but the bed may roll over it.")]
    [SerializeField] private Collider2D holeCollider;
    [Tooltip("Centre of the hole.")]
    [SerializeField] private Transform holeCentre;
    [Tooltip("When the bed's middle is inside this ellipse (half-width, half-height) it falls in.")]
    [SerializeField] private Vector2 holeRadii = new Vector2(1.25f, 0.9f);
    [Tooltip("Gap between her feet and the bed when she comes out of the door.")]
    [SerializeField] private float gapToBed = 0.15f;
    [SerializeField] private float fallTime = 0.9f;
    [SerializeField] private string droppedMessage = "It crashed down to the lobby... a way across the hole!";

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip crashClip;

    private Collider2D bedCol;
    private bool falling;

    void Awake()
    {
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        bool here = GameProgress.Get(StoryFlag.BedTakenOut) && !GameProgress.Get(StoryFlag.BedDropped);
        if (bed == null) return;
        bed.gameObject.SetActive(here);
        if (!here) return;
        bedCol = bed.GetComponent<Collider2D>();
        if (holeCollider != null && bedCol != null) Physics2D.IgnoreCollision(holeCollider, bedCol, true);
    }

    IEnumerator Start()
    {
        if (bed == null || !bed.gameObject.activeSelf) yield break;
        // wait until the room transition has put her at the door she came out of
        yield return null; yield return null;
        PlaceInFrontOfHer();
        var a = FindAnyInteractHeld();
        if (a) { bed.ForceGrab(); Debug.Log("[Floor2Bed] She came out still holding the bed.", this); }
        else Debug.Log("[Floor2Bed] The bed is out on the second floor (hold A next to it to move it).", this);
    }

    // the bed goes in front of her, in the direction she faces when she comes out (down, into the hall)
    void PlaceInFrontOfHer()
    {
        if (player == null || bedCol == null) return;
        Vector2 feet = player.position;
        bed.transform.position = new Vector3(feet.x, feet.y, bed.transform.position.z);
        Physics2D.SyncTransforms();
        float top = bedCol.bounds.max.y;
        float shift = top - (feet.y - gapToBed);
        Vector3 p = bed.transform.position - new Vector3(0f, shift, 0f);
        bed.transform.position = p;
        var rb = bed.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = p;
        Physics2D.SyncTransforms();
    }

    static bool FindAnyInteractHeld()
    {
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.uKey.isPressed) return true;
        var g = UnityEngine.InputSystem.Gamepad.current;
        if (g != null && g.buttonNorth.isPressed) return true;
        var j = UnityEngine.InputSystem.Joystick.current;
        if (j != null && j.trigger != null && j.trigger.isPressed) return true;
        return false;
    }

    void Update()
    {
        if (falling || bed == null || !bed.gameObject.activeInHierarchy || bedCol == null || holeCentre == null) return;
        Vector2 c = bedCol.bounds.center;
        Vector2 d = c - (Vector2)holeCentre.position;
        float e = (d.x * d.x) / (holeRadii.x * holeRadii.x) + (d.y * d.y) / (holeRadii.y * holeRadii.y);
        if (e <= 1f) StartCoroutine(Fall());
    }

    IEnumerator Fall()
    {
        falling = true;
        Debug.Log("[Floor2Bed] The bed tips over the edge of the hole...", this);
        bed.Lock();                                            // she lets go, it's not hers any more
        if (bedCol != null) bedCol.enabled = false;
        var tilt = bed.GetComponent<BedTilt>();
        var sr = tilt != null && tilt.Visual != null ? tilt.Visual : bed.GetComponentInChildren<SpriteRenderer>();
        Transform t = bed.transform;
        Vector3 from = t.position, to = holeCentre.position;
        Vector3 s0 = t.localScale;
        Color c0 = sr != null ? sr.color : Color.white;
        float spin = Random.value < 0.5f ? -25f : 25f;
        for (float e = 0f; e < fallTime; e += Time.deltaTime)
        {
            float k = e / fallTime, kk = k * k;                 // speeds up as it drops
            t.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
            t.localScale = s0 * Mathf.Lerp(1f, 0.25f, kk);
            t.rotation = Quaternion.Euler(0f, 0f, spin * kk);
            if (sr != null) sr.color = Color.Lerp(c0, new Color(0.05f, 0.05f, 0.07f, 0f), kk);
            yield return null;
        }
        bed.gameObject.SetActive(false);
        CameraShake.Shake(0.5f, 0.2f);
        if (audioSource != null && crashClip != null) audioSource.PlayOneShot(crashClip);
        GameProgress.Set(StoryFlag.BedDropped);
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(droppedMessage);
        Debug.Log("[Floor2Bed] The bed crashed down into the Lobby (BedDropped).", this);
    }

    void OnDrawGizmosSelected()
    {
        if (holeCentre == null) return;
        Gizmos.color = Color.cyan;
        Vector3 c = holeCentre.position;
        for (int i = 0; i < 32; i++)
        {
            float a0 = i / 32f * Mathf.PI * 2f, a1 = (i + 1) / 32f * Mathf.PI * 2f;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0) * holeRadii.x, Mathf.Sin(a0) * holeRadii.y),
                            c + new Vector3(Mathf.Cos(a1) * holeRadii.x, Mathf.Sin(a1) * holeRadii.y));
        }
    }
}
