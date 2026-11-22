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
}
