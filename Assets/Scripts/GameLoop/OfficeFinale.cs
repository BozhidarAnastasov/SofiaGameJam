using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.InputSystem;

/// The ending, in the corridor and office. Two seconds after she comes in, he bursts out of the
/// Lobby door behind her and comes down the corridor. If he catches up with her on the way, he
/// grabs her. When he reaches the office doorway he stops and throws his knife at her, misses, and
/// it smashes the window. She climbs out through the broken window (A) -> the win screen.
public class OfficeFinale : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private EntityAI psycho;
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("The Lobby door he comes out of.")]
    [SerializeField] private Transform entrance;
    [Tooltip("Points down the corridor (around the rubble) before the doorway.")]
    [SerializeField] private Transform[] corridorPath;
    [Tooltip("The office doorway, where he stops and throws (the circled spot).")]
    [SerializeField] private Transform throwSpot;
    [Tooltip("Centre of the window glass on the office's right wall.")]
    [SerializeField] private Transform window;
    [Tooltip("Where the escape works from (A within reach of this).")]
    [SerializeField] private Transform windowStand;
    [SerializeField] private float windowReach = 1.4f;
    [SerializeField] private EscapeEndingScreen winScreen;
    [Tooltip("Sorting reference for the knife / glass (a room sprite).")]
    [SerializeField] private SpriteRenderer roomArt;

    [Header("Timing")]
    [SerializeField] private float delayBehindHer = 2f;
    [Tooltip("His speed down the corridor (she walks 3, runs 5).")]
    [SerializeField] private float corridorSpeed = 2.7f;
    [Tooltip("If he gets this close on the way, he grabs her.")]
    [SerializeField] private float catchDistance = 1.3f;
    [SerializeField] private float windUp = 0.6f;
    [SerializeField] private float knifeSpeed = 14f;

    [Header("Text")]
    [SerializeField] private string throwMessage = "He throws his knife...!";
    [SerializeField] private string brokenMessage = "The window shattered! Get out! (A)";

    private bool windowBroken, escaped;
    private InteractPrompt prompt;
    private InputAction fallback;
    private SpriteRenderer windowHole;

    InputAction Interact => interactAction != null && interactAction.action != null ? interactAction.action : fallback;

    void Awake()
    {
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
    }

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null)
        {
            fallback = new InputAction("WindowInteract", InputActionType.Button);
            fallback.AddBinding("<Keyboard>/e"); fallback.AddBinding("<Gamepad>/buttonNorth"); fallback.AddBinding("<Joystick>/trigger");
        }
        Interact.Enable();
    }

    void OnDisable() { if (fallback != null) { fallback.Disable(); fallback.Dispose(); fallback = null; } }

    IEnumerator Start()
    {
        if (psycho == null || entrance == null || throwSpot == null) { Debug.LogWarning("[OfficeFinale] Not wired - run the setup step.", this); yield break; }
        while (RoomTransition.IsBusy) yield return null;              // let the fade-in finish
        yield return new WaitForSeconds(delayBehindHer);

        Debug.Log("[OfficeFinale] He bursts out of the door behind her.", this);
        CameraShake.Shake(0.3f, 0.1f);
        psycho.VisionOff = true;                                       // this is a pursuit, not a search
        psycho.ScriptSpeed = corridorSpeed;
        psycho.ScriptAppear(entrance.position, Vector2.right);
        int n = corridorPath != null ? corridorPath.Length : 0;
        for (int i = 0; i <= n; i++)
        {
            Transform target = i < n ? corridorPath[i] : throwSpot;
            if (target == null) continue;
            psycho.ScriptWalkTo(target.position);
            while (!psycho.ScriptArrived)
            {
                if (psycho.Current == EntityAI.State.Grabbing) yield break;
                if (player != null && psycho.Current != EntityAI.State.Chasing &&
                    Vector2.Distance(psycho.transform.position, player.position) <= catchDistance)
                {
                    Debug.Log("[OfficeFinale] He caught up with her.", this);
                    psycho.ChaseNow();                                 // the chase ends in a grab
                    yield break;
                }
                yield return null;
            }
        }

        // at the doorway: stop, wind up, throw
        var anim = psycho.GetComponentInChildren<DirectionalSpriteAnimator>();
        if (anim != null && player != null) anim.FaceTowards(player.position);
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(throwMessage);
        yield return new WaitForSeconds(windUp);
        yield return ThrowKnife();
        BreakWindow();
    }

    IEnumerator ThrowKnife()
    {
        Vector3 from = psycho.transform.position + Vector3.up * 1.1f;
        Vector3 to = window != null ? window.position : from + Vector3.right * 8f;
        // aimed at her: the knife passes close to her, then flies on into the glass
        Vector3 aim = player != null ? player.position + Vector3.up * 1f : (from + to) * 0.5f;
        var knife = MakeKnife();
        knife.transform.position = from;
        float d1 = Vector3.Distance(from, aim), d2 = Vector3.Distance(aim, to);
        float t1 = d1 / knifeSpeed, t2 = d2 / knifeSpeed;
        Debug.Log("[OfficeFinale] Knife thrown - it misses her and flies into the window.", this);
        for (float e = 0f; e < t1 + t2; e += Time.deltaTime)
        {
            Vector3 p = e < t1 ? Vector3.Lerp(from, aim + Vector3.up * 0.35f, e / t1)            // just over her head
                               : Vector3.Lerp(aim + Vector3.up * 0.35f, to, (e - t1) / t2);
            knife.transform.position = p;
            knife.transform.Rotate(0f, 0f, -1080f * Time.deltaTime);                          // spinning
            yield return null;
        }
        knife.transform.position = to;
        knife.transform.rotation = Quaternion.Euler(0f, 0f, -20f);                            // stuck in the frame
    }

    void BreakWindow()
    {
        windowBroken = true;
        CameraShake.Shake(0.4f, 0.18f);
        if (window != null)
        {
            windowHole = MakeSprite("BrokenWindow", HoleSprite(), window.position, 2);
            windowHole.color = new Color(1f, 1f, 1f, 0.95f);
            StartCoroutine(Shards(window.position));
        }
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(brokenMessage);
        Debug.Log("[OfficeFinale] The window is broken - she can climb out (A at the window).", this);
    }

    void Update()
    {
        if (!windowBroken || escaped || player == null) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);
        var move = player.GetComponent<TopDownMovement>();
        Vector2 stand = windowStand != null ? (Vector2)windowStand.position : (Vector2)window.position;
        bool inRange = (move == null || (move.enabled && move.CanMove)) && Vector2.Distance(player.position, stand) <= windowReach;
        if (prompt != null) prompt.Request(this, inRange);
        if (inRange && Interact.WasPressedThisFrame()) Escape();
    }

    void Escape()
    {
        escaped = true;
        if (prompt != null) prompt.Request(this, false);
        Debug.Log("[OfficeFinale] She climbs out of the window - ESCAPED.", this);
        if (psycho != null) psycho.ScriptVanish();                    // he's left behind
        var move = player.GetComponent<TopDownMovement>();
        if (move != null) move.CanMove = false;
        if (winScreen != null) winScreen.TriggerEnding();
        else Debug.LogWarning("[OfficeFinale] No win screen assigned.", this);
    }

    // ---------------------------------------------------------------- simple generated art
    SpriteRenderer MakeSprite(string n, Sprite sp, Vector3 at, int orderAbove)
    {
        var go = new GameObject(n);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        if (roomArt != null) { sr.sortingLayerID = roomArt.sortingLayerID; sr.sortingOrder = roomArt.sortingOrder + orderAbove; sr.sharedMaterial = roomArt.sharedMaterial; }
        return sr;
    }

    SpriteRenderer MakeKnife()
    {
        const int W = 28, H = 7;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) tex.SetPixel(x, y, clear);
        for (int x = 0; x < 9; x++) for (int y = 2; y < 5; y++) tex.SetPixel(x, y, new Color(0.22f, 0.13f, 0.08f));      // handle
        for (int y = 1; y < 6; y++) tex.SetPixel(9, y, new Color(0.35f, 0.35f, 0.38f));                                   // guard
        for (int x = 10; x < W; x++)
        {
            int top = x < W - 6 ? 5 : 5 - (x - (W - 6));                                                                  // pointed tip
            for (int y = 2; y < Mathf.Max(3, top); y++) tex.SetPixel(x, y, y >= top - 1 ? new Color(0.92f, 0.94f, 0.97f) : new Color(0.62f, 0.65f, 0.7f));
        }
        tex.Apply();
        var sr = MakeSprite("Knife", Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 36f), Vector3.zero, 50);
        if (player != null)
        {
            var girl = player.GetComponent<SpriteRenderer>();
            if (girl != null) { sr.sortingLayerID = girl.sortingLayerID; sr.sortingOrder = girl.sortingOrder + 50; }
        }
        return sr;
    }

    // a dark, jagged hole with cracks where the glass was
    static Sprite HoleSprite()
    {
        const int W = 40, H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var rnd = new System.Random(7);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W - 0.5f, v = (y + 0.5f) / H - 0.5f;
                float edge = 0.36f + 0.06f * Mathf.Sin(y * 0.9f) + 0.04f * Mathf.Sin(y * 2.3f + 1f);
                float r = Mathf.Sqrt(u * u / (edge * edge) + v * v / 0.2f);
                float a = r < 1f ? 0.9f : 0f;
                tex.SetPixel(x, y, new Color(0.02f, 0.03f, 0.06f, a));
            }
        // cracks running out of the hole
        for (int c = 0; c < 9; c++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, len = 10 + (float)rnd.NextDouble() * 30f;
            for (float s = 6f; s < len; s += 0.5f)
            {
                int x = Mathf.RoundToInt(W / 2f + Mathf.Cos(ang) * s * 0.45f), y = Mathf.RoundToInt(H / 2f + Mathf.Sin(ang) * s);
                if (x >= 0 && x < W && y >= 0 && y < H) tex.SetPixel(x, y, new Color(0.85f, 0.92f, 1f, 0.85f));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 45f);
    }

    IEnumerator Shards(Vector3 at)
    {
        var tex = new Texture2D(3, 3) { filterMode = FilterMode.Point };
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, y, new Color(0.8f, 0.9f, 1f, (x + y) % 2 == 0 ? 1f : 0.4f));
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 45f);
        var bits = new System.Collections.Generic.List<(Transform t, Vector2 v, float floorY)>();
        for (int i = 0; i < 22; i++)
        {
            var sr = MakeSprite("GlassShard", sp, at + (Vector3)(Random.insideUnitCircle * 0.3f), 3);
            sr.color = new Color(0.8f, 0.9f, 1f, 0.9f);
            bits.Add((sr.transform, new Vector2(Random.Range(-2.5f, -0.3f), Random.Range(0.5f, 2.5f)), at.y - Random.Range(0.6f, 1.4f)));
        }
        for (float e = 0f; e < 0.8f; e += Time.deltaTime)
        {
            for (int i = 0; i < bits.Count; i++)
            {
                var b = bits[i];
                if (b.t.position.y <= b.floorY && b.v.y < 0f) continue;                     // landed on the floor
                b.v += Vector2.down * 9f * Time.deltaTime;
                b.t.position += (Vector3)(b.v * Time.deltaTime);
                bits[i] = b;
            }
            yield return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (window != null) Gizmos.DrawWireSphere(window.position, 0.3f);
        if (windowStand != null) Gizmos.DrawWireSphere(windowStand.position, windowReach);
        Gizmos.color = Color.red;
        if (throwSpot != null) Gizmos.DrawWireSphere(throwSpot.position, 0.4f);
    }
}
