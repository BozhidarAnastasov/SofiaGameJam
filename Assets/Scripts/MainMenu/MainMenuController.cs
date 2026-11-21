using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] private string firstGameScene = "Lobby";

    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("UI Buttons (Drag your 4 buttons here)")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button backButton; // Inside SettingsPanel (leave empty if AudioSettingsManager handles it)
    [SerializeField] private Button quitButton;

    [Header("Menu Audio (Optional)")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip hoverClip;

    private void Awake()
    {
        BindButton(playButton, PlayGame);
        BindButton(settingsButton, OpenSettings);
        BindButton(backButton, CloseSettings);
        BindButton(quitButton, QuitGame);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(PlayGame);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
        if (backButton != null) backButton.onClick.RemoveListener(CloseSettings);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
    }

    private void BindButton(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn == null) return;

        // Clear duplicate listeners and wire click in code
        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);

        // Lock out keyboard/controller navigation (mouse only, zero keybinds)
        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        // Auto-wire pointer hover sound
        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        enterEntry.callback.AddListener(_ =>
        {
            if (btn.interactable) PlayHoverSound();
        });
        trigger.triggers.Add(enterEntry);
    }

    public void PlayGame()
    {
        PlayClickSound();
        SceneManager.LoadScene(firstGameScene);
    }

    public void OpenSettings()
    {
        PlayClickSound();
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        PlayClickSound();
        ShowMainMenu();
    }

    public void QuitGame()
    {
        PlayClickSound();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    private void ShowMainMenu()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
    }

    public void PlayHoverSound()
    {
        if (uiAudioSource != null && hoverClip != null)
        {
            uiAudioSource.PlayOneShot(hoverClip);
        }
    }

    private void PlayClickSound()
    {
        if (uiAudioSource != null && clickClip != null)
        {
            uiAudioSource.PlayOneShot(clickClip);
        }
    }
}