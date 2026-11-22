using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Navigation")]
    [SerializeField] private string firstGameScene = "Lobby";

    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;

    [Header("Menu Navigation")]
    [SerializeField] private InputActionAsset arcadeActions;
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Selectable[] settingsOptions;

    [Header("Menu Audio (Optional)")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip clickClip;
    [SerializeField] private AudioClip hoverClip;

    private InputAction startAction;
    private InputAction exitAction;
    private InputAction submitAction;
    private bool enabledStart;
    private bool enabledExit;

    private void Start()
    {
        Time.timeScale = 1f;
        ConfigureNavigation(new Selectable[] { playButton, settingsButton, controlsButton, quitButton });
        ConfigureNavigation(settingsOptions);
        ConfigureNavigation(new Selectable[] { controlsBackButton });
        ShowPanel(mainMenuPanel, playButton);
    }

    private void OnEnable()
    {
        startAction = arcadeActions?.FindAction("Arcade/ButtonStart");
        exitAction = arcadeActions?.FindAction("Arcade/ButtonExit");
        submitAction = arcadeActions?.FindAction("Arcade/ButtonA");
        enabledStart = startAction != null && !startAction.enabled;
        enabledExit = exitAction != null && !exitAction.enabled;
        if (enabledStart) startAction.Enable();
        if (enabledExit) exitAction.Enable();
    }

    private void OnDisable()
    {
        if (enabledStart) startAction.Disable();
        if (enabledExit) exitAction.Disable();
    }

    private void Update()
    {
        if (exitAction != null && exitAction.WasPressedThisFrame())
        {
            if (controlsPanel.activeSelf) CloseControls();
            else if (settingsPanel.activeSelf) settingsBackButton.onClick.Invoke();
        }
        else if (startAction != null && startAction.WasPressedThisFrame()
            && (submitAction == null || !submitAction.WasPressedThisFrame()))
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null)
                ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);
        }
    }

    private void ConfigureNavigation(Selectable[] options)
    {
        for (int i = 0; i < options.Length; i++)
        {
            Selectable option = options[i];
            option.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = options[(i + options.Length - 1) % options.Length],
                selectOnDown = options[(i + 1) % options.Length]
            };
            ColorBlock colors = option.colors;
            colors.selectedColor = new Color(1f, 0.78f, 0.48f);
            option.colors = colors;

            if (hoverClip == null || !(option is Button)) continue;
            var trigger = option.GetComponent<EventTrigger>();
            if (trigger == null) trigger = option.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ =>
            {
                if (option.IsInteractable()) PlayHoverSound();
            });
            trigger.triggers.Add(entry);
        }
    }

    public void PlayGame()
    {
        PlayClickSound();
        SceneManager.LoadScene(firstGameScene);
    }

    public void OpenSettings()
    {
        PlayClickSound();
        ShowPanel(settingsPanel, settingsOptions[0]);
    }

    public void CloseSettings()
    {
        PlayClickSound();
        ShowPanel(mainMenuPanel, settingsButton);
    }

    public void OpenControls()
    {
        PlayClickSound();
        ShowPanel(controlsPanel, controlsBackButton);
    }

    public void CloseControls()
    {
        PlayClickSound();
        ShowPanel(mainMenuPanel, controlsButton);
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

    private void ShowPanel(GameObject panel, Selectable selection)
    {
        mainMenuPanel.SetActive(panel == mainMenuPanel);
        settingsPanel.SetActive(panel == settingsPanel);
        controlsPanel.SetActive(panel == controlsPanel);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(selection.gameObject);
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
