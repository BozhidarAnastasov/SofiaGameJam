using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Final screen when the psycho catches her: the screen cuts to black and his face lunges
/// out of the darkness (the 4 frames of psycho-death-screen), with a jolt and a red pulse.
/// Then the template's lose screen loads (when the game was started from StartScene);
/// when you play this scene directly, press Interact to try again.
public class DeathScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image background;
    [SerializeField] private Image face;
    [Tooltip("psycho_death_screen_00 ... 03: far away -> right in front of you.")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float[] frameTimes = { 0.35f, 0.18f, 0.12f };
    [SerializeField] private float holdTime = 2.5f;

    [Header("Afterwards")]
    [SerializeField] private bool goToLoseScreen = true;
    [SerializeField] private InputActionReference retryAction;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stinger;

    public UnityEvent onShown;
    public UnityEvent onFinished;

    private bool shown;

    void Awake()
    {
        if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
        if (face != null) face.enabled = false;
    }

    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);   // fine to keep it switched off while editing
        if (shown) return;
        shown = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        if (group != null) group.alpha = 1f;               // hard cut to black
        if (background != null) background.color = Color.black;
        yield return new WaitForSeconds(0.25f);

        if (audioSource != null && stinger != null) audioSource.PlayOneShot(stinger);
        onShown.Invoke();
        if (face != null && frames != null && frames.Length > 0)
        {
            face.enabled = true;
            var rt = face.rectTransform;
            for (int i = 0; i < frames.Length; i++)
            {
                face.sprite = frames[i];
                float t = i < frameTimes.Length ? frameTimes[i] : 0.1f;
                float punch = i == frames.Length - 1 ? 1.12f : 1.04f;
                for (float e = 0f; e < t; e += Time.deltaTime)
                {
                    float k = e / t;
                    rt.localScale = Vector3.one * Mathf.Lerp(punch, 1f, k);
                    rt.anchoredPosition = Random.insideUnitCircle * (i == frames.Length - 1 ? 14f : 4f) * (1f - k);
                    yield return null;
                }
            }
            // hold on the last frame with a slow red pulse and a nervous jitter
            for (float e = 0f; e < holdTime; e += Time.deltaTime)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(e * 6f);
                face.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), pulse * 0.35f);
                rt.anchoredPosition = Random.insideUnitCircle * 1.5f;
                yield return null;
            }
            face.color = Color.white;
            rt.anchoredPosition = Vector2.zero;
        }
        else yield return new WaitForSeconds(holdTime);

        onFinished.Invoke();
        if (goToLoseScreen && ArcadeSceneManager.Instance != null)
        {
            ArcadeSceneManager.Instance.LoadLoseScreen(0);
            yield break;
        }
        // played directly from the editor: press Interact to restart the scene
        if (retryAction != null)
        {
            retryAction.action.Enable();
            while (!retryAction.action.WasPressedThisFrame()) yield return null;
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
#if UNITY_EDITOR
            else UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        }
    }
}
