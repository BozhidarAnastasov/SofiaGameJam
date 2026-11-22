using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class AudioSettingsManager : MonoBehaviour
{
    [Header("Panels & Back Button")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject previousPanel;
    [SerializeField] private Button backButton;

    [Header("UI Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Direct Audio Sources (Drag your Music object here!)")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource[] sfxSources;

    [Header("Optional Click Sound")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip clickClip;

    [Header("Optional Audio Mixer (Leave empty if using Direct Audio Sources)")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private string masterParam = "MasterVolume";
    [SerializeField] private string musicParam = "MusicVolume";
    [SerializeField] private string sfxParam = "SFXVolume";

    private const string MasterPrefKey = "Pref_MasterVol";
    private const string MusicPrefKey = "Pref_MusicVol";
    private const string SfxPrefKey = "Pref_SFXVol";

    private void Awake()
    {
        AutoFindAudioSourcesIfEmpty();

        SetupSlider(masterSlider);
        SetupSlider(musicSlider);
        SetupSlider(sfxSlider);

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(BackToGame);
            backButton.onClick.AddListener(BackToGame);
        }
    }

    private void Start()
    {
        float masterVol = PlayerPrefs.GetFloat(MasterPrefKey, 1f);
        float musicVol = PlayerPrefs.GetFloat(MusicPrefKey, 0.8f);
        float sfxVol = PlayerPrefs.GetFloat(SfxPrefKey, 1f);

        if (masterSlider != null)
        {
            masterSlider.SetValueWithoutNotify(masterVol);
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(musicVol);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(sfxVol);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        ApplyMaster(masterVol);
        ApplyMusic(musicVol);
        ApplySFX(sfxVol);
    }

    private void OnDestroy()
    {
        if (backButton != null) backButton.onClick.RemoveListener(BackToGame);
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(SetMasterVolume);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);
    }

    public void SetMasterVolume(float value)
    {
        ApplyMaster(value);
        PlayerPrefs.SetFloat(MasterPrefKey, value);
    }

    public void SetMusicVolume(float value)
    {
        ApplyMusic(value);
        PlayerPrefs.SetFloat(MusicPrefKey, value);
    }

    public void SetSFXVolume(float value)
    {
        ApplySFX(value);
        PlayerPrefs.SetFloat(SfxPrefKey, value);
    }

    public void BackToGame()
    {
        if (uiAudioSource != null && clickClip != null)
        {
            uiAudioSource.PlayOneShot(clickClip);
        }
        else GameAudio.UiClick();

        PlayerPrefs.Save();
        Time.timeScale = 1f;

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (previousPanel != null) previousPanel.SetActive(true);
    }

    private void SetupSlider(Slider slider)
    {
        if (slider == null) return;
        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;

    }

    private void ApplyMaster(float value)
    {
        float clamped = Mathf.Clamp01(value);
        AudioListener.volume = clamped;
        ApplyMixerVolume(masterParam, clamped);
    }

    // Ambience/Music slider -> music only (the MainMenu track and any looping background music).
    private void ApplyMusic(float value)
    {
        float clamped = Mathf.Clamp01(value);

        if (musicSource != null) musicSource.volume = clamped;
        foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (IsMusic(s)) s.volume = clamped;

        ApplyMixerVolume(musicParam, clamped);
    }

    // SFX slider -> sound effects only. Never touches music, even if a music source was dragged into
    // an SFX field by mistake. (GameAudio's effects read the saved SFX value themselves.)
    private void ApplySFX(float value)
    {
        float clamped = Mathf.Clamp01(value);

        if (uiAudioSource != null && !IsMusic(uiAudioSource))
        {
            uiAudioSource.volume = clamped;
        }

        if (sfxSources != null)
        {
            for (int i = 0; i < sfxSources.Length; i++)
            {
                if (sfxSources[i] != null && !IsMusic(sfxSources[i]))
                {
                    sfxSources[i].volume = clamped;
                }
            }
        }

        ApplyMixerVolume(sfxParam, clamped);
    }

    /// Music = the assigned music source, or any looping AudioSource that isn't one of the game's
    /// sound-effect loops (footsteps, box scrape...) or GameAudio's own effect players.
    private bool IsMusic(AudioSource s)
    {
        if (s == null) return false;
        if (s == musicSource) return true;
        return s.loop && s.GetComponent<SoundLoop>() == null && s.GetComponent<GameAudio>() == null;
    }

    private void ApplyMixerVolume(string parameterName, float linearValue)
    {
        if (mainMixer == null || string.IsNullOrEmpty(parameterName)) return;

        float dB = Mathf.Log10(Mathf.Clamp(linearValue, 0.0001f, 1f)) * 20f;
        mainMixer.SetFloat(parameterName, dB);
    }

    private void AutoFindAudioSourcesIfEmpty()
    {
        if (musicSource != null) return;

        // Automatically grabs the first looping AudioSource in the scene as background music
        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < allSources.Length; i++)
        {
            if (allSources[i].loop && allSources[i].GetComponent<SoundLoop>() == null)
            {
                musicSource = allSources[i];
                break;
            }
        }
    }
}
