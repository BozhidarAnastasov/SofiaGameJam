using System.Collections;
using ArcadesTemplate.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// The ending, in the corridor and office. Two seconds after she comes in, he bursts out of the
/// Lobby door behind her and comes down the corridor. If he catches up with her on the way, he
/// grabs her. When he reaches the office doorway he stops and throws his knife at her, misses, and
/// it smashes the window. She climbs out through the broken window (A) -> plays the right-to-left
/// psycho retreat + afterimage + bloody "YOU SURVIVED" text -> automatically transitions to MainMenu.
public class OfficeFinale : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private EntityAI psycho;
    [SerializeField] private Transform player;
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("The Lobby door he comes out of.")]
    [SerializeField] private Transform entrance;
    [Tooltip("Points down the corridor (around the rubble) before the doorway.")]
    [SerializeField] private Transform[] corridorPath;
    [Tooltip("The office doorway, where he stops and throws (the circled spot).")]
    [SerializeField] private Transform throwSpot;
    [Tooltip("Centre of the window glass on the office's right wall.")]
    [SerializeField] private Transform window;
    [Tooltip("Where the escape works from (A within reach of this).")]
    [SerializeField] private Transform windowStand;
    [SerializeField] private float windowReach = 1.4f;
    [SerializeField] private WinScreen winManager;
    [Tooltip("Sorting reference for the knife / glass (a room sprite).")]
    [SerializeField] private SpriteRenderer roomArt;

    [Header("Timing")]
    [SerializeField] private float delayBehindHer = 2f;
    [Tooltip("His speed down the corridor (she walks 3, runs 5).")]
    [SerializeField] private float corridorSpeed = 3.6f;
    [Tooltip("After the throw he keeps coming at her at this speed.")]
    [SerializeField] private float afterThrowSpeed = 3.4f;
    [Tooltip("If he gets this close on the way, he grabs her.")]
    [SerializeField] private float catchDistance = 1.3f;
    [SerializeField] private float windUp = 0.6f;
    [SerializeField] private float knifeSpeed = 14f;

    [Header("Text")]
    [SerializeField] private string enterMessage = "The back office corridor... there has to be a window or way outside ahead!";
    [SerializeField] private string chaseMessage = "He broke through the door behind me—don't look back, RUN!";
    [SerializeField] private string throwMessage = "It's a dead end—watch out, he's throwing his knife!";
    [SerializeField] private string brokenMessage = "The glass shattered—that's my chance! Jump out the window (<color=#E8C874>A</color>)!";

    [Header("Auto Win Sequence -> Main Menu")]
    [Tooltip("Sliced psycho frames (0 = leftmost faint face, 3 = rightmost close-up). Plays 3 -> 2 -> 1 -> 0.")]
    [SerializeField] private Sprite[] psychoFrames;
    [SerializeField] private float[] retreatStepDurations = { 0.65f, 0.75f, 0.90f };
    [SerializeField] private float afterimageHoldAndDissolve = 1.8f;
    [SerializeField] private TMP_FontAsset surviveFont;
    [SerializeField] private string escapedText = "YOU SURVIVED";
    [SerializeField] private float surviveFontSize = 105f;
    [SerializeField] private Color bloodTopColor = new Color(0.88f, 0.06f, 0.06f, 1f);
    [SerializeField] private Color bloodBottomColor = new Color(0.28f, 0.0f, 0.02f, 1f);
    [Tooltip("How long 'YOU SURVIVED' stays visible before automatically fading to the Main Menu.")]
    [SerializeField] private float surviveHoldBeforeMenu = 2.4f;
    [SerializeField] private float finalFadeOutTime = 1.1f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Ending Audio (Optional)")]
    [SerializeField] private AudioSource endingAudioSource;
    [SerializeField] private AudioClip escapeWindowClip;
    [SerializeField] private AudioClip surviveStingerClip;

    private bool windowBroken, escaped;
    private InteractPrompt prompt;
    private InputAction fallback;
    private SpriteRenderer windowHole;

    InputAction Interact => interactAction != null && interactAction.action != null ? interactAction.action : fallback;

    void Awake()
    {
        if (player == null)
        {
            var p = FindFirstObjectByType<TopDownMovement>();
            if (p != null) player = p.transform;
        }
        if (winManager == null)
        {
            winManager = FindFirstObjectByType<WinScreen>(FindObjectsInactive.Include);
        }

        // Upgrade legacy third-person placeholder lines if still serialized in a scene
        if (throwMessage == "He throws his knife...!")
            throwMessage = "It's a dead end—watch out, he's throwing his knife!";
        if (brokenMessage == "The window shattered! Get out! (A)")
            brokenMessage = "The glass shattered—that's my chance! Jump out the window (<color=#E8C874>A</color>)!";
    }

    void OnEnable()
    {
        if (interactAction == null || interactAction.action == null)
        {
            fallback = new InputAction("WindowInteract", InputActionType.Button);
            fallback.AddBinding("<Keyboard>/e");
            fallback.AddBinding("<Gamepad>/buttonNorth");
            fallback.AddBinding("<Joystick>/trigger");
        }
        Interact.Enable();
    }

    void OnDisable()
    {
        if (fallback != null)
        {
            fallback.Disable();
            fallback.Dispose();
            fallback = null;
        }
    }

    IEnumerator Start()
    {
        if (psycho == null || entrance == null || throwSpot == null)
        {
            Debug.LogWarning("[OfficeFinale] Not wired - run the setup step.", this);
            yield break;
        }
        while (RoomTransition.IsBusy) yield return null;              // let the fade-in finish
        if (ArcadeMessageService.Instance != null && !string.IsNullOrEmpty(enterMessage))
            ArcadeMessageService.Instance.ShowMessage(enterMessage);
        yield return new WaitForSeconds(delayBehindHer);

        Debug.Log("[OfficeFinale] He bursts out of the door behind her.", this);
        CameraShake.Shake(0.3f, 0.1f);
        if (ArcadeMessageService.Instance != null && !string.IsNullOrEmpty(chaseMessage))
            ArcadeMessageService.Instance.ShowMessage(chaseMessage);
        psycho.VisionOff = true;                                       // this is a pursuit, not a search
        psycho.ScriptSpeed = corridorSpeed;
        psycho.ScriptAppear(entrance.position, Vector2.right);
        int n = corridorPath != null ? corridorPath.Length : 0;
        for (int i = 0; i <= n; i++)
        {
            Transform target = i < n ? corridorPath[i] : throwSpot;
            if (target == null) continue;
            psycho.ScriptWalkTo(target.position);
            while (!psycho.ScriptArrived)
            {
                if (psycho.Current == EntityAI.State.Grabbing) yield break;
                if (player != null && psycho.Current != EntityAI.State.Chasing &&
                    Vector2.Distance(psycho.transform.position, player.position) <= catchDistance)
                {
                    Debug.Log("[OfficeFinale] He caught up with her.", this);
                    psycho.ChaseNow();                                 // the chase ends in a grab
                    yield break;
                }
                yield return null;
            }
        }

        // at the doorway: stop, wind up, throw
        var anim = psycho.GetComponentInChildren<DirectionalSpriteAnimator>();
        if (anim != null && player != null) anim.FaceTowards(player.position);
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(throwMessage);
        yield return new WaitForSeconds(windUp);
        yield return ThrowKnife();
        BreakWindow();

        // the knife is gone, but he isn't: he comes at her with his bare hands
        yield return new WaitForSeconds(0.4f);
        psycho.ScriptSpeed = afterThrowSpeed;
        while (!escaped && player != null)
        {
            if (psycho.Current == EntityAI.State.Grabbing) yield break;
            if (psycho.Current != EntityAI.State.Chasing &&
                Vector2.Distance(psycho.transform.position, player.position) <= catchDistance)
            {
                Debug.Log("[OfficeFinale] He reached her before she got out.", this);
                psycho.ChaseNow();
                yield break;
            }
            psycho.ScriptWalkTo(player.position);
            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator ThrowKnife()
    {
        Vector3 from = psycho.transform.position + Vector3.up * 1.1f;
        Vector3 to = window != null ? window.position : from + Vector3.right * 8f;
        // aimed at her: the knife passes close to her, then flies on into the glass
        Vector3 aim = player != null ? player.position + Vector3.up * 1f : (from + to) * 0.5f;
        var knife = MakeKnife();
        knife.transform.position = from;
        float d1 = Vector3.Distance(from, aim), d2 = Vector3.Distance(aim, to);
        float t1 = d1 / knifeSpeed, t2 = d2 / knifeSpeed;
        Debug.Log("[OfficeFinale] Knife thrown - it misses her and flies into the window.", this);
        for (float e = 0f; e < t1 + t2; e += Time.deltaTime)
        {
            Vector3 p = e < t1 ? Vector3.Lerp(from, aim + Vector3.up * 0.35f, e / t1)            // just over her head
                               : Vector3.Lerp(aim + Vector3.up * 0.35f, to, (e - t1) / t2);
            knife.transform.position = p;
            knife.transform.Rotate(0f, 0f, -1080f * Time.deltaTime);                          // spinning
            yield return null;
        }
        knife.transform.position = to;
        knife.transform.rotation = Quaternion.Euler(0f, 0f, -20f);                            // stuck in the frame
    }

    void BreakWindow()
    {
        windowBroken = true;
        CameraShake.Shake(0.4f, 0.18f);
        if (window != null)
        {
            windowHole = MakeSprite("BrokenWindow", HoleSprite(), window.position, 2);
            windowHole.color = new Color(1f, 1f, 1f, 0.95f);
            StartCoroutine(Shards(window.position));
        }
        if (ArcadeMessageService.Instance != null) ArcadeMessageService.Instance.ShowMessage(brokenMessage);
        Debug.Log("[OfficeFinale] The window is broken - she can climb out (A at the window).", this);
    }

    void Update()
    {
        if (!windowBroken || escaped || player == null) return;
        if (prompt == null) prompt = player.GetComponentInChildren<InteractPrompt>(true);
        var move = player.GetComponent<TopDownMovement>();
        Vector2 stand = windowStand != null ? (Vector2)windowStand.position : (Vector2)window.position;
        bool inRange = (move == null || (move.enabled && move.CanMove)) && Vector2.Distance(player.position, stand) <= windowReach;
        if (prompt != null) prompt.Request(this, inRange);
        if (inRange && Interact.WasPressedThisFrame()) Escape();
    }

    void Escape()
    {
        escaped = true;
        if (prompt != null) prompt.Request(this, false);

        if (PlayerItemHintPopup.Instance != null)
        {
            PlayerItemHintPopup.Instance.HideHint(null);
        }

        Debug.Log("[OfficeFinale] She climbs out of the window - SURVIVED.", this);
        if (psycho != null) psycho.ScriptVanish();                    // he's left behind
        var move = player.GetComponent<TopDownMovement>();
        if (move != null) move.CanMove = false;

        if (endingAudioSource != null && escapeWindowClip != null)
        {
            endingAudioSource.PlayOneShot(escapeWindowClip);
        }
        else
        {
            GameAudio.Play(GameAudio.Sounds != null ? GameAudio.Sounds.girlExhaust : null);
        }

        // Run the self-contained ending sequence so it flows cleanly to Main Menu
        StartCoroutine(OwnEnding());
    }

    // Plays right-to-left psycho retreat (3 -> 0) into cold night mist, ghostly afterimage dissolve,
    // high-contrast "YOU ESCAPED" reveal with subtitle + interactive Main Menu prompt, then fades out.
    IEnumerator OwnEnding()
    {
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        EnsureWorkingEventSystem();

        var go = new GameObject("EscapeWinEnding_Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 2000;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        var rootGroup = go.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = true;

        var bg = CreateFullImage("BlackBG", go.transform, preserveAspect: false);
        bg.color = Color.black;
        bg.raycastTarget = true;

        // Subtle cold moonlight horizon layer that rises as she escapes outside
        var nightGlow = CreateFullImage("NightGlow", go.transform, preserveAspect: false);
        nightGlow.sprite = MakeRadialGlowSprite();
        nightGlow.color = new Color(0.16f, 0.24f, 0.38f, 0f);

        var faceCurrent = CreateFullImage("Psycho_Current", go.transform, preserveAspect: true);
        var faceBlend = CreateFullImage("Psycho_Blend", go.transform, preserveAspect: true);
        var faceAfterimage = CreateFullImage("Psycho_Afterimage", go.transform, preserveAspect: true);
        faceCurrent.enabled = faceBlend.enabled = faceAfterimage.enabled = false;

        // Readability shroud over the receding silhouette so text & button are crystal clear
        var readabilityShroud = CreateFullImage("ReadabilityShroud", go.transform, preserveAspect: false);
        readabilityShroud.color = new Color(0.02f, 0.04f, 0.08f, 0f);

        // Cold window-leap flash settling into darkness
        var leapFlash = CreateFullImage("LeapFlash", go.transform, preserveAspect: false);
        leapFlash.color = new Color(0.72f, 0.84f, 1f, 0.42f);

        // 1. Leap out into the night (cold flash -> deep night black)
        for (float e = 0f; e < 0.38f; e += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(e / 0.38f);
            rootGroup.alpha = Mathf.Clamp01(k * 1.4f);
            leapFlash.color = new Color(0.72f, 0.84f, 1f, Mathf.Lerp(0.42f, 0f, k * k));
            yield return null;
        }
        rootGroup.alpha = 1f;
        leapFlash.enabled = false;
        yield return new WaitForSecondsRealtime(0.12f);

        // 2. Right-to-Left Psycho Retreat (3 -> 2 -> 1 -> 0) + Cold Desaturating Distance + Afterimage
        if (psychoFrames != null && psychoFrames.Length > 0 && HasAnySprite(psychoFrames))
        {
            int rightIdx = psychoFrames.Length - 1;
            faceCurrent.enabled = true;
            faceCurrent.sprite = psychoFrames[rightIdx];
            faceCurrent.color = Color.white;

            int step = 0;
            for (int i = rightIdx; i > 0; i--)
            {
                Sprite closeSp = psychoFrames[i];
                Sprite farSp = psychoFrames[i - 1];
                float dur = (retreatStepDurations != null && step < retreatStepDurations.Length) ? retreatStepDurations[step] : 0.72f;
                step++;

                float closeProgress = (float)(rightIdx - i) / rightIdx;
                float farProgress = (float)(rightIdx - (i - 1)) / rightIdx;
                Color closeTint = Color.Lerp(Color.white, new Color(0.58f, 0.66f, 0.80f, 0.82f), closeProgress);
                Color farTint = Color.Lerp(Color.white, new Color(0.58f, 0.66f, 0.80f, 0.72f), farProgress);

                faceCurrent.sprite = closeSp;
                faceBlend.enabled = true;
                faceBlend.sprite = farSp;

                for (float e = 0f; e < dur; e += Time.unscaledDeltaTime)
                {
                    float raw = Mathf.Clamp01(e / dur);
                    float k = Mathf.SmoothStep(0f, 1f, raw);

                    // Smooth receding pull-back into the hospital corridor behind her
                    faceCurrent.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.91f, k);
                    faceBlend.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, k);

                    faceCurrent.color = new Color(closeTint.r, closeTint.g, closeTint.b, closeTint.a * (1f - k));
                    faceBlend.color = new Color(farTint.r, farTint.g, farTint.b, farTint.a * k);

                    float overall = Mathf.Lerp(closeProgress, farProgress, k);
                    nightGlow.color = new Color(0.16f, 0.25f, 0.42f, Mathf.Lerp(0f, 0.22f, overall));
                    yield return null;
                }

                faceCurrent.sprite = farSp;
                faceCurrent.color = farTint;
                faceCurrent.rectTransform.localScale = Vector3.one;
                faceBlend.enabled = false;
            }

            // Lingering ghostly afterimage on frame 0 sinking into cold night mist
            faceAfterimage.enabled = true;
            faceAfterimage.sprite = psychoFrames[0];
            Color startLastColor = faceCurrent.color;

            for (float e = 0f; e < afterimageHoldAndDissolve; e += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(e / afterimageHoldAndDissolve);
                float sk = Mathf.SmoothStep(0f, 1f, k);

                faceCurrent.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.80f, sk);
                faceCurrent.color = new Color(startLastColor.r, startLastColor.g, startLastColor.b, Mathf.Lerp(startLastColor.a, 0.14f, sk));

                float echoAlpha = Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI) * 0.40f * (1f - k);
                faceAfterimage.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.01f, 1.15f, sk);
                faceAfterimage.rectTransform.anchoredPosition = Random.insideUnitCircle * (1.2f * (1f - k));
                faceAfterimage.color = new Color(0.68f, 0.78f, 0.92f, echoAlpha);

                readabilityShroud.color = new Color(0.02f, 0.04f, 0.08f, Mathf.Lerp(0f, 0.68f, sk));
                nightGlow.color = new Color(0.18f, 0.28f, 0.46f, Mathf.Lerp(0.22f, 0.42f, sk));
                yield return null;
            }

            // Keep a very faint distant echo frozen behind the dark shroud for atmosphere, zero clutter
            faceCurrent.color = new Color(0.42f, 0.50f, 0.65f, 0.12f);
            faceAfterimage.enabled = false;
        }
        else
        {
            readabilityShroud.color = new Color(0.02f, 0.04f, 0.08f, 0.68f);
            nightGlow.color = new Color(0.18f, 0.28f, 0.46f, 0.42f);
        }

        // 3. Cinematic "YOU ESCAPED" Title + Drifting Night Motes + Divider + Interactive Metal Button
        if (endingAudioSource != null && surviveStingerClip != null)
        {
            endingAudioSource.PlayOneShot(surviveStingerClip);
        }

        // Gentle drifting cold night-air particles in the background
        StartCoroutine(DriftNightMotes(go.transform));

        TMP_FontAsset resolvedFont = ResolveEscapeFont();

        var textGO = new GameObject("Txt_YouSurvived", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        var txtRT = textGO.GetComponent<RectTransform>();
        txtRT.anchorMin = txtRT.anchorMax = txtRT.pivot = new Vector2(0.5f, 0.5f);
        txtRT.sizeDelta = new Vector2(1500f, 260f);
        txtRT.anchoredPosition = new Vector2(0f, 68f);

        var txtGroup = textGO.AddComponent<CanvasGroup>();
        txtGroup.alpha = 0f;
        txtGroup.blocksRaycasts = false;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = string.IsNullOrWhiteSpace(escapedText) ? "YOU ESCAPED" : escapedText;
        tmp.fontSize = Mathf.Max(surviveFontSize, 125f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.characterSpacing = 22f;
        if (resolvedFont != null) tmp.font = resolvedFont;
        tmp.enableVertexGradient = true;
        tmp.color = Color.white;
        // Distinct dawn-ivory to deep crimson relief palette so "YOU ESCAPED" feels triumphant & readable
        Color crispTop = new Color(0.96f, 0.93f, 0.88f, 1f);
        Color crispBottom = new Color(0.72f, 0.16f, 0.18f, 1f);
        tmp.colorGradient = new VertexGradient(crispTop, crispTop, crispBottom, crispBottom);
        tmp.raycastTarget = false;
        ApplyEscapeTextShadow(textGO, new Vector2(0f, -6f));

        // Subtle ornamental divider line between title and epilogue
        var divGO = new GameObject("Divider", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        divGO.transform.SetParent(go.transform, false);
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin = divRT.anchorMax = divRT.pivot = new Vector2(0.5f, 0.5f);
        divRT.anchoredPosition = new Vector2(0f, -8f);
        divRT.sizeDelta = new Vector2(460f, 2f);
        var divGroup = divGO.GetComponent<CanvasGroup>();
        divGroup.alpha = 0f;
        divGroup.blocksRaycasts = false;
        var divImg = divGO.GetComponent<Image>();
        divImg.sprite = MakeRadialGlowSprite();
        divImg.color = new Color(0.78f, 0.84f, 0.95f, 0.65f);
        divImg.raycastTarget = false;

        // Atmospheric subtitle beneath "YOU ESCAPED"
        var subGO = new GameObject("Txt_EscapeSubtitle", typeof(RectTransform));
        subGO.transform.SetParent(go.transform, false);
        var subRT = subGO.GetComponent<RectTransform>();
        subRT.anchorMin = subRT.anchorMax = subRT.pivot = new Vector2(0.5f, 0.5f);
        subRT.sizeDelta = new Vector2(1200f, 80f);
        subRT.anchoredPosition = new Vector2(0f, -52f);

        var subGroup = subGO.AddComponent<CanvasGroup>();
        subGroup.alpha = 0f;
        subGroup.blocksRaycasts = false;

        var subTmp = subGO.AddComponent<TextMeshProUGUI>();
        subTmp.text = "The hospital fades behind you into the cold night air.";
        subTmp.fontSize = 30f;
        subTmp.fontStyle = FontStyles.Italic;
        subTmp.characterSpacing = 4f;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = new Color(0.84f, 0.89f, 0.96f, 0.92f);
        if (TMP_Settings.defaultFontAsset != null) subTmp.font = TMP_Settings.defaultFontAsset;
        subTmp.raycastTarget = false;
        ApplyEscapeTextShadow(subGO, new Vector2(0f, -3f));

        // Interactive Main Menu button styled with the game's riveted metal panel
        bool returnRequested = false;
        Button menuBtn = CreateEscapeMenuButton(go.transform, resolvedFont, () => returnRequested = true, out CanvasGroup btnGroup, out RectTransform btnRT);
        Vector2 btnTargetPos = new Vector2(0f, -162f);
        btnRT.anchoredPosition = btnTargetPos + new Vector2(0f, -18f);
        btnGroup.alpha = 0f;

        // Smooth focus-pull reveal on "YOU ESCAPED" + staggered divider, subtitle & button
        const float revealTime = 0.95f;
        for (float e = 0f; e < revealTime; e += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(e / revealTime);
            float easeOut = 1f - Mathf.Pow(1f - k, 3f);

            txtGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.3f));
            txtRT.localScale = Vector3.one * Mathf.Lerp(1.22f, 1f, easeOut);
            tmp.characterSpacing = Mathf.Lerp(24f, 10f, easeOut);

            float subK = Mathf.Clamp01((k - 0.30f) / 0.70f);
            divGroup.alpha = Mathf.SmoothStep(0f, 1f, subK);
            divRT.sizeDelta = new Vector2(Mathf.Lerp(120f, 520f, subK), 2.5f);
            subGroup.alpha = Mathf.SmoothStep(0f, 1f, subK);
            subRT.anchoredPosition = new Vector2(0f, Mathf.Lerp(-66f, -52f, subK));

            float btnK = Mathf.Clamp01((k - 0.52f) / 0.48f);
            float btnEase = 1f - Mathf.Pow(1f - btnK, 3f);
            btnGroup.alpha = btnEase;
            btnRT.anchoredPosition = btnTargetPos + new Vector2(0f, Mathf.Lerp(-18f, 0f, btnEase));
            yield return null;
        }
        txtGroup.alpha = 1f;
        divGroup.alpha = 1f;
        subGroup.alpha = 1f;
        btnGroup.alpha = 1f;
        btnRT.anchoredPosition = btnTargetPos;

        // 4. Hold with gentle breathing glow; player can click/press A early OR let it auto-transition
        float waitDuration = Mathf.Max(surviveHoldBeforeMenu, 4.5f);
        float holdTimer = 0f;
        while (holdTimer < waitDuration && !returnRequested)
        {
            holdTimer += Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.0f);
            Color top = Color.Lerp(crispTop, Color.white, pulse * 0.35f);
            Color bot = Color.Lerp(crispBottom, new Color(0.88f, 0.24f, 0.24f, 1f), pulse * 0.25f);
            tmp.colorGradient = new VertexGradient(top, top, bot, bot);
            nightGlow.color = new Color(0.18f, 0.28f, 0.46f, Mathf.Lerp(0.36f, 0.50f, pulse));

            if (WasEscapeContinuePressed())
            {
                GameAudio.UiClick();
                break;
            }
            yield return null;
        }

        if (menuBtn != null) menuBtn.interactable = false;

        // 5. Fade out cleanly into the Main Menu
        var curtain = CreateFullImage("FinalCurtain", go.transform, preserveAspect: false);
        curtain.color = new Color(0f, 0f, 0f, 0f);
        curtain.transform.SetAsLastSibling();

        for (float e = 0f; e < finalFadeOutTime; e += Time.unscaledDeltaTime)
        {
            curtain.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, e / finalFadeOutTime));
            yield return null;
        }
        curtain.color = Color.black;
        yield return new WaitForSecondsRealtime(0.15f);

        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else if (Application.CanStreamedLevelBeLoaded("Main_scene 1"))
        {
            SceneManager.LoadScene("Main_scene 1");
        }
        else if (ArcadeSceneManager.Instance != null)
        {
            ArcadeSceneManager.Instance.LoadWinScreen(0);
        }
        else
        {
            SceneManager.LoadScene(0);
        }
    }

    private Image CreateFullImage(string name, Transform parent, bool preserveAspect = true)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.preserveAspect = preserveAspect;
        img.raycastTarget = false;
        return img;
    }

    private TMP_FontAsset ResolveEscapeFont()
    {
        if (surviveFont != null) return surviveFont;
        TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < fonts.Length; i++)
        {
            if (fonts[i] != null && fonts[i].name.IndexOf("ChainsawCarnage", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return fonts[i];
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static void ApplyEscapeTextShadow(GameObject targetObj, Vector2 shadowOffset)
    {
        var outline = targetObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(2f, -2f);

        var shadow = targetObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = shadowOffset;
    }

    private static Button CreateEscapeMenuButton(Transform parent, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick, out CanvasGroup cg, out RectTransform rt)
    {
        var btnGO = new GameObject("Btn_ReturnMainMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup));
        btnGO.transform.SetParent(parent, false);

        rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(680f, 66f);

        cg = btnGO.GetComponent<CanvasGroup>();
        var bg = btnGO.GetComponent<Image>();
        Texture2D panelTex = Resources.Load<Texture2D>("Hotbar/Panel");
        Color normalBg = panelTex != null ? new Color(0.88f, 0.90f, 0.96f, 0.95f) : new Color(0.05f, 0.08f, 0.13f, 0.78f);
        Color hoverBg = panelTex != null ? new Color(1f, 0.72f, 0.72f, 1f) : new Color(0.14f, 0.04f, 0.06f, 0.92f);
        if (panelTex != null)
        {
            panelTex.filterMode = FilterMode.Bilinear;
            bg.sprite = Sprite.Create(panelTex, new Rect(0, 0, panelTex.width, panelTex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));
            bg.type = Image.Type.Sliced;
        }
        bg.color = normalBg;
        bg.raycastTarget = true;

        var border = btnGO.AddComponent<Outline>();
        border.effectColor = new Color(0.45f, 0.58f, 0.78f, 0.55f);
        border.effectDistance = new Vector2(1.5f, -1.5f);

        var btn = btnGO.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { GameAudio.UiClick(); onClick?.Invoke(); });

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(btnGO.transform, false);
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = new Vector2(18f, 4f); lblRT.offsetMax = new Vector2(-18f, -4f);

        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text = "Press A to Return to Main Menu";
        lbl.fontSize = 40f;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = 22f;
        lbl.fontSizeMax = 42f;
        lbl.characterSpacing = 3f;
        lbl.alignment = TextAlignmentOptions.Center;
        if (font != null) lbl.font = font;
        Color normalTxt = new Color(0.90f, 0.93f, 0.97f, 1f);
        Color hoverTxt = new Color(1f, 0.32f, 0.32f, 1f);
        lbl.color = normalTxt;
        lbl.raycastTarget = false;
        ApplyEscapeTextShadow(lblGO, new Vector2(0f, -3f));

        var trigger = btnGO.AddComponent<EventTrigger>();
        void AddEvt(EventTriggerType t, UnityEngine.Events.UnityAction cb)
        {
            var entry = new EventTrigger.Entry { eventID = t };
            entry.callback.AddListener(_ => cb());
            trigger.triggers.Add(entry);
        }
        RectTransform btnTransform = rt;
        AddEvt(EventTriggerType.PointerEnter, () =>
        {
            if (!btn.interactable) return;
            GameAudio.UiHover();
            btnTransform.localScale = Vector3.one * 1.06f;
            lbl.color = hoverTxt;
            bg.color = hoverBg;
            border.effectColor = new Color(0.95f, 0.25f, 0.25f, 0.9f);
        });
        AddEvt(EventTriggerType.PointerExit, () =>
        {
            btnTransform.localScale = Vector3.one;
            lbl.color = normalTxt;
            bg.color = normalBg;
            border.effectColor = new Color(0.45f, 0.58f, 0.78f, 0.55f);
        });

        return btn;
    }

    private bool WasEscapeContinuePressed()
    {
        if (Interact != null && Interact.WasPressedThisFrame()) return true;
        if (Keyboard.current != null &&
            (Keyboard.current.spaceKey.wasPressedThisFrame ||
             Keyboard.current.enterKey.wasPressedThisFrame ||
             Keyboard.current.uKey.wasPressedThisFrame))
        {
            return true;
        }
        return false;
    }

    private static void EnsureWorkingEventSystem()
    {
        EventSystem es = EventSystem.current != null ? EventSystem.current : Object.FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            es.gameObject.SetActive(true);
            es.enabled = true;
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Object.Destroy(legacy);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }
    }

    private IEnumerator DriftNightMotes(Transform canvasParent)
    {
        var moteRoot = new GameObject("NightMotes", typeof(RectTransform));
        moteRoot.transform.SetParent(canvasParent, false);
        var rootRT = moteRoot.GetComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;

        Sprite dot = MakeRadialGlowSprite();
        const int count = 26;
        var motes = new (RectTransform rt, Image img, Vector2 pos, Vector2 vel, float baseAlpha, float phase)[count];
        for (int i = 0; i < count; i++)
        {
            var mGO = new GameObject("Mote", typeof(RectTransform), typeof(Image));
            mGO.transform.SetParent(rootRT, false);
            var rt = mGO.GetComponent<RectTransform>();
            float size = Random.Range(6f, 22f);
            rt.sizeDelta = new Vector2(size, size);
            Vector2 startPos = new Vector2(Random.Range(-920f, 920f), Random.Range(-500f, 500f));
            rt.anchoredPosition = startPos;

            var img = mGO.GetComponent<Image>();
            img.sprite = dot;
            img.raycastTarget = false;
            float baseA = Random.Range(0.14f, 0.42f);
            img.color = new Color(0.76f, 0.86f, 1f, 0f);

            motes[i] = (rt, img, startPos, new Vector2(Random.Range(8f, 26f), Random.Range(14f, 38f)), baseA, Random.Range(0f, 6.28f));
        }

        float elapsed = 0f;
        while (moteRoot != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float globalFade = Mathf.Clamp01(elapsed / 0.8f);
            for (int i = 0; i < count; i++)
            {
                var m = motes[i];
                if (m.rt == null) continue;
                m.pos += m.vel * Time.unscaledDeltaTime;
                m.pos.x += Mathf.Sin(elapsed * 1.2f + m.phase) * 8f * Time.unscaledDeltaTime;
                if (m.pos.y > 560f) m.pos.y = -560f;
                if (m.pos.x > 980f) m.pos.x = -980f;
                m.rt.anchoredPosition = m.pos;

                float twinkle = 0.65f + 0.35f * Mathf.Sin(elapsed * 2.3f + m.phase);
                m.img.color = new Color(0.76f, 0.86f, 1f, m.baseAlpha * twinkle * globalFade);
                motes[i] = m;
            }
            yield return null;
        }
    }

    private static Sprite cachedRadialGlow;
    private static Sprite MakeRadialGlowSprite()
    {
        if (cachedRadialGlow != null) return cachedRadialGlow;
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Vector2 center = new Vector2(S * 0.5f, S * 0.62f);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - center.x) / (S * 0.55f);
                float dy = (y - center.y) / (S * 0.48f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        cachedRadialGlow = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return cachedRadialGlow;
    }

    private bool HasAnySprite(Sprite[] arr)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i] != null) return true;
        return false;
    }

    // ---------------------------------------------------------------- simple generated art
    SpriteRenderer MakeSprite(string n, Sprite sp, Vector3 at, int orderAbove)
    {
        var go = new GameObject(n);
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        if (roomArt != null) { sr.sortingLayerID = roomArt.sortingLayerID; sr.sortingOrder = roomArt.sortingOrder + orderAbove; sr.sharedMaterial = roomArt.sharedMaterial; }
        return sr;
    }

    SpriteRenderer MakeKnife()
    {
        const int W = 28, H = 7;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) tex.SetPixel(x, y, clear);
        for (int x = 0; x < 9; x++) for (int y = 2; y < 5; y++) tex.SetPixel(x, y, new Color(0.22f, 0.13f, 0.08f));      // handle
        for (int y = 1; y < 6; y++) tex.SetPixel(9, y, new Color(0.35f, 0.35f, 0.38f));                                   // guard
        for (int x = 10; x < W; x++)
        {
            int top = x < W - 6 ? 5 : 5 - (x - (W - 6));                                                                  // pointed tip
            for (int y = 2; y < Mathf.Max(3, top); y++) tex.SetPixel(x, y, y >= top - 1 ? new Color(0.92f, 0.94f, 0.97f) : new Color(0.62f, 0.65f, 0.7f));
        }
        tex.Apply();
        var sr = MakeSprite("Knife", Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 36f), Vector3.zero, 50);
        if (player != null)
        {
            var girl = player.GetComponent<SpriteRenderer>();
            if (girl != null) { sr.sortingLayerID = girl.sortingLayerID; sr.sortingOrder = girl.sortingOrder + 50; }
        }
        return sr;
    }

    // a dark, jagged hole with cracks where the glass was
    static Sprite HoleSprite()
    {
        const int W = 40, H = 96;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var rnd = new System.Random(7);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W - 0.5f, v = (y + 0.5f) / H - 0.5f;
                float edge = 0.36f + 0.06f * Mathf.Sin(y * 0.9f) + 0.04f * Mathf.Sin(y * 2.3f + 1f);
                float r = Mathf.Sqrt(u * u / (edge * edge) + v * v / 0.2f);
                float a = r < 1f ? 0.9f : 0f;
                tex.SetPixel(x, y, new Color(0.02f, 0.03f, 0.06f, a));
            }
        // cracks running out of the hole
        for (int c = 0; c < 9; c++)
        {
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, len = 10 + (float)rnd.NextDouble() * 30f;
            for (float s = 6f; s < len; s += 0.5f)
            {
                int x = Mathf.RoundToInt(W / 2f + Mathf.Cos(ang) * s * 0.45f), y = Mathf.RoundToInt(H / 2f + Mathf.Sin(ang) * s);
                if (x >= 0 && x < W && y >= 0 && y < H) tex.SetPixel(x, y, new Color(0.85f, 0.92f, 1f, 0.85f));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 45f);
    }

    IEnumerator Shards(Vector3 at)
    {
        var tex = new Texture2D(3, 3) { filterMode = FilterMode.Point };
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++) tex.SetPixel(x, y, new Color(0.8f, 0.9f, 1f, (x + y) % 2 == 0 ? 1f : 0.4f));
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, 3, 3), new Vector2(0.5f, 0.5f), 45f);
        var bits = new System.Collections.Generic.List<(Transform t, Vector2 v, float floorY)>();
        for (int i = 0; i < 22; i++)
        {
            var sr = MakeSprite("GlassShard", sp, at + (Vector3)(Random.insideUnitCircle * 0.3f), 3);
            sr.color = new Color(0.8f, 0.9f, 1f, 0.9f);
            bits.Add((sr.transform, new Vector2(Random.Range(-2.5f, -0.3f), Random.Range(0.5f, 2.5f)), at.y - Random.Range(0.6f, 1.4f)));
        }
        for (float e = 0f; e < 0.8f; e += Time.deltaTime)
        {
            for (int i = 0; i < bits.Count; i++)
            {
                var b = bits[i];
                if (b.t.position.y <= b.floorY && b.v.y < 0f) continue;                     // landed on the floor
                b.v += Vector2.down * 9f * Time.deltaTime;
                b.t.position += (Vector3)(b.v * Time.deltaTime);
                bits[i] = b;
            }
            yield return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (window != null) Gizmos.DrawWireSphere(window.position, 0.3f);
        if (windowStand != null) Gizmos.DrawWireSphere(windowStand.position, windowReach);
        Gizmos.color = Color.red;
        if (throwSpot != null) Gizmos.DrawWireSphere(throwSpot.position, 0.4f);
    }
}