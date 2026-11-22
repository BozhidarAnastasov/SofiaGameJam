using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;

/// Dr. Mundo's cabinet: the first time she starts dragging the bed, the noise brings him.
/// His shadow creeps in through the doorway, a second later he is standing in the door. He walks
/// around the bed (3 s) without touching it, stands still looking around (3 s) and leaves again.
/// His vision cone is on the whole time: keep out of it (or hide under the coat) - if he sees her,
/// he chases her; when he loses her he carries on with his round.
public class PatientRoomVisit : MonoBehaviour
{
    [SerializeField] private EntityAI psycho;
    [SerializeField] private PushableBox bed;
    [Tooltip("Where he comes in and leaves (just inside the doorway).")]
    [SerializeField] private Transform door;
    [Tooltip("The floor sprite - his shadow is drawn on it.")]
    [SerializeField] private SpriteRenderer floor;
    [Tooltip("Walkable area (world): his round around the bed stays inside it.")]
    [SerializeField] private Rect floorArea = new Rect(-5.9f, -4.8f, 11.8f, 6.1f);

    [Header("Timing")]
    [Tooltip("Seconds after she starts dragging the bed before his shadow appears.")]
    [SerializeField] private float startDelay = 4f;
    [SerializeField] private float shadowTime = 1f;
    [SerializeField] private float roundTime = 3f;
    [SerializeField] private float idleTime = 3f;
    [Tooltip("How far from the bed's edge he walks.")]
    [SerializeField] private float roundDistance = 0.9f;
    [SerializeField] private string message = "Footsteps outside... he heard the bed!";

    [Header("Shadow")]
    [SerializeField] private Vector2 shadowSize = new Vector2(1.1f, 3.2f);
    [SerializeField, Range(0f, 1f)] private float shadowDarkness = 0.75f;

    private bool started, finished;
    private SpriteRenderer shadow;

    void Start()
    {
        if (GameProgress.Get(StoryFlag.CabinetVisitDone) || GameProgress.Get(StoryFlag.BedTakenOut)) enabled = false;
    }

    void Update()
    {
        if (started || bed == null || psycho == null || !bed.IsGrabbed || RoomTransition.IsBusy) return;
        started = true;
        StartCoroutine(Visit());
    }

    IEnumerator Visit()
    {
        Debug.Log($"[PatientRoomVisit] She moved the bed - he's coming in {startDelay} s.", this);
        yield return new WaitForSeconds(startDelay);
        if (RoomTransition.IsBusy) yield break;               // she's already leaving
        if (ArcadeMessageService.Instance != null && !string.IsNullOrEmpty(message)) ArcadeMessageService.Instance.ShowMessage(message);

        // 1. his shadow grows in through the doorway
        shadow = MakeShadow();
        for (float e = 0f; e < shadowTime; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / shadowTime);
            shadow.transform.localScale = new Vector3(shadowSize.x * Mathf.Lerp(0.6f, 1f, k), shadowSize.y * k, 1f);
            shadow.color = new Color(0f, 0f, 0f, shadowDarkness * k);
            yield return null;
        }

        // 2. he's in the door
        Vector2 doorPos = door.position;
        psycho.ScriptAppear(doorPos, BedCentre() - doorPos);
        StartCoroutine(FadeShadow(0.5f));
        Debug.Log("[PatientRoomVisit] He is in the doorway.", this);

        // 3. a round around the bed (he doesn't touch it)
        float until = Time.time + roundTime;
        var round = RoundPoints();
        int i = NearestIndex(round, doorPos);
        while (Time.time < until)
        {
            psycho.ScriptWalkTo(round[i]);
            while (!psycho.ScriptArrived && Time.time < until || psycho.Hunting)
            {
                if (psycho.Current == EntityAI.State.Grabbing) yield break;
                if (psycho.Hunting) until += Time.deltaTime;   // the round pauses while he's after her
                yield return null;
            }
            i = (i + 1) % round.Length;
        }

