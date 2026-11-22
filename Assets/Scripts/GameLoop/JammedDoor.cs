using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;

/// The finale's door (Lobby -> office). With the key card selected the lock clicks open, but the door
/// is jammed: every press of A shoves it (door shakes, she throws her shoulder into it, a flickering
/// "A" over her head tells the player to keep mashing). It gives way after 'pressesNeeded' presses and she gets through. The first shove is loud:
/// he comes down the stairs and walks straight at her, timed so that steady mashing is just enough.
/// While she's at it she panics: trembles, sweats, glances back over her shoulder at him.
/// Sits on the same object as the door's DoorExit (DoorExit forwards the A presses here).
[RequireComponent(typeof(DoorExit))]
public class JammedDoor : MonoBehaviour
{
    [SerializeField] private int pressesNeeded = 40;
    [Tooltip("Shaken on every shove (the door picture). Empty = only the camera shakes.")]
    [SerializeField] private Transform doorArt;
    [SerializeField] private Transform player;

    [Header("The psycho")]
    [SerializeField] private EntityAI psycho;
    [Tooltip("From the foot of the stairs to the door. He appears at the first point.")]
    [SerializeField] private Transform[] approach;
    [Tooltip("Seconds he needs from the stairs to the door (steady mashing of 40 takes ~6-8 s).")]
    [SerializeField] private float arriveAfter = 9f;

    [Header("Text")]
    [SerializeField] private string jammedMessage = "The lock clicks... but it's jammed! Mash A!";
    [SerializeField] private string comingMessage = "Footsteps on the stairs... HURRY!";
    [SerializeField] private string openMessage = "It gives way!";

    private int presses;
    private bool started, opening;

    // panic
    private Vector3 girlScale;
    private DirectionalSpriteAnimator girlAnim;
    private float shoveUntil;
    private SpriteRenderer alarm;

