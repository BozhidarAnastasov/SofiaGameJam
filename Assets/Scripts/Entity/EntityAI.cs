using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.Universal;

/// The psycho. Waits outside until BeginEntrance() (called when the door lamp scare ends),
/// kicks the door in after a short delay, walks straight to the lever and shoves it back up,
/// then patrols. His red vision cone shows where he is looking. If the girl is inside it
/// (and not hidden), he chases her at 1.5x her speed; when he reaches her he grabs her
/// and the death screen appears.
[RequireComponent(typeof(Rigidbody2D))]
public class EntityAI : MonoBehaviour
{
    public enum State { Waiting, Entering, GoingToLever, AtLever, Patrolling, Searching, Chasing, Grabbing }

    [Header("Scene")]
    [SerializeField] private Transform girl;
    [SerializeField] private LeverSwitch lever;
    [SerializeField] private Transform leverStandPoint;
    [SerializeField] private Transform doorOutside;
    [SerializeField] private Transform doorInside;
    [SerializeField] private SpriteRenderer doorOpen;
    [SerializeField] private SpriteRenderer brokenDoor;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private DeathScreen deathScreen;

    [Header("Timing")]
    [Tooltip("Seconds between the lamp's last flash and the door being kicked in.")]
    [SerializeField] private float kickDelay = 2f;
    [SerializeField] private float leverPause = 1.5f;
    [SerializeField] private float lookAroundTime = 1.6f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 1.6f;
    [Tooltip("Chase speed = this x the girl's speed (at least x her walk speed).")]
    [SerializeField] private float chaseMultiplier = 1.5f;
    [SerializeField] private float grabRange = 0.9f;

    [Header("Vision")]
    [SerializeField] private Light2D visionLight;
    [Tooltip("How far his cone reaches. Standing up she is seen anywhere; crawling only inside the cone.")]
    [SerializeField] private float viewRange = 6.5f;
    [SerializeField] private float visionAngle = 70f;
    [Tooltip("Range multiplier while she crawls (1 = the whole visible cone counts).")]
    [SerializeField] private float crawlViewMultiplier = 1f;
    [Tooltip("The cone starts this far in front of him, so his own light doesn't paint him red.")]
    [SerializeField] private float coneForward = 0.55f;
    [SerializeField] private float coneHeight = 0.45f;
    [Tooltip("Seconds without seeing her before he gives up the chase and searches.")]
    [SerializeField] private float loseSightTime = 3f;
    [SerializeField] private float turnSpeed = 300f;

    [Header("Grab")]
    [Tooltip("Sideways distance from her to his pivot so the grab art lines up with where she was.")]
    [SerializeField] private float grabOffsetX = 0.55f;
    [SerializeField] private float deathScreenDelay = 1.3f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip kickClip;
    [SerializeField] private AudioClip spottedClip;
    [Tooltip("Where each blow lands in the EnemyHittingDoor clip (seconds). Every blow shakes the camera; " +
             "the last one is the kick that breaks the door in.")]
    [SerializeField] private float[] knockTimes = { 0.02f, 0.27f, 0.52f, 0.77f, 1.05f, 1.28f, 1.53f };
    [Tooltip("Her scream when he spots her won't repeat more often than this (seconds).")]
    [SerializeField] private float spottedSoundCooldown = 8f;
    [Tooltip("His footsteps are full volume next to her and fade out this far away.")]
    [SerializeField] private float footstepHearingRange = 12f;
    private SoundLoop footsteps;
    private float lastSpottedSound = -999f;
    private Vector2 lastFootPos;
    private float footSpeed;

    public UnityEvent onEnter;
    public UnityEvent onSpotted;
    public UnityEvent onGrab;

    public State Current => state;

