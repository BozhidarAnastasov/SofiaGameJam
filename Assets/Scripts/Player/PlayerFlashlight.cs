using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// The girl's flashlight: press the button (Arcade D) to switch it on/off.
/// It shines as a cone in the direction she is facing and turns smoothly with her.
public class PlayerFlashlight : MonoBehaviour
{
    [SerializeField] private InputActionReference toggleAction;
    [SerializeField] private Light2D flashlight;
    [SerializeField] private bool startOn = true;
    [Tooltip("Off: she has to find the flashlight first (the pickup on the Lobby's red chair).")]
    [SerializeField] private bool startWithFlashlight = false;

    /// Whether she has picked up the flashlight. Kept across room changes; reset when the game starts.
    public static bool HasFlashlight { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnGameStart() => HasFlashlight = false;

    /// Called by the flashlight pickup.
    public void Give()
    {
        HasFlashlight = true;
        if (flashlight != null) flashlight.enabled = true;
        GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.flashlightOn : null, 1f, 0.04f, Owner);
        Debug.Log("[PlayerFlashlight] She has the flashlight now: D switches it on/off.", this);
    }

    [Header("Beam")]
    [Tooltip("Shine as a cone where she looks (off = a round glow around her).")]
    [SerializeField] private bool aimWithFacing = true;
    [SerializeField] private float coneOuterAngle = 80f;
    [SerializeField] private float coneInnerAngle = 35f;
    [SerializeField] private float beamLength = 5f;
    [SerializeField] private float turnSpeed = 540f;     // degrees per second

    [Header("While crawling")]
    [Tooltip("Where the light sits while she crawls: x = in front of her (towards the side she faces), y = height. Her hands.")]
    [SerializeField] private Vector2 crawlHandOffset = new Vector2(0.55f, 0.22f);
    [Tooltip("How fast the light moves between chest and hands (units per second).")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("Sound (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickClip;

    private DirectionalSpriteAnimator anim;
    private TopDownMovement movement;
    private Vector3 standPos;              // chest height, as placed on the prefab

    public bool IsOn => flashlight != null && flashlight.enabled;

    // her sounds are tagged with the player root so they stop when she dies
    GameObject Owner => movement != null ? movement.gameObject : (GetComponentInParent<TopDownMovement>() is TopDownMovement m && m != null ? m.gameObject : gameObject);

    void Awake()
    {
        anim = GetComponentInChildren<DirectionalSpriteAnimator>();
        if (flashlight == null)
            foreach (var l in GetComponentsInChildren<Light2D>(true))
                if (l.lightType != Light2D.LightType.Global) { flashlight = l; break; }
        if (flashlight == null) { Debug.LogWarning($"{name}: no Light2D for the flashlight.", this); return; }
        movement = GetComponentInParent<TopDownMovement>();
        standPos = flashlight.transform.localPosition;

        if (startWithFlashlight) HasFlashlight = true;
        flashlight.enabled = startOn && HasFlashlight;
        Debug.Log($"[PlayerFlashlight] Has flashlight: {HasFlashlight}.", this);
        if (aimWithFacing)
        {
            flashlight.lightType = Light2D.LightType.Point;
            flashlight.pointLightOuterAngle = coneOuterAngle;
            flashlight.pointLightInnerAngle = coneInnerAngle;
            flashlight.pointLightOuterRadius = beamLength;
            flashlight.transform.rotation = Quaternion.Euler(0f, 0f, AngleFor(anim != null ? anim.Facing : Vector2.down));
        }
    }

    void OnEnable() { if (toggleAction != null) toggleAction.action.Enable(); }

    void Update()
    {
        if (flashlight == null) return;

        if (HasFlashlight && toggleAction != null && toggleAction.action.WasPressedThisFrame())
        {
            flashlight.enabled = !flashlight.enabled;
            if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip);
            else if (GameAudio.Sounds != null) GameAudio.Play(flashlight.enabled ? GameAudio.Sounds.flashlightOn : GameAudio.Sounds.flashlightOff, 1f, 0.04f, Owner);
        }

        // crawling: the light goes down to her hands, in front of her; standing: back to the chest
        bool crawling = movement != null && movement.IsCrawling;
        Vector3 target = standPos;
        if (crawling)
        {
            float side = anim != null ? anim.Side : 1f;
            target = new Vector3(crawlHandOffset.x * side, crawlHandOffset.y, standPos.z);
        }
        flashlight.transform.localPosition = Vector3.MoveTowards(flashlight.transform.localPosition, target, moveSpeed * Time.deltaTime);

        if (aimWithFacing && anim != null)
        {
            float targetT = AngleFor(anim.Facing);
            float z = Mathf.MoveTowardsAngle(flashlight.transform.eulerAngles.z, targetT, turnSpeed * Time.deltaTime);
            flashlight.transform.rotation = Quaternion.Euler(0f, 0f, z);
        }
    }

    // a 2D light's cone points along its local "up"
    static float AngleFor(Vector2 dir) => Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
}
