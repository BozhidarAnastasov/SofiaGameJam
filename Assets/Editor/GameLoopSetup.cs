// Tools > Horror Room > 19. Game Loop Batch 1  /  20. Game Loop Batch 2 (rockfall intro, Electrical ambush, coat walk)
//   Lobby        : the bed only lies across the hole once it was pushed down on the 2nd floor (F9 = test),
//                  SurgicalWing door needs power, office door needs the key card selected.
//   SurgicalWing : coat on the bench by the lockers (hide under it: coat selected, hold C, stay still),
//                  key card on the stool in the far top-left corner of the operating room,
//                  flickering surgery lamp with a smaller pool of light,
//                  operating room blocked until she has the flashlight.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public static class GameLoopSetup
{
    const string LobbyPath = "Assets/Scenes/Rooms/Lobby.unity";
    const string SurgPath = "Assets/Scenes/Rooms/SurgicalWing.unity";
    const string CoatPath = "Assets/Images/Rooms/SurgicalWing/Coat.png";
    const string CardPath = "Assets/Images/Rooms/SurgicalWing/Keycard.png";
    const string PilePath = "Assets/Resources/Coat_Hide.png";

    // SurgicalWing background is 1024x572 px at 40 px/unit, centred on the Background object.
    static Vector2 SurgPx(float x, float y) => new Vector2((x - 512f) / 40f, (286f - y) / 40f);

    [MenuItem("Tools/Horror Room/19. Game Loop Batch 1 (bed, coat, card, lamp, locks)")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;

        // The coat lies on the bench: art bottom at y -4.675, pivot pulled down to y -5.08 so it sorts in front of the bench.
        Import(CoatPath, new Vector2(0.5f, -0.463f));
        // The card sits on the left stool's seat: pivot at the stool's foot so it sorts just in front of the stool.
        Import(CardPath, new Vector2(0.5f, -2.986f));
        Import(PilePath, new Vector2(0.5f, 0.08f));

        SetupLobby();
        SetupSurgicalWing();

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Batch 1 done: Lobby (bed after the drop, power + card locks) and SurgicalWing (coat, card, lamp, OR lock) saved.");
    }

    static void Import(string path, Vector2 pivot)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[GameLoop] Missing texture " + path); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 40f;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
    }

    static InputActionReference ButtonA() =>
        AssetDatabase.LoadAllAssetsAtPath("Assets/Input/ArcadeControls.inputactions")
            .OfType<InputActionReference>().FirstOrDefault(r => r.action != null && r.action.name == "ButtonA");

    static GameObject Find(UnityEngine.SceneManagement.Scene scene, string name) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
             .Select(t => t.gameObject).FirstOrDefault(g => g.name == name);

    static T Get<T>(GameObject g) where T : Component { var c = g.GetComponent<T>(); return c != null ? c : g.AddComponent<T>(); }

    static GameObject Child(Transform parent, string name)
    {
        var t = parent != null ? parent.Find(name) : null;
        if (t != null) return t.gameObject;
        var g = new GameObject(name);
        if (parent != null) g.transform.SetParent(parent, false);
        return g;
    }

    // ------------------------------------------------------------------ Lobby
    static void SetupLobby()
    {
        var scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);

        var bed = Find(scene, "Bed");
        if (bed == null) Debug.LogWarning("[GameLoop] Lobby: no 'Bed' object found.");
        else
        {
            var gate = Child(bed.transform.parent, "Progress_BedDropped");
            var pt = Get<ProgressToggle>(gate);
            var so = new SerializedObject(pt);
            so.FindProperty("flag").enumValueIndex = (int)StoryFlag.BedDropped;
            var show = so.FindProperty("showWhenSet");
            show.arraySize = 1; show.GetArrayElementAtIndex(0).objectReferenceValue = bed;
            so.FindProperty("hideWhenSet").arraySize = 0;
            so.FindProperty("debugKeyEnabled").boolValue = true;
            so.ApplyModifiedProperties();
            Debug.Log("[GameLoop] Lobby: the Bed now appears only after StoryFlag.BedDropped (F9 sets it while testing).");
        }

        var surg = Find(scene, "Door_ToSurgicalWing");
        if (surg != null)
        {
            var so = new SerializedObject(Get<DoorLock>(surg));
            so.FindProperty("requirement").enumValueIndex = (int)DoorLock.Requirement.StoryFlag;
            so.FindProperty("flag").enumValueIndex = (int)StoryFlag.PowerOn;
            so.FindProperty("lockedMessage").stringValue = "The doors are electric... no power.";
            so.ApplyModifiedProperties();
        }
        else Debug.LogWarning("[GameLoop] Lobby: no 'Door_ToSurgicalWing'.");

        var office = Find(scene, "Door_ToCorridorOffice");
        if (office != null)
        {
            var so = new SerializedObject(Get<DoorLock>(office));
            so.FindProperty("requirement").enumValueIndex = (int)DoorLock.Requirement.SelectedItem;
            so.FindProperty("itemName").stringValue = "Card";
            so.FindProperty("lockedMessage").stringValue = "Locked. The card reader blinks red...";
            so.FindProperty("selectItemMessage").stringValue = "The card reader... select the key card.";
            so.ApplyModifiedProperties();
        }
        else Debug.LogWarning("[GameLoop] Lobby: no 'Door_ToCorridorOffice'.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------------ SurgicalWing
    static void SetupSurgicalWing()
    {
        var scene = EditorSceneManager.OpenScene(SurgPath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        var bgSr = all.Select(t => t.GetComponent<SpriteRenderer>())
                      .FirstOrDefault(r => r != null && r.sprite != null && r.sprite.name == "SurgicalWing");
        if (bgSr == null) { Debug.LogWarning("[GameLoop] SurgicalWing background not found."); return; }
        var bgT = bgSr.transform;
        var room = bgT.parent;
        var stoolL = Find(scene, "StoolL");
        var props = Find(scene, "Props");
        var propSr = stoolL != null ? stoolL.GetComponent<SpriteRenderer>() : null;
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var a = ButtonA();

        // --- key card on the stool in the far top-left corner
        var card = Child(props != null ? props.transform : room, "Card");
        card.transform.position = bgT.TransformPoint(new Vector2(-10.175f, 2.66f));
        card.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
        SetSprite(card, CardPath, propSr);
        Pickup(card, StoryFlag.HasCard, player, a, 1.25f, "A key card... \"OFFICE\" is scratched on it.");

        // --- coat on the bench by the lockers
        var coat = Child(props != null ? props.transform : room, "Coat");
        coat.transform.position = bgT.TransformPoint(new Vector2(-9.1f, -5.08f));
        coat.transform.localScale = Vector3.one;
        SetSprite(coat, CoatPath, propSr);
        Pickup(coat, StoryFlag.HasCoat, player, a, 1.3f, "A heavy coat. Select it, then hold C and keep still to hide under it.");

        // --- flickering surgery lamp over the table
        var lampGo = Child(room, "SurgeryLamp");
        lampGo.transform.position = bgT.TransformPoint(new Vector2(1.55f, 1.55f));
        var light = Get<Light2D>(lampGo);
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(0.82f, 0.95f, 1f);
        light.intensity = 0.9f;
        light.pointLightOuterRadius = 2.2f;
        light.pointLightInnerRadius = 0.35f;
        var lso = new SerializedObject(light);
        var layers = lso.FindProperty("m_ApplyToSortingLayers");
        if (layers != null)
        {
            layers.arraySize = SortingLayer.layers.Length;
            for (int i = 0; i < SortingLayer.layers.Length; i++) layers.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
            lso.ApplyModifiedProperties();
        }
        var lamp = Get<SurgeryLamp>(lampGo);
        var so = new SerializedObject(lamp);
        so.FindProperty("lamp").objectReferenceValue = light;
        so.FindProperty("background").objectReferenceValue = bgSr;
        so.ApplyModifiedProperties();

        // --- operating room blocked without the flashlight (the passage behind the OR doors, x 6.95..9.825)
        var lockGo = Child(room, "Lock_OperatingRoom");
        lockGo.transform.position = bgT.TransformPoint(new Vector2(8.39f, -3.28f));
        var box = Get<BoxCollider2D>(lockGo);
        box.isTrigger = false;
        box.size = new Vector2(3.1f, 0.3f);
        box.offset = Vector2.zero;
        var zso = new SerializedObject(Get<ZoneLock>(lockGo));
        zso.FindProperty("flag").enumValueIndex = (int)StoryFlag.HasFlashlight;
        zso.FindProperty("lockedMessage").stringValue = "It's pitch black in there... I need a light.";
        zso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void SetSprite(GameObject g, string path, SpriteRenderer like)
    {
        var sr = Get<SpriteRenderer>(g);
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sr.sprite == null) Debug.LogWarning("[GameLoop] Sprite not found: " + path);
        if (like != null)
        {
            sr.sortingLayerID = like.sortingLayerID; sr.sortingOrder = like.sortingOrder;
            sr.sharedMaterial = like.sharedMaterial;
        }
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    static void Pickup(GameObject g, StoryFlag flag, TopDownMovement player, InputActionReference a, float reach, string msg)
    {
        var so = new SerializedObject(Get<StoryPickup>(g));
        so.FindProperty("flag").enumValueIndex = (int)flag;
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        so.FindProperty("interactAction").objectReferenceValue = a;
        so.FindProperty("reach").floatValue = reach;
        so.FindProperty("pickupMessage").stringValue = msg;
        so.ApplyModifiedProperties();
    }

    // =====================================================================================
    // 20. Game Loop Batch 2: Lobby rockfall intro, Electrical ambush rework, the psycho's coat walk
    // =====================================================================================
    const string ElecPath = "Assets/Scenes/Rooms/Electrical.unity";
    const string GenDir = "Assets/Prefabs/Generated";
    const string PsychoPrefab = GenDir + "/Psycho.prefab";
    const string DeathPrefab = GenDir + "/PsychoDeathScreen.prefab";
    const string OpeningPath = "Assets/Images/Rooms/Lobby/Lobby_EntranceOpening.png";
    const string RubblePath = "Assets/Images/Rooms/Lobby/Lobby_EntranceRubble.png";
    const string HolePath = "Assets/Images/Rooms/Lobby/Lobby_ElectricalDoorHole.png";

    // Lobby background: 1024x572 px at 28 px/unit, centred on Room_Lobby.
    static Vector2 LobbyPx(float x, float y) => new Vector2((x - 512f) / 28f, (286f - y) / 28f);

    [MenuItem("Tools/Horror Room/20. Game Loop Batch 2 (rockfall intro, Electrical ambush, coat walk)")]
    public static void RunBatch2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;

        foreach (var p in new[] { OpeningPath, RubblePath, HolePath }) ImportPpu(p, 28f, new Vector2(0.5f, 0.5f));
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Generated");

        if (!SetupElectrical()) return;
        SetupLobbyBatch2();

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Batch 2 done: Electrical (no power-off, door stays kicked, he waits there after the coat walk) " +
                  "and Lobby (entrance + rockfall, psycho walk after the coat, dark hole instead of the Electrical door) saved.");
    }

    static void ImportPpu(string path, float ppu, Vector2 pivot)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[GameLoop] Missing texture " + path); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
    }

    static void AllLayers(Light2D light)
    {
        var so = new SerializedObject(light);
        var layers = so.FindProperty("m_ApplyToSortingLayers");
        if (layers == null) return;
        layers.arraySize = SortingLayer.layers.Length;
        for (int i = 0; i < SortingLayer.layers.Length; i++) layers.GetArrayElementAtIndex(i).intValue = SortingLayer.layers[i].id;
        so.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------ Electrical
    static bool SetupElectrical()
    {
        var scene = EditorSceneManager.OpenScene(ElecPath, OpenSceneMode.Single);
        var psycho = Object.FindFirstObjectByType<EntityAI>();
        var death = Object.FindFirstObjectByType<DeathScreen>(FindObjectsInactive.Include);
        if (psycho == null || death == null) { Debug.LogWarning("[GameLoop] Electrical: psycho or death screen not found - stopped."); return false; }

        var so = new SerializedObject(psycho);
        so.FindProperty("pushLeverBack").boolValue = false;          // no more power-off
        so.FindProperty("lurksInElectrical").boolValue = true;       // after the coat walk he waits here
        so.FindProperty("standingAlwaysSeen").boolValue = true;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // copies for the Lobby (same art, animator, vision cone and death screen)
        PrefabUtility.SaveAsPrefabAsset(psycho.gameObject, PsychoPrefab);
        PrefabUtility.SaveAsPrefabAsset(death.gameObject, DeathPrefab);
        Debug.Log("[GameLoop] Electrical updated; psycho + death screen saved as prefabs for the Lobby.");
        return true;
    }

    // ------------------------------------------------------------------ Lobby
    static void SetupLobbyBatch2()
    {
        var scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        var room = Find(scene, "Room_Lobby");
        var bgGo = Find(scene, "Background");
        var swGo = Find(scene, "SouthWall");
        var bgSr = bgGo != null ? bgGo.GetComponent<SpriteRenderer>() : null;
        var swSr = swGo != null ? swGo.GetComponent<SpriteRenderer>() : null;
        if (room == null || bgSr == null) { Debug.LogWarning("[GameLoop] Lobby: Room_Lobby / Background not found."); return; }
        var rt = room.transform;
        var player = Object.FindFirstObjectByType<TopDownMovement>();

        // ---- 1. start of the game: she stands just inside the entrance in the near (south) wall
        Vector2 start = LobbyPx(500f, 499f);                                    // (-0.43, -7.6)
        if (player != null) { Undo.RecordObject(player.transform, "start"); player.transform.position = new Vector3(start.x, start.y, player.transform.position.z); }
        var spawnMarker = Find(scene, "PlayerSpawn");
        if (spawnMarker != null) spawnMarker.transform.position = start;

        var col = Child(rt, "EntranceCollapse");
        col.transform.position = LobbyPx(500f, 538f);
        var landing = Child(col.transform, "Landing"); landing.transform.position = LobbyPx(500f, 538f);

        var opening = Get<SpriteRenderer>(Child(col.transform, "Opening"));
        opening.transform.position = LobbyPx(500f, 553f);
        opening.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OpeningPath);
        var wallLike = swSr != null ? swSr : bgSr;
        opening.sortingLayerID = wallLike.sortingLayerID; opening.sortingOrder = wallLike.sortingOrder + 1; opening.sharedMaterial = wallLike.sharedMaterial;

        var rubble = Get<SpriteRenderer>(Child(col.transform, "RubbleShadow"));
        rubble.transform.position = LobbyPx(500f, 542f);
        rubble.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RubblePath);
        rubble.sortingLayerID = bgSr.sortingLayerID; rubble.sortingOrder = bgSr.sortingOrder + 2; rubble.sharedMaterial = bgSr.sharedMaterial;

        var spillGo = Child(col.transform, "MoonlightSpill");
        spillGo.transform.position = LobbyPx(500f, 568f);
        spillGo.transform.rotation = Quaternion.identity;                       // shines up into the room
        var spill = Get<Light2D>(spillGo);
        spill.lightType = Light2D.LightType.Point;
        spill.color = new Color(0.62f, 0.74f, 1f);
        spill.intensity = 1.2f;
        spill.pointLightOuterRadius = 4.5f; spill.pointLightInnerRadius = 0.4f;
        spill.pointLightOuterAngle = 140f; spill.pointLightInnerAngle = 70f;
        AllLayers(spill);

        var ec = new SerializedObject(Get<EntranceCollapse>(col));
        ec.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        ec.FindProperty("opening").objectReferenceValue = opening;
        ec.FindProperty("rubbleShadow").objectReferenceValue = rubble;
        ec.FindProperty("spill").objectReferenceValue = spill;
        ec.FindProperty("floor").objectReferenceValue = bgSr;
        ec.FindProperty("landingCentre").objectReferenceValue = landing.transform;
        ec.FindProperty("landingWidth").floatValue = 2.6f;
        ec.ApplyModifiedProperties();

        // ---- 2. the Electrical side door becomes a dark hole once he kicked it in
        var door = Find(scene, "Door_ToElectrical");
        if (door != null)
        {
            var hole = Get<SpriteRenderer>(Child(door.transform, "KickedHole"));
            hole.transform.position = LobbyPx(46f, 279f);
            hole.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HolePath);
            hole.sortingLayerID = bgSr.sortingLayerID; hole.sortingOrder = bgSr.sortingOrder + 3; hole.sharedMaterial = bgSr.sharedMaterial;
            var gate = Child(rt, "Progress_DoorKicked");
            var pso = new SerializedObject(Get<ProgressToggle>(gate));
            pso.FindProperty("flag").enumValueIndex = (int)StoryFlag.ElectricalDoorKicked;
            var show = pso.FindProperty("showWhenSet"); show.arraySize = 1; show.GetArrayElementAtIndex(0).objectReferenceValue = hole.gameObject;
            pso.FindProperty("hideWhenSet").arraySize = 0;
            pso.FindProperty("debugKeyEnabled").boolValue = false;
            pso.ApplyModifiedProperties();
        }
        else Debug.LogWarning("[GameLoop] Lobby: no Door_ToElectrical.");

        // ---- 3. the psycho + his death screen, and his walk after the coat
        var old = Find(scene, "Psycho"); if (old != null) Object.DestroyImmediate(old);
        var oldDeath = Find(scene, "PsychoDeathScreen"); if (oldDeath != null) Object.DestroyImmediate(oldDeath);
        var death = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefab), scene);
        death.name = "PsychoDeathScreen";
        var psychoGo = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PsychoPrefab), scene);
        psychoGo.name = "Psycho";

        var walk = Child(rt, "PsychoWalk_Coat");
        Transform P(string n, Vector2 at) { var g = Child(walk.transform, n); g.transform.position = at; return g.transform; }
        var toStairs = new[]
        {
            P("S0_BottomRight", new Vector2(13.6f, -5.3f)),
            P("S1", new Vector2(5.0f, -3.2f)),
            P("S2", new Vector2(-1.3f, -1.0f)),
            P("S3", new Vector2(-1.6f, 2.0f)),
            P("S4", new Vector2(-11.8f, 1.5f)),
            P("S5_StairsFoot", new Vector2(-14.9f, 0.8f)),
        };
        var top = P("StairsTop", new Vector2(-14.85f, 3.9f));
        var toElec = new[]
        {
            P("E0", new Vector2(-14.9f, 0.8f)),
            P("E1_ElectricalDoor", new Vector2(-15.9f, 0.0f)),
        };
        psychoGo.transform.position = toStairs[0].position;

        var ai = psychoGo.GetComponent<EntityAI>();
        var aso = new SerializedObject(ai);
        aso.FindProperty("girl").objectReferenceValue = player != null ? player.transform : null;
        aso.FindProperty("deathScreen").objectReferenceValue = death.GetComponent<DeathScreen>();
        aso.FindProperty("lurksInElectrical").boolValue = false;
        aso.FindProperty("standingAlwaysSeen").boolValue = false;            // big room: only his cone counts
        aso.FindProperty("patrolPoints").arraySize = 0;
        foreach (var n in new[] { "lever", "leverStandPoint", "doorOutside", "doorInside", "doorOpen", "brokenDoor", "lurkPoint" })
        { var pr = aso.FindProperty(n); if (pr != null) pr.objectReferenceValue = null; }
        aso.ApplyModifiedProperties();

        var lso = new SerializedObject(Get<LobbyStalker>(walk));
        lso.FindProperty("psycho").objectReferenceValue = ai;
        var ts = lso.FindProperty("toStairs"); ts.arraySize = toStairs.Length;
        for (int i = 0; i < toStairs.Length; i++) ts.GetArrayElementAtIndex(i).objectReferenceValue = toStairs[i];
        lso.FindProperty("stairsTop").objectReferenceValue = top;
        var te = lso.FindProperty("toElectrical"); te.arraySize = toElec.Length;
        for (int i = 0; i < toElec.Length; i++) te.GetArrayElementAtIndex(i).objectReferenceValue = toElec[i];
        lso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // =====================================================================================
    // 21. Electrical: add the hotbar (the same UI as the Lobby), so items survive going in and out
    // =====================================================================================
    [MenuItem("Tools/Horror Room/21. Electrical: Add Hotbar (keep items between rooms)")]
    public static void AddHotbarToElectrical()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;

        var elec = EditorSceneManager.OpenScene(ElecPath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include) != null)
        {
            Debug.Log("[GameLoop] Electrical already has a hotbar - nothing to do.");
        }
        else
        {
            var lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            var inv = lobby.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Inventory>(true)).FirstOrDefault();
            if (inv == null) { Debug.LogWarning("[GameLoop] No Inventory found in the Lobby."); EditorSceneManager.CloseScene(lobby, true); return; }
            var uiRoot = inv.transform.root.gameObject;                 // the Lobby's UI canvas (HotBar inside)
            var copy = Object.Instantiate(uiRoot);
            copy.name = uiRoot.name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, elec);
            EditorSceneManager.CloseScene(lobby, true);
            EditorSceneManager.MarkSceneDirty(elec);
            EditorSceneManager.SaveScene(elec);
            Debug.Log($"[GameLoop] Copied the Lobby's '{uiRoot.name}' (hotbar + Inventory) into Electrical and saved it.");
        }

        if (!string.IsNullOrEmpty(returnTo) && returnTo != ElecPath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
    }

    // =====================================================================================
    // 22. Floor2 -> Patient Room (Dr. Mundo's cabinet): door centred on the Dr. Mundo door + hotbar in the room
    // =====================================================================================
    const string Floor2Path = "Assets/Scenes/Rooms/Floor2.unity";
    const string PatientPath = "Assets/Scenes/Rooms/Patient Room.unity";

    [MenuItem("Tools/Horror Room/22. Floor2: Door to Patient Room (Dr. Mundo)")]
    public static void Floor2ToPatientRoom()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var a = ButtonA();

        // ---- Floor2: the door in front of "Dr. Mundo" (2ndFloor_2.png, 100 px/unit, door at x ~1.8 on the top wall)
        var f2 = EditorSceneManager.OpenScene(Floor2Path, OpenSceneMode.Single);
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var door = Find(f2, "DoorToPatients");
        if (door == null)
        {
            var spawns = Find(f2, "Spawns");
            door = Child(spawns != null ? spawns.transform : null, "DoorToPatients");
        }
        door.transform.position = new Vector3(1.8f, 2.4f, 0f);
        var exit = Get<DoorExit>(door);
        var so = new SerializedObject(exit);
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        if (a != null) so.FindProperty("interactAction").objectReferenceValue = a;
        so.FindProperty("reachDistance").floatValue = 1.3f;
        so.FindProperty("sceneName").stringValue = "Patient Room";
        so.FindProperty("entryId").stringValue = "PatientsFromFloor2";
        so.FindProperty("fainting").boolValue = true;
        so.FindProperty("requireLeverPulled").boolValue = false;
        so.FindProperty("requireKickedOpen").boolValue = false;
        so.ApplyModifiedProperties();
        // she comes back out right in front of the same door
        var back = Find(f2, "SpawnPointPatients");
        if (back != null)
        {
            back.transform.position = new Vector3(1.8f, 1.85f, 0f);
            var sp = back.GetComponent<RoomSpawnPoint>();
            if (sp != null) { var sso = new SerializedObject(sp); sso.FindProperty("entryId").stringValue = "Floor2FromPatients"; sso.FindProperty("faceDirection").vector2Value = Vector2.down; sso.ApplyModifiedProperties(); }
        }
        EditorSceneManager.MarkSceneDirty(f2);
        EditorSceneManager.SaveScene(f2);

        // ---- Patient Room: needs the hotbar so her items survive the visit
        var pr = EditorSceneManager.OpenScene(PatientPath, OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<Inventory>(FindObjectsInactive.Include) == null)
        {
            var lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            var inv = lobby.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Inventory>(true)).FirstOrDefault();
            if (inv != null)
            {
                var copy = Object.Instantiate(inv.transform.root.gameObject);
                copy.name = inv.transform.root.name;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, pr);
            }
            EditorSceneManager.CloseScene(lobby, true);
        }
        EditorSceneManager.MarkSceneDirty(pr);
        EditorSceneManager.SaveScene(pr);

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Floor2: A at the Dr. Mundo door -> Patient Room (arrives at 'PatientsFromFloor2'); coming back she stands in front of it. Patient Room has the hotbar. Both saved.");
    }

    // =====================================================================================
    // 23. Patient Room: the bed can be pushed / pulled (hold A next to it, then walk)
    // =====================================================================================
    [MenuItem("Tools/Horror Room/23. Patient Room: Push / Pull the Bed")]
    public static void PatientRoomBed()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;

        var pr = EditorSceneManager.OpenScene(PatientPath, OpenSceneMode.Single);
        var bed = Find(pr, "Bed");
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (bed == null) { Debug.LogWarning("[GameLoop] Patient Room: no 'Bed' object."); return; }
        // the bed's footprint as a box that stays inside the room's walls (the old outline poked into the wall,
        // so the first grab only shoved it out of the wall and she let go)
        var oldCol = bed.GetComponent<Collider2D>();
        Bounds bb = oldCol != null ? oldCol.bounds : new Bounds(bed.transform.position, Vector3.one);
        var wall = Find(pr, "Background") != null ? Find(pr, "Background").GetComponent<EdgeCollider2D>() : null;
        if (wall != null)
        {
            Bounds wb = wall.bounds;
            float top = wb.max.y - 0.15f, bottom = wb.min.y + 0.15f, left = wb.min.x + 1.2f, right = wb.max.x - 1.2f;
            float minX = Mathf.Max(bb.min.x, left), maxX = Mathf.Min(bb.max.x, right);
            float minY = Mathf.Max(bb.min.y, bottom), maxY = Mathf.Min(bb.max.y, top);
            bb.SetMinMax(new Vector3(minX, minY, 0f), new Vector3(maxX, maxY, 0f));
        }
        foreach (var c in bed.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
        var boxCol = bed.AddComponent<BoxCollider2D>();
        Vector3 ls = bed.transform.lossyScale;
        boxCol.offset = bed.transform.InverseTransformPoint(bb.center);
        boxCol.size = new Vector2(bb.size.x / Mathf.Abs(ls.x), bb.size.y / Mathf.Abs(ls.y));
        Debug.Log($"[GameLoop] Patient Room bed collider: world {bb.min} .. {bb.max} (inside the walls).");

        var rb = Get<Rigidbody2D>(bed);
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;          // PushableBox makes it dynamic only while she holds it
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.mass = 40f;

        var box = Get<PushableBox>(bed);
        var so = new SerializedObject(box);
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        var a = ButtonA(); if (a != null) so.FindProperty("interactAction").objectReferenceValue = a;
        so.FindProperty("lever").objectReferenceValue = null;
        so.FindProperty("grabDistance").floatValue = 0.7f;   // from her feet to the bed's edge
        so.FindProperty("dragSpeed").floatValue = 0.28f;     // heavy bed: much slower than the box
        so.FindProperty("stuckDistance").floatValue = 0.4f;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(pr);
        EditorSceneManager.SaveScene(pr);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != PatientPath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Patient Room: the Bed can be pushed / pulled - stand next to it, HOLD A and walk; let go of A to drop it. Saved.");
    }

    // =====================================================================================
    // 24. The bed's journey: tilt while dragged, drag it out of the Patient Room, push it into Floor2's hole
    // =====================================================================================
    [MenuItem("Tools/Horror Room/24. Bed: Drag Out of the Cabinet + Into Floor2's Hole")]
    public static void BedJourney()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var a = ButtonA();

        // ---- Patient Room: tilt + leave the room together with the bed
        var pr = EditorSceneManager.OpenScene(PatientPath, OpenSceneMode.Single);
        var bed = Find(pr, "Bed");
        var box = bed != null ? bed.GetComponent<PushableBox>() : null;
        if (box == null) { Debug.LogWarning("[GameLoop] Patient Room: run step 23 first (the Bed has no PushableBox)."); return; }
        { var tso = new SerializedObject(Get<BedTilt>(bed)); tso.FindProperty("tiltPerSpeed").floatValue = 16f; tso.ApplyModifiedProperties(); }
        var prPlayer = Object.FindFirstObjectByType<TopDownMovement>();
        var exitGo = Find(pr, "Exit");
        var exitDoor = exitGo != null ? exitGo.GetComponent<DoorExit>() : null;
        var carry = Get<BedCarryExit>(Child(bed.transform.parent, "BedCarryExit"));
        var cso = new SerializedObject(carry);
        cso.FindProperty("bed").objectReferenceValue = box;
        cso.FindProperty("player").objectReferenceValue = prPlayer != null ? prPlayer.transform : null;
        cso.FindProperty("exit").objectReferenceValue = exitGo != null ? exitGo.transform : null;
        cso.FindProperty("reach").floatValue = exitDoor != null ? Mathf.Max(1.3f, new SerializedObject(exitDoor).FindProperty("reachDistance").floatValue) : 1.3f;
        cso.FindProperty("sceneName").stringValue = "Floor2";
        cso.FindProperty("entryId").stringValue = "Floor2FromPatients";
        cso.ApplyModifiedProperties();

        // what the bed looks like, to copy it onto the second floor
        var bedSr = bed.GetComponent<SpriteRenderer>();
        Sprite bedSprite = bedSr != null ? bedSr.sprite : null;
        int layer = bedSr != null ? bedSr.sortingLayerID : 0, order = bedSr != null ? bedSr.sortingOrder : 0;
        var mat = bedSr != null ? bedSr.sharedMaterial : null;
        var sortPoint = bedSr != null ? bedSr.spriteSortPoint : SpriteSortPoint.Center;
        float bedScale = bed.transform.lossyScale.x;
        float prGirl = prPlayer != null ? prPlayer.transform.lossyScale.x : 1f;
        EditorSceneManager.MarkSceneDirty(pr);
        EditorSceneManager.SaveScene(pr);

        // ---- Floor2: the same bed, sized to the girl there, + the hole it can fall through
        var f2 = EditorSceneManager.OpenScene(Floor2Path, OpenSceneMode.Single);
        var f2Player = Object.FindFirstObjectByType<TopDownMovement>();
        float f2Girl = f2Player != null ? f2Player.transform.lossyScale.x : 1f;
        var bg = Find(f2, "Background");
        var hole = bg != null ? bg.GetComponent<CapsuleCollider2D>() : null;

        var old = Find(f2, "Bed_FromCabinet"); if (old != null) Object.DestroyImmediate(old);
        var fb = new GameObject("Bed_FromCabinet");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fb, f2);
        fb.transform.position = new Vector3(1.8f, 0.9f, 0f);
        float sc = bedScale * (f2Girl / Mathf.Max(0.01f, prGirl));
        fb.transform.localScale = new Vector3(sc, sc, 1f);
        var sr = fb.AddComponent<SpriteRenderer>();
        sr.sprite = bedSprite; sr.sortingLayerID = layer; sr.sortingOrder = order; sr.spriteSortPoint = sortPoint;
        if (mat != null) sr.sharedMaterial = mat;
        fb.AddComponent<PolygonCollider2D>();                     // shape from the bed sprite
        var rb = fb.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.bodyType = RigidbodyType2D.Kinematic; rb.constraints = RigidbodyConstraints2D.FreezeRotation; rb.mass = 40f;
        var fbox = fb.AddComponent<PushableBox>();
        var bso = new SerializedObject(fbox);
        bso.FindProperty("player").objectReferenceValue = f2Player != null ? f2Player.transform : null;
        if (a != null) bso.FindProperty("interactAction").objectReferenceValue = a;
        bso.FindProperty("grabDistance").floatValue = 0.7f;
        bso.FindProperty("dragSpeed").floatValue = 0.28f;
        bso.FindProperty("stuckDistance").floatValue = 0.4f;
        bso.ApplyModifiedProperties();
        { var tso = new SerializedObject(fb.AddComponent<BedTilt>()); tso.FindProperty("tiltPerSpeed").floatValue = 16f; tso.ApplyModifiedProperties(); }

        var evGo = Child(bg != null ? bg.transform : null, "BedHole");
        Vector2 hc = hole != null ? (Vector2)hole.transform.TransformPoint(hole.offset) : new Vector2(3.1f, -1.35f);
        evGo.transform.position = hc;
        var ev = Get<Floor2Bed>(evGo);
        var eso = new SerializedObject(ev);
        eso.FindProperty("bed").objectReferenceValue = fbox;
        eso.FindProperty("player").objectReferenceValue = f2Player != null ? f2Player.transform : null;
        eso.FindProperty("holeCollider").objectReferenceValue = hole;
        eso.FindProperty("holeCentre").objectReferenceValue = evGo.transform;
        eso.FindProperty("holeRadii").vector2Value = hole != null ? new Vector2(hole.size.x * 0.36f, hole.size.y * 0.33f) : new Vector2(1.25f, 0.9f);
        eso.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(f2);
        EditorSceneManager.SaveScene(f2);

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log($"[GameLoop] Bed journey set up: Patient Room bed tilts and leaves with her; Floor2 has 'Bed_FromCabinet' (scale {sc:0.###}) and the hole at {hc}. Both saved.");
    }

    // =====================================================================================
    // 25. Patient Room: when she first drags the bed his shadow creeps in, he walks round the bed, waits, leaves
    // =====================================================================================
    [MenuItem("Tools/Horror Room/25. Patient Room: Psycho Visit When the Bed Moves")]
    public static void PatientRoomVisitSetup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var psychoAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PsychoPrefab);
        var deathAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefab);
        if (psychoAsset == null || deathAsset == null) { Debug.LogWarning("[GameLoop] Run step 20 first (it makes the psycho prefabs)."); return; }

        var pr = EditorSceneManager.OpenScene(PatientPath, OpenSceneMode.Single);
        var bed = Find(pr, "Bed");
        var box = bed != null ? bed.GetComponent<PushableBox>() : null;
        if (box == null) { Debug.LogWarning("[GameLoop] Patient Room: run step 23 first (the bed isn't draggable yet)."); return; }
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var bgGo = Find(pr, "Background");
        var floor = bgGo != null ? bgGo.GetComponent<SpriteRenderer>() : null;

        var old = Find(pr, "Psycho"); if (old != null) Object.DestroyImmediate(old);
        var oldDeath = Find(pr, "PsychoDeathScreen"); if (oldDeath != null) Object.DestroyImmediate(oldDeath);
        var death = (GameObject)PrefabUtility.InstantiatePrefab(deathAsset, pr); death.name = "PsychoDeathScreen";
        var psychoGo = (GameObject)PrefabUtility.InstantiatePrefab(psychoAsset, pr); psychoGo.name = "Psycho";
        // same size relation to the girl as in Electrical (the girl there has scale 1)
        float girl = player != null ? player.transform.lossyScale.x : 1f;
        psychoGo.transform.localScale = new Vector3(girl, girl, 1f);

        var visitGo = Find(pr, "PsychoVisit_Bed");
        if (visitGo == null) { visitGo = new GameObject("PsychoVisit_Bed"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(visitGo, pr); }
        var doorPt = Child(visitGo.transform, "Door"); doorPt.transform.position = new Vector3(0f, -4.4f, 0f);
        psychoGo.transform.position = doorPt.transform.position;

        var ai = psychoGo.GetComponent<EntityAI>();
        var aso = new SerializedObject(ai);
        aso.FindProperty("girl").objectReferenceValue = player != null ? player.transform : null;
        aso.FindProperty("deathScreen").objectReferenceValue = death.GetComponent<DeathScreen>();
        aso.FindProperty("lurksInElectrical").boolValue = false;
        aso.FindProperty("standingAlwaysSeen").boolValue = false;      // only his cone counts: stay out of it / hide
        aso.FindProperty("patrolPoints").arraySize = 0;
        foreach (var n in new[] { "lever", "leverStandPoint", "doorOutside", "doorInside", "doorOpen", "brokenDoor", "lurkPoint" })
        { var pp = aso.FindProperty(n); if (pp != null) pp.objectReferenceValue = null; }
        aso.ApplyModifiedProperties();

        var vso = new SerializedObject(Get<PatientRoomVisit>(visitGo));
        vso.FindProperty("psycho").objectReferenceValue = ai;
        vso.FindProperty("bed").objectReferenceValue = box;
        vso.FindProperty("door").objectReferenceValue = doorPt.transform;
        vso.FindProperty("floor").objectReferenceValue = floor;
        vso.FindProperty("startDelay").floatValue = 4f;
        vso.FindProperty("roundTime").floatValue = 3f;
        vso.FindProperty("idleTime").floatValue = 3f;
        var wall = bgGo != null ? bgGo.GetComponent<EdgeCollider2D>() : null;
        if (wall != null)
        {
            Bounds wb = wall.bounds;
            vso.FindProperty("floorArea").rectValue = Rect.MinMaxRect(wb.min.x + 1.3f, wb.min.y + 0.3f, wb.max.x - 1.3f, wb.max.y - 0.25f);
        }
        vso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(pr);
        EditorSceneManager.SaveScene(pr);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != PatientPath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Patient Room: first time she drags the bed, his shadow creeps in through the door, he walks round the bed (3 s), waits (3 s) and leaves. Saved.");
    }

    // =====================================================================================
    // 26. Floor2: after the bed crashes through the hole he comes up the stairs and patrols (hole, then desk) until she leaves
    // =====================================================================================
    [MenuItem("Tools/Horror Room/26. Floor2: Psycho Patrol After the Bed Falls")]
    public static void Floor2PatrolSetup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var psychoAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PsychoPrefab);
        var deathAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefab);
        if (psychoAsset == null || deathAsset == null) { Debug.LogWarning("[GameLoop] Run step 20 first (it makes the psycho prefabs)."); return; }

        var f2 = EditorSceneManager.OpenScene(Floor2Path, OpenSceneMode.Single);
        var player = Object.FindFirstObjectByType<TopDownMovement>();

        var old = Find(f2, "Psycho"); if (old != null) Object.DestroyImmediate(old);
        var oldDeath = Find(f2, "PsychoDeathScreen"); if (oldDeath != null) Object.DestroyImmediate(oldDeath);
        var death = (GameObject)PrefabUtility.InstantiatePrefab(deathAsset, f2); death.name = "PsychoDeathScreen";
        var psychoGo = (GameObject)PrefabUtility.InstantiatePrefab(psychoAsset, f2); psychoGo.name = "Psycho";
        float girl = player != null ? player.transform.lossyScale.x : 1f;       // same size relation as in Electrical
        psychoGo.transform.localScale = new Vector3(girl, girl, 1f);

        var root = Find(f2, "PsychoPatrol_Floor2");
        if (root == null) { root = new GameObject("PsychoPatrol_Floor2"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, f2); }
        Transform P(string n, float x, float y) { var g = Child(root.transform, n); g.transform.position = new Vector3(x, y, 0f); return g.transform; }
        var top = P("StairsTop", -3.1f, 1.9f);
        var off = P("OffStairs", -2.3f, 0.9f);
        var pts = new[]
        {
            // around the hole (centre 3.1, -1.35)
            P("Hole_Top", 3.1f, 0.65f), P("Hole_Right", 5.7f, -1.35f), P("Hole_Bottom", 3.1f, -3.35f), P("Hole_Left", 0.5f, -1.35f),
            // around the desk (centre -4.5, -2.6)
            P("Desk_RightTop", -2.6f, -1.6f), P("Desk_RightBottom", -2.6f, -3.4f), P("Desk_LeftBottom", -6.4f, -3.4f), P("Desk_LeftTop", -6.4f, -1.6f),
        };
        psychoGo.transform.position = top.position;

        var ai = psychoGo.GetComponent<EntityAI>();
        var aso = new SerializedObject(ai);
        aso.FindProperty("girl").objectReferenceValue = player != null ? player.transform : null;
        aso.FindProperty("deathScreen").objectReferenceValue = death.GetComponent<DeathScreen>();
        aso.FindProperty("lurksInElectrical").boolValue = false;
        aso.FindProperty("standingAlwaysSeen").boolValue = false;      // big floor: only his cone counts
        aso.FindProperty("patrolPoints").arraySize = 0;
        foreach (var n in new[] { "lever", "leverStandPoint", "doorOutside", "doorInside", "doorOpen", "brokenDoor", "lurkPoint" })
        { var pp = aso.FindProperty(n); if (pp != null) pp.objectReferenceValue = null; }
        aso.ApplyModifiedProperties();

        var fso = new SerializedObject(Get<Floor2Patrol>(root));
        fso.FindProperty("psycho").objectReferenceValue = ai;
        fso.FindProperty("stairsTop").objectReferenceValue = top;
        fso.FindProperty("offStairs").objectReferenceValue = off;
        var pa = fso.FindProperty("patrol"); pa.arraySize = pts.Length;
        for (int i = 0; i < pts.Length; i++) pa.GetArrayElementAtIndex(i).objectReferenceValue = pts[i];
        fso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(f2);
        EditorSceneManager.SaveScene(f2);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != Floor2Path) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Floor2: 2 s after the bed falls he comes up the stairs and patrols the hole, then the desk, in a loop until she leaves. Saved.");
    }

    // =====================================================================================
    // 27. Floor2: slower pace for her and the psycho (that floor is zoomed in and the girl is smaller)
    // =====================================================================================
    [MenuItem("Tools/Horror Room/27. Floor2: Slower Pace (girl + psycho)")]
    public static void Floor2Pace()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var f2 = EditorSceneManager.OpenScene(Floor2Path, OpenSceneMode.Single);
        var go = Find(f2, "RoomPace");
        if (go == null) { go = new GameObject("RoomPace"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, f2); }
        var so = new SerializedObject(Get<RoomPace>(go));
        so.FindProperty("factor").floatValue = 0.6f;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(f2);
        EditorSceneManager.SaveScene(f2);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != Floor2Path) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Floor2 runs at 0.6x speed for the girl and the psycho (RoomPace object). Saved.");
    }

    // =====================================================================================
    // 28. Operating room: a lamp that clearly flickers + a card that's easy to pick up
    // =====================================================================================
    [MenuItem("Tools/Horror Room/28. Operating Room: Stronger Lamp Flicker + Card Pickup")]
    public static void OperatingRoomFix()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var sw = EditorSceneManager.OpenScene(SurgPath, OpenSceneMode.Single);

        var lampGo = Find(sw, "SurgeryLamp");
        var lamp = lampGo != null ? lampGo.GetComponent<SurgeryLamp>() : null;
        if (lamp != null)
        {
            var so = new SerializedObject(lamp);
            so.FindProperty("baseIntensity").floatValue = 1.1f;
            so.FindProperty("wobble").floatValue = 0.25f;
            so.FindProperty("dipEvery").vector2Value = new Vector2(0.4f, 2.0f);
            so.FindProperty("blackoutChance").floatValue = 0.3f;
            so.FindProperty("burstChance").floatValue = 0.45f;
            so.FindProperty("ringDarkness").floatValue = 0.5f;
            so.FindProperty("dipDarkness").floatValue = 0.85f;      // the painted glow goes dark with the lamp
            so.ApplyModifiedProperties();
        }
        else Debug.LogWarning("[GameLoop] SurgicalWing: no SurgeryLamp - run step 19 first.");

        var card = Find(sw, "Card");
        var pick = card != null ? card.GetComponent<StoryPickup>() : null;
        if (pick != null)
        {
            var so = new SerializedObject(pick);
            so.FindProperty("reach").floatValue = 1.5f;
            so.ApplyModifiedProperties();
            // trigger for the team's InteractionDetector, around the card picture
            var sr = card.GetComponent<SpriteRenderer>();
            var trig = card.GetComponent<CircleCollider2D>();
            if (trig == null) trig = card.AddComponent<CircleCollider2D>();
            trig.isTrigger = true;
            trig.radius = 0.9f / Mathf.Max(0.01f, card.transform.lossyScale.x);
            if (sr != null && sr.sprite != null) trig.offset = sr.sprite.bounds.center;
        }
        else Debug.LogWarning("[GameLoop] SurgicalWing: no Card - run step 19 first.");

        EditorSceneManager.MarkSceneDirty(sw);
        EditorSceneManager.SaveScene(sw);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != SurgPath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Operating room: lamp flickers hard (stutters, dips, blackouts), card reach 1.5 + interaction trigger. Saved.");
    }

    // =====================================================================================
    // 29. Finale: the office door is jammed - mash A 20 times while he comes from the stairs
    // =====================================================================================
    [MenuItem("Tools/Horror Room/29. Finale: Jammed Office Door (mash A, he comes)")]
    public static void FinaleJammedDoor()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        var door = Find(scene, "Door_ToCorridorOffice");
        var psycho = Object.FindFirstObjectByType<EntityAI>(FindObjectsInactive.Include);
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var room = Find(scene, "Room_Lobby");
        if (door == null || psycho == null) { Debug.LogWarning("[GameLoop] Lobby: need Door_ToCorridorOffice and the Psycho (run step 20 first)."); return; }

        // the door picture (if there is one near the door) shakes with every shove
        Transform art = null;
        foreach (var r in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (r.sprite != null && r.sprite.name.ToLowerInvariant().Contains("door") && Vector2.Distance(r.transform.position, door.transform.position) < 2.5f && r.transform != door.transform)
            { art = r.transform; break; }

        var root = Child(room != null ? room.transform : null, "PsychoFinale");
        Transform P(string n, float x, float y) { var g = Child(root.transform, n); g.transform.position = new Vector3(x, y, 0f); return g.transform; }
        var pts = new[]
        {
            P("A0_StairsFoot", -14.9f, 0.8f), P("A1", -11.8f, 1.5f), P("A2", -1.6f, 1.1f), P("A3", -1.3f, -1.0f),
            P("A4", 5.0f, -3.2f), P("A5", 13.0f, -3.6f), P("A6_AtTheDoor", 14.6f, -2.4f),
        };

        var jam = Get<JammedDoor>(door);
        var so = new SerializedObject(jam);
        so.FindProperty("pressesNeeded").intValue = 40;
        so.FindProperty("doorArt").objectReferenceValue = art;
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        so.FindProperty("psycho").objectReferenceValue = psycho;
        var ap = so.FindProperty("approach"); ap.arraySize = pts.Length;
        for (int i = 0; i < pts.Length; i++) ap.GetArrayElementAtIndex(i).objectReferenceValue = pts[i];
        so.FindProperty("arriveAfter").floatValue = 9f;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != LobbyPath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Finale: with the card selected the office door is jammed - 40 A presses to force it; the first shove brings him from the stairs (9 s to the door)" + (art != null ? $", door art '{art.name}' shakes." : ".") + " Lobby saved.");
    }

    // =====================================================================================
    // 30. Office finale: he follows her down the corridor, throws his knife at the doorway, the window breaks, she escapes
    // =====================================================================================
    const string OfficePath = "Assets/Scenes/Rooms/CorridorOffice.unity";
    const string WinPrefab = GenDir + "/WinScreen.prefab";

    [MenuItem("Tools/Horror Room/30. Office Finale: Knife, Window, Escape")]
    public static void OfficeFinaleSetup()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var psychoAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PsychoPrefab);
        var deathAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DeathPrefab);
        if (psychoAsset == null || deathAsset == null) { Debug.LogWarning("[GameLoop] Run step 20 first (it makes the psycho prefabs)."); return; }

        // the escape ending: the team's EscapeEndingScreen if a prefab of it exists, otherwise OfficeFinale's own
        // ("YOU ESCAPED" -> the arcade's Win screen). No other scene is opened.
        var winAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WinPrefab);
        var sc = EditorSceneManager.OpenScene(OfficePath, OpenSceneMode.Single);
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var bgGo = Find(sc, "Background");
        var bgSr = bgGo != null ? bgGo.GetComponent<SpriteRenderer>() : null;
        foreach (var n in new[] { "Psycho", "PsychoDeathScreen", "WinScreen" }) { var o = Find(sc, n); if (o != null) Object.DestroyImmediate(o); }
        var death = (GameObject)PrefabUtility.InstantiatePrefab(deathAsset, sc); death.name = "PsychoDeathScreen";
        GameObject winGo = null;
        if (winAsset != null) { winGo = (GameObject)PrefabUtility.InstantiatePrefab(winAsset, sc); winGo.name = "WinScreen"; }
        var psychoGo = (GameObject)PrefabUtility.InstantiatePrefab(psychoAsset, sc); psychoGo.name = "Psycho";
        float girl = player != null ? player.transform.lossyScale.x / 1.15f : 1f;      // same size relation as in Electrical
        psychoGo.transform.localScale = new Vector3(girl, girl, 1f);

        var root = Find(sc, "OfficeFinale");
        if (root == null) { root = new GameObject("OfficeFinale"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, sc); }
        Transform P(string n, float x, float y) { var g = Child(root.transform, n); g.transform.position = new Vector3(x, y, 0f); return g.transform; }
        var entrance = P("Entrance_LobbyDoor", -18.0f, 0f);
        var path = new[] { P("C0", -12.0f, -0.75f), P("C1_UnderRubble", -4.6f, -0.9f), P("C2", -0.8f, -0.9f) };
        var throwSpot = P("ThrowSpot_OfficeDoorway", 7.6f, -0.2f);
        var window = P("Window", 17.95f, 4.6f);
        var stand = P("WindowStand", 17.3f, 4.3f);
        psychoGo.transform.position = entrance.position;

        var ai = psychoGo.GetComponent<EntityAI>();
        var aso = new SerializedObject(ai);
        aso.FindProperty("girl").objectReferenceValue = player != null ? player.transform : null;
        aso.FindProperty("deathScreen").objectReferenceValue = death.GetComponent<DeathScreen>();
        aso.FindProperty("lurksInElectrical").boolValue = false;
        aso.FindProperty("standingAlwaysSeen").boolValue = true;       // once he's after her, he doesn't lose her
        aso.FindProperty("patrolPoints").arraySize = 0;
        foreach (var n in new[] { "lever", "leverStandPoint", "doorOutside", "doorInside", "doorOpen", "brokenDoor", "lurkPoint" })
        { var pp = aso.FindProperty(n); if (pp != null) pp.objectReferenceValue = null; }
        aso.ApplyModifiedProperties();

        var fso = new SerializedObject(Get<OfficeFinale>(root));
        fso.FindProperty("psycho").objectReferenceValue = ai;
        fso.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        var a = ButtonA(); if (a != null) fso.FindProperty("interactAction").objectReferenceValue = a;
        fso.FindProperty("entrance").objectReferenceValue = entrance;
        var cp = fso.FindProperty("corridorPath"); cp.arraySize = path.Length;
        for (int i = 0; i < path.Length; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = path[i];
        fso.FindProperty("throwSpot").objectReferenceValue = throwSpot;
        fso.FindProperty("window").objectReferenceValue = window;
        fso.FindProperty("windowStand").objectReferenceValue = stand;
        fso.FindProperty("winScreen").objectReferenceValue = winGo != null ? winGo.GetComponent<EscapeEndingScreen>() : null;
        fso.FindProperty("roomArt").objectReferenceValue = bgSr;
        fso.FindProperty("corridorSpeed").floatValue = 3.6f;
        fso.FindProperty("afterThrowSpeed").floatValue = 3.4f;
        fso.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != OfficePath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[GameLoop] Office finale set up: 2 s after she enters he follows her down the corridor, throws the knife at the doorway, the window on the right wall breaks, A at the window -> win screen. Saved.");
    }
}
