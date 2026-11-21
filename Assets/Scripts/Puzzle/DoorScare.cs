using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

/// After the power comes back: the small lamp over the door starts flickering, and on the
/// last flash the psycho's face shows in the door's window. Then the lamp dies and he's gone.
/// Call Play() (the Lever's On Pulled event does this automatically after Tools > Horror Room > 6).
public class DoorScare : MonoBehaviour
{
    [Header("Lamp over the door")]
    [SerializeField] private SpriteRenderer lamp;
    [SerializeField] private Sprite lampOff;
    [SerializeField] private Sprite lampOn;
    [SerializeField] private Light2D lampLight;

    [Header("Face in the window")]
    [SerializeField] private SpriteRenderer face;
    [Tooltip("How long his face stays visible in the last flash.")]
    [SerializeField] private float faceHold = 0.7f;

    [Header("Timing")]
    [Tooltip("Wait after the lever before the lamp starts.")]
    [SerializeField] private float startDelay = 1.2f;
    [Tooltip("Seconds per state, alternating ON, OFF, ON, OFF... (keep an even count so it ends dark before the last flash).")]
    [SerializeField] private float[] flicker = { 0.08f, 0.12f, 0.05f, 0.35f, 0.15f, 0.06f, 0.04f, 0.1f, 0.06f, 0.6f };
    [Tooltip("The last flash is brighter than the normal flickers.")]
    [SerializeField] private float lastFlashBoost = 1.6f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip flickerClip;
    [SerializeField] private AudioClip scareClip;

    public UnityEvent onFaceShown;
    public UnityEvent onFinished;

    private bool played;
    private float baseIntensity = 1f;

    void Awake()
    {
        if (lampLight != null) baseIntensity = lampLight.intensity;
        SetLamp(false, 1f);
        if (face != null) face.enabled = false;
    }

    public void Play()
    {
        if (played) return;
        played = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(startDelay);

        bool on = false;
        foreach (float d in flicker)
        {
            on = !on;
            SetLamp(on, Random.Range(0.6f, 1f));
            if (on && audioSource != null && flickerClip != null) audioSource.PlayOneShot(flickerClip, 0.5f);
            yield return new WaitForSeconds(d);
        }

        // last flash: brighter, and he is there
        SetLamp(true, lastFlashBoost);
        if (face != null) face.enabled = true;
        if (audioSource != null && scareClip != null) audioSource.PlayOneShot(scareClip);
        onFaceShown.Invoke();
        yield return new WaitForSeconds(faceHold);

        // the bulb dies, and the window is empty again
        SetLamp(false, 1f);
        if (face != null) face.enabled = false;
        onFinished.Invoke();
    }

    void SetLamp(bool on, float strength)
    {
        if (lamp != null) lamp.sprite = on ? lampOn : lampOff;
        if (lampLight != null)
        {
            lampLight.enabled = on;
            lampLight.intensity = baseIntensity * strength;
        }
    }
}
