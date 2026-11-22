using UnityEngine;

/// A looping sound on a character (footsteps). Added from code; set Clip / Playing / Volume / Pitch
/// every frame and it fades in and out smoothly, starts at a random point in the loop (so it never
/// sounds like the same take restarting), follows the SFX slider and dips when GameAudio ducks.
public class SoundLoop : MonoBehaviour
{
    public float fadeTime = 0.12f;

    public bool Playing { get; set; }
    public float Volume { get; set; } = 1f;
    public float Pitch { get; set; } = 1f;

    private AudioSource source;
    private float level;

    public static SoundLoop Create(GameObject owner)
    {
        var loop = owner.AddComponent<SoundLoop>();
        loop.source = owner.AddComponent<AudioSource>();
        loop.source.playOnAwake = false;
        loop.source.loop = true;
        loop.source.spatialBlend = 0f;
        loop.source.volume = 0f;
        return loop;
    }

    /// Change the looping clip (does nothing if it's the same one).
    public void SetClip(AudioClip clip)
    {
        if (source == null || source.clip == clip) return;
        bool was = source.isPlaying;
        source.Stop();
        source.clip = clip;
        if (was && clip != null) StartAtRandomPoint();
    }

    /// Cut it off right away (e.g. when the psycho grabs her).
    public void StopNow() { Playing = false; level = 0f; if (source != null) { source.volume = 0f; source.Stop(); } }

    void StartAtRandomPoint()
    {
        if (source.clip == null) return;
        source.time = Random.Range(0f, source.clip.length * 0.9f);
        source.Play();
    }

    void Update()
    {
        if (source == null) return;
        float target = Playing && source.clip != null ? 1f : 0f;
        level = fadeTime <= 0f ? target : Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / fadeTime);

        if (level > 0f && !source.isPlaying) StartAtRandomPoint();
        else if (level <= 0f && source.isPlaying) source.Pause();

        source.volume = Mathf.Clamp01(level * Volume * GameAudio.SfxVolume * GameAudio.DuckFactor);
        source.pitch = Pitch;
    }
}
