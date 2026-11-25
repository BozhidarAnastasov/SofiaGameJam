using System.Collections;
using System.Collections.Generic;
using ArcadesTemplate.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Survival-horror inner-monologue system:
/// Uses the game's scratched riveted iron UI art (Resources/Hotbar/Panel & Slot) in the bottom-center HUD gap
/// between Stamina (bottom-left) and Hotbar (bottom-right), complete with her live portrait, a clue item icon
/// badge (Crowbar / Keycard / Flashlight / Power / Coat), and natural first-person thoughts near puzzles.
public class PlayerMonologue : MonoBehaviour
{
    public enum ClueIcon
    {
        None,
        Crowbar,
        Keycard,
        Flashlight,
        Power,
        Coat
    }

    private static PlayerMonologue instance;

    [Header("Timing")]
    [SerializeField] private float scanInterval = 0.25f;
    [SerializeField] private float repeatCooldown = 22f;
    [SerializeField] private float minGapBetweenAutoHints = 4.2f;
    [SerializeField] private float charsPerSecond = 50f;

    private TopDownMovement movement;
    private SpriteRenderer playerRenderer;
    private Inventory inventory;

    private Canvas overlayCanvas;
    private CanvasGroup barGroup;
    private RectTransform barRT;
    private TMP_Text speakerTagText;
    private TMP_Text monologueText;
    private RectTransform textContainerRT;
    private Image portraitImage;
    private RectTransform portraitRT;
    private GameObject clueBadgeObj;
    private Image clueIconImage;
    private RectTransform clueIconRT;
    private Coroutine displayRoutine;

    private readonly Dictionary<string, float> shownUntil = new Dictionary<string, float>();
    private float nextScanTime;
    private float lastAutoHintTime = -10f;
    private string currentDisplayedMessage;

    private bool hadCrowbar;
    private bool hadFlashlight;
    private bool hadCoat;
    private bool hadCard;
    private bool seededInitialState;
    private bool warnedAboutStalkerInRoom;

