using UnityEngine;
using UnityEngine.UI;

public class CreepyMenuEffects : MonoBehaviour
{
    [Header("Subtle Breathing Motion")]
    [SerializeField] private RectTransform backgroundArt;
    [SerializeField] private float zoomAmount = 0.025f; // Slight scale pulse
    [SerializeField] private float zoomSpeed = 0.4f;
    [SerializeField] private float swayAmount = 6f;     // Tiny pixel drift

    [Header("Broken Doorway Light Flicker")]
    [SerializeField] private Image doorwayGlow;
    [SerializeField] private AudioSource buzzAudioSource; // Optional quiet electrical buzz
    [SerializeField] private float maxGlowAlpha = 0.14f;

    private Vector2 startPos;
    private Vector3 startScale;
    private float flickerTimer;

    void Start()
    {
        if (backgroundArt != null)
        {
            startPos = backgroundArt.anchoredPosition;
            startScale = backgroundArt.localScale * 1.04f; // Base slight overscale so edges never show
        }
    }

    void Update()
    {
        // 1. Slow, uneasy "breathing" zoom and drift on the artwork
        if (backgroundArt != null)
        {
            float scaleOffset = Mathf.Sin(Time.time * zoomSpeed) * zoomAmount;
            backgroundArt.localScale = startScale + new Vector3(scaleOffset, scaleOffset, 0f);

            float swayX = Mathf.Sin(Time.time * 0.27f) * swayAmount;
            float swayY = Mathf.Cos(Time.time * 0.19f) * (swayAmount * 0.5f);
            backgroundArt.anchoredPosition = startPos + new Vector2(swayX, swayY);
        }

        // 2. Erratic hospital fluorescent flicker in the doorway
        if (doorwayGlow != null)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                // 80% of the time stay off/dead, 20% brief nervous flicker
                bool spike = Random.value > 0.80f;
                Color c = doorwayGlow.color;
                c.a = spike ? Random.Range(0.04f, maxGlowAlpha) : 0f;
                doorwayGlow.color = c;

                if (buzzAudioSource != null)
                {
                    buzzAudioSource.volume = spike ? 0.15f : 0f;
                }

                flickerTimer = spike ? Random.Range(0.03f, 0.12f) : Random.Range(0.15f, 1.8f);
            }
        }
    }
}