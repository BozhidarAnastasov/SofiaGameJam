using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Fade between rooms: the screen darkens to black, the next scene loads, then it brightens again.
/// Created automatically the first time a door uses it; survives the scene change.
public class RoomTransition : MonoBehaviour
{
    public static bool IsBusy => instance != null && instance.busy;

    /// Set by DoorExit for the stairs: no door closing behind her when the next room fades in.
    public static bool SilentArrival { get; set; }

    static RoomTransition instance;

    [SerializeField] private float fadeOutTime = 0.8f;
    [SerializeField] private float holdBlack = 0.3f;
    [SerializeField] private float fadeInTime = 1.0f;

    private Image black;
    private bool busy;

    /// Fade to black, load 'sceneName', fade back in.
    public static void FaintAndLoad(string sceneName, Transform player)
    {
        if (instance == null) Create();
        if (instance.busy) return;
        instance.StartCoroutine(instance.Run(sceneName, player));
    }

    static void Create()
    {
        var go = new GameObject("RoomTransition");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RoomTransition>();
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var g = new GameObject("Black", typeof(RectTransform));
        g.transform.SetParent(go.transform, false);
        var rt = (RectTransform)g.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        instance.black = g.AddComponent<Image>();
        instance.black.raycastTarget = false;
        instance.SetAlpha(0f);
    }

    void SetAlpha(float a) => black.color = new Color(0f, 0f, 0f, a);

    IEnumerator Run(string sceneName, Transform player)
    {
        busy = true;
        Debug.Log($"[RoomTransition] 1/4 Fading out of '{SceneManager.GetActiveScene().name}' -> '{sceneName}' (entry '{RoomSpawnPoint.NextEntry}').");
        var move = player != null ? player.GetComponent<TopDownMovement>() : null;
        if (move != null) move.CanMove = false;

        // darker...
        for (float e = 0f; e < fadeOutTime; e += Time.deltaTime)
        {
            SetAlpha(Mathf.SmoothStep(0f, 1f, e / fadeOutTime));
            yield return null;
        }
        SetAlpha(1f);
        yield return new WaitForSeconds(holdBlack);

        // ...load in the dark...
        Debug.Log($"[RoomTransition] 2/4 Black. Loading '{sceneName}'...");
        FindFirstObjectByType<Inventory>()?.SaveForRoom(sceneName);
        var load = SceneManager.LoadSceneAsync(sceneName);
        if (load == null) { Debug.LogError($"[RoomTransition] Couldn't load '{sceneName}' (not in Build Settings?)."); Inventory.ClearRoomSave(); SetAlpha(0f); if (move != null) move.CanMove = true; busy = false; yield break; }
        while (!load.isDone) yield return null;
        yield return null;                                   // one frame so the new scene's Awake/Start have run
        Debug.Log($"[RoomTransition] 3/4 Loaded '{SceneManager.GetActiveScene().name}'. Placing her...");
        RoomSpawnPoint.PlacePending();                       // arrival point for the door she came through
        var newMove = FindFirstObjectByType<TopDownMovement>();
        if (newMove != null) newMove.CanMove = false;
        else Debug.LogWarning("[RoomTransition] The new scene has no Player (TopDownMovement)!");
        if (!SilentArrival) GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.doorClose : null, 0.8f);   // the door shuts behind her
        SilentArrival = false;

        // ...and brighter
        for (float e = 0f; e < fadeInTime; e += Time.deltaTime)
        {
            SetAlpha(1f - Mathf.SmoothStep(0f, 1f, e / fadeInTime));
            yield return null;
        }
        SetAlpha(0f);
        if (newMove != null) newMove.CanMove = true;
        Debug.Log($"[RoomTransition] 4/4 Faded into '{sceneName}', she can move." + (newMove != null ? $" Position {newMove.transform.position}." : ""));
        busy = false;
    }
}
