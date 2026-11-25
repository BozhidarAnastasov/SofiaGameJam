
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownMovement : MonoBehaviour
{
    [Tooltip("Normal speed. Matches the walk animation's Walk Reference Speed so her feet don't slide.")]
    [SerializeField] private float walkSpeed = 2f;
    [Tooltip("Speed while the run button is held. Matches the Run Reference Speed of the animator.")]
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float crawlSpeed = 0.8f;
    [Tooltip("Stick values below this count as no input.")]
    [SerializeField] private float inputDeadZone = 0.2f;
    [SerializeField, Min(0.1f)] private float sprintDuration = 3f;
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 3f;
    [SerializeField, Min(0.1f)] private float staminaRecoveryDuration = 4f;

    [SerializeField] private InputActionReference moveAction;
    [Tooltip("Hold this button to run (Arcade B). Without it she always walks.")]
    [SerializeField] private InputActionReference sprintAction;
    [Tooltip("Hold this button to crawl (Arcade C). The Player prefab already has it set to ButtonC.")]
    [SerializeField] private InputActionReference crawlAction;

    private InputAction crouchAction;
    private Rigidbody2D rb;
    private Animator animator;
    private static readonly int PickupState = Animator.StringToHash("Base Layer.PickUp");
    private bool movementAllowed = true;
    private Vector2 moveInput;
    private float stamina = 1f;
    private float ghostStamina = 1f;
    private float staminaRecoveryStartsAt;
    private float fullStaminaSince;
    private bool exhausted;
    private Canvas staminaCanvas;
    private CanvasGroup staminaHudGroup;
    private RectTransform staminaBarFrame;
    private Vector2 staminaFrameBasePos;
    private RectTransform staminaFill;
    private RectTransform staminaGhostFill;
    private RectTransform staminaEdgeSpark;
    private Image staminaFillImage;
    private Image staminaBloodDrip;
    private RectTransform pulseIconRT;
    private Image pulseIconImage;
    private TMP_Text staminaLabel;
    private static Sprite cachedPanelSprite;
    private static Sprite cachedSlotSprite;
    private static Sprite cachedBloodSprite;
    private static Sprite cachedStaminaFillSprite;
    private static Sprite cachedPulseIconSprite;

    /// True while moving with the run button held and stamina available.
    public bool IsRunning { get; private set; }

    public bool IsCrawling { get; private set; }

    /// Freeze movement, e.g. during the pick-up animation or a cutscene.
    public bool CanMove
    {
        get => movementAllowed && !IsPlayingPickup();
        set => movementAllowed = value;
    }

    private bool IsPlayingPickup()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            return false;
        return animator.GetCurrentAnimatorStateInfo(0).fullPathHash == PickupState ||
               (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == PickupState);
    }

    /// Slows her down, e.g. while she drags the heavy box (1 = normal).
    public float SpeedMultiplier { get; set; } = 1f;

    /// Turn running off temporarily (while she holds the box).
    public bool AllowRun { get; set; } = true;

    /// Turn crawling off temporarily (while she holds the box, climbs...).
    public bool AllowCrawl { get; set; } = true;

    /// Her normal walking speed.
    public float WalkSpeed => walkSpeed * RoomPace.Factor;

    /// How fast she is moving right now (0 when standing still).
    public float CurrentSpeed { get; private set; }

    /// Make her stand up right away.
    public void StandUp(){ IsCrawling = false;}

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        if (moveAction == null || sprintAction == null)
        {
            Debug.LogError($"{name}: assign Move Action and Sprint Action on TopDownMovement.", this);
            enabled = false;
            return;
        }
        moveAction.action.Enable();
        sprintAction.action.Enable();
        // the crawl button (C); fall back to an action called "Crouch" or "ButtonC" in the same map
        crouchAction = crawlAction != null ? crawlAction.action : null;
        if (crouchAction == null && sprintAction.action.actionMap != null)
            crouchAction = sprintAction.action.actionMap.FindAction("Crouch") ?? sprintAction.action.actionMap.FindAction("ButtonC");
        if (crouchAction == null) Debug.LogWarning($"{name}: no crawl action found, so she can't crawl. Assign Crawl Action on TopDownMovement.", this);
        crouchAction?.Enable();
    }

    void Start()
    {
        BuildHorrorStaminaBar();
    }

    void OnDestroy()
    {
        if (staminaCanvas != null) Destroy(staminaCanvas.gameObject);
    }

    private void BuildHorrorStaminaBar()
    {
        // Dedicated resolution-independent HUD canvas so stamina scales cleanly on every screen size
        var canvasGO = new GameObject("StaminaHUD_Canvas");
        staminaCanvas = canvasGO.AddComponent<Canvas>();
        staminaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        staminaCanvas.overrideSorting = true;
        staminaCanvas.sortingOrder = 85;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var hudRoot = new GameObject("StaminaHUD", typeof(RectTransform), typeof(CanvasGroup));
        hudRoot.transform.SetParent(canvasGO.transform, false);
        staminaHudGroup = hudRoot.GetComponent<CanvasGroup>();
        staminaHudGroup.alpha = 0.65f;
        staminaHudGroup.blocksRaycasts = false;
        staminaHudGroup.interactable = false;

        var rootRT = hudRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = rootRT.pivot = Vector2.zero;
        rootRT.anchoredPosition = new Vector2(32f, 28f);
        rootRT.sizeDelta = new Vector2(310f, 62f);

        // Header label ("STAMINA" / "OUT OF BREATH")
        var labelObj = new GameObject("StaminaLabel", typeof(RectTransform));
        labelObj.transform.SetParent(rootRT, false);
        var lblRT = labelObj.GetComponent<RectTransform>();
        lblRT.anchorMin = lblRT.anchorMax = lblRT.pivot = new Vector2(0f, 1f);
        lblRT.anchoredPosition = new Vector2(46f, -2f);
        lblRT.sizeDelta = new Vector2(260f, 22f);

        staminaLabel = labelObj.AddComponent<TextMeshProUGUI>();
        staminaLabel.text = "STAMINA";
        staminaLabel.fontSize = 16f;
        staminaLabel.characterSpacing = 5.5f;
        staminaLabel.alignment = TextAlignmentOptions.BottomLeft;
        staminaLabel.color = new Color(0.78f, 0.75f, 0.72f, 0.88f);
        staminaLabel.raycastTarget = false;
        TMP_FontAsset horrorFont = FindHorrorFont();
        if (horrorFont != null)
        {
            staminaLabel.font = horrorFont;
            staminaLabel.fontSize = 20f;
            staminaLabel.characterSpacing = 3f;
        }

        var lblOutline = labelObj.AddComponent<Outline>();
        lblOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        lblOutline.effectDistance = new Vector2(1.2f, -1.2f);

        // Main bar housing using the game's actual scratched, riveted Hotbar/Panel.png art
        staminaBarFrame = CreateBarImage("Frame", rootRT, Color.white);
        staminaBarFrame.anchorMin = staminaBarFrame.anchorMax = staminaBarFrame.pivot = Vector2.zero;
        staminaFrameBasePos = new Vector2(40f, 6f);
        staminaBarFrame.anchoredPosition = staminaFrameBasePos;
        staminaBarFrame.sizeDelta = new Vector2(264f, 30f);

        // Dripping blood overlay (Hotbar/Blood.png) underneath when critically low / exhausted
        Sprite bloodSp = LoadHotbarSprite("Blood", sliced: false);
        if (bloodSp != null)
        {
            RectTransform bloodRT = CreateBarImage("BloodDrips", staminaBarFrame, new Color(1f, 1f, 1f, 0f));
            bloodRT.anchorMin = new Vector2(0f, 1f);
            bloodRT.anchorMax = new Vector2(1f, 1f);
            bloodRT.pivot = new Vector2(0.5f, 1f);
            bloodRT.anchoredPosition = new Vector2(0f, 2f);
            bloodRT.sizeDelta = new Vector2(8f, 52f);
            staminaBloodDrip = bloodRT.GetComponent<Image>();
            staminaBloodDrip.sprite = bloodSp;
            staminaBloodDrip.preserveAspect = false;
        }

        var frameImg = staminaBarFrame.GetComponent<Image>();
        Sprite panelSp = LoadHotbarSprite("Panel", sliced: true);
        if (panelSp != null)
        {
            frameImg.sprite = panelSp;
            frameImg.type = Image.Type.Sliced;
            frameImg.color = new Color(0.92f, 0.92f, 0.95f, 0.98f);
        }
        else
        {
            frameImg.color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
        }

        // Left recessed badge with pulsing heartbeat / breath icon
        RectTransform badgeRT = CreateBarImage("PulseBadge", rootRT, Color.white);
        badgeRT.anchorMin = badgeRT.anchorMax = badgeRT.pivot = Vector2.zero;
        badgeRT.anchoredPosition = new Vector2(0f, 3f);
        badgeRT.sizeDelta = new Vector2(36f, 36f);
        var badgeImg = badgeRT.GetComponent<Image>();
        Sprite slotSp = LoadHotbarSprite("Slot", sliced: false);
        if (slotSp != null)
        {
            badgeImg.sprite = slotSp;
            badgeImg.color = new Color(0.9f, 0.9f, 0.92f, 0.98f);
        }
        else
        {
            badgeImg.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        }

        pulseIconRT = CreateBarImage("PulseIcon", badgeRT, new Color(0.84f, 0.78f, 0.74f, 0.95f));
        pulseIconRT.anchorMin = pulseIconRT.anchorMax = pulseIconRT.pivot = new Vector2(0.5f, 0.5f);
        pulseIconRT.anchoredPosition = Vector2.zero;
        pulseIconRT.sizeDelta = new Vector2(22f, 22f);
        pulseIconImage = pulseIconRT.GetComponent<Image>();
        pulseIconImage.sprite = GetPulseIconSprite();
        pulseIconImage.preserveAspect = true;

        // Recessed dark blood trough inside the riveted metal frame
        RectTransform trough = CreateBarImage("Trough", staminaBarFrame, new Color(0.03f, 0.02f, 0.03f, 0.94f));
        trough.anchorMin = trough.anchorMax = trough.pivot = Vector2.zero;
        trough.anchoredPosition = new Vector2(9f, 7f);
        trough.sizeDelta = new Vector2(246f, 16f);

        // Trailing crimson ghost bar
        staminaGhostFill = CreateBarImage("GhostFill", trough, new Color(0.62f, 0.07f, 0.09f, 0.85f));
        staminaGhostFill.anchorMin = staminaGhostFill.anchorMax = staminaGhostFill.pivot = Vector2.zero;
        staminaGhostFill.anchoredPosition = Vector2.zero;
        staminaGhostFill.sizeDelta = new Vector2(246f, 16f);

        // Main textured stamina fill
        staminaFill = CreateBarImage("Fill", trough, new Color(0.84f, 0.80f, 0.74f, 1f));
        staminaFill.anchorMin = staminaFill.anchorMax = staminaFill.pivot = Vector2.zero;
        staminaFill.anchoredPosition = Vector2.zero;
        staminaFill.sizeDelta = new Vector2(246f, 16f);
        staminaFillImage = staminaFill.GetComponent<Image>();
        staminaFillImage.sprite = GetStaminaFillSprite();

        // Bright leading-edge meniscus spark so drain/recovery reads crisply in the dark
        staminaEdgeSpark = CreateBarImage("EdgeSpark", trough, new Color(1f, 0.92f, 0.85f, 0.85f));
        staminaEdgeSpark.anchorMin = staminaEdgeSpark.anchorMax = new Vector2(0f, 0.5f);
        staminaEdgeSpark.pivot = new Vector2(0.5f, 0.5f);
        staminaEdgeSpark.sizeDelta = new Vector2(3f, 18f);

        fullStaminaSince = Time.time;
    }

    private static TMP_FontAsset FindHorrorFont()
    {
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null && fonts[i].name.IndexOf("ChainsawCarnage", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return fonts[i];
        }
        return null;
    }

    private static Sprite LoadHotbarSprite(string name, bool sliced)
    {
        if (name == "Panel" && cachedPanelSprite != null) return cachedPanelSprite;
        if (name == "Slot" && cachedSlotSprite != null) return cachedSlotSprite;
        if (name == "Blood" && cachedBloodSprite != null) return cachedBloodSprite;

        Texture2D tex = Resources.Load<Texture2D>("Hotbar/" + name);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Bilinear;

        Vector4 border = sliced ? new Vector4(14f, 14f, 14f, 14f) : Vector4.zero;
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);

        if (name == "Panel") cachedPanelSprite = sp;
        else if (name == "Slot") cachedSlotSprite = sp;
        else if (name == "Blood") cachedBloodSprite = sp;
        return sp;
    }

    private static Sprite GetPulseIconSprite()
    {
        if (cachedPulseIconSprite != null) return cachedPulseIconSprite;
        // Crisp pixel-art anatomical heart / ECG pulse icon
        string[] rows =
        {
            "....###...###....",
            "...#####.#####...",
            "..#############..",
            "..######o######..",
            "..####oo#oo####..",
            ".ooo#o##o##oooo..",
            "...#####o#####...",
            "....#########....",
            ".....#######.....",
            "......#####......",
            ".......###.......",
            "........#........",
        };
        int h = rows.Length, w = rows[0].Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                char c = rows[h - 1 - y][x];
                Color col = c == 'o' ? Color.white
                          : c == '#' ? new Color(0.82f, 0.22f, 0.24f, 0.95f)
                          : new Color(0f, 0f, 0f, 0f);
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        cachedPulseIconSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return cachedPulseIconSprite;
    }

    private static Sprite GetStaminaFillSprite()
    {
        if (cachedStaminaFillSprite != null) return cachedStaminaFillSprite;
        const int W = 96, H = 12;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < H; y++)
        {
            float v = (float)y / (H - 1);
            float verticalShade = (y == H - 2 || y == H - 3) ? 1.0f : Mathf.Lerp(0.58f, 0.90f, v);
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / (W - 1);
                // Subtle tick notches at 25%, 50%, 75% (25% = exhaustion recovery threshold!)
                bool notch = (x == W / 4 || x == W / 2 || x == (W * 3) / 4);
                float horiz = Mathf.Lerp(0.80f, 1.0f, u) * (notch ? 0.72f : 1f);
                float b = verticalShade * horiz;
                tex.SetPixel(x, y, new Color(b, b, b, 1f));
            }
        }

        tex.Apply();
        cachedStaminaFillSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        return cachedStaminaFillSprite;
    }

    private void UpdateStaminaVisuals()
    {
        if (staminaCanvas != null)
        {
            bool showCanvas = Time.timeScale > 0.01f;
            if (staminaCanvas.enabled != showCanvas) staminaCanvas.enabled = showCanvas;
        }
        if (staminaFill == null) return;

        float clamped = Mathf.Clamp01(stamina);

        if (ghostStamina > clamped)
            ghostStamina = Mathf.MoveTowards(ghostStamina, clamped, Time.deltaTime * 0.36f);
        else
            ghostStamina = clamped;

        staminaFill.localScale = new Vector3(clamped, 1f, 1f);
        if (staminaGhostFill != null)
            staminaGhostFill.localScale = new Vector3(Mathf.Clamp01(ghostStamina), 1f, 1f);

        if (staminaEdgeSpark != null)
        {
            staminaEdgeSpark.gameObject.SetActive(clamped > 0.02f && clamped < 0.99f);
            staminaEdgeSpark.anchoredPosition = new Vector2(246f * clamped, 0f);
        }

        Color activeColor;
        if (exhausted)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
            activeColor = Color.Lerp(new Color(0.42f, 0.03f, 0.05f, 1f), new Color(0.98f, 0.16f, 0.16f, 1f), pulse);
        }
        else if (clamped > 0.45f)
        {
            float t = (clamped - 0.45f) / 0.55f;
            activeColor = Color.Lerp(new Color(0.84f, 0.42f, 0.24f, 1f), new Color(0.86f, 0.82f, 0.76f, 1f), t);
        }
        else
        {
            float t = clamped / 0.45f;
            activeColor = Color.Lerp(new Color(0.86f, 0.09f, 0.12f, 1f), new Color(0.84f, 0.42f, 0.24f, 1f), t);
        }

        if (staminaFillImage != null) staminaFillImage.color = activeColor;

        // Blood drips seep out of the riveted plate when exhausted or critically low (< 30%)
        if (staminaBloodDrip != null)
        {
            float targetBlood = exhausted ? 0.92f : Mathf.Clamp01((0.30f - clamped) / 0.30f) * 0.65f;
            Color bc = staminaBloodDrip.color;
            bc.a = Mathf.MoveTowards(bc.a, targetBlood, Time.deltaTime * 2.8f);
            staminaBloodDrip.color = bc;
        }

        // Dynamic heartbeat icon speed & double-thump rhythm
        if (pulseIconRT != null)
        {
            float bpmRate = exhausted ? 15f : IsRunning ? 11f : Mathf.Lerp(12f, 4.5f, clamped);
            float phase = Mathf.Repeat(Time.time * bpmRate * 0.25f, 1f);
            float beat = (phase < 0.12f) ? Mathf.Sin(phase / 0.12f * Mathf.PI)
                       : (phase > 0.18f && phase < 0.28f) ? Mathf.Sin((phase - 0.18f) / 0.10f * Mathf.PI) * 0.65f
                       : 0f;
            float scale = 1f + beat * (exhausted ? 0.28f : IsRunning ? 0.20f : 0.09f);
            pulseIconRT.localScale = new Vector3(scale, scale, 1f);
            if (pulseIconImage != null)
                pulseIconImage.color = Color.Lerp(activeColor, new Color(1f, 0.28f, 0.28f, 1f), exhausted ? 0.7f : beat * 0.5f);
        }

        if (staminaBarFrame != null)
        {
            staminaBarFrame.anchoredPosition = exhausted
                ? staminaFrameBasePos + Random.insideUnitCircle * 1.6f
                : staminaFrameBasePos;
        }

        if (staminaLabel != null)
        {
            if (exhausted)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
                staminaLabel.text = "OUT OF BREATH";
                staminaLabel.color = Color.Lerp(new Color(0.68f, 0.10f, 0.12f, 0.95f), new Color(1f, 0.28f, 0.28f, 1f), p);
            }
            else
            {
                staminaLabel.text = "STAMINA";
                staminaLabel.color = new Color(0.78f, 0.75f, 0.72f, 0.88f);
            }
        }

        if (clamped < 0.999f || IsRunning || exhausted)
        {
            fullStaminaSince = Time.time;
        }

        if (staminaHudGroup != null)
        {
            float targetAlpha = (Time.time - fullStaminaSince > 1.6f) ? 0.52f : 1f;
            staminaHudGroup.alpha = Mathf.MoveTowards(staminaHudGroup.alpha, targetAlpha, Time.deltaTime * 2.5f);
        }
    }

    private static RectTransform CreateBarImage(string objectName, Transform parent, Color color)
    {
        var bar = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        var image = bar.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    void OnDisable()
    {
        moveAction?.action.Disable();
        sprintAction?.action.Disable();
        crouchAction?.Disable();
        IsCrawling = false;
        IsRunning = false;
        moveInput = Vector2.zero;
        if (footsteps != null) footsteps.StopNow();
        GameAudio.StopOwner(gameObject);          // her breathing etc. stops with her (e.g. caught by the psycho)
    }

    void Update()
    {
        moveInput = CanMove ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        if (moveInput.magnitude < inputDeadZone) moveInput = Vector2.zero;

        bool hasInput = moveInput != Vector2.zero;
        IsCrawling = CanMove && AllowCrawl && crouchAction != null && crouchAction.IsPressed();
        bool sprintHeld = sprintAction.action.IsPressed();
        if (exhausted && !sprintHeld && stamina >= 0.25f) exhausted = false;

        IsRunning = hasInput && AllowRun && !IsCrawling && sprintHeld && !exhausted;
        if (IsRunning)
        {
            stamina = Mathf.MoveTowards(stamina, 0f, Time.deltaTime / sprintDuration);
            staminaRecoveryStartsAt = Time.time + staminaRecoveryDelay;
            exhausted = stamina == 0f;
        }
        else if (Time.time >= staminaRecoveryStartsAt)
        {
            stamina = Mathf.MoveTowards(stamina, 1f, Time.deltaTime / staminaRecoveryDuration);
        }

        UpdateStaminaVisuals();

        // out of breath the moment the stamina bar runs dry
        if (exhausted && !wasExhausted) GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.girlExhaust : null, 0.8f, 0.04f, gameObject);
        wasExhausted = exhausted;
        UpdateFootsteps(hasInput);
    }

    [Header("Footsteps")]
    [SerializeField, Range(0f, 1f)] private float walkStepVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float runStepVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float crawlVolume = 0.6f;
    private SoundLoop footsteps;
    private bool wasExhausted;
    private int footstepMode = -1;          // 0 walk, 1 run, 2 crawl

    // Walk / run / crawl loops, only while she actually moves (a crawler who stops is silent).
    void UpdateFootsteps(bool hasInput)
    {
        var lib = GameAudio.Sounds;
        if (lib == null) return;
        if (footsteps == null) footsteps = SoundLoop.Create(gameObject);

        bool moving = CanMove && hasInput;
        footsteps.Playing = moving;
        if (!moving) return;
        int mode = IsCrawling ? 2 : IsRunning ? 1 : 0;
        if (mode != footstepMode)
        {
            // random variation each time she switches between walking, running and crawling
            footsteps.SetClip(GameAudio.Pick(mode == 2 ? lib.girlCrawl : mode == 1 ? lib.girlRun : lib.girlWalk));
            footstepMode = mode;
        }
        footsteps.Volume = mode == 2 ? crawlVolume : mode == 1 ? runStepVolume : walkStepVolume;
        footsteps.Pitch = Mathf.Lerp(0.85f, 1f, SpeedMultiplier);      // slower, heavier steps while dragging the box
    }

    void FixedUpdate()
    {
        if (!CanMove || moveInput == Vector2.zero) { CurrentSpeed = 0f; return; }

        float speed = (IsCrawling ? crawlSpeed : IsRunning ? runSpeed : walkSpeed) * SpeedMultiplier * RoomPace.Factor;   // RoomPace: rooms built at another scale
        CurrentSpeed = speed;
        rb.MovePosition(rb.position + moveInput.normalized * speed * Time.fixedDeltaTime);
    }
}
