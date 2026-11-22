using UnityEngine;
using UnityEngine.InputSystem;

/// Shows / hides objects depending on a story flag, and keeps them in sync while the room is open.
/// Example (Lobby): the Bed only lies across the hole once StoryFlag.BedDropped is set.
///
/// Put this on its own GameObject (not on one of the targets), because a disabled object
/// can't switch itself back on.
public class ProgressToggle : MonoBehaviour
{
    [SerializeField] private StoryFlag flag = StoryFlag.BedDropped;
    [Tooltip("Active only when the flag is set.")]
    [SerializeField] private GameObject[] showWhenSet;
    [Tooltip("Active only while the flag is NOT set.")]
    [SerializeField] private GameObject[] hideWhenSet;

    [Header("Testing")]
    [Tooltip("Editor / development builds only: press this key to set the flag right away.")]
    [SerializeField] private bool debugKeyEnabled = true;
    [SerializeField] private Key debugKey = Key.F9;

    private int applied = -1;

    void Awake() => Apply();

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (debugKeyEnabled && Keyboard.current != null && Keyboard.current[debugKey].wasPressedThisFrame)
        {
            Debug.Log($"[ProgressToggle] Debug key {debugKey}: setting {flag}.", this);
            GameProgress.Set(flag);
        }
#endif
        Apply();
    }

    void Apply()
    {
        bool on = GameProgress.Get(flag);
        if (applied == (on ? 1 : 0)) return;
        applied = on ? 1 : 0;
        if (showWhenSet != null) foreach (var g in showWhenSet) if (g != null) g.SetActive(on);
        if (hideWhenSet != null) foreach (var g in hideWhenSet) if (g != null) g.SetActive(!on);
        Debug.Log($"[ProgressToggle] '{name}': {flag} is {(on ? "set" : "not set")} -> " +
                  $"{(showWhenSet != null ? showWhenSet.Length : 0)} shown when set, {(hideWhenSet != null ? hideWhenSet.Length : 0)} hidden when set.", this);
    }
}
