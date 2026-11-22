using UnityEngine;

/// Speed correction for a room that was built at another scale than the others (Floor2: zoomed-in
/// camera, smaller girl), so walking, running, crawling and the psycho don't feel too fast there.
/// Put one in the room and set Factor (1 = normal). Rooms without it run at normal speed.
public class RoomPace : MonoBehaviour
{
    /// Multiplier the girl's and the psycho's speeds use in the current room.
    public static float Factor { get; private set; } = 1f;

    [Tooltip("1 = normal speed. Floor2 uses less because its camera is zoomed in and the girl is smaller.")]
    [SerializeField, Range(0.2f, 1.5f)] private float factor = 0.6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() => Factor = 1f;

    void Awake()
    {
        Factor = factor;
        Debug.Log($"[RoomPace] '{gameObject.scene.name}' runs at {factor:0.##}x speed.", this);
    }

    void OnValidate() { if (Application.isPlaying && isActiveAndEnabled) Factor = factor; }

    void OnDestroy() => Factor = 1f;
}
