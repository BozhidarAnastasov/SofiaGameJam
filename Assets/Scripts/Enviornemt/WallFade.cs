using UnityEngine;

/// Put on the south (front) wall sprite. When the girl's feet are inside one of the fade zones
/// (the floor strip the wall covers), the wall turns see-through so she stays visible.
[RequireComponent(typeof(SpriteRenderer))]
public class WallFade : MonoBehaviour
{
    [SerializeField] private Transform player;
    [Tooltip("World-space areas: while her feet (pivot) are inside one, the wall fades.")]
    [SerializeField] private Rect[] fadeZones = new Rect[0];
    [Range(0f, 1f)]
    [SerializeField] private float fadedAlpha = 0.35f;
    [Tooltip("Alpha change per second.")]
    [SerializeField] private float fadeSpeed = 4f;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (player == null)
        {
            var move = FindFirstObjectByType<TopDownMovement>();
            if (move != null) player = move.transform;
        }
    }

    void Update()
    {
        bool behind = false;
        if (player != null)
        {
            Vector2 feet = player.position;
            foreach (var zone in fadeZones)
                if (zone.Contains(feet)) { behind = true; break; }
        }
        Color c = sr.color;
        c.a = Mathf.MoveTowards(c.a, behind ? fadedAlpha : 1f, fadeSpeed * Time.deltaTime);
        sr.color = c;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.8f);
        foreach (var z in fadeZones)
            Gizmos.DrawWireCube(new Vector3(z.center.x, z.center.y, 0f), new Vector3(z.width, z.height, 0f));
    }
}
