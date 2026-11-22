using System.Collections;
using ArcadesTemplate.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// The breaker lever on the wall panel. It is too high for the girl: she has to push the box
/// under it, then press Interact next to it. She climbs onto the box (crouch, step up, land),
/// stretches up and yanks the lever down, the power comes back (lights flicker on) and she
/// jumps back down. The climb/pull is built from her existing frames plus squash & stretch.
public class LeverSwitch : MonoBehaviour
{
    [Header("Lever")]
    [SerializeField] private SpriteRenderer leverRenderer;
    [Tooltip("Optional: lever frames from up to down. Leave empty to move the handle into the Pulled pose instead.")]
    [SerializeField] private Sprite[] pullFrames;
    [Tooltip("Seconds for the lever to go from up to pulled. 0 = snaps straight into the pulled pose.")]
    [SerializeField] private float pullTime = 0.15f;

    [Header("Pulled rotation")]
    [Tooltip("On: when pulled, only the Z rotation changes (to Pulled Z). Off: use the full Pulled pose below.")]
    [SerializeField] private bool rotateZOnly = true;
    [SerializeField] private float pulledZ = -100f;

    [Header("Pulled pose (relative to where the lever starts, used when Rotate Z Only is off)")]
    [Tooltip("Set by Tools > Horror Room > 7 from the pose you placed the Lever in.")]
    [SerializeField] private Vector3 pulledOffset = new Vector3(0.282f, -0.701f, -0.27f);
    [SerializeField] private Vector3 pulledEuler = new Vector3(162.84f, -14.19f, -60.16f);
    [SerializeField] private Vector3 pulledScale = new Vector3(1.14f, 1f, 1f);

    [Header("Puzzle")]
    [SerializeField] private PushableBox box;
    [Tooltip("Where the box has to be (its pivot) for her to reach the lever.")]
    [SerializeField] private Transform boxSpot;
    [SerializeField] private float boxTolerance = 0.5f;
    [SerializeField] private Transform player;
    [Tooltip("How close she has to stand to the lever for Interact to do anything.")]
    [SerializeField] private float reachDistance = 2f;
    [SerializeField] private InputActionReference interactAction;

    [Header("Girl poses (filled in by Tools > Horror Room > 5)")]
    [SerializeField] private Sprite idleLeft;
    [SerializeField] private Sprite idleRight;
    [Tooltip("Run frame with the front knee raised: used as the stepping-up pose.")]
    [SerializeField] private Sprite stepLeft;
    [SerializeField] private Sprite stepRight;

    [Header("Timing")]
    [SerializeField] private float crouchTime = 0.1f;
    [SerializeField] private float hopTime = 0.3f;
    [SerializeField] private float hopHeight = 0.35f;
    [SerializeField] private float reachTime = 0.2f;

    [Header("Messages")]
    [SerializeField] private string tooHighMessage = "It's too high... maybe I can stand on something.";

    [Header("Power comes back")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] private Color poweredColor = new Color(0.85f, 0.9f, 1f);
    [SerializeField] private float poweredIntensity = 0.9f;

    public UnityEvent onPulled;
    public UnityEvent onTooHigh;

    public bool IsPulled { get; private set; }

    /// Electricity restored (the lever was pulled once). Kept across room changes; reset on game start.
    public static bool PowerRestored { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() => PowerRestored = false;

    bool InReach => player != null && Vector2.Distance(player.position, transform.position) <= reachDistance;
    bool BoxInPlace => box != null && boxSpot != null &&
                       Vector2.Distance(box.transform.position, boxSpot.position) <= boxTolerance;

    /// True when pressing Interact right now would pull the lever (the box then won't grab).
    public bool CanPullNow => isActiveAndEnabled && !IsPulled && !busy && InReach && BoxInPlace;
    private bool busy;

    // player parts, cached for the sequence
    private Rigidbody2D prb;
    private SpriteRenderer psr;
    private Animator panim;
    private DirectionalSpriteAnimator pdir;
    private TopDownMovement pmove;
    private Vector3 baseScale;

    // where the handle starts (captured at startup)
    private Vector3 upPos, upScale;
    private Quaternion upRot;

    void Awake()
    {
        if (player != null) pmove = player.GetComponent<TopDownMovement>();
        if (leverRenderer != null)
        {
            var lt = leverRenderer.transform;
            upPos = lt.localPosition; upRot = lt.localRotation; upScale = lt.localScale;
        }
    }

    void OnEnable() { if (interactAction != null) interactAction.action.Enable(); }

    private InteractPrompt prompt;

    void Update()
    {
        if (player == null) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);

        bool canMove = pmove == null || (pmove.enabled && pmove.CanMove);
        bool holdingBox = box != null && box.IsGrabbed;
        bool boxHasPriority = !BoxInPlace && box != null && box.PlayerInGrabRange;
        bool canInteract = canMove && InReach && !IsPulled && !busy && !holdingBox && !boxHasPriority;
        InputAction action = interactAction?.action;
        if (prompt != null) prompt.Request(this, canInteract, action);
        if (!canInteract || action == null || !action.WasPressedThisFrame()) return;

        if (BoxInPlace) StartCoroutine(PullSequence());
        else
        {
            onTooHigh.Invoke();
            if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(tooHighMessage);
            else Debug.Log(tooHighMessage);
        }
    }

