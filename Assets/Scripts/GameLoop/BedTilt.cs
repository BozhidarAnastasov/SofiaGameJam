using UnityEngine;

/// The bed leans slightly towards the way it's being dragged, like a heavy thing on squeaky wheels.
/// Only the picture turns (a child copy of the sprite); the collider stays straight so it never
/// snags on walls. Put it on the same object as the PushableBox.
[RequireComponent(typeof(PushableBox))]
public class BedTilt : MonoBehaviour
{
    [Tooltip("Largest lean in degrees.")]
    [SerializeField] private float maxTilt = 9f;
    [Tooltip("Degrees of lean per unit/second of sideways speed.")]
    [SerializeField] private float tiltPerSpeed = 16f;
    [Tooltip("How fast it leans in / straightens (degrees per second).")]
    [SerializeField] private float turnSpeed = 40f;
    [Tooltip("Straighten up again when she lets go.")]
    [SerializeField] private bool settleWhenReleased = true;

    private PushableBox box;
    private Transform visual;
    private float angle;

    /// The turning picture (other scripts can fade / move it).
    public SpriteRenderer Visual { get; private set; }

    void Awake()
    {
        box = GetComponent<PushableBox>();
        var src = GetComponent<SpriteRenderer>();
        if (src == null) { visual = transform; return; }
        var go = new GameObject("BedVisual");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = src.sprite; sr.color = src.color; sr.flipX = src.flipX; sr.flipY = src.flipY;
        sr.sortingLayerID = src.sortingLayerID; sr.sortingOrder = src.sortingOrder;
        sr.sharedMaterial = src.sharedMaterial; sr.spriteSortPoint = src.spriteSortPoint;
        src.enabled = false;
        visual = go.transform;
        Visual = sr;
    }

    void Update()
    {
        Vector2 v = box.IsGrabbed ? box.Velocity : Vector2.zero;
        float target = angle;
        if (box.IsGrabbed && v.sqrMagnitude > 0.01f)
            target = Mathf.Clamp(-v.x * tiltPerSpeed, -maxTilt, maxTilt);   // dragged right -> leans right
        else if (!box.IsGrabbed && settleWhenReleased)
            target = 0f;
        angle = Mathf.MoveTowards(angle, target, turnSpeed * Time.deltaTime);
        visual.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
