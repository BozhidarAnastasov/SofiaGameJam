using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Ending transition when the girl successfully evades the psycho:
/// Plays the 4 frames in REVERSE (03 -> 00: close up -> far away), smoothly
/// cross-fading and shrinking his face back into the darkness before loading MainMenu.
public class EscapeEndingScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup rootGroup;      // CanvasGroup on the root Ending Panel
    [SerializeField] private Image background;           // Fullscreen black/dark UI Image
    [SerializeField] private Image facePrimary;          // Main UI Image for the face
    [SerializeField] private Image faceCrossfade;        // Optional 2nd UI Image right on top of facePrimary for smooth frame blending
    [SerializeField] private CanvasGroup escapedTextGroup; // Optional "Escaped" text CanvasGroup (leave empty if none)

    [Header("Frames (Assign same order as DeathScreen: 00 = far, 03 = close)")]
    [Tooltip("psycho_death_screen_00 (far) ... 03 (close). Script automatically plays them in reverse (03 -> 00).")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Duration of each step as he recedes further away (slowing down as she gets safe).")]
    [SerializeField] private float[] retreatStepTimes = { 0.55f, 0.70f, 0.85f };
    [SerializeField] private float finalFadeOutTime = 1.6f;
    [SerializeField] private float postFadeHoldTime = 1.2f;

    [Header("Scene Transition")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private Color finalScreenColor = Color.black; // Or set to soft white/pale blue for "stepping outside"

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip escapeReliefClip; // Heavy door slam / breath of relief / ambient wind

    public UnityEvent onEndingStarted;
    public UnityEvent onEndingFinished;

    private bool triggered;

    private void Awake()
    {
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        if (facePrimary != null) facePrimary.enabled = false;
        if (faceCrossfade != null) faceCrossfade.enabled = false;
        if (escapedTextGroup != null) escapedTextGroup.alpha = 0f;
    }

    /// Call this from your final exit Door / Trigger when she escapes!
    public void TriggerEnding()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (triggered) return;
        triggered = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartCoroutine(RunEscapeSequence());
    }

    private IEnumerator RunEscapeSequence()
    {
        // Freeze chaser & player movement while running UI on unscaled time
        Time.timeScale = 0f;

        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;

            // Smooth 0.35s fade to black as she slips through the exit
            for (float e = 0f; e < 0.35f; e += Time.unscaledDeltaTime)
            {
                rootGroup.alpha = Mathf.Clamp01(e / 0.35f);
                yield return null;
            }
            rootGroup.alpha = 1f;
        }

        if (background != null) background.color = Color.black;

        if (audioSource != null && escapeReliefClip != null)
        {
            audioSource.PlayOneShot(escapeReliefClip);
        }
        else GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.girlExhaust : null);   // her breath of relief

        onEndingStarted?.Invoke();
        yield return new WaitForSecondsRealtime(0.2f);

        if (facePrimary != null && frames != null && frames.Length > 0)
        {
            int lastIdx = frames.Length - 1;
            RectTransform primaryRT = facePrimary.rectTransform;
            RectTransform crossRT = faceCrossfade != null ? faceCrossfade.rectTransform : null;

            // Start on closest frame (03), slightly dim & cold, receding away
            facePrimary.enabled = true;
            facePrimary.sprite = frames[lastIdx];
            facePrimary.color = new Color(0.85f, 0.88f, 0.95f, 0.9f);
            primaryRT.localScale = Vector3.one;
            primaryRT.anchoredPosition = Vector2.zero;

            int stepIndex = 0;

            // Step backward: 03 -> 02 -> 01 -> 00
            for (int i = lastIdx; i > 0; i--)
            {
                Sprite currentSprite = frames[i];
                Sprite nextFartherSprite = frames[i - 1];

                float duration = stepIndex < retreatStepTimes.Length ? retreatStepTimes[stepIndex] : 0.7f;
                stepIndex++;

                // Overall alpha gradually drops as he falls further behind (0.9 -> 0.45)
                float startAlpha = Mathf.Lerp(0.9f, 0.55f, (float)(lastIdx - i) / lastIdx);
                float endAlpha = Mathf.Lerp(0.9f, 0.55f, (float)(lastIdx - (i - 1)) / lastIdx);

                if (faceCrossfade != null && nextFartherSprite != null)
                {
                    // Smooth dual-image crossfade + subtle receding shrink
                    facePrimary.sprite = currentSprite;
                    faceCrossfade.enabled = true;
                    faceCrossfade.sprite = nextFartherSprite;

                    for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
                    {
                        float k = Mathf.SmoothStep(0f, 1f, e / duration);
                        float scale = Mathf.Lerp(1f, 0.93f, k);

                        primaryRT.localScale = Vector3.one * scale;
                        crossRT.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, k);

                        facePrimary.color = new Color(0.8f, 0.85f, 0.95f, startAlpha * (1f - k));
                        faceCrossfade.color = new Color(0.8f, 0.85f, 0.95f, endAlpha * k);

                        yield return null;
                    }

                    // Hand off next sprite to primary for the next loop iteration
                    facePrimary.sprite = nextFartherSprite;
                    facePrimary.color = new Color(0.8f, 0.85f, 0.95f, endAlpha);
                    primaryRT.localScale = Vector3.one;
                    faceCrossfade.enabled = false;
                }
                else
                {
                    // Single-image fallback if faceCrossfade isn't assigned
                    facePrimary.sprite = currentSprite;
                    for (float e = 0f; e < duration; e += Time.unscaledDeltaTime)
                    {
                        float k = Mathf.SmoothStep(0f, 1f, e / duration);
                        primaryRT.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, k);
                        facePrimary.color = new Color(0.8f, 0.85f, 0.95f, Mathf.Lerp(startAlpha, endAlpha, k));
                        yield return null;
                    }
                    if (nextFartherSprite != null) facePrimary.sprite = nextFartherSprite;
                    primaryRT.localScale = Vector3.one;
                }
            }

            // Final dissolve on frame 00 (furthest away): shrinks into pure darkness and vanishes
            Color startFadeColor = facePrimary.color;
            Color startBgColor = background != null ? background.color : Color.black;

            for (float e = 0f; e < finalFadeOutTime; e += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, e / finalFadeOutTime);

                primaryRT.localScale = Vector3.one * Mathf.Lerp(1f, 0.82f, k);
                facePrimary.color = new Color(
                    startFadeColor.r * (1f - k),
                    startFadeColor.g * (1f - k),
                    startFadeColor.b * (1f - k),
                    Mathf.Lerp(startFadeColor.a, 0f, k)
                );

                if (background != null)
                {
                    background.color = Color.Lerp(startBgColor, finalScreenColor, k);
                }

                if (escapedTextGroup != null)
                {
                    escapedTextGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.3f) / 0.7f));
                }

                yield return null;
            }

            facePrimary.enabled = false;
        }
        else
        {
            yield return new WaitForSecondsRealtime(finalFadeOutTime);
        }

        // Hold briefly on the quiet empty screen (or "Escaped" text) before returning to Main Menu
        yield return new WaitForSecondsRealtime(postFadeHoldTime);

        // Fade out "Escaped" text cleanly if used
        if (escapedTextGroup != null && escapedTextGroup.alpha > 0f)
        {
            for (float e = 0f; e < 0.6f; e += Time.unscaledDeltaTime)
            {
                escapedTextGroup.alpha = 1f - (e / 0.6f);
                yield return null;
            }
        }

        onEndingFinished?.Invoke();

        // Always restore normal timeScale before loading MainMenu!
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}