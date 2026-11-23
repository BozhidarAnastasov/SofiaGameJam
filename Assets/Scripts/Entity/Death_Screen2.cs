using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Death_Screen2 : MonoBehaviour
{
    [Header("Your Hierarchy Objects")]
    [SerializeField] private GameObject deadScreenPanel; // Drag DeadScreen_Panel here
    [SerializeField] private Image faceBase;             // Drag Face_base here
    [SerializeField] private Image faceBlend;            // Drag Face_Blend here
    [SerializeField] private GameObject youDiedGraphic;  // Drag YouDied here
    [SerializeField] private GameObject txtYouDied;      // Drag Txt_YouDied here
    [SerializeField] private GameObject deadBtnCanvas;   // Drag Dead_BTN_Canva here

    [Header("Buttons Inside Dead_BTN_Canva")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Lunge / Jumpscare Animation")]
    [Tooltip("Optional: Drop lunge frames here if animating Face_base, or leave empty to use your existing sprite on Face_base & Face_Blend.")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float[] frameTimes = { 0.35f, 0.18f, 0.12f, 0.10f };
    [SerializeField] private float scareDuration = 1.2f;
    [SerializeField] private float textDelay = 0.4f;

    [Header("Sound (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stinger;

    public UnityEvent onShown;
    public UnityEvent onFinished;

    private bool shown;

    private void Awake()
    {
        // Auto-wire Retry and Main Menu buttons (zero keybinds)
        BindButton(retryButton, RetryLevel);
        BindButton(mainMenuButton, GoToMainMenu);

        // Hide everything cleanly at scene start
        if (deadScreenPanel != null) deadScreenPanel.SetActive(false);
        if (youDiedGraphic != null) youDiedGraphic.SetActive(false);
        if (txtYouDied != null) txtYouDied.SetActive(false);
        if (deadBtnCanvas != null) deadBtnCanvas.SetActive(false);
    }

    private void OnDestroy()
    {
        if (retryButton != null) retryButton.onClick.RemoveListener(RetryLevel);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(GoToMainMenu);
    }

    public void Show()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (shown) return;
        shown = true;

        // Unlock cursor so the player can click the Dead_BTN_Canva buttons
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        StartCoroutine(RunDeathSequence());
    }

    private IEnumerator RunDeathSequence()
    {
        // Freeze gameplay while keeping UI animation running on unscaled time
        Time.timeScale = 0f;

        if (deadScreenPanel != null) deadScreenPanel.SetActive(true);
        if (faceBase != null) faceBase.gameObject.SetActive(false);
        if (faceBlend != null) faceBlend.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(0.2f);

        if (audioSource != null && stinger != null)
        {
            audioSource.PlayOneShot(stinger);
        }
        else GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.youDied : null);

        onShown?.Invoke();

        // 1. Show & jolt Face_base (supports sprite array OR single assigned sprite)
        if (faceBase != null)
        {
            faceBase.gameObject.SetActive(true);
            RectTransform baseRect = faceBase.rectTransform;

            if (frames != null && frames.Length > 0)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    if (frames[i] != null) faceBase.sprite = frames[i];
                    float t = i < frameTimes.Length ? frameTimes[i] : 0.1f;
                    bool last = (i == frames.Length - 1);
                    float punch = last ? 1.12f : 1.04f;
                    float shake = last ? 14f : 4f;

                    for (float e = 0f; e < t; e += Time.unscaledDeltaTime)
                    {
                        float k = e / t;
                        baseRect.localScale = Vector3.one * Mathf.Lerp(punch, 1f, k);
                        baseRect.anchoredPosition = Random.insideUnitCircle * shake * (1f - k);
                        yield return null;
                    }
                }
            }
            else
            {
                // Single sprite punch-in jolt on Face_base
                for (float e = 0f; e < 0.25f; e += Time.unscaledDeltaTime)
                {
                    float k = e / 0.25f;
                    baseRect.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, k);
                    baseRect.anchoredPosition = Random.insideUnitCircle * 12f * (1f - k);
                    yield return null;
                }
            }

            baseRect.localScale = Vector3.one;
            baseRect.anchoredPosition = Vector2.zero;
        }

        // 2. Pulse Face_Blend over Face_base (eerie red/blend overlay + jitter)
        if (faceBlend != null)
        {
            faceBlend.gameObject.SetActive(true);
            RectTransform blendRect = faceBlend.rectTransform;
            Color baseBlendColor = faceBlend.color;

            for (float e = 0f; e < scareDuration; e += Time.unscaledDeltaTime)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(e * 8f);
                Color c = baseBlendColor;
                c.a = Mathf.Lerp(0.15f, 0.75f, pulse);
                faceBlend.color = c;

                blendRect.anchoredPosition = Random.insideUnitCircle * 2f;
                if (faceBase != null) faceBase.rectTransform.anchoredPosition = Random.insideUnitCircle * 1.2f;

                yield return null;
            }

            blendRect.anchoredPosition = Vector2.zero;
            if (faceBase != null) faceBase.rectTransform.anchoredPosition = Vector2.zero;
        }
        else
        {
            yield return new WaitForSecondsRealtime(scareDuration);
        }

        // 3. Reveal "YouDied" banner & "Txt_YouDied"
        if (youDiedGraphic != null) youDiedGraphic.SetActive(true);
        if (txtYouDied != null) txtYouDied.SetActive(true);

        yield return new WaitForSecondsRealtime(textDelay);

        // 4. Reveal Dead_BTN_Canva so player can click Retry or Main Menu
        if (deadBtnCanvas != null) deadBtnCanvas.SetActive(true);

        onFinished?.Invoke();

        // no mouse needed: arcade A/Start = Retry, B/Exit = Main Menu
        yield return DeathScreenArcadeInput.WaitForChoice(RetryLevel, GoToMainMenu);
    }

    public void RetryLevel()
    {
        GameAudio.UiClick();
        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.name);
    }

    public void GoToMainMenu()
    {
        GameAudio.UiClick();
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName)) SceneManager.LoadScene(mainMenuSceneName);
        else SceneManager.LoadScene(0);
    }

    private void BindButton(Button btn, UnityAction action)
    {
        if (btn == null) return;
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);

        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
    }
}