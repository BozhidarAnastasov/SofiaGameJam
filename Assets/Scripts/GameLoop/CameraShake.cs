using UnityEngine;

/// Screen shake that works together with RoomCamera's follow: the offset is added after the camera
/// has moved and removed again before it moves next frame, so the follow never drifts.
/// Call CameraShake.Shake(seconds, strength) from anywhere.
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    private Vector3 applied;
    private float until, strength, duration;

    public static void Shake(float seconds, float amount)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var s = cam.GetComponent<CameraShake>();
        if (s == null) s = cam.gameObject.AddComponent<CameraShake>();
        // a stronger / longer shake wins over a weaker one still running
        float left = Mathf.Max(0f, s.until - Time.time);
        if (amount >= s.strength * (left / Mathf.Max(0.01f, s.duration)) || left <= 0f)
        {
            s.strength = amount; s.duration = seconds; s.until = Time.time + seconds;
        }
    }

    void Update()
    {
        transform.position -= applied;          // undo last frame's offset before RoomCamera follows
        applied = Vector3.zero;
    }

    void LateUpdate()
    {
        float left = until - Time.time;
        if (left <= 0f) return;
        float k = Mathf.Clamp01(left / Mathf.Max(0.01f, duration));
        applied = (Vector3)(Random.insideUnitCircle * strength * k);
        transform.position += applied;
    }
}
