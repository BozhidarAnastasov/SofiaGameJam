using System.Collections.Generic;
using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Hiding under the coat. With the coat SELECTED in the hotbar: hold the crawl button (C) and stay
/// still for a moment -> she pulls the coat over herself and becomes a pile of clothes on the floor.
/// The enemy can't see her while she is hidden - unless her flashlight is on: then he can spot the light,
/// but only up close and straight ahead (narrowed vision). The flashlight (D) works under the coat too.
/// Moving, letting go of C or selecting another item uncovers her. Without the coat selected, C just crouches.
///
/// It only works while he hasn't noticed her: if he is already chasing her, the coat won't fool him.
/// Added to the player automatically in every room, so no prefab or scene changes are needed.
public class CoatHide : MonoBehaviour
{
    [Tooltip("How long she has to stay crouched and still before the coat covers her.")]
    [SerializeField] private float stillTime = 0.4f;
    [SerializeField] private string tooLateMessage = "He's already seen you... RUN!";
    [SerializeField] private string firstHideMessage = "Hidden under the coat. Don't move.";
    [SerializeField] private string lightOnMessage = "Your flashlight is on... he could spot the light (D).";

    const string PileSpritePath = "Coat_Hide";     // Assets/Resources/Coat_Hide.png

    private TopDownMovement move;
    private SpriteRenderer pile;
    private float stillFor;
    private bool hidden, refused, toldOnce;
    private readonly List<SpriteRenderer> hiddenSprites = new List<SpriteRenderer>();
    private readonly List<Light2D> hiddenLights = new List<Light2D>();

    /// True while she is under the coat.
    public static bool IsHidden { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        IsHidden = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToPlayer();
    }

    static void OnSceneLoaded(Scene s, LoadSceneMode m) => AttachToPlayer();

    static void AttachToPlayer()
    {
        IsHidden = false;
        var p = Object.FindFirstObjectByType<TopDownMovement>();
        if (p != null && p.GetComponent<CoatHide>() == null) p.gameObject.AddComponent<CoatHide>();
    }

    void Awake() => move = GetComponent<TopDownMovement>();

    void Update()
    {
        // only with the coat selected in the hotbar; otherwise C is just crouching
        if (move == null || !CoatSelected()) { if (hidden) Uncover(); stillFor = 0f; return; }

        bool crouchedStill = move.enabled && move.IsCrawling && move.CurrentSpeed <= 0.01f;
        if (hidden)
        {
            if (!crouchedStill) Uncover();
            return;
        }

        if (!crouchedStill) { stillFor = 0f; refused = false; return; }
        stillFor += Time.deltaTime;
        if (stillFor < stillTime || refused) return;

        if (EnemyIsOnHer())
        {
            refused = true;                                  // say it once per attempt
            Debug.Log("[CoatHide] Tried to hide, but he's already chasing her: no effect.", this);
            if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(tooLateMessage);
            return;
        }
        Cover();
    }

    static bool CoatSelected() => GameProgress.HasCoat && GameProgress.SelectedItemName().StartsWith("Coat");

    static bool EnemyIsOnHer()
    {
        foreach (var e in Object.FindObjectsByType<EntityAI>(FindObjectsSortMode.None))
            if (e.isActiveAndEnabled && (e.Current == EntityAI.State.Chasing || e.Current == EntityAI.State.Grabbing))
                return true;
        return false;
    }

    void Cover()
    {
        hidden = IsHidden = true;
        hiddenSprites.Clear(); hiddenLights.Clear();
        SpriteRenderer main = GetComponent<SpriteRenderer>();
        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
            if (r.enabled && r != pile) { r.enabled = false; hiddenSprites.Add(r); }
        // her lights stay on under the coat, so the player can still see where she is

        if (pile == null)
        {
            var go = new GameObject("CoatPile");
            go.transform.SetParent(transform, false);
            pile = go.AddComponent<SpriteRenderer>();
            pile.sprite = Resources.Load<Sprite>(PileSpritePath);
            if (pile.sprite == null) Debug.LogWarning("[CoatHide] Assets/Resources/Coat_Hide.png is missing.", this);
        }
        if (main != null)
        {
            pile.sortingLayerID = main.sortingLayerID; pile.sortingOrder = main.sortingOrder;
            pile.sharedMaterial = main.sharedMaterial; pile.spriteSortPoint = main.spriteSortPoint;
        }
        // undo the player's own scale so the coat keeps its art size
        Vector3 s = transform.lossyScale;
        pile.transform.localScale = new Vector3(s.x != 0 ? 1f / s.x : 1f, s.y != 0 ? 1f / s.y : 1f, 1f);
        pile.enabled = true;

        var flash = GetComponentInChildren<PlayerFlashlight>();
        bool lightOn = flash != null && flash.IsOn;
        Debug.Log($"[CoatHide] She is hidden under the coat (flashlight {(lightOn ? "ON - he can still spot the light up close" : "off")}).", this);
        if (ArcadeMessageService.Instance != null)
        {
            if (lightOn) ArcadeMessageService.Instance.ShowMessage(lightOnMessage);
            else if (!toldOnce) { ArcadeMessageService.Instance.ShowMessage(firstHideMessage); toldOnce = true; }
        }
    }

    void Uncover()
    {
        hidden = IsHidden = false;
        stillFor = 0f;
        foreach (var r in hiddenSprites) if (r != null) r.enabled = true;
        foreach (var l in hiddenLights) if (l != null) l.enabled = true;
        hiddenSprites.Clear(); hiddenLights.Clear();
        if (pile != null) pile.enabled = false;
        Debug.Log("[CoatHide] She threw the coat off.", this);
    }

    void OnDisable() { if (hidden) Uncover(); }
}