    private State state = State.Waiting;
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;
    private DirectionalSpriteAnimator anim;
    private TopDownMovement girlMove;
    private Rigidbody2D girlRb;
    private Vector2 lookDir = Vector2.left;
    private Vector2 lastSeen;
    private float lostTimer;
    private int patrolIndex;
    private bool pausing;
    private bool spottedWhileEntering;
    private Coroutine routine;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponentInChildren<DirectionalSpriteAnimator>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.mass = 100f;
        rb.linearDamping = 0f;
        if (girl != null) { girlMove = girl.GetComponent<TopDownMovement>(); girlRb = girl.GetComponent<Rigidbody2D>(); }

        // the red cone shows exactly what he can see
        if (visionLight != null) { visionLight.pointLightOuterRadius = viewRange; visionLight.pointLightOuterAngle = visionAngle; }

        // hidden outside until the door is kicked
        SetVisible(false);
        rb.simulated = false;
        if (doorOpen != null) doorOpen.enabled = false;
        if (brokenDoor != null) brokenDoor.enabled = false;
    }

    void SetVisible(bool on)
    {
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = on;
        foreach (var l in GetComponentsInChildren<Light2D>(true)) l.enabled = on;   // vision cone + presence glow
    }

    /// Start the entrance (hooked to the door lamp scare's On Finished).
    public void BeginEntrance()
    {
        if (state != State.Waiting) return;
        routine = StartCoroutine(Entrance());
    }

    IEnumerator Entrance()
    {
        // He bangs on the door first. Each blow in the banging clip jolts the camera, and the
        // clip's last (hardest) blow IS the kick: the scene follows the clip's own playback time,
        // so picture and sound stay locked together.
        var lib = GameAudio.Sounds;
        bool bang = kickClip == null && lib != null && GameAudio.Pick(lib.enemyHitDoor) != null && knockTimes != null && knockTimes.Length > 0;
        float lastKnock = bang ? knockTimes[knockTimes.Length - 1] : 0f;
        yield return new WaitForSeconds(Mathf.Max(0f, kickDelay - lastKnock));
        if (bang)
        {
            var src = GameAudio.PlayTracked(lib.enemyHitDoor);
            var clip = src != null ? src.clip : null;
            float started = Time.time;
            for (int i = 0; i < knockTimes.Length; i++)
            {
                // wait for this blow on the clip's own clock (falls back to game time if it stopped)
                while (true)
                {
                    bool playing = src != null && src.isPlaying && src.clip == clip;
                    float t = playing ? src.time : Time.time - started;
                    if (t >= knockTimes[i]) break;
                    yield return null;
                }
                if (i < knockTimes.Length - 1) StartCoroutine(CameraShake(0.09f, 0.035f));   // the door shudders
            }
        }
        // the door gets kicked in and slams onto the floor
        if (doorOpen != null) doorOpen.enabled = true;
        if (brokenDoor != null) brokenDoor.enabled = true;
        if (audioSource != null && kickClip != null) audioSource.PlayOneShot(kickClip);
        GameAudio.Play(lib != null ? lib.doorSlam : null);
        StartCoroutine(CameraShake(0.35f, 0.12f));

        transform.position = doorOutside != null ? doorOutside.position : transform.position;
        SetVisible(true);
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Kinematic;     // through the doorway without bumping the wall collider
        if (col != null) col.enabled = false;
        lookDir = doorInside != null ? ((Vector2)(doorInside.position - transform.position)).normalized : Vector2.left;
        state = State.Entering;
        onEnter.Invoke();
    }

    void FixedUpdate()
    {
        // how fast he really moves (kinematic MovePosition while entering has no velocity)
        footSpeed = rb.simulated ? (rb.position - lastFootPos).magnitude / Time.fixedDeltaTime : 0f;
        lastFootPos = rb.position;

        switch (state)
        {
            case State.Entering:
                if (MoveKinematic(doorInside.position, walkSpeed))
                {
                    // only now, fully inside the room, does he get his collider back
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    if (col != null) col.enabled = true;
                    if (spottedWhileEntering) StartChase();
                    else state = State.GoingToLever;
                }
                break;
            case State.GoingToLever:
                if (Steer(leverStandPoint.position, walkSpeed)) { state = State.AtLever; routine = StartCoroutine(AtLever()); }
                break;
            case State.Patrolling:
                if (pausing || patrolPoints == null || patrolPoints.Length == 0) { Stop(); break; }
                if (Steer(patrolPoints[patrolIndex].position, walkSpeed)) routine = StartCoroutine(LookAround(true));
                break;
            case State.Searching:
                if (pausing) { Stop(); break; }
                if (Steer(lastSeen, walkSpeed)) routine = StartCoroutine(LookAround(false));
                break;
            case State.Chasing:
                float herSpeed = girlMove != null ? Mathf.Max(girlMove.CurrentSpeed, girlMove.WalkSpeed) : walkSpeed;
                Steer(girl.position, herSpeed * chaseMultiplier);
                if (Vector2.Distance(rb.position, girl.position) <= grabRange) Grab();
                break;
            default:
                if (rb.simulated && rb.bodyType == RigidbodyType2D.Dynamic) Stop();
                break;
        }
    }

    // His heavy steps: louder the closer he is to her, quicker while he chases.
    void UpdateFootsteps()
    {
        var lib = GameAudio.Sounds;
        if (lib == null) return;
        if (footsteps == null) { footsteps = SoundLoop.Create(gameObject); footsteps.SetClip(GameAudio.Pick(lib.enemyWalk)); }
        bool walking = state != State.Waiting && state != State.Grabbing && footSpeed > 0.1f;
        footsteps.Playing = walking;
        if (!walking) return;
        float dist = girl != null ? Vector2.Distance(rb.position, girl.position) : 0f;
        footsteps.Volume = Mathf.Lerp(1f, 0.1f, Mathf.Clamp01(dist / footstepHearingRange));
        footsteps.Pitch = Mathf.Clamp(footSpeed / Mathf.Max(0.01f, walkSpeed), 0.9f, 1.35f);
    }

    void Update()
    {
        UpdateFootsteps();
        if (state == State.Waiting || state == State.Grabbing || girl == null) return;

        if (CanSeeGirl())
        {
            lastSeen = girl.position;
            lostTimer = 0f;
            // He starts outside the room's wall collider. Turning his collider back on while he is
            // still in the doorway leaves him stuck outside the wall, so finish walking in first.
            if (state == State.Entering) spottedWhileEntering = true;
            else if (state != State.Chasing) StartChase();
        }
        else if (state == State.Chasing)
        {
            lostTimer += Time.deltaTime;
            if (lostTimer >= loseSightTime) state = State.Searching;
        }

        // the vision cone follows where he looks, starting just in front of him
        if (visionLight != null)
        {
            visionLight.transform.position = new Vector3(rb.position.x + lookDir.x * coneForward,
                                                         rb.position.y + lookDir.y * coneForward + coneHeight,
                                                         visionLight.transform.position.z);
            float target = Mathf.Atan2(-lookDir.x, lookDir.y) * Mathf.Rad2Deg;
            float z = Mathf.MoveTowardsAngle(visionLight.transform.eulerAngles.z, target, turnSpeed * Time.deltaTime);
            visionLight.transform.rotation = Quaternion.Euler(0f, 0f, z);
        }
    }

    void StartChase()
    {
        if (routine != null) StopCoroutine(routine);
        pausing = false;
        spottedWhileEntering = false;
        lostTimer = 0f;
        state = State.Chasing;
        if (Time.time - lastSpottedSound >= spottedSoundCooldown)
        {
            lastSpottedSound = Time.time;
            if (audioSource != null && spottedClip != null) audioSource.PlayOneShot(spottedClip);
            else GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.girlNoticeEnemy : null, 1f, 0.04f, girl.gameObject);
        }
        onSpotted.Invoke();
    }

    bool CanSeeGirl()
    {
        if (girlMove != null && !girlMove.enabled) return false;
        var girlSr = girl.GetComponentInChildren<SpriteRenderer>();
        if (girlSr != null && !girlSr.enabled) return false;

        Vector2 eye = rb.position;
        Vector2 her = girl.position;
        Vector2 to = her - eye;
        float dist = to.magnitude;
        bool crawling = girlMove != null && girlMove.IsCrawling;

        // standing up she is always seen (once he is in the room); only crawling can hide her
        if (!crawling) return true;
        float range = viewRange * crawlViewMultiplier;
        if (dist > range) return false;
        // compare against where the cone actually points
        Vector2 coneDir = visionLight != null ? (Vector2)visionLight.transform.up : lookDir;
        if (Vector2.Angle(coneDir, to) > visionAngle * 0.5f) return false;

        foreach (var hit in Physics2D.LinecastAll(eye, her))
        {
            var cover = hit.collider.GetComponent<VisionCover>();
            if (cover != null && (!cover.lowCover || crawling)) return false;
        }
        return true;
    }

    // ---- movement helpers: return true when the target is reached
    private Vector2 steerTarget;
    private float steerTimer;

    bool Steer(Vector2 target, float speed)
    {
        // if something blocks him on the way, give up on this point after a few seconds
        if ((target - steerTarget).sqrMagnitude > 0.04f) { steerTarget = target; steerTimer = 0f; }
        steerTimer += Time.fixedDeltaTime;
        Vector2 d = target - rb.position;
        if (d.magnitude < 0.12f || (state != State.Chasing && steerTimer > 6f)) { Stop(); steerTimer = 0f; return true; }
        Vector2 v = d.normalized * speed;
        rb.linearVelocity = v;
        lookDir = Vector2.Lerp(lookDir, v.normalized, 0.2f).normalized;
        return false;
    }

    bool MoveKinematic(Vector2 target, float speed)
    {
        Vector2 d = target - rb.position;
        float step = speed * Time.fixedDeltaTime;
        if (d.magnitude <= step) { rb.MovePosition(target); return true; }
        rb.MovePosition(rb.position + d.normalized * step);
        lookDir = Vector2.Lerp(lookDir, d.normalized, 0.2f).normalized;
        return false;
    }

    void Stop() { if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = Vector2.zero; }

    IEnumerator AtLever()
    {
        Stop();
        if (lever != null)
        {
            lookDir = ((Vector2)(lever.transform.position - transform.position)).normalized;
            if (anim != null) anim.FaceTowards(lever.transform.position);
            yield return new WaitForSeconds(0.5f);
            lever.PushBackUp(0.2f);                       // lights die again
        }
        yield return new WaitForSeconds(leverPause);
        patrolIndex = 0;
        state = State.Patrolling;
    }

    // stop, look left and right, then go on (next patrol point / back to patrolling)
    IEnumerator LookAround(bool nextPoint)
    {
        pausing = true;
        Stop();
        Vector2 baseDir = lookDir;
        float t = 0f;
        while (t < lookAroundTime)
        {
            t += Time.deltaTime;
            float a = Mathf.Sin(t / lookAroundTime * Mathf.PI * 2f) * 60f * Mathf.Deg2Rad;
            lookDir = new Vector2(baseDir.x * Mathf.Cos(a) - baseDir.y * Mathf.Sin(a), baseDir.x * Mathf.Sin(a) + baseDir.y * Mathf.Cos(a));
            yield return null;
        }
        lookDir = baseDir;
        if (nextPoint && patrolPoints != null && patrolPoints.Length > 0) patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        state = State.Patrolling;
        pausing = false;
    }

    void Grab()
    {
        state = State.Grabbing;
        if (routine != null) StopCoroutine(routine);
        if (footsteps != null) footsteps.StopNow();
        // silence everything coming from her (breathing, scream, steps, box scrape...): only the kill is heard now
        GameAudio.StopOwner(girl.gameObject);
        foreach (var loop in girl.GetComponentsInChildren<SoundLoop>(true)) loop.StopNow();
        foreach (var box in FindObjectsByType<PushableBox>(FindObjectsSortMode.None)) box.StopDragSound();
        // The kill beats (grab, gasp, lift, knife raise, stab + duck) are animation events on
        // Psycho_Grab_Left/Right, so they land on the frames. Without an animator, time them here.
        if (anim == null) StartCoroutine(KillSoundsWithoutAnimator());
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (col != null) col.enabled = false;
        if (visionLight != null) visionLight.enabled = false;

        // the girl is now part of his grab animation: hide her and freeze her controls
        if (girlMove != null) { girlMove.CanMove = false; girlMove.enabled = false; }
        if (girlRb != null) { girlRb.linearVelocity = Vector2.zero; girlRb.simulated = false; }
        foreach (var r in girl.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
        foreach (var l in girl.GetComponentsInChildren<Light2D>(true)) l.enabled = false;
        var girlAnim = girl.GetComponentInChildren<DirectionalSpriteAnimator>();
        if (girlAnim != null) girlAnim.enabled = false;

        // line his grab art up with where she was (grab_left: she is on his left)
        float side = girl.position.x < transform.position.x ? -1f : 1f;
        Vector3 p = new Vector3(girl.position.x - side * grabOffsetX, girl.position.y, transform.position.z);
        transform.position = p; rb.position = p;
        if (anim != null) { anim.FaceTowards(girl.position); anim.PlayGrab(); }
        onGrab.Invoke();
        StartCoroutine(ShowDeath());
    }

    // Same timings as the events on the grab clips (frames at 0 / 0.25 / 0.5 / 0.75 s).
    IEnumerator KillSoundsWithoutAnimator()
    {
        string[] ids = { "KillGrab", "KillGasp", "KillLift", "KillKnifeRaise", "KillStab" };
        float[] at = { 0f, 0.08f, 0.25f, 0.5f, 1.1f };
        float t = 0f;
        for (int i = 0; i < ids.Length; i++)
        {
            yield return new WaitForSeconds(at[i] - t);
            t = at[i];
            GameAudio.Play(ids[i]);
            if (ids[i] == "KillStab") GameAudio.Duck();
        }
    }

    IEnumerator ShowDeath()
    {
        yield return new WaitForSeconds(deathScreenDelay);
        if (deathScreen != null) { deathScreen.gameObject.SetActive(true); deathScreen.Show(); }
    }

    IEnumerator CameraShake(float time, float amount)
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        Vector3 home = cam.transform.position;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = 1f - e / time;
            cam.transform.position = home + (Vector3)(Random.insideUnitCircle * amount * k);
            yield return null;
        }
        cam.transform.position = home;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Vector3 p = transform.position;
        Vector3 dir = Application.isPlaying ? (Vector3)lookDir : Vector3.left;
        float h = visionAngle * 0.5f * Mathf.Deg2Rad;
        Vector3 a = new Vector3(dir.x * Mathf.Cos(h) - dir.y * Mathf.Sin(h), dir.x * Mathf.Sin(h) + dir.y * Mathf.Cos(h));
        Vector3 b = new Vector3(dir.x * Mathf.Cos(-h) - dir.y * Mathf.Sin(-h), dir.x * Mathf.Sin(-h) + dir.y * Mathf.Cos(-h));
        Gizmos.DrawLine(p, p + a * viewRange); Gizmos.DrawLine(p, p + b * viewRange);
        Gizmos.DrawWireSphere(p, grabRange);
        if (patrolPoints != null) { Gizmos.color = Color.yellow; foreach (var t in patrolPoints) if (t != null) Gizmos.DrawWireSphere(t.position, 0.15f); }
    }
}
