using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Plays the game's sound effects from Resources/GameSounds. Nothing to place in a scene:
/// it creates itself the first time a sound is played and survives room changes (so a door
/// sound keeps playing through the fade). Respects the SFX slider from the settings menu.
///
///   GameAudio.Play(GameAudio.Sounds.doorOpen);
///   GameAudio.Duck();   // pull ambience/loops down for a moment (the stab)
public class GameAudio : MonoBehaviour
{
    const string SfxPrefKey = "Pref_SFXVol";      // same key as AudioSettingsManager
    const int PoolSize = 10;
    const float RepeatGuard = 0.08f;              // same clip twice this fast = one sound (blend-tree events fire per clip)

    static GameAudio instance;
    static GameSounds sounds;
    static bool triedLoad;

    /// The sound library (Assets/Resources/GameSounds.asset). Null if it is missing.
    public static GameSounds Sounds
    {
        get
        {
            if (sounds == null && !triedLoad)
            {
                triedLoad = true;
                sounds = Resources.Load<GameSounds>("GameSounds");
                if (sounds == null) Debug.LogWarning("[GameAudio] Resources/GameSounds.asset not found: no sound effects.");
            }
            return sounds;
        }
    }

    /// Current ambience multiplier (1 = normal, lower while ducked). SoundLoops follow it.
    public static float DuckFactor { get; private set; } = 1f;

    public static float SfxVolume => PlayerPrefs.GetFloat(SfxPrefKey, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; sounds = null; triedLoad = false; DuckFactor = 1f; }

    private AudioSource[] pool;
    private GameObject[] owners = new GameObject[PoolSize];
    private int next;
    private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
    private Coroutine duckRoutine;
    private readonly Dictionary<AudioSource, float> duckedSources = new Dictionary<AudioSource, float>();

    static GameAudio Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("GameAudio");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameAudio>();
            }
            return instance;
        }
    }

    void Awake()
    {
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;            // 2D: the camera jumps room to room, so keep it simple and clear
            pool[i] = s;
        }
    }

    /// Play one clip from the slot (a random one if it holds several), with a slight pitch change
    /// so repeats don't sound copy-pasted. Returns the clip played (or null).
    /// 'owner' tags the sound (e.g. the girl) so StopOwner can cut it off later.
    public static AudioClip Play(AudioClip[] slot, float volume = 1f, float pitchJitter = 0.04f, GameObject owner = null)
    {
        AudioClip clip = Pick(slot);
        if (clip == null) return null;
        Instance.PlayClip(clip, volume, 1f + Random.Range(-pitchJitter, pitchJitter), owner);
        return clip;
    }

    /// Play at normal pitch and hand back the source, so a scene can follow the clip's own
    /// playback time (source.time) and land visuals exactly on its hits.
    public static AudioSource PlayTracked(AudioClip[] slot, float volume = 1f, GameObject owner = null)
    {
        AudioClip clip = Pick(slot);
        return clip == null ? null : Instance.PlayClip(clip, volume, 1f, owner);
    }

    /// Stop every one-shot that was played for this owner (e.g. her breathing when she dies).
    public static void StopOwner(GameObject owner)
    {
        if (instance == null || owner == null) return;
        var a = instance;
        for (int i = 0; i < a.pool.Length; i++)
            if (a.owners[i] == owner && a.pool[i].isPlaying) { a.pool[i].Stop(); a.owners[i] = null; }
    }

    /// Same as Play, by slot name (see GameSounds.Get). Used by animation events.
    public static AudioClip Play(string id, float volume = 1f)
    {
        var lib = Sounds;
        if (lib == null) return null;
        var slot = lib.Get(id);
        if (slot == null) { Debug.LogWarning($"[GameAudio] Unknown sound '{id}'."); return null; }
        return Play(slot, volume);
    }

    /// Play a specific clip (e.g. one assigned by hand in the Inspector).
    public static void PlayClip(AudioClip clip, float volume = 1f) { if (clip != null) Instance.PlayClip(clip, volume, 1f); }

    public static AudioClip Pick(AudioClip[] slot)
    {
        if (slot == null || slot.Length == 0) return null;
        int start = Random.Range(0, slot.Length);
        for (int i = 0; i < slot.Length; i++)
        {
            var c = slot[(start + i) % slot.Length];
            if (c != null) return c;
        }
        return null;
    }

    AudioSource PlayClip(AudioClip clip, float volume, float pitch, GameObject owner = null)
    {
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(clip, out float t) && now - t < RepeatGuard) return null;
        lastPlayed[clip] = now;

        // a free source if there is one, else the oldest
        AudioSource s = null;
        for (int i = 0; i < pool.Length; i++)
        {
            var c = pool[(next + i) % pool.Length];
            if (!c.isPlaying) { s = c; break; }
        }
        if (s == null) s = pool[next];
        int index = System.Array.IndexOf(pool, s);
        next = (index + 1) % pool.Length;

        s.Stop();
        s.clip = clip;
        s.pitch = pitch;
        s.volume = Mathf.Clamp01(volume * SfxVolume);
        s.Play();
        owners[index] = owner;
        return s;
    }

    /// Pull the ambience (looping sources: footsteps, hums, music) down to 'depth', hold, then let it back up.
    /// Runs on unscaled time, so it also works while the death screen has paused the game.
    public static void Duck(float depth = 0.15f, float attack = 0.04f, float hold = 1.2f, float release = 1.5f)
    {
        var a = Instance;
        if (a.duckRoutine != null) a.StopCoroutine(a.duckRoutine);
        a.duckRoutine = a.StartCoroutine(a.DuckRoutine(depth, attack, hold, release));
    }

    IEnumerator DuckRoutine(float depth, float attack, float hold, float release)
    {
        // other looping sources in the scene (music / ambience) that aren't SoundLoops
        foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (s == null || !s.loop || !s.isPlaying || s.gameObject == gameObject) continue;
            if (s.GetComponent<SoundLoop>() != null) continue;
            if (!duckedSources.ContainsKey(s)) duckedSources[s] = s.volume;
        }

        float from = DuckFactor;
        for (float e = 0f; e < attack; e += Time.unscaledDeltaTime) { Apply(Mathf.Lerp(from, depth, e / attack)); yield return null; }
        Apply(depth);
        yield return new WaitForSecondsRealtime(hold);
        for (float e = 0f; e < release; e += Time.unscaledDeltaTime) { Apply(Mathf.Lerp(depth, 1f, e / release)); yield return null; }
        Apply(1f);
        duckedSources.Clear();
        duckRoutine = null;
    }

    void Apply(float factor)
    {
        DuckFactor = factor;
        foreach (var kv in duckedSources) if (kv.Key != null) kv.Key.volume = kv.Value * factor;
    }
}