    IEnumerator PullSequence()
    {
        busy = true;
        if (prompt != null) prompt.Request(this, false);
        prb = player.GetComponent<Rigidbody2D>();
        psr = player.GetComponentInChildren<SpriteRenderer>();
        panim = player.GetComponentInChildren<Animator>();
        pdir = player.GetComponentInChildren<DirectionalSpriteAnimator>();
        baseScale = psr.transform.localScale;

        // take over from the Animator: she is posed by hand for the whole sequence
        if (pmove != null) { pmove.StandUp(); pmove.CanMove = false; }
        if (pdir != null) pdir.enabled = false;
        if (panim != null) panim.enabled = false;
        psr.flipX = false;

        box.Lock();
        yield return Move(box.transform, box.transform.position, boxSpot.position, 0.12f, 0f, box.Body);

        Vector3 ground = player.position;
        if (prb != null) prb.simulated = false;           // no collisions while she climbs
        int order = psr.sortingOrder;
        if (box.Renderer != null) psr.sortingOrder = box.Renderer.sortingOrder + 1;

        // ---- climb up
        yield return Hop(ground, box.StandPoint, box.StandPoint.x >= ground.x);

        // ---- pull the lever
        bool leverRight = transform.position.x >= player.position.x;
        psr.sprite = leverRight ? idleRight : idleLeft;
        yield return Squash(1f, 1f, 0.97f, 1.07f, reachTime);          // stretch up on tiptoe
        yield return new WaitForSeconds(0.12f);                        // grab
        yield return PullLever();                                      // lever goes down step by step
        IsPulled = true;
        PowerRestored = true;
        onPulled.Invoke();
        if (globalLight != null) StartCoroutine(PowerFlicker());
        StartCoroutine(Shake(leverRenderer != null ? leverRenderer.transform : null, 0.15f, 0.02f));
        yield return Squash(1.05f, 0.92f, 1f, 1f, 0.15f);              // settle
        yield return new WaitForSeconds(0.5f);

        // ---- climb down where she came from
        yield return Hop(player.position, ground, ground.x >= player.position.x);

        if (prb != null) { prb.position = ground; prb.simulated = true; }
        psr.sortingOrder = order;
        psr.transform.localScale = baseScale;
        if (panim != null) panim.enabled = true;
        if (pdir != null) { pdir.enabled = true; pdir.FaceTowards(transform.position); }
        if (pmove != null) pmove.CanMove = true;
        busy = false;
    }

    // The lever steps through its frames from up to down; she pulls her weight down with it.
    IEnumerator PullLever()
    {
        int n = pullFrames != null ? pullFrames.Length : 0;
        if (leverRenderer == null) { yield return Squash(0.97f, 1.07f, 1.05f, 0.92f, 0.1f); yield break; }
        if (n < 2)
        {
            // straight from the up position into the pulled pose
            var lt = leverRenderer.transform;
            Vector3 p1 = upPos + pulledOffset;
            Quaternion r1 = Quaternion.Euler(pulledEuler);
            Vector3 s1 = Vector3.Scale(upScale, pulledScale);
            if (rotateZOnly)
            {
                // just swing it on Z around its pivot: transform rotation z -> pulledZ
                Vector3 e0 = upRot.eulerAngles;
                p1 = upPos; s1 = upScale;
                r1 = Quaternion.Euler(e0.x, e0.y, pulledZ);
            }
            for (float e = 0f; e < pullTime; e += Time.deltaTime)
            {
                float k = e / pullTime; k *= k;                     // starts stiff, then gives way
                lt.localPosition = Vector3.Lerp(upPos, p1, k);
                lt.localRotation = Quaternion.Slerp(upRot, r1, k);
                lt.localScale = Vector3.Lerp(upScale, s1, k);
                psr.transform.localScale = Scale(Mathf.Lerp(0.97f, 1.05f, k), Mathf.Lerp(1.07f, 0.92f, k));
                yield return null;
            }
            lt.localPosition = p1; lt.localRotation = r1; lt.localScale = s1;
            psr.transform.localScale = Scale(1.05f, 0.92f);
            yield break;
        }
        float step = pullTime / (n - 1);
        for (int i = 1; i < n; i++)
        {
            float k = (float)i / (n - 1);
            k = k * k;                                   // starts stiff, then gives way
            leverRenderer.sprite = pullFrames[i];
            psr.transform.localScale = Scale(Mathf.Lerp(0.97f, 1.05f, k), Mathf.Lerp(1.07f, 0.92f, k));
            yield return new WaitForSeconds(step * (i == 1 ? 1.6f : 1f));   // first notch is the hardest
        }
    }