    // the flickering "A" over her head
    private SpriteRenderer aKey;
    private float pop;

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
            aKey = MakeWorldSprite("MashA", KeyA(), Color.white);
            if (player != null) { girlScale = player.localScale; girlAnim = player.GetComponentInChildren<DirectionalSpriteAnimator>(); }
            StartCoroutine(HeComes());
            StartCoroutine(Panic());
        }
        StartCoroutine(Shake(doorArt, 0.12f, 0.06f));
        CameraShake.Shake(0.1f, 0.04f);
        shoveUntil = Time.time + 0.12f;                       // she throws her shoulder into it
        pop = 1f;
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
        EndPanic();
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
        if (!opening && player != null) psycho.ScriptWalkTo(player.position);   // at the door: straight at her
    }

    // ------------------------------------------------------------------ her panic
    IEnumerator Panic()
    {
        float nextGlance = Time.time + 1.2f, nextSweat = Time.time + 0.3f;
        while (!opening && player != null)
        {
            if (psycho != null && psycho.Current == EntityAI.State.Grabbing) { EndPanic(); yield break; }
            float near = Danger();                                             // 0 far .. 1 right behind her

            // trembling, faster and stronger the closer he gets; a hard squash on every shove
            float tremble = Mathf.Lerp(0.012f, 0.035f, near);
            float sx = 1f + Mathf.Sin(Time.time * Mathf.Lerp(28f, 45f, near)) * tremble;
            float sy = 1f - Mathf.Sin(Time.time * Mathf.Lerp(31f, 50f, near) + 1f) * tremble;
            if (Time.time < shoveUntil) { sx = 1.09f; sy = 0.92f; }
            player.localScale = new Vector3(girlScale.x * sx, girlScale.y * sy, girlScale.z);

            // facing: into the door - but she keeps glancing back over her shoulder at him
            if (girlAnim != null)
            {
                bool glancing = Time.time >= nextGlance && Time.time < nextGlance + Mathf.Lerp(0.35f, 0.55f, near);
                Vector2 toDoor = (Vector2)(transform.position - player.position);
                var psr = psycho != null ? psycho.GetComponentInChildren<SpriteRenderer>() : null;
                Vector2 toHim = psr != null && psr.enabled
                    ? (Vector2)(psycho.transform.position - player.position)
                    : (approach != null && approach.Length > 0 && approach[0] != null ? (Vector2)(approach[0].position - player.position) : -toDoor);
                girlAnim.FacingOverride = glancing ? toHim : toDoor;
                if (Time.time >= nextGlance + 0.6f) nextGlance = Time.time + Mathf.Lerp(2f, 0.9f, near) + Random.Range(0f, 0.6f);
            }

            // sweat drops flying off her head
            if (Time.time >= nextSweat) { StartCoroutine(Sweat()); nextSweat = Time.time + Mathf.Lerp(0.6f, 0.2f, near); }

            // a red "!" over her head once he's close
            ShowAlarm(near > 0.35f, near);

            // the "A" over her head flickers - faster the closer he is - and pops on every press
            if (aKey != null)
            {
                pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime * 6f);
                float rate = Mathf.Lerp(6f, 14f, near);
                bool on = Mathf.Repeat(Time.time * rate, 1f) < 0.6f || pop > 0.5f;
                aKey.enabled = on;
                float h = 2.45f * Mathf.Abs(player.lossyScale.y);
                aKey.transform.position = player.position + new Vector3(-0.1f, h, 0f);
                float sc = 1f + pop * 0.35f;
                aKey.transform.localScale = new Vector3(sc, sc, 1f);
                aKey.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.35f), pop);
            }
            yield return null;
        }
    }

    float Danger()
    {
        if (psycho == null || player == null) return 0f;
        var sr = psycho.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || !sr.enabled) return 0.15f;                           // not here yet: already scared
        float d = Vector2.Distance(psycho.transform.position, player.position);
        return Mathf.Clamp01(1f - (d - 1.5f) / 14f);
    }

    void EndPanic()
    {
        if (player != null && girlScale != Vector3.zero) player.localScale = girlScale;
        if (girlAnim != null) girlAnim.FacingOverride = null;
        if (alarm != null) Destroy(alarm.gameObject);
        if (aKey != null) Destroy(aKey.gameObject);
    }

    IEnumerator Sweat()
    {
        if (player == null) yield break;
        var sr = MakeWorldSprite("Sweat", Drop(), new Color(0.75f, 0.88f, 1f, 0.95f));
        float side = Random.value < 0.5f ? -1f : 1f;
        float h = 2.1f * Mathf.Abs(player.lossyScale.y);
        Vector3 p = player.position + new Vector3(side * 0.25f, h, 0f);
        Vector2 v = new Vector2(side * Random.Range(0.8f, 1.6f), Random.Range(1.2f, 2.2f));
        for (float e = 0f; e < 0.55f; e += Time.deltaTime)
        {
            v += Vector2.down * 7f * Time.deltaTime;
            p += (Vector3)(v * Time.deltaTime);
            sr.transform.position = p;
            sr.color = new Color(0.75f, 0.88f, 1f, 0.95f * (1f - e / 0.55f));
            yield return null;
        }
        Destroy(sr.gameObject);
    }

    void ShowAlarm(bool on, float near)
    {
        if (!on) { if (alarm != null) alarm.enabled = false; return; }
        if (alarm == null) alarm = MakeWorldSprite("Alarm", Exclaim(), new Color(1f, 0.25f, 0.2f));
        alarm.enabled = true;
        float h = 2.55f * Mathf.Abs(player.lossyScale.y);
        float bob = Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.08f;
        alarm.transform.position = player.position + new Vector3(0.35f, h + bob, 0f);
        float s = 1f + near * 0.4f;
        alarm.transform.localScale = new Vector3(s, s, 1f);
    }

    SpriteRenderer MakeWorldSprite(string n, Sprite sp, Color c)
    {
        var go = new GameObject(n);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp; sr.color = c;
        var girl = player.GetComponent<SpriteRenderer>();
        if (girl != null) sr.sortingLayerID = girl.sortingLayerID;
        sr.sortingOrder = 500;
        var prompt = player.GetComponentInChildren<InteractPrompt>(true);
        var promptSr = prompt != null ? prompt.GetComponent<SpriteRenderer>() : null;
        if (promptSr != null) sr.sharedMaterial = promptSr.sharedMaterial;      // readable in the dark
        return sr;
    }

    static Sprite drop, exclaim, keyA;
    // a dark key cap with a white "A" on it
    static Sprite KeyA()
    {
        if (keyA != null) return keyA;
        string[] rows =
        {
            ".############.",
            "#oooooooooooo#",
            "#oooooAAooooo#",
            "#ooooAooAoooo#",
            "#oooAooooAooo#",
            "#oooAooooAooo#",
            "#oooAAAAAAooo#",
            "#oooAooooAooo#",
            "#oooAooooAooo#",
            "#oooAooooAooo#",
            "#oooooooooooo#",
            "#############.",
            ".###########..",
        };
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char ch = rows[y][x];
                Color c = ch == '#' ? new Color(0.85f, 0.85f, 0.9f) : ch == 'o' ? new Color(0.12f, 0.12f, 0.16f) : ch == 'A' ? Color.white : new Color(0, 0, 0, 0);
                tex.SetPixel(x, h - 1 - y, c);
            }
        tex.Apply();
        return keyA = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 22f);
    }
    static Sprite Drop()
    {
        if (drop != null) return drop;
        string[] rows = { "..#..", ".###.", "#####", "#####", ".###." };
        return drop = FromRows(rows, 40f);
    }
    static Sprite Exclaim()
    {
        if (exclaim != null) return exclaim;
        string[] rows = { ".##.", ".##.", ".##.", ".##.", ".##.", "....", ".##.", ".##." };
        return exclaim = FromRows(rows, 22f);
    }
    static Sprite FromRows(string[] rows, float ppu)
    {
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, h - 1 - y, rows[y][x] == '#' ? Color.white : new Color(0, 0, 0, 0));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), ppu);
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

    void OnDestroy() { if (aKey != null) Destroy(aKey.gameObject); if (alarm != null) Destroy(alarm.gameObject); }

    void OnDrawGizmosSelected()
    {
        if (approach == null) return;
        Gizmos.color = Color.red;
        for (int i = 1; i < approach.Length; i++) if (approach[i - 1] && approach[i]) Gizmos.DrawLine(approach[i - 1].position, approach[i].position);
    }
}
