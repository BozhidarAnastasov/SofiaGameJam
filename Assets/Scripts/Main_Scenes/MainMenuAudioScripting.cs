using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class MainMenuAudioAndSettings : MonoBehaviour
{
    [Header("UI Panels & Navigation")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject mainButtonsGroup;
    [SerializeField] private GameObject settingsButton; // Drag BTN_Settings here so focus returns on close

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer gameAudioMixer;

    [Header("Settings Controls & Their Labels Above")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private TMP_Text masterLabel;

    [SerializeField] private Slider ambienceSlider;
    [SerializeField] private TMP_Text ambienceLabel;

    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text sfxLabel;

    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private TMP_Text fullscreenLabel;

    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text closeButtonLabel;

    [Header("Text Highlight Style")]
    [SerializeField] private Color selectedTextColor = new Color(0.65f, 0.08f, 0.10f, 1f); // Deep blood red
    [SerializeField] private Color unselectedTextColor = new Color(0.55f, 0.55f, 0.55f, 1f); // Muted ash grey
    [SerializeField] private float selectedLabelScale = 1.08f;
    [SerializeField] private bool showSelectionBrackets = true;

    [Header("Slider Bar & Handle Highlight Style")]
    [SerializeField] private Color selectedFillColor = new Color(0.65f, 0.08f, 0.10f, 1f);     // Blood red fill bar
    [SerializeField] private Color unselectedFillColor = new Color(0.28f, 0.28f, 0.30f, 0.85f); // Dim grey fill bar
    [SerializeField] private Color selectedHandleColor = new Color(0.88f, 0.85f, 0.80f, 1f);    // Bright bone-white knob
    [SerializeField] private Color unselectedHandleColor = new Color(0.45f, 0.45f, 0.45f, 1f);  // Muted grey knob
    [SerializeField] private Color selectedBgColor = new Color(0.16f, 0.05f, 0.06f, 0.95f);     // Subtle dark red track tint
    [SerializeField] private Color unselectedBgColor = new Color(0.08f, 0.09f, 0.11f, 0.75f);   // Dark neutral track
    [SerializeField] private float selectedSliderScale = 1.05f;
    [SerializeField] private float selectedHandleScale = 1.2f;

    [Header("Slider & Joystick Tuning")]
    [SerializeField] private float sliderStepSpeed = 0.6f;
    [SerializeField] private float stickDeadzone = 0.45f;
    [SerializeField] private float stickRepeatDelay = 0.22f;

    [Header("Menu Audio Sources")]
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource uiSfxSource;
    [SerializeField] private AudioClip panelOpenSound;
    [SerializeField] private AudioClip panelCloseSound;

    private const string MASTER_KEY = "MasterVol";
    private const string AMBIENCE_KEY = "AmbienceVol";
    private const string SFX_KEY = "SFXVol";

    private class SettingsEntry
    {
        public Selectable control;
        public TMP_Text label;
        public string originalText;

        // Cached visual parts for Sliders / Toggles
        public Image sliderFill;
        public Image sliderHandle;
        public Image sliderBackground;
        public Image toggleCheckmark;
        public Image toggleBackground;
    }

    private readonly List<SettingsEntry> entries = new List<SettingsEntry>();
    private int selectedIndex = 0;

    private ArcadeControls controls;
    private float nextVerticalMoveTime = 0f;
    private bool stickReturnedVertical = true;
    private Canvas parentCanvas;

    private void Awake()
    {
        controls = new ArcadeControls();
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = FindFirstObjectByType<Canvas>();

        RegisterEntry(masterSlider, masterLabel);
        RegisterEntry(ambienceSlider, ambienceLabel);
        RegisterEntry(sfxSlider, sfxLabel);
        RegisterEntry(fullscreenToggle, fullscreenLabel);

        if (closeButton != null)
        {
            if (closeButtonLabel == null)
                closeButtonLabel = closeButton.GetComponentInChildren<TMP_Text>(true);

            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(CloseSettings);
            RegisterEntry(closeButton, closeButtonLabel);
        }

        // Closes Settings with Escape, 'Y', Gamepad Select, or Arcade ButtonExit (button9)
        controls.Arcade.ButtonExit.performed += _ =>
        {
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                CloseSettings();
            }
        };
    }

    private void RegisterEntry(Selectable ctrl, TMP_Text lbl)
    {
        if (ctrl == null) return;

        // Disable built-in navigation & color tint so our custom highlight colors stay 100% accurate
        ctrl.navigation = new Navigation { mode = Navigation.Mode.None };
        ctrl.transition = Selectable.Transition.None;

        string cleanText = "";
        if (lbl != null)
        {
            cleanText = lbl.text;
            lbl.color = unselectedTextColor;
        }

        var entry = new SettingsEntry
        {
            control = ctrl,
            label = lbl,
            originalText = cleanText
        };

        // Auto-grab Slider Fill, Handle, and Background track images
        if (ctrl is Slider slider)
        {
            if (slider.fillRect != null)
                entry.sliderFill = slider.fillRect.GetComponent<Image>();

            if (slider.handleRect != null)
                entry.sliderHandle = slider.handleRect.GetComponent<Image>();

            Transform bgTransform = slider.transform.Find("Background");
            if (bgTransform != null)
                entry.sliderBackground = bgTransform.GetComponent<Image>();
        }
        // Auto-grab Toggle box & checkmark if Fullscreen toggle is used
        else if (ctrl is Toggle toggle)
        {
            entry.toggleBackground = toggle.targetGraphic as Image;
            entry.toggleCheckmark = toggle.graphic as Image;
        }

        entries.Add(entry);
    }

    private void OnEnable()
    {
        controls.Arcade.Enable();
    }

    private void OnDisable()
    {
        controls.Arcade.Disable();
    }

    private void OnDestroy()
    {
        controls.Dispose();
    }

    private void Start()
    {
        float masterVal = PlayerPrefs.GetFloat(MASTER_KEY, 0.8f);
        float ambienceVal = PlayerPrefs.GetFloat(AMBIENCE_KEY, 0.8f);
        float sfxVal = PlayerPrefs.GetFloat(SFX_KEY, 0.8f);

        if (masterSlider != null)
        {
            masterSlider.value = masterVal;
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (ambienceSlider != null)
        {
            ambienceSlider.value = ambienceVal;
            ambienceSlider.onValueChanged.AddListener(SetAmbienceVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = sfxVal;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        SetMasterVolume(masterVal);
        SetAmbienceVolume(ambienceVal);
        SetSFXVolume(sfxVal);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (ambienceSource != null && !ambienceSource.isPlaying) ambienceSource.Play();
    }

    private void Update()
    {
        if (settingsPanel == null || !settingsPanel.activeInHierarchy || entries.Count == 0)
            return;

        HandleMouseHover();
        HandleKeyboardAndArcadeNavigation();
    }

    // 1. MOUSE: Highlights both the slider bar and the text above when hovering or dragging
    private void HandleMouseHover()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePos = mouse.position.ReadValue();
        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        for (int i = 0; i < entries.Count; i++)
        {
            RectTransform ctrlRect = entries[i].control.GetComponent<RectTransform>();
            RectTransform lblRect = entries[i].label != null ? entries[i].label.GetComponent<RectTransform>() : null;

            bool overCtrl = ctrlRect != null && RectTransformUtility.RectangleContainsScreenPoint(ctrlRect, mousePos, uiCam);
            bool overLbl = lblRect != null && RectTransformUtility.RectangleContainsScreenPoint(lblRect, mousePos, uiCam);

            if (overCtrl || overLbl)
            {
                if (selectedIndex != i)
                    HighlightEntry(i);

                if (entries[i].control == closeButton && mouse.leftButton.wasPressedThisFrame)
                {
                    CloseSettings();
                }
                break;
            }
        }
    }

    // 2. KEYBOARD (Arrow Keys + Enter) & ARCADE STICK / GAMEPAD
    private void HandleKeyboardAndArcadeNavigation()
    {
        Keyboard kb = Keyboard.current;

        // Vertical Selection (Up / Down Arrows)
        if (kb != null)
        {
            if (kb.upArrowKey.wasPressedThisFrame)
                SwitchEntry(-1);
            else if (kb.downArrowKey.wasPressedThisFrame)
                SwitchEntry(1);
        }
        

        Vector2 stick = GetJoystickOrGamepadVector();
        if (Mathf.Abs(stick.y) < 0.2f)
        {
            stickReturnedVertical = true;
        }
        else if (stickReturnedVertical || Time.unscaledTime >= nextVerticalMoveTime)
        {
            if (stick.y > stickDeadzone)
            {
                stickReturnedVertical = false;
                nextVerticalMoveTime = Time.unscaledTime + stickRepeatDelay;
                SwitchEntry(-1);
            }
            else if (stick.y < -stickDeadzone)
            {
                stickReturnedVertical = false;
                nextVerticalMoveTime = Time.unscaledTime + stickRepeatDelay;
                SwitchEntry(1);
            }
        }

        // Horizontal Slider Adjustment (Left / Right Arrows or Arcade Stick X)
        Selectable activeControl = entries[selectedIndex].control;
        if (activeControl is Slider activeSlider)
        {
            float horizontalInput = 0f;

            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) horizontalInput -= 1f;
                if (kb.rightArrowKey.isPressed) horizontalInput += 1f;
            }

            if (Mathf.Abs(stick.x) > stickDeadzone)
            {
                horizontalInput = Mathf.Sign(stick.x);
            }

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                activeSlider.value = Mathf.Clamp(
                    activeSlider.value + horizontalInput * sliderStepSpeed * Time.unscaledDeltaTime,
                    activeSlider.minValue,
                    activeSlider.maxValue
                );
            }
        }

        // Confirm Action for Toggle or Close Button (Enter / Arcade ButtonA / ButtonStart)
       bool confirmPressed = (kb != null && (kb.enterKey.wasPressedThisFrame ||
                                              kb.numpadEnterKey.wasPressedThisFrame ||
                                              kb.spaceKey.wasPressedThisFrame)) ||
                              controls.Arcade.ButtonA.WasPressedThisFrame() ||
                              controls.Arcade.ButtonStart.WasPressedThisFrame() ||
                              (Joystick.current != null && Joystick.current.trigger.wasPressedThisFrame) ||
                              (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                                                           Gamepad.current.buttonNorth.wasPressedThisFrame ||
                                                           Gamepad.current.startButton.wasPressedThisFrame));

        var startCtrl = controls.Arcade.ButtonStart.activeControl;
        if (controls.Arcade.ButtonStart.WasPressedThisFrame() && (startCtrl == null || !(startCtrl.device is Keyboard)))
            confirmPressed = true;

        var btnACtrl = controls.Arcade.ButtonA.activeControl;
        if (controls.Arcade.ButtonA.WasPressedThisFrame() && (btnACtrl == null || !(btnACtrl.device is Keyboard)))
            confirmPressed = true;

        if (confirmPressed)
        {
            if (activeControl is Toggle toggle)
            {
                toggle.isOn = !toggle.isOn;
            }
            else if (activeControl == closeButton)
            {
                CloseSettings();
            }
        }
    }

    private Vector2 GetJoystickOrGamepadVector()
    {
        Vector2 stick = Vector2.zero;
        if (Joystick.current != null)
            stick = Joystick.current.stick.ReadValue();

        if (Gamepad.current != null && stick.sqrMagnitude < 0.05f)
        {
            Vector2 padStick = Gamepad.current.leftStick.ReadValue();
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            stick = padStick.sqrMagnitude > dpad.sqrMagnitude ? padStick : dpad;
        }
        return stick;
    }

    private void SwitchEntry(int direction)
    {
        if (entries.Count == 0) return;
        int next = (selectedIndex + direction + entries.Count) % entries.Count;
        HighlightEntry(next);
    }

    private void HighlightEntry(int index)
    {
        selectedIndex = index;

        for (int i = 0; i < entries.Count; i++)
        {
            bool isSelected = (i == selectedIndex);
            SettingsEntry item = entries[i];

            // 1. Highlight the TMP Label above
            if (item.label != null)
            {
                item.label.color = isSelected ? selectedTextColor : unselectedTextColor;
                item.label.transform.localScale = isSelected
                    ? new Vector3(selectedLabelScale, selectedLabelScale, 1f)
                    : Vector3.one;

                if (!string.IsNullOrEmpty(item.originalText))
                {
                    item.label.text = (isSelected && showSelectionBrackets)
                        ? $"> {item.originalText} <"
                        : item.originalText;
                }
            }

            // 2. Highlight the Slider itself (Fill bar, Handle knob, Background track & subtle scale)
            if (item.control is Slider)
            {
                item.control.transform.localScale = isSelected
                    ? new Vector3(selectedSliderScale, selectedSliderScale, 1f)
                    : Vector3.one;

                if (item.sliderFill != null)
                    item.sliderFill.color = isSelected ? selectedFillColor : unselectedFillColor;

                if (item.sliderBackground != null)
                    item.sliderBackground.color = isSelected ? selectedBgColor : unselectedBgColor;

                if (item.sliderHandle != null)
                {
                    item.sliderHandle.color = isSelected ? selectedHandleColor : unselectedHandleColor;
                    item.sliderHandle.transform.localScale = isSelected
                        ? new Vector3(selectedHandleScale, selectedHandleScale, 1f)
                        : Vector3.one;
                }
            }
            // 3. Highlight Toggle box & checkmark if present
            else if (item.control is Toggle)
            {
                if (item.toggleBackground != null)
                    item.toggleBackground.color = isSelected ? selectedFillColor : unselectedBgColor;

                if (item.toggleCheckmark != null)
                    item.toggleCheckmark.color = isSelected ? selectedHandleColor : selectedFillColor;
            }
            // 4. Highlight Back / Close Button (scales the button itself + tints its graphic if no TMP text exists)
            else if (item.control is Button btn)
            {
                btn.transform.localScale = isSelected
                    ? new Vector3(selectedLabelScale, selectedLabelScale, 1f)
                    : Vector3.one;

                if (item.label == null && btn.targetGraphic != null)
                {
                    btn.targetGraphic.color = isSelected ? selectedTextColor : unselectedTextColor;
                }
            }
        }


        if (EventSystem.current != null && entries[selectedIndex].control != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(entries[selectedIndex].control.gameObject);
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        if (mainButtonsGroup != null) mainButtonsGroup.SetActive(false);

        nextVerticalMoveTime = Time.unscaledTime + 0.2f;
        stickReturnedVertical = false;

        if (entries.Count > 0)
            HighlightEntry(0);

        if (uiSfxSource != null && panelOpenSound != null)
            uiSfxSource.PlayOneShot(panelOpenSound);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainButtonsGroup != null) mainButtonsGroup.SetActive(true);

        if (settingsButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(settingsButton);
        }

        PlayerPrefs.Save();

        if (uiSfxSource != null && panelCloseSound != null)
            uiSfxSource.PlayOneShot(panelCloseSound);
    }

    public void SetMasterVolume(float sliderValue) => ApplyMixerVolume(MASTER_KEY, sliderValue);
    public void SetAmbienceVolume(float sliderValue) => ApplyMixerVolume(AMBIENCE_KEY, sliderValue);
    public void SetSFXVolume(float sliderValue) => ApplyMixerVolume(SFX_KEY, sliderValue);

    private void ApplyMixerVolume(string parameterName, float linearValue)
    {
        float safeValue = Mathf.Clamp(linearValue, 0.0001f, 1f);
        float dB = Mathf.Log10(safeValue) * 20f;

        if (gameAudioMixer != null)
            gameAudioMixer.SetFloat(parameterName, dB);

        PlayerPrefs.SetFloat(parameterName, safeValue);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
}