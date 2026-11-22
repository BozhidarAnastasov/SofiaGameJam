using System.Collections;
using System.Collections.Generic;
using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Lobby intro. The game starts with the girl just inside the hospital entrance, the open doorway
/// behind her with cold moonlight spilling in. A couple of seconds later the ground rumbles and rocks
/// come down outside: we never see the rocks themselves (the entrance is in the near wall), only
/// their shadows dropping onto the floor in the moonlight, dust and the screen shaking. Then the light
/// is cut off and a heap of rock shadows fills the doorway: the way out is blocked.
/// Happens once per game (StoryFlag.EntranceCollapsed); afterwards the doorway is simply blocked.
public class EntranceCollapse : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private Transform player;
    [Tooltip("The open doorway in the near wall (drawn over the south wall).")]
    [SerializeField] private SpriteRenderer opening;
    [Tooltip("The heap of rock shadows that fills the doorway after the collapse.")]
    [SerializeField] private SpriteRenderer rubbleShadow;
    [Tooltip("Moonlight coming in through the doorway.")]
    [SerializeField] private Light2D spill;
    [Tooltip("Floor sprite whose sorting layer the falling shadows use (the room background).")]
    [SerializeField] private SpriteRenderer floor;
    [Tooltip("Where the rocks land: centre of the doorway on the floor, and its width.")]
    [SerializeField] private Transform landingCentre;
    [SerializeField] private float landingWidth = 2.6f;

    [Header("Timing")]
    [SerializeField] private float startDelay = 2f;
    [SerializeField] private int rocks = 8;
    [SerializeField] private float fallTime = 0.4f;
    [SerializeField] private Vector2 gapBetweenRocks = new Vector2(0.08f, 0.28f);
    [SerializeField] private string blockedMessage = "No... the entrance is blocked.";

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip rumbleClip;
    [SerializeField] private AudioClip impactClip;

    private static Sprite blob, mote;
    private float spillFull;
    private readonly List<SpriteRenderer> temp = new List<SpriteRenderer>();

    void Awake()
    {
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
        if (spill != null) spillFull = spill.intensity;
        if (GameProgress.Get(StoryFlag.EntranceCollapsed)) { ShowBlocked(); return; }
        if (rubbleShadow != null) { rubbleShadow.enabled = true; SetAlpha(rubbleShadow, 0f); }
    }

    void Start()
    {
        if (GameProgress.Get(StoryFlag.EntranceCollapsed)) return;
        // only the very first arrival (start of the game) shows the open entrance behind her
        if (!string.IsNullOrEmpty(RoomSpawnPoint.NextEntry)) { GameProgress.Set(StoryFlag.EntranceCollapsed); ShowBlocked(); return; }
        var anim = player != null ? player.GetComponentInChildren<DirectionalSpriteAnimator>() : null;
        if (anim != null && landingCentre != null) anim.FaceTowards(player.position + Vector3.up);   // looking into the lobby
        StartCoroutine(Collapse());
    }

    void ShowBlocked()
    {
        if (spill != null) spill.enabled = false;
        if (opening != null) opening.color = new Color(0.22f, 0.22f, 0.26f, 1f);
        if (rubbleShadow != null) { rubbleShadow.enabled = true; SetAlpha(rubbleShadow, 1f); }
    }

    IEnumerator Collapse()
    {
        Debug.Log("[EntranceCollapse] Entrance open behind her; collapse in " + startDelay + " s.", this);
        yield return new WaitForSeconds(startDelay);

        // rumble
        if (audioSource != null && rumbleClip != null) audioSource.PlayOneShot(rumbleClip);
        for (float e = 0f; e < 0.9f; e += 0.15f)
        {
            CameraShake.Shake(0.2f, Mathf.Lerp(0.02f, 0.07f, e / 0.9f));
            if (Random.value < 0.6f) StartCoroutine(Dust(RandomLanding(), 3, 0.6f));
            yield return new WaitForSeconds(0.15f);
        }

        // rocks: their shadows drop onto the floor (big and faint while high up, small and dark on impact)
        for (int i = 0; i < rocks; i++)
        {
            StartCoroutine(Rock(RandomLanding(), Random.Range(0.35f, 0.75f), i == rocks - 1));
            yield return new WaitForSeconds(Random.Range(gapBetweenRocks.x, gapBetweenRocks.y));
        }
        yield return new WaitForSeconds(fallTime + 0.1f);

        // the doorway is closed: light cut off, the heap's shadow fills it
        for (float e = 0f; e < 0.35f; e += Time.deltaTime)
        {
            float k = e / 0.35f;
            if (spill != null) spill.intensity = Mathf.Lerp(spill.intensity, 0f, k);
            if (rubbleShadow != null) SetAlpha(rubbleShadow, k);
            if (opening != null) opening.color = Color.Lerp(Color.white, new Color(0.22f, 0.22f, 0.26f, 1f), k);
            yield return null;
        }
        ShowBlocked();
        foreach (var r in temp) if (r != null) Destroy(r.gameObject, 0.5f);   // the falling shadows merge into the heap
        GameProgress.Set(StoryFlag.EntranceCollapsed);
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(blockedMessage);
        Debug.Log("[EntranceCollapse] The entrance is blocked.", this);
    }

    Vector2 RandomLanding()
    {
        Vector2 c = landingCentre != null ? (Vector2)landingCentre.position : (Vector2)transform.position;
        return c + new Vector2(Random.Range(-landingWidth * 0.5f, landingWidth * 0.5f), Random.Range(-0.25f, 0.35f));
    }

    IEnumerator Rock(Vector2 at, float size, bool last)
    {
        var sr = MakeSprite("RockShadow", Blob(), at, 2);
        temp.Add(sr);
        for (float e = 0f; e < fallTime; e += Time.deltaTime)
        {
            float k = e / fallTime; k *= k;                                    // accelerates as it falls
            float s = Mathf.Lerp(size * 2.6f, size, k);
            sr.transform.localScale = new Vector3(s, s * 0.55f, 1f);
            SetAlpha(sr, Mathf.Lerp(0.12f, 0.85f, k));
            yield return null;
        }
        sr.transform.localScale = new Vector3(size, size * 0.55f, 1f);
        SetAlpha(sr, 0.85f);
        if (audioSource != null && impactClip != null) audioSource.PlayOneShot(impactClip, last ? 1f : 0.7f);
        CameraShake.Shake(last ? 0.45f : 0.2f, last ? 0.22f : 0.11f);
        if (spill != null) spill.intensity = Mathf.Max(0.05f, spill.intensity * 0.8f);   // the gap gets smaller
        StartCoroutine(Dust(at, last ? 14 : 6, 0.9f));
    }

    IEnumerator Dust(Vector2 at, int count, float life)
    {
        var bits = new List<(SpriteRenderer sr, Vector2 v)>();
        for (int i = 0; i < count; i++)
        {
            var sr = MakeSprite("Dust", Mote(), at + Random.insideUnitCircle * 0.25f, 3);
            float s = Random.Range(0.6f, 1.4f);
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.color = new Color(0.62f, 0.64f, 0.66f, 0.7f);
            bits.Add((sr, new Vector2(Random.Range(-0.9f, 0.9f), Random.Range(0.2f, 1.1f))));
        }
        for (float e = 0f; e < life; e += Time.deltaTime)
        {
            float k = e / life;
            foreach (var (sr, v) in bits)
            {
                sr.transform.position += (Vector3)(v * Time.deltaTime * (1f - k));
                SetAlpha(sr, 0.7f * (1f - k));
            }
            yield return null;
        }
        foreach (var (sr, _) in bits) Destroy(sr.gameObject);
    }

    SpriteRenderer MakeSprite(string n, Sprite sp, Vector2 at, int orderAboveFloor)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, true);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.color = Color.black;
        if (floor != null) { sr.sortingLayerID = floor.sortingLayerID; sr.sortingOrder = floor.sortingOrder + orderAboveFloor; sr.sharedMaterial = floor.sharedMaterial; }
        return sr;
    }

    static void SetAlpha(SpriteRenderer sr, float a) { var c = sr.color; c.a = a; sr.color = c; }

    // soft round blob (1 unit wide) for the rock shadows
    static Sprite Blob()
    {
        if (blob != null) return blob;
        const int N = 48;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float r = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
                float a = 1f - Mathf.SmoothStep(0.55f, 1f, r);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return blob = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    // tiny dust speck
    static Sprite Mote()
    {
        if (mote != null) return mote;
        var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, y, new Color(1, 1, 1, (x == 1 || y == 1) ? 1f : 0.3f));
        tex.Apply();
        return mote = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 28f);
    }
}