        // 4. stands still, looking around
        psycho.ScriptWalkTo(psycho.transform.position);
        for (float e = 0f; e < idleTime || psycho.Hunting; e += Time.deltaTime)
        {
            if (psycho.Current == EntityAI.State.Grabbing) yield break;
            yield return null;
        }

        // 5. leaves through the door
        psycho.ScriptWalkTo(doorPos);
        while (!psycho.ScriptArrived || psycho.Hunting)
        {
            if (psycho.Current == EntityAI.State.Grabbing) yield break;
            yield return null;
        }
        psycho.ScriptVanish();
        Finish();
        Debug.Log("[PatientRoomVisit] He left the cabinet.", this);
    }

    Vector2 BedCentre()
    {
        var c = bed != null ? bed.GetComponent<Collider2D>() : null;
        return c != null ? (Vector2)c.bounds.center : (Vector2)bed.transform.position;
    }

    // corners of a loop around wherever the bed is now, kept on the floor
    Vector2[] RoundPoints()
    {
        var c = bed.GetComponent<Collider2D>();
        Bounds b = c != null ? c.bounds : new Bounds(bed.transform.position, Vector3.one * 2f);
        float d = roundDistance;
        Vector2[] p =
        {
            new Vector2(b.max.x + d, b.min.y - d),
            new Vector2(b.max.x + d, b.center.y),
            new Vector2(b.max.x + d, b.max.y - 0.2f),
            new Vector2(b.max.x + d, b.center.y),
            new Vector2(b.max.x + d, b.min.y - d),
            new Vector2(b.center.x, b.min.y - d),
            new Vector2(b.min.x - d, b.min.y - d),
            new Vector2(b.center.x, b.min.y - d),
        };
        for (int k = 0; k < p.Length; k++)
            p[k] = new Vector2(Mathf.Clamp(p[k].x, floorArea.xMin, floorArea.xMax), Mathf.Clamp(p[k].y, floorArea.yMin, floorArea.yMax));
        return p;
    }

    static int NearestIndex(Vector2[] pts, Vector2 from)
    {
        int best = 0; float bd = float.MaxValue;
        for (int k = 0; k < pts.Length; k++) { float d = (pts[k] - from).sqrMagnitude; if (d < bd) { bd = d; best = k; } }
        return best;
    }

    SpriteRenderer MakeShadow()
    {
        const int W = 32, H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float v = y / (float)(H - 1);                                  // 0 at the door -> 1 into the room
                float half = Mathf.Lerp(0.45f, 0.28f, v) * (1f - 0.35f * Mathf.Pow(v, 6f));   // a figure stretched by light behind it
                float dx = Mathf.Abs((x + 0.5f) / W - 0.5f);
                float a = (1f - Mathf.SmoothStep(half * 0.6f, half, dx)) * (1f - Mathf.SmoothStep(0.8f, 1f, v));
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        var go = new GameObject("PsychoShadow");
        go.transform.SetParent(transform, false);
        go.transform.position = door.position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), H);   // 1 unit tall, grows up into the room
        sr.color = new Color(0f, 0f, 0f, 0f);
        if (floor != null) { sr.sortingLayerID = floor.sortingLayerID; sr.sortingOrder = floor.sortingOrder + 1; sr.sharedMaterial = floor.sharedMaterial; }
        go.transform.localScale = new Vector3(shadowSize.x * 0.6f, 0f, 1f);
        return sr;
    }

    IEnumerator FadeShadow(float time)
    {
        if (shadow == null) yield break;
        Color c0 = shadow.color;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(c0.a, 0f, e / time));
            yield return null;
        }
        Destroy(shadow.gameObject);
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        GameProgress.Set(StoryFlag.CabinetVisitDone);
    }

    // she left in the middle of it (with or without the bed): the visit counts as over
    void OnDestroy()
    {
        if (started && !finished && psycho != null && psycho.Current != EntityAI.State.Grabbing) Finish();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(floorArea.center, floorArea.size);
        if (door != null) Gizmos.DrawWireSphere(door.position, 0.3f);
    }
}
