using UnityEngine;
using UnityEngine.Rendering.Universal;

/// The operating-room lamp: a small pool of light on the table that flickers nervously
/// (small wobble, sudden dips, now and then a short blackout).
///
/// The lamp's glow is painted into the background, so a soft dark "shade" is laid over the painted
/// glow: a ring that pulls the bright area in (smaller radius), and a disc that darkens it whenever
/// the lamp dips. Both are generated at runtime, no extra art needed.
public class SurgeryLamp : MonoBehaviour
{
    [Header("Light")]
    [SerializeField] private Light2D lamp;
    [SerializeField] private float baseIntensity = 0.9f;
    [SerializeField] private float outerRadius = 2.2f;
    [SerializeField] private float innerRadius = 0.35f;

    [Header("Flicker")]
    [Tooltip("Constant small wobble (0..1 of the intensity).")]
    [SerializeField] private float wobble = 0.12f;
    [Tooltip("Seconds between sudden dips (random in this range).")]
    [SerializeField] private Vector2 dipEvery = new Vector2(0.8f, 3.5f);
    [Tooltip("Chance that a dip becomes a short blackout.")]
    [SerializeField, Range(0f, 1f)] private float blackoutChance = 0.25f;
    [SerializeField] private Vector2 blackoutLength = new Vector2(0.15f, 0.6f);

    [Header("Shade over the painted glow")]
    [SerializeField] private SpriteRenderer background;
    [Tooltip("World radius where the painted glow starts to be darkened.")]
    [SerializeField] private float shadeInner = 1.8f;
    [Tooltip("World radius of the whole shade (covers the painted glow).")]
    [SerializeField] private float shadeOuter = 3.8f;
    [SerializeField, Range(0f, 1f)] private float ringDarkness = 0.45f;
    [SerializeField, Range(0f, 1f)] private float dipDarkness = 0.55f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource buzz;

    private SpriteRenderer ring, disc;
    private float nextDip, dipUntil, dipLevel = 1f, seed;

    void Awake()
    {
        seed = Random.value * 100f;
        if (lamp == null) lamp = GetComponentInChildren<Light2D>();
        if (lamp != null)
        {
            lamp.lightType = Light2D.LightType.Point;
            lamp.pointLightOuterRadius = outerRadius;
            lamp.pointLightInnerRadius = innerRadius;
            lamp.pointLightOuterAngle = 360f; lamp.pointLightInnerAngle = 360f;
        }
        if (background != null)
        {
            ring = MakeShade("LampShade_Ring", true);
            disc = MakeShade("LampShade_Dip", false);
        }
        nextDip = Time.time + Random.Range(dipEvery.x, dipEvery.y);
        Debug.Log($"[SurgeryLamp] Flickering lamp ready (radius {outerRadius}, shade {(background != null ? "on" : "off")}).", this);
    }

    SpriteRenderer MakeShade(string objName, bool ringShape)
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float inner = shadeInner / shadeOuter;
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float r = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
                float a;
                if (ringShape) a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, inner + 0.3f, r)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, r)));
                else a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, r));
                px[y * N + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(a) * 255));
            }
        tex.SetPixels32(px); tex.Apply();
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N / (shadeOuter * 2f));
        sr.sortingLayerID = background.sortingLayerID;
        sr.sortingOrder = background.sortingOrder + 1;            // over the floor art, under props and the girl
        sr.sharedMaterial = background.sharedMaterial;            // black stays black under any light
        sr.color = new Color(1, 1, 1, 0);
        return sr;
    }

    void Update()
    {
        float t = Time.time;
        if (t >= nextDip)
        {
            bool blackout = Random.value < blackoutChance;
            dipLevel = blackout ? 0.02f : Random.Range(0.35f, 0.7f);
            dipUntil = t + (blackout ? Random.Range(blackoutLength.x, blackoutLength.y) : Random.Range(0.05f, 0.18f));
            nextDip = dipUntil + Random.Range(dipEvery.x, dipEvery.y);
        }
        float dip = t < dipUntil ? dipLevel : 1f;
        float w = 1f + (Mathf.PerlinNoise(seed, t * 9f) - 0.5f) * 2f * wobble;
        float level = Mathf.Clamp01(dip * w);                  // 0 = dark, ~1 = full

        if (lamp != null) lamp.intensity = baseIntensity * level;
        if (ring != null) ring.color = new Color(1, 1, 1, ringDarkness);
        if (disc != null) disc.color = new Color(1, 1, 1, (1f - Mathf.Clamp01(level)) * dipDarkness);
        if (buzz != null) buzz.volume = 0.25f * level;
        else UpdateHum(level);
    }

    [Header("Hum (GameSounds.surgicalLamp, used when no Buzz source is assigned)")]
    [SerializeField, Range(0f, 1f)] private float humVolume = 1f;
    [Tooltip("Full volume within this distance of the lamp, fading out to 'humFar'.")]
    [SerializeField] private float humNear = 3f;
    [SerializeField] private float humFar = 12f;
    private SoundLoop hum;
    private Transform listener;

    // the lamp's buzz follows its flicker: dips get quieter, blackouts go silent
    void UpdateHum(float level)
    {
        var lib = GameAudio.Sounds;
        if (lib == null) return;
        if (hum == null)
        {
            hum = SoundLoop.Create(gameObject);
            hum.fadeTime = 0.03f;                    // quick, so the buzz cuts with the light
            hum.SetClip(GameAudio.Pick(lib.surgicalLamp));
            hum.Playing = true;
        }
        if (listener == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) listener = p.transform; }
        float dist = listener != null ? Vector2.Distance(listener.position, transform.position) : 0f;
        float near = 1f - Mathf.Clamp01(Mathf.InverseLerp(humNear, humFar, dist));
        hum.Volume = humVolume * Mathf.Clamp01(level) * near;
    }
}