    private static Sprite cachedPanelSprite;
    private static Sprite cachedSlotSprite;
    private static readonly Dictionary<ClueIcon, Sprite> cachedClueSprites = new Dictionary<ClueIcon, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        ArcadeMessageService.FallbackHandler = msg => TrySayDirect(msg);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachIfGameplayScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachIfGameplayScene();
    }

    private static void AttachIfGameplayScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.buildIndex == 0) return;

        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (player == null) return;

        if (ArcadeMessageService.Instance == null)
        {
            var svcGO = new GameObject("RuntimeMessageService");
            svcGO.AddComponent<ArcadeMessageService>();
        }

        if (player.GetComponent<PlayerMonologue>() == null)
        {
            player.gameObject.AddComponent<PlayerMonologue>();
        }
    }

    public static bool TrySayDirect(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        if (instance == null)
        {
            var p = Object.FindFirstObjectByType<TopDownMovement>();
            if (p != null) instance = p.GetComponent<PlayerMonologue>() ?? p.gameObject.AddComponent<PlayerMonologue>();
        }
        if (instance == null) return false;
        instance.SayImmediate(message, InferClueFromMessage(message));
        return true;
    }

    private static ClueIcon InferClueFromMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return ClueIcon.None;
        string lower = msg.ToLowerInvariant();
        if (lower.Contains("crowbar") || lower.Contains("plank") || lower.Contains("boarded")) return ClueIcon.Crowbar;
        if (lower.Contains("card")) return ClueIcon.Keycard;
        if (lower.Contains("flashlight") || lower.Contains("dark") || lower.Contains("light")) return ClueIcon.Flashlight;
        if (lower.Contains("power") || lower.Contains("lever") || lower.Contains("electric")) return ClueIcon.Power;
        if (lower.Contains("coat")) return ClueIcon.Coat;
        return ClueIcon.None;
    }

    private void Awake()
    {
        instance = this;
        movement = GetComponent<TopDownMovement>();
        playerRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        BuildSubtitleUI();
    }

    private void Start()
    {
        inventory = Object.FindFirstObjectByType<Inventory>();
        SeedOwnedItems();
        RefreshPortraitSprite();
        nextScanTime = Time.time + 0.7f;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (overlayCanvas != null) Destroy(overlayCanvas.gameObject);
    }

    private void SeedOwnedItems()
    {
        hadFlashlight = GameProgress.HasFlashlight;
        hadCoat = GameProgress.HasCoat;
        hadCard = GameProgress.HasCard;
        hadCrowbar = inventory != null && inventory.HasItemContaining("Crowbar");
        seededInitialState = true;
    }

    private void Update()
    {
        if (overlayCanvas != null)
        {
            bool allowVisible = Time.timeScale > 0.01f;
            if (overlayCanvas.enabled != allowVisible)
                overlayCanvas.enabled = allowVisible;
        }

        if (Time.timeScale <= 0.01f) return;
        if (inventory == null) inventory = Object.FindFirstObjectByType<Inventory>();

        if (barGroup != null && barGroup.alpha > 0.05f)
        {
            RefreshPortraitSprite();
            if (clueBadgeObj != null && clueBadgeObj.activeSelf && clueIconRT != null)
            {
                float s = 1f + 0.07f * Mathf.Sin(Time.time * 4.2f);
                clueIconRT.localScale = new Vector3(s, s, 1f);
            }
        }

        CheckNewlyPickedUpItems();

        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + scanInterval;

        if (RoomTransition.IsBusy || EnemyIsHunting()) return;
        if (Time.time - lastAutoHintTime < minGapBetweenAutoHints) return;

        EvaluateNearbyPuzzleHints();
    }

    private void RefreshPortraitSprite()
    {
        if (portraitImage == null || playerRenderer == null || playerRenderer.sprite == null) return;
        if (!CoatHide.IsHidden)
        {
            portraitImage.sprite = playerRenderer.sprite;
            portraitImage.enabled = true;
        }
    }

    private void CheckNewlyPickedUpItems()
    {
        if (!seededInitialState) SeedOwnedItems();

        bool nowCrowbar = inventory != null && inventory.HasItemContaining("Crowbar");
        if (nowCrowbar && !hadCrowbar)
        {
            SayImmediate("A rusted <color=#E88374>crowbar</color>... strong enough to rip the planks off those boarded doors.", ClueIcon.Crowbar);
        }
        hadCrowbar = nowCrowbar;

        bool nowFlashlight = GameProgress.HasFlashlight;
        if (nowFlashlight && !hadFlashlight)
        {
            SayImmediate("It still works! Press <color=#E8C874>D</color> for the <color=#E8C874>flashlight</color>—now I can see inside dark wards.", ClueIcon.Flashlight);
        }
        hadFlashlight = nowFlashlight;

        bool nowCoat = GameProgress.HasCoat;
        if (nowCoat && !hadCoat)
        {
            SayImmediate("A heavy ward <color=#E8C874>coat</color>... if I select it and hold <color=#E8C874>C</color> without moving, I can hide under it.", ClueIcon.Coat);
        }
        hadCoat = nowCoat;

        bool nowCard = GameProgress.HasCard;
        if (nowCard && !hadCard)
        {
            SayImmediate("Dr. Mundo's <color=#E88374>keycard</color>! That has to unlock the administration office back in the lobby.", ClueIcon.Keycard);
        }
        hadCard = nowCard;
    }

    private void EvaluateNearbyPuzzleHints()
    {
        Vector2 pos = transform.position;
        string scene = gameObject.scene.name;

        // 0. Subtle stealth warning the first time she gets near the patrolling psycho in a room (skip in finale chase!)
        if (!warnedAboutStalkerInRoom && scene != "CorridorOffice" && Object.FindFirstObjectByType<OfficeFinale>() == null)
        {
            foreach (var enemy in Object.FindObjectsByType<EntityAI>(FindObjectsSortMode.None))
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                var sr = enemy.GetComponentInChildren<SpriteRenderer>();
                if (sr == null || !sr.enabled) continue;
                float d = Vector2.Distance(pos, enemy.transform.position);
                if (d <= 7.5f && d > 2.2f)
                {
                    warnedAboutStalkerInRoom = true;
                    string stealthTip = GameProgress.HasCoat
                        ? "He's right there... stay out of his light, or select my <color=#E8C874>coat</color> and hold <color=#E8C874>C</color> to hide!"
                        : "Footsteps... someone's patrolling in here. Crouch (<color=#E8C874>C</color>) behind furniture and stay out of his light!";
                    TryAutoSay("stalker_near_" + scene, stealthTip, GameProgress.HasCoat ? ClueIcon.Coat : ClueIcon.None);
                    return;
                }
            }
        }

        // 1. Boarded-up doors (BarricadedDoor)
        foreach (var bar in Object.FindObjectsByType<BarricadedDoor>(FindObjectsSortMode.None))
        {
            var exit = bar.GetComponent<DoorExit>();
            if (exit == null || exit.enabled) continue;
            if (Vector2.Distance(pos, bar.transform.position) > 2.2f) continue;
            if (bar.HasToolSelected) return;

            bool hasCrowbar = inventory != null && inventory.HasItemContaining("Crowbar");
            if (hasCrowbar)
                TryAutoSay("barricade_select_" + bar.name, "I already found a <color=#E88374>crowbar</color>—select it in my hotbar and press <color=#E8C874>A</color> to pry these planks off.", ClueIcon.Crowbar);
            else
                TryAutoSay("barricade_need_" + bar.name, "Heavy planks are nailed right into the frame... I need a <color=#E88374>crowbar</color> to force them off.", ClueIcon.Crowbar);
            return;
        }

        // 2. Story-locked doors (Keycard office door / Power-locked SurgicalWing door)
        foreach (var dLock in Object.FindObjectsByType<DoorLock>(FindObjectsSortMode.None))
        {
            if (dLock.IsOpen) continue;
            var jam = dLock.GetComponent<JammedDoor>();
            if (jam != null && jam.IsOpen) continue;
            if (Vector2.Distance(pos, dLock.transform.position) > 2.2f) continue;

            if (dLock.Needs == DoorLock.Requirement.SelectedItem)
            {
                if (GameProgress.HasCard)
                    TryAutoSay("office_select_card", "The card reader's live—select my <color=#E88374>keycard</color> in the hotbar and swipe it (<color=#E8C874>A</color>).", ClueIcon.Keycard);
                else
                    TryAutoSay("office_need_card", "An electronic staff lock... I'll need a <color=#E88374>keycard</color>. Maybe one of the surgeons left theirs in the Surgical Wing?", ClueIcon.Keycard);
                return;
            }

            if (dLock.Flag == StoryFlag.PowerOn)
            {
                TryAutoSay("door_need_power_" + dLock.name, "The mag-lock is completely dead... I need to restore the <color=#E8C874>power</color> in the Electrical room first.", ClueIcon.Power);
                return;
            }
        }

        // 3. Electrical room high breaker lever & crate puzzle
        foreach (var lever in Object.FindObjectsByType<LeverSwitch>(FindObjectsSortMode.None))
        {
            if (lever.IsPulled) continue;
            if (Vector2.Distance(pos, lever.transform.position) > 2.6f) continue;

            if (lever.CanPullNow)
                TryAutoSay("lever_ready", "The crate's braced underneath—climb up and pull the <color=#E8C874>breaker lever</color> (<color=#E8C874>A</color>)!", ClueIcon.Power);
            else
                TryAutoSay("lever_too_high", "The main breaker is mounted way too high... if I drag that <color=#E8C874>crate</color> under it (<color=#E8C874>hold A</color>), I can climb up.", ClueIcon.Power);
            return;
        }

        // 4. Power-locked exit inside Electrical before pulling lever
        foreach (var exit in Object.FindObjectsByType<DoorExit>(FindObjectsSortMode.None))
        {
            if (!exit.enabled || !exit.NeedsPower) continue;
            if (Vector2.Distance(pos, exit.transform.position) > 2.0f) continue;

            if (scene == "Electrical")
                TryAutoSay("elec_exit_power", "The door won't budge without electricity... that <color=#E8C874>breaker lever</color> on the wall has to turn it back on.", ClueIcon.Power);
            else
                TryAutoSay("exit_power_" + exit.name, "No power to the lock... I need to find the hospital's <color=#E8C874>breaker switch</color>.", ClueIcon.Power);
            return;
        }

        // 5. Dark Operating Room barrier (ZoneLock)
        foreach (var zLock in Object.FindObjectsByType<ZoneLock>(FindObjectsSortMode.None))
        {
            var col = zLock.GetComponent<Collider2D>();
            if (col == null || !col.enabled) continue;
            if (Vector2.Distance(pos, col.ClosestPoint(pos)) > 2.1f) continue;

            if (zLock.Flag == StoryFlag.HasFlashlight)
            {
                TryAutoSay("zone_need_flashlight", "I can't see my own hands in that operating room... I need a <color=#E8C874>flashlight</color>. Maybe behind the lobby reception desk?", ClueIcon.Flashlight);
                return;
            }
        }

        // 6. Patient Room rolling hospital bed
        if (scene == "Patient Room" && !GameProgress.Get(StoryFlag.BedTakenOut))
        {
            var carry = Object.FindFirstObjectByType<BedCarryExit>();
            var bed = carry != null ? Object.FindFirstObjectByType<PushableBox>() : null;
            if (bed != null && !bed.IsGrabbed && Vector2.Distance(pos, bed.transform.position) <= 2.3f)
            {
                TryAutoSay("patient_bed_hint", "The wheels on this <color=#E8C874>hospital bed</color> still turn... if I hold <color=#E8C874>A</color> and wheel it out, I could bridge that collapsed floor.", ClueIcon.None);
                return;
            }
        }

        // 7. Floor 2 hole puzzle
        if (scene == "Floor2" && !GameProgress.BedDropped)
        {
            var f2Bed = Object.FindFirstObjectByType<Floor2Bed>();
            GameObject holeObj = GameObject.Find("Hole") ?? GameObject.Find("Blocker_hole");
            Vector2 holePos = holeObj != null ? (Vector2)holeObj.transform.position : new Vector2(3.1f, -1.35f);
            if (Vector2.Distance(pos, holePos) <= 3.0f)
            {
                if (GameProgress.Get(StoryFlag.BedTakenOut) && f2Bed != null)
                    TryAutoSay("floor2_push_bed", "Right below here is the lobby desk—shove the <color=#E8C874>bed</color> into this hole and it'll wedge across the gap!", ClueIcon.None);
                else
                    TryAutoSay("floor2_find_bed", "This hole drops straight down to the lobby gap... maybe one of the <color=#E8C874>patient rooms</color> has a rolling bed I can push in.", ClueIcon.None);
                return;
            }
        }

        // 8. Lobby floor chasm & collapsed entrance
        if (scene == "Lobby")
        {
            if (!GameProgress.BedDropped)
            {
                GameObject lobbyHole = GameObject.Find("Blocker_hole");
                Vector2 gapSpot = new Vector2(11.5f, -1.6f);
                bool nearGap = Vector2.Distance(pos, gapSpot) <= 2.8f ||
                               (lobbyHole != null && lobbyHole.TryGetComponent(out Collider2D hc) && Vector2.Distance(pos, hc.ClosestPoint(pos)) <= 1.8f);
                if (nearGap)
                {
                    if (GameProgress.Get(StoryFlag.BedTakenOut))
                        TryAutoSay("lobby_gap_bed_ready", "That bed is waiting upstairs—push it through the <color=#E8C874>second-floor hole</color> to bridge across to the desk!", ClueIcon.Flashlight);
                    else
                        TryAutoSay("lobby_gap_need_bridge", "Something glinted behind the desk, but the floor caved in... maybe I can drop something long from the <color=#E8C874>second floor</color>.", ClueIcon.Flashlight);
                    return;
                }
            }
            else if (!GameProgress.HasFlashlight)
            {
                var bridge = Object.FindFirstObjectByType<BedBridge>();
                if (bridge != null && Vector2.Distance(pos, new Vector2(11.57f, -1.6f)) <= 2.3f)
                {
                    TryAutoSay("lobby_bridge_climb", "The bed jammed right across the chasm! Press <color=#E8C874>A</color> at the foot of the bed to climb over.", ClueIcon.Flashlight);
                    return;
                }
            }

            if (GameProgress.Get(StoryFlag.EntranceCollapsed))
            {
                var collapse = Object.FindFirstObjectByType<EntranceCollapse>();
                if (collapse != null && Vector2.Distance(pos, collapse.transform.position) <= 2.2f)
                {
                    TryAutoSay("lobby_entrance_caved", "Tons of rubble... nobody's getting out through the front doors.", ClueIcon.None);
                    return;
                }
            }
        }
    }

    private bool EnemyIsHunting()
    {
        foreach (var e in Object.FindObjectsByType<EntityAI>(FindObjectsSortMode.None))
        {
            if (e != null && e.isActiveAndEnabled && e.Hunting)
                return true;
        }
        return false;
    }

    private void TryAutoSay(string key, string line, ClueIcon clue)
    {
        if (shownUntil.TryGetValue(key, out float until) && Time.time < until)
            return;

        shownUntil[key] = Time.time + repeatCooldown;
        lastAutoHintTime = Time.time;
        ShowSubtitle(line, clue, isUrgent: false);
    }

    public void SayImmediate(string rawLine, ClueIcon clue = ClueIcon.None)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return;
        if (barGroup != null && barGroup.alpha > 0.5f && currentDisplayedMessage == rawLine) return;
        ShowSubtitle(rawLine, clue, isUrgent: true);
    }

    private void ShowSubtitle(string text, ClueIcon clue, bool isUrgent)
    {
        if (monologueText == null) BuildSubtitleUI();
        if (monologueText == null) return;

        currentDisplayedMessage = text;
        ConfigureClueBadge(clue);

        if (displayRoutine != null) StopCoroutine(displayRoutine);
        displayRoutine = StartCoroutine(AnimateSubtitleRoutine(text, isUrgent));
    }

    private void ConfigureClueBadge(ClueIcon clue)
    {
        Sprite clueSp = LoadClueSprite(clue);
        bool hasClue = clue != ClueIcon.None && clueSp != null;

        if (clueBadgeObj != null) clueBadgeObj.SetActive(hasClue);
        if (hasClue && clueIconImage != null)
        {
            clueIconImage.sprite = clueSp;
        }

        if (textContainerRT != null)
        {
            // Expand right margin cleanly when no right-hand item clue badge is shown
            textContainerRT.offsetMin = new Vector2(96f, 10f);
            textContainerRT.offsetMax = new Vector2(hasClue ? -92f : -22f, -24f);
        }
    }

    private IEnumerator AnimateSubtitleRoutine(string line, bool isUrgent)
    {
        RefreshPortraitSprite();

        string formatted = (line.StartsWith("\"") || line.StartsWith("“")) ? line : $"\"{line}\"";
        monologueText.text = formatted;
        monologueText.maxVisibleCharacters = 0;
        monologueText.ForceMeshUpdate();

        int totalChars = Mathf.Max(1, monologueText.textInfo.characterCount);

        float startAlpha = barGroup.alpha;
        Vector2 targetPos = new Vector2(0f, 24f);
        Vector2 startPos = startAlpha < 0.1f ? new Vector2(0f, 12f) : barRT.anchoredPosition;

        const float fadeIn = 0.20f;
        float elapsed = 0f;
        float speed = isUrgent ? charsPerSecond * 1.45f : charsPerSecond;

        while (monologueText.maxVisibleCharacters < totalChars || barGroup.alpha < 0.99f)
        {
            elapsed += Time.deltaTime;
            float fadeK = Mathf.Clamp01(elapsed / fadeIn);
            float easeFade = 1f - Mathf.Pow(1f - fadeK, 3f);

            barGroup.alpha = Mathf.Lerp(startAlpha, 1f, easeFade);
            barRT.anchoredPosition = Vector2.Lerp(startPos, targetPos, easeFade);
            monologueText.maxVisibleCharacters = Mathf.Clamp(Mathf.FloorToInt(elapsed * speed), 0, totalChars);

            if (portraitRT != null)
            {
                float talkBob = Mathf.Abs(Mathf.Sin(elapsed * 16f)) * 2.2f;
                portraitRT.anchoredPosition = new Vector2(0f, talkBob);
            }

            yield return null;
        }

        barGroup.alpha = 1f;
        barRT.anchoredPosition = targetPos;
        if (portraitRT != null) portraitRT.anchoredPosition = Vector2.zero;
        monologueText.maxVisibleCharacters = 99999;

        float hold = Mathf.Clamp(2.0f + totalChars * 0.032f, 2.6f, 4.8f);
        yield return new WaitForSeconds(hold);

        const float fadeOut = 0.42f;
        for (float e = 0f; e < fadeOut; e += Time.deltaTime)
        {
            float k = e / fadeOut;
            barGroup.alpha = Mathf.Lerp(1f, 0f, k);
            barRT.anchoredPosition = Vector2.Lerp(targetPos, new Vector2(0f, 16f), k * k);
            yield return null;
        }

        barGroup.alpha = 0f;
        currentDisplayedMessage = null;
        displayRoutine = null;
    }

    private void BuildSubtitleUI()
    {
        if (overlayCanvas != null) return;

        var canvasGO = new GameObject("PlayerMonologue_Canvas");
        overlayCanvas = canvasGO.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 90;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Positioned in the open bottom-center HUD lane between Stamina (left) and Hotbar (right)
        var barGO = new GameObject("MonologueBar", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        barGO.transform.SetParent(canvasGO.transform, false);

        barGroup = barGO.GetComponent<CanvasGroup>();
        barGroup.alpha = 0f;
        barGroup.blocksRaycasts = false;
        barGroup.interactable = false;

        barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = barRT.anchorMax = new Vector2(0.5f, 0f);
        barRT.pivot = new Vector2(0.5f, 0f);
        barRT.anchoredPosition = new Vector2(0f, 24f);
        barRT.sizeDelta = new Vector2(880f, 92f);

        var bgImg = barGO.GetComponent<Image>();
        Sprite panelSp = LoadUISprite("Hotbar/Panel", sliced: true);
        if (panelSp != null)
        {
            bgImg.sprite = panelSp;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.92f, 0.92f, 0.95f, 0.96f);
        }
        else
        {
            bgImg.color = new Color(0.05f, 0.04f, 0.06f, 0.92f);
        }
        bgImg.raycastTarget = false;

        Sprite slotSp = LoadUISprite("Hotbar/Slot", sliced: false);

        // Left recessed character portrait frame
        var portFrameGO = new GameObject("PortraitFrame", typeof(RectTransform), typeof(Image), typeof(Mask));
        portFrameGO.transform.SetParent(barRT, false);
        var portFrameRT = portFrameGO.GetComponent<RectTransform>();
        portFrameRT.anchorMin = portFrameRT.anchorMax = new Vector2(0f, 0.5f);
        portFrameRT.pivot = new Vector2(0f, 0.5f);
        portFrameRT.anchoredPosition = new Vector2(14f, 0f);
        portFrameRT.sizeDelta = new Vector2(68f, 68f);

        var portFrameImg = portFrameGO.GetComponent<Image>();
        if (slotSp != null)
        {
            portFrameImg.sprite = slotSp;
            portFrameImg.color = Color.white;
        }
        else
        {
            portFrameImg.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        }
        portFrameImg.raycastTarget = false;
        portFrameGO.GetComponent<Mask>().showMaskGraphic = true;

        var portGO = new GameObject("GirlPortrait", typeof(RectTransform), typeof(Image));
        portGO.transform.SetParent(portFrameRT, false);
        portraitRT = portGO.GetComponent<RectTransform>();
        portraitRT.anchorMin = portraitRT.anchorMax = portraitRT.pivot = new Vector2(0.5f, 0.32f);
        portraitRT.anchoredPosition = Vector2.zero;
        portraitRT.sizeDelta = new Vector2(82f, 82f);

        portraitImage = portGO.GetComponent<Image>();
        portraitImage.preserveAspect = true;
        portraitImage.raycastTarget = false;
        portraitImage.enabled = false;

        // Small speaker header tag ("INNER THOUGHT")
        var tagGO = new GameObject("SpeakerTag", typeof(RectTransform));
        tagGO.transform.SetParent(barRT, false);
        var tagRT = tagGO.GetComponent<RectTransform>();
        tagRT.anchorMin = new Vector2(0f, 1f);
        tagRT.anchorMax = new Vector2(1f, 1f);
        tagRT.pivot = new Vector2(0.5f, 1f);
        tagRT.offsetMin = new Vector2(98f, -24f);
        tagRT.offsetMax = new Vector2(-96f, -6f);

        speakerTagText = tagGO.AddComponent<TextMeshProUGUI>();
        speakerTagText.text = "INNER THOUGHT";
        speakerTagText.fontSize = 11.5f;
        speakerTagText.characterSpacing = 6f;
        speakerTagText.alignment = TextAlignmentOptions.Left;
        speakerTagText.color = new Color(0.72f, 0.34f, 0.36f, 0.90f);
        speakerTagText.raycastTarget = false;

        // Main monologue text container
        var txtGO = new GameObject("MonologueText", typeof(RectTransform));
        txtGO.transform.SetParent(barRT, false);
        textContainerRT = txtGO.GetComponent<RectTransform>();
        textContainerRT.anchorMin = Vector2.zero;
        textContainerRT.anchorMax = Vector2.one;
        textContainerRT.offsetMin = new Vector2(96f, 10f);
        textContainerRT.offsetMax = new Vector2(-92f, -24f);

        monologueText = txtGO.AddComponent<TextMeshProUGUI>();
        monologueText.fontSize = 22f;
        monologueText.enableAutoSizing = true;
        monologueText.fontSizeMin = 16f;
        monologueText.fontSizeMax = 23f;
        monologueText.fontStyle = FontStyles.Italic;
        monologueText.alignment = TextAlignmentOptions.Left;
        monologueText.textWrappingMode = TextWrappingModes.Normal;
        monologueText.richText = true;
        monologueText.color = new Color(0.93f, 0.90f, 0.86f, 1f);
        monologueText.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            monologueText.font = TMP_Settings.defaultFontAsset;

        var outline = txtGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.92f);
        outline.effectDistance = new Vector2(1.1f, -1.1f);

        // Right recessed item clue slot (shows Crowbar / Keycard / Flashlight / Power / Coat)
        clueBadgeObj = new GameObject("ClueBadge", typeof(RectTransform), typeof(Image));
        clueBadgeObj.transform.SetParent(barRT, false);
        var clueFrameRT = clueBadgeObj.GetComponent<RectTransform>();
        clueFrameRT.anchorMin = clueFrameRT.anchorMax = new Vector2(1f, 0.5f);
        clueFrameRT.pivot = new Vector2(1f, 0.5f);
        clueFrameRT.anchoredPosition = new Vector2(-14f, 0f);
        clueFrameRT.sizeDelta = new Vector2(68f, 68f);

        var clueFrameImg = clueBadgeObj.GetComponent<Image>();
        if (slotSp != null)
        {
            clueFrameImg.sprite = slotSp;
            clueFrameImg.color = Color.white;
        }
        else
        {
            clueFrameImg.color = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        }
        clueFrameImg.raycastTarget = false;

        var iconGO = new GameObject("ClueIcon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(clueFrameRT, false);
        clueIconRT = iconGO.GetComponent<RectTransform>();
        clueIconRT.anchorMin = clueIconRT.anchorMax = clueIconRT.pivot = new Vector2(0.5f, 0.5f);
        clueIconRT.anchoredPosition = Vector2.zero;
        clueIconRT.sizeDelta = new Vector2(46f, 46f);

        clueIconImage = iconGO.GetComponent<Image>();
        clueIconImage.preserveAspect = true;
        clueIconImage.raycastTarget = false;

        clueBadgeObj.SetActive(false);
    }

    private static Sprite LoadUISprite(string resourcePath, bool sliced)
    {
        if (resourcePath == "Hotbar/Panel" && cachedPanelSprite != null) return cachedPanelSprite;
        if (resourcePath == "Hotbar/Slot" && cachedSlotSprite != null) return cachedSlotSprite;

        Texture2D tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null) return null;
        tex.filterMode = FilterMode.Bilinear;

        Vector4 border = sliced ? new Vector4(14f, 14f, 14f, 14f) : Vector4.zero;
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);

        if (resourcePath == "Hotbar/Panel") cachedPanelSprite = sp;
        else if (resourcePath == "Hotbar/Slot") cachedSlotSprite = sp;
        return sp;
    }

    private static Sprite LoadClueSprite(ClueIcon clue)
    {
        if (clue == ClueIcon.None) return null;
        if (cachedClueSprites.TryGetValue(clue, out Sprite cached) && cached != null) return cached;

        string path = clue switch
        {
            ClueIcon.Crowbar => "DoorHints/Crowbar",
            ClueIcon.Keycard => "DoorHints/Keycard",
            ClueIcon.Flashlight => "DoorHints/Flashlight",
            ClueIcon.Power => "DoorHints/Power",
            ClueIcon.Coat => "Coat_Hide",
            _ => null
        };
        if (path == null) return null;

        Sprite sp = Resources.Load<Sprite>(path);
        if (sp == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(path);
            if (tex != null)
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        cachedClueSprites[clue] = sp;
        return sp;
    }
}
