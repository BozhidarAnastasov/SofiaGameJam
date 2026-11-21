using UnityEngine;

/// Follows the girl around a room that is bigger than the screen and never shows past the room's edges.
/// Built by Tools > Hospital Rooms. "Bounds" is the room's Background sprite.
[RequireComponent(typeof(Camera))]
public class RoomCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("The room's background sprite: the camera stays inside it.")]
    [SerializeField] private SpriteRenderer bounds;
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("Look a bit above her feet so her whole body is centred.")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 1f);

    private Camera cam;
    private Vector3 velocity;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (target == null)
        {
            var player = FindFirstObjectByType<TopDownMovement>();
            if (player != null) target = player.transform;
        }
    }

    void Start()
    {
        if (target != null) transform.position = Clamp(Desired());
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, Clamp(Desired()), ref velocity, smoothTime);
    }

    Vector3 Desired() => new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);

    Vector3 Clamp(Vector3 p)
    {
        if (bounds == null || !cam.orthographic) return p;
        Bounds b = bounds.bounds;
        float h = cam.orthographicSize, w = h * cam.aspect;
        p.x = b.size.x > 2f * w ? Mathf.Clamp(p.x, b.min.x + w, b.max.x - w) : b.center.x;
        p.y = b.size.y > 2f * h ? Mathf.Clamp(p.y, b.min.y + h, b.max.y - h) : b.center.y;
        return p;
    }
}