    // crouch -> step-up pose in the air -> land with a little squash
    IEnumerator Hop(Vector3 from, Vector3 to, bool right)
    {
        psr.sprite = right ? idleRight : idleLeft;
        yield return Squash(1f, 1f, 1.06f, 0.9f, crouchTime);          // crouch
        psr.sprite = right ? stepRight : stepLeft;
        psr.transform.localScale = Scale(0.96f, 1.06f);                  // stretch in the air
        yield return Move(player, from, to, hopTime, hopHeight, prb);
        psr.sprite = right ? idleRight : idleLeft;
        yield return Squash(1.06f, 0.92f, 1f, 1f, 0.12f);              // land
    }

    Vector3 Scale(float x, float y) => new Vector3(baseScale.x * x, baseScale.y * y, baseScale.z);

    IEnumerator Squash(float x0, float y0, float x1, float y1, float time)
    {
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, e / time);
            psr.transform.localScale = Scale(Mathf.Lerp(x0, x1, k), Mathf.Lerp(y0, y1, k));
            yield return null;
        }
        psr.transform.localScale = Scale(x1, y1);
    }

    // Move from a to b in 'time' seconds with an upward arc of 'arc' units.
    static IEnumerator Move(Transform t, Vector3 a, Vector3 b, float time, float arc, Rigidbody2D rb)
    {
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = e / time;
            Vector3 p = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
            t.position = p;
            if (rb != null) rb.position = p;
            yield return null;
        }
        t.position = b;
        if (rb != null) rb.position = b;
    }

    static IEnumerator Shake(Transform t, float time, float amount)
    {
        if (t == null) yield break;
        Vector3 home = t.localPosition;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            t.localPosition = home + (Vector3)(Random.insideUnitCircle * amount);
            yield return null;
        }
        t.localPosition = home;
    }

    private float darkIntensity = -1f;
    private Color darkColor;

    /// The psycho shoves the lever back up: it snaps up and the lights die again.
    public void PushBackUp(float time = 0.25f) => StartCoroutine(PushBack(time));

    IEnumerator PushBack(float time)
    {
        if (leverRenderer != null)
        {
            var lt = leverRenderer.transform;
            Vector3 p0 = lt.localPosition, s0 = lt.localScale; Quaternion r0 = lt.localRotation;
            for (float e = 0f; e < time; e += Time.deltaTime)
            {
                float k = e / time;
                lt.localPosition = Vector3.Lerp(p0, upPos, k);
                lt.localRotation = Quaternion.Slerp(r0, upRot, k);
                lt.localScale = Vector3.Lerp(s0, upScale, k);
                yield return null;
            }
            lt.localPosition = upPos; lt.localRotation = upRot; lt.localScale = upScale;
            if (pullFrames != null && pullFrames.Length > 0) leverRenderer.sprite = pullFrames[0];
        }
        if (globalLight != null && darkIntensity >= 0f)
        {
            float lit = globalLight.intensity; Color litColor = globalLight.color;
            float[] pattern = { 0.2f, 1f, 0.05f, 0.6f, 0f };
            foreach (float p in pattern)
            {
                globalLight.intensity = Mathf.Lerp(darkIntensity, lit, p);
                globalLight.color = Color.Lerp(darkColor, litColor, p);
                yield return new WaitForSeconds(Random.Range(0.05f, 0.12f));
            }
            globalLight.intensity = darkIntensity; globalLight.color = darkColor;
        }
    }

    IEnumerator PowerFlicker()
    {
        float off = globalLight.intensity;
        Color offColor = globalLight.color;
        darkIntensity = off; darkColor = offColor;
        float[] pattern = { 0.6f, 0.05f, 0.9f, 0.1f, 0.15f, 1f, 0.3f, 1f };
        foreach (float p in pattern)
        {
            globalLight.color = Color.Lerp(offColor, poweredColor, p);
            globalLight.intensity = Mathf.Lerp(off, poweredIntensity, p);
            yield return new WaitForSeconds(Random.Range(0.04f, 0.14f));
        }
        globalLight.color = poweredColor;
        globalLight.intensity = poweredIntensity;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
        if (boxSpot != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(boxSpot.position, boxTolerance); }
    }
}
