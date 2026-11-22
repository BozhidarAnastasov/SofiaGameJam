using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;

/// The finale's door (Lobby -> office). With the key card selected the lock clicks open, but the door
/// is jammed: every press of A shoves it a bit (shake + a bar filling over her head). It gives way after
/// 'pressesNeeded' presses and she gets through. The first shove is loud: he comes down the stairs and
/// walks straight at her, timed so that steady mashing is just enough. If he sees her he charges.
/// Sits on the same object as the door's DoorExit (DoorExit forwards the A presses here).
[RequireComponent(typeof(DoorExit))]
public class JammedDoor : MonoBehaviour
{
    [SerializeField] private int pressesNeeded = 20;
    [Tooltip("Shaken on every shove (the door picture). Empty = only the camera shakes.")]
    [SerializeField] private Transform doorArt;
    [SerializeField] private Transform player;

    [Header("The psycho")]
    [SerializeField] private EntityAI psycho;
    [Tooltip("From the foot of the stairs to the door. He appears at the first point.")]
    [SerializeField] private Transform[] approach;
    [Tooltip("Seconds he needs from the stairs to the door (steady mashing takes ~4-6 s).")]
    [SerializeField] private float arriveAfter = 11f;

    [Header("Text")]
    [SerializeField] private string jammedMessage = "The lock clicks... but it's jammed! Mash A!";
    [SerializeField] private string comingMessage = "Footsteps on the stairs... HURRY!";
    [SerializeField] private string openMessage = "It gives way!";

    private int presses;
    private bool started, opening;
    private SpriteRenderer barBack, barFill;

    /// Forced open already (this run): from then on it's a normal door.
    public bool IsOpen => GameProgress.Get(StoryFlag.OfficeOpen);

    void Awake()
    {
        if (player == null) { var p = FindFirstObjectByType<TopDownMovement>(); if (p != null) player = p.transform; }
    }

    /// One shove (called by DoorExit when A is pressed at the door and the card is selected).
    public void Press()
    {
        if (opening || IsOpen) return;
        presses++;
        if (!started)
        {
            started = true;
            Debug.Log($"[JammedDoor] Jammed - {pressesNeeded} shoves needed. He's coming from the stairs.", this);
            if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(jammedMessage);
            StartCoroutine(HeComes());
        }
        StartCoroutine(Shake(doorArt, 0.12f, 0.06f));
        CameraShake.Shake(0.1f, 0.04f);
        if (player != null && player.TryGetComponent(out DirectionalSpriteAnimator anim)) anim.FaceTowards(transform.position);
        ShowBar((float)presses / pressesNeeded);
        if (presses >= pressesNeeded) StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        opening = true;
        GameProgress.Set(StoryFlag.OfficeOpen);
        Debug.Log("[JammedDoor] The door gives way.", this);
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(openMessage);
        CameraShake.Shake(0.3f, 0.12f);
        yield return Shake(doorArt, 0.25f, 0.12f);
        HideBar();
        GetComponent<DoorExit>().Leave();                      // through, before he gets her
    }

    IEnumerator HeComes()
    {
        if (psycho == null || approach == null || approach.Length == 0) yield break;
        yield return new WaitForSeconds(0.6f);
        if (opening || RoomTransition.IsBusy) yield break;
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(comingMessage);

        float length = 0f;
        for (int i = 1; i < approach.Length; i++) length += Vector2.Distance(approach[i - 1].position, approach[i].position);
        psycho.ScriptSpeed = Mathf.Max(0.5f, length / Mathf.Max(1f, arriveAfter));
        Vector2 first = approach[0].position;
        psycho.ScriptAppear(first, approach.Length > 1 ? (Vector2)approach[1].position - first : Vector2.right);
        Debug.Log($"[JammedDoor] He walks {length:0.0} units to the door in {arriveAfter} s ({psycho.ScriptSpeed:0.00} u/s).", this);
        for (int i = 1; i < approach.Length; i++)
        {
            psycho.ScriptWalkTo(approach[i].position);
            while (!psycho.ScriptArrived || psycho.Hunting)
            {
                if (psycho.Current == EntityAI.State.Grabbing || opening) yield break;
                yield return null;
            }
        }
        // at the door and she's still there: straight at her
        if (!opening && player != null) psycho.ScriptWalkTo(player.position);
    }

    static IEnumerator Shake(Transform t, float time, float amount)
    {
        if (t == null) yield break;
        Vector3 home = t.localPosition;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            t.localPosition = home + (Vector3)(Random.insideUnitCircle * amount * (1f - e / time));
            yield return null;
        }
        t.localPosition = home;
    }

    // a small bar over her head that fills with every shove
    void ShowBar(float k)
    {
        if (player == null) return;
        if (barBack == null)
        {
            barBack = MakeBar("JamBar", new Color(0.05f, 0.05f, 0.05f, 0.85f), 0);
            barFill = MakeBar("JamFill", new Color(0.85f, 0.25f, 0.2f, 1f), 1);
        }
        Vector3 at = player.position + new Vector3(0f, 2.7f * Mathf.Abs(player.lossyScale.y), 0f);
        barBack.transform.position = at;
        barBack.transform.localScale = new Vector3(1.2f, 0.14f, 1f);
        barFill.transform.position = at - new Vector3(0.6f * (1f - k), 0f, 0f);
        barFill.transform.localScale = new Vector3(1.2f * Mathf.Clamp01(k), 0.1f, 1f);
    }

    void HideBar()
    {
        if (barBack != null) Destroy(barBack.gameObject);
        if (barFill != null) Destroy(barFill.gameObject);
    }

    static Sprite pixel;
    SpriteRenderer MakeBar(string n, Color c, int order)
    {
        if (pixel == null)
        {
            var tex = new Texture2D(1, 1); tex.SetPixel(0, 0, Color.white); tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
        var go = new GameObject(n);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pixel; sr.color = c;
        var girl = player.GetComponent<SpriteRenderer>();
        if (girl != null) sr.sortingLayerID = girl.sortingLayerID;
        // same material as the "A" prompt over her head, so the bar is readable in the dark
        var promptSr = player.GetComponentInChildren<InteractPrompt>(true)?.GetComponent<SpriteRenderer>();
        if (promptSr != null) sr.sharedMaterial = promptSr.sharedMaterial;
        sr.sortingOrder = 500 + order;                        // above everything in the room
        return sr;
    }

    void OnDrawGizmosSelected()
    {
        if (approach == null) return;
        Gizmos.color = Color.red;
        for (int i = 1; i < approach.Length; i++) if (approach[i - 1] && approach[i]) Gizmos.DrawLine(approach[i - 1].position, approach[i].position);
    }
}
