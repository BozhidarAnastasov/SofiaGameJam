// Tools > Horror Room > ...
// 1. Setup Electrical Room Import : import settings for the 1080p room so the girl (2 units) is to scale.
// 2. Add Shadow + Flashlight      : select a character (Player / Psycho) first.
// 3. Make Scene Dark              : dims the Global Light 2D to a cold blue so the flashlight matters.
// 5. Build Box + Lever Puzzle     : pushable box (cut out of the room art) + breaker lever on the left wall.
// 6. Add Door Lamp Scare          : lamp over the door flickers after the lever; last flash shows his face in the window.
// 7. Use Current Lever Pose as Pulled : pose the Lever by hand, run this, it becomes the pulled state.
// 8. Add Interact Prompt          : shows the active binding over the girl's head.
// 9. Set Arcade Controls          : A = interact, B = run, C = crawl, D = flashlight (Player prefab + open scene).
// 10. Build Psycho Ambush         : desk cover, door kick, the psycho (vision cone, chase, grab) and the death screen.
// 11. Add Door Exit               : press A at the open door to go to the information room.
// 12. Fix Character Lighting      : faint glow so the girl and the psycho read in the dark room.
// 13. Match Other Rooms           : Room_Electrical at door-matched scale, camera 4.5 + black background like the Lobby.
// 14. Connect Door to Lobby       : the exit loads the Lobby; she arrives at the Lobby's left side door.
// 15. Connect Hospital Rooms      : Lobby top door <-> SurgicalWing, Lobby right door <-> CorridorOffice (both ways).
// 16. Lobby Bed Bridge            : climb over the bed to the info desk side; kick it to tip it and slide back.
// 17. Lobby Flashlight on Chair    : she starts without it; pick it up from the red chair behind the info desk.
// 18. Lobby -> Electrical Door     : boarded-up left side door; pry the planks off with the crowbar selected.
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class HorrorRoomSetup
{
    const string RoomPath = "Assets/Images/electrical_room_1080p.png";
    const string ShadowPath = "Assets/Images/character_shadow.png";

    // In the room art the girl should be ~265 px tall (1080p version); she is 2 units tall -> 132.5 px per unit.
    const float RoomPPU = 132.5f;

    [MenuItem("Tools/Horror Room/1. Setup Electrical Room Import")]
    public static void SetupRoomImport()
    {
        var ti = AssetImporter.GetAtPath(RoomPath) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[HorrorRoom] Missing " + RoomPath); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = RoomPPU;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 2048;
        ti.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);
        ti.SaveAndReimport();

        var shadow = AssetImporter.GetAtPath(ShadowPath) as TextureImporter;
        if (shadow != null)
        {
            shadow.textureType = TextureImporterType.Sprite;
            shadow.spriteImportMode = SpriteImportMode.Single;
            shadow.spritePixelsPerUnit = 100;
            shadow.alphaIsTransparency = true;
            shadow.mipmapEnabled = false;
            shadow.SaveAndReimport();
        }
        Debug.Log($"[HorrorRoom] Room imported: {1920f / RoomPPU:0.##} x {1080f / RoomPPU:0.##} units. " +
                  $"Main Camera Orthographic Size {1080f / RoomPPU / 2f:0.##} shows the whole room.");
    }

    [MenuItem("Tools/Horror Room/2. Add Shadow + Flashlight to Selected Character")]
    public static void AddShadowAndLight()
    {
        var go = Selection.activeGameObject;
        if (go == null) { EditorUtility.DisplayDialog("Horror Room", "Select the Player (or the Psycho) first.", "OK"); return; }
        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowPath);
        if (shadowSprite == null) { Debug.LogWarning("[HorrorRoom] Run step 1 first (shadow sprite not imported)."); return; }

        // Soft oval under the feet. Sprites pivot on the feet, so local (0,0) is the ground point.
        if (go.transform.Find("Shadow") == null)
        {
            var sh = new GameObject("Shadow");
            Undo.RegisterCreatedObjectUndo(sh, "Add shadow");
            sh.transform.SetParent(go.transform, false);
            sh.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            sh.transform.localScale = new Vector3(0.9f, 0.9f, 1f);   // ~1.15 x 0.43 units
            var r = sh.AddComponent<SpriteRenderer>();
            r.sprite = shadowSprite;
            if (sr != null) { r.sortingLayerID = sr.sortingLayerID; r.sortingOrder = sr.sortingOrder - 1; }
        }

        // Flashlight: a warm point light around the character (only for the girl, skip for the psycho).
        if (go.GetComponentInChildren<TopDownMovement>() != null || go.GetComponent<TopDownMovement>() != null)
        {
            if (go.transform.Find("Flashlight") == null)
            {
                var fl = new GameObject("Flashlight");
                Undo.RegisterCreatedObjectUndo(fl, "Add flashlight");
                fl.transform.SetParent(go.transform, false);
                fl.transform.localPosition = new Vector3(0f, 1f, 0f);     // chest height
                var l = fl.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = new Color(1f, 0.9f, 0.75f);
                l.intensity = 1.1f;
                l.pointLightInnerRadius = 0.5f;
                l.pointLightOuterRadius = 3.5f;
                l.falloffIntensity = 0.7f;
            }
        }
        EditorUtility.SetDirty(go);
        Debug.Log("[HorrorRoom] Added shadow" + (go.transform.Find("Flashlight") ? " + flashlight" : "") + " to " + go.name +
                  ". If it's a prefab instance, use Overrides > Apply All to put it on the prefab.");
    }

    // Floor outline of electrical_room_1080p in world units (sprite centred at 0,0, PPU 132.5):
    // back = bottom of the shelves, sides = where the walls meet the floor, front = front wall.
    static readonly Vector2[] FloorOutline =
    {
        new Vector2(-5.12f, 1.55f),   // back-left  (shelf base)
        new Vector2( 5.44f, 1.55f),   // back-right
        new Vector2( 5.87f, -3.78f),  // front-right
        new Vector2(-5.71f, -3.78f),  // front-left
        new Vector2(-5.12f, 1.55f),   // close the loop
    };

    [MenuItem("Tools/Horror Room/4. Fit Walls + Camera to Electrical Room")]
    public static void FitWallsAndCamera()
    {
        var go = Selection.activeGameObject;
        var sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null || !sr.sprite.name.StartsWith("electrical_room"))
        {
            EditorUtility.DisplayDialog("Horror Room", "Select the Background object that shows electrical_room_1080p.", "OK");
            return;
        }
        // The old polygon traced the previous room: turn it off (not deleted, so nothing is lost).
        foreach (var poly in go.GetComponents<PolygonCollider2D>())
        {
            Undo.RecordObject(poly, "Disable old room collider");
            poly.enabled = false;
        }
        var edge = go.GetComponent<EdgeCollider2D>();
        if (edge == null) edge = Undo.AddComponent<EdgeCollider2D>(go);
        Undo.RecordObject(edge, "Fit room walls");
        var s = go.transform.lossyScale;
        edge.points = System.Array.ConvertAll(FloorOutline, p => new Vector2(p.x / s.x, p.y / s.y));
        edge.edgeRadius = 0.05f;

        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Fit camera to room");
            cam.orthographic = true;
            cam.orthographicSize = 1080f / RoomPPU / 2f * s.y;   // whole room height on screen
            Undo.RecordObject(cam.transform, "Centre camera");
            var p = cam.transform.position;
            cam.transform.position = new Vector3(go.transform.position.x, go.transform.position.y, p.z);
        }
        Debug.Log("[HorrorRoom] Walls fitted (Edge Collider 2D, old Polygon Collider 2D disabled) and camera set to " +
                  (cam != null ? cam.orthographicSize.ToString("0.##") : "-") + ". Box piles have no collider yet.");
    }

    // ---------------------------------------------------------------- box + lever puzzle
    const string BoxPath = "Assets/Images/push_box.png";
    const string LeverUpPath = "Assets/Images/lever_up.png";
    const string LeverDownPath = "Assets/Images/lever_down.png";
    const string LeverMidPath = "Assets/Images/lever_mid.png";
    const int LeverFrames = 7;                                     // 0 = up, 3 = towards the door, 6 = down at the floor
    static string LeverFramePath(int i) => $"Assets/Images/lever_pull_{i}.png";
    const string S128 = "Assets/Animations/girlAnimation/Sprites128/";

    static Sprite FindSprite(string sheet, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(S128 + sheet).OfType<Sprite>().FirstOrDefault(sp => sp.name == name);

    // the box needs the player, the Interact button (hold U to grab) and the lever
    static void AssignBox(LeverSwitch lever)
    {
        var box = Object.FindFirstObjectByType<PushableBox>();
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (box == null) return;
        var so = new SerializedObject(box);
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        so.FindProperty("interactAction").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Input/ArcadeControls.inputactions")
            .OfType<InputActionReference>().FirstOrDefault(r => r.action != null && r.action.name == "ButtonA");
        so.FindProperty("lever").objectReferenceValue = lever;
        so.ApplyModifiedProperties();
    }

    // lever frames + the girl's poses for climbing and pulling
    static void AssignLeverSprites(LeverSwitch lever)
    {
        var so = new SerializedObject(lever);
        so.FindProperty("pullFrames").arraySize = 0;                    // plain wooden handle, moved into the pulled pose
        var lsr = lever.GetComponent<SpriteRenderer>();
        if (lsr != null && !lever.IsPulled) lsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LeverUpPath);
        so.FindProperty("idleLeft").objectReferenceValue = FindSprite("girl128_idle_left.png", "girl_idle_left_00");
        so.FindProperty("idleRight").objectReferenceValue = FindSprite("girl128_idle_right.png", "girl_idle_right_00");
        so.FindProperty("stepLeft").objectReferenceValue = FindSprite("girl128_run_left.png", "girl_run_left_07");   // knee up
        so.FindProperty("stepRight").objectReferenceValue = FindSprite("girl128_run_right.png", "girl_run_right_07");
        so.ApplyModifiedProperties();
        if (so.FindProperty("idleLeft").objectReferenceValue == null)
            Debug.LogWarning("[HorrorRoom] Girl sprites not found: run Tools > Horror Animation > Setup Everything first.");
    }
    // Positions in the room (local to the Background, PPU 132.5), measured on electrical_room_1080p.
    static readonly Vector2 BoxStart = new Vector2(-3.811f, 0.415f);   // where the box sits in the art
    static readonly Vector2 BoxSpot = new Vector2(-4.84f, 0.20f);      // under the lever, against the wall
    static readonly Vector2 LeverPos = new Vector2(-5.675f, 0.40f);    // lever hinge on the panel

    static void ImportSprite(string path, Vector2 pivot)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[HorrorRoom] Missing " + path); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = RoomPPU;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Clamp;
        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = pivot;
        ti.SetTextureSettings(st);
        ti.SaveAndReimport();
    }

    [MenuItem("Tools/Horror Room/5. Build Box + Lever Puzzle")]
    public static void BuildBoxLeverPuzzle()
    {
        var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("electrical_room"));
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (bg == null || player == null)
        {
            EditorUtility.DisplayDialog("Horror Room", "Needs the Background showing electrical_room_1080p and the Player in the open scene.", "OK");
            return;
        }
        ImportSprite(BoxPath, new Vector2(72f / 182f, 40f / 183f));     // pivot = where the box touches the floor
        ImportSprite(LeverUpPath, new Vector2(18f / 38f, 61f / 120f));  // pivot = lever hinge
        ImportSprite(LeverMidPath, new Vector2(18f / 38f, 61f / 120f));
        ImportSprite(LeverDownPath, new Vector2(18f / 38f, 61f / 120f));
        for (int i = 0; i < LeverFrames; i++) ImportSprite(LeverFramePath(i), new Vector2(34f / 110f, 74f / 148f)); // pivot = centre of the metal plate

        var existing = Object.FindFirstObjectByType<LeverSwitch>();
        if (existing != null)                                            // already built: just refresh sprites/poses
        {
            AssignLeverSprites(existing);
            AssignBox(existing);
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("[HorrorRoom] Puzzle already in the scene: lever, poses and box (hold U to grab) updated.");
            return;
        }

        var t = bg.transform;
        var playerSr = player.GetComponentInChildren<SpriteRenderer>();

        // Pushable box
        var boxGo = new GameObject("PushBox");
        Undo.RegisterCreatedObjectUndo(boxGo, "Build puzzle");
        boxGo.transform.position = t.TransformPoint(BoxStart);
        boxGo.transform.localScale = t.lossyScale;
        var bsr = boxGo.AddComponent<SpriteRenderer>();
        bsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoxPath);
        bsr.spriteSortPoint = SpriteSortPoint.Pivot;
        if (playerSr != null) { bsr.sortingLayerID = playerSr.sortingLayerID; bsr.sortingOrder = playerSr.sortingOrder; bsr.sharedMaterial = playerSr.sharedMaterial; }
        boxGo.AddComponent<Rigidbody2D>();
        var col = boxGo.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.85f, 0.35f);   // the box's footprint on the floor
        col.offset = new Vector2(0.04f, 0.2f);
        var box = boxGo.AddComponent<PushableBox>();

        // Lever + the spot the box has to reach
        var leverGo = new GameObject("Lever");
        Undo.RegisterCreatedObjectUndo(leverGo, "Build puzzle");
        leverGo.transform.position = t.TransformPoint(LeverPos);
        leverGo.transform.localScale = t.lossyScale;
        var lsr = leverGo.AddComponent<SpriteRenderer>();
        lsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LeverUpPath);
        lsr.sortingLayerID = bg.sortingLayerID;
        lsr.sortingOrder = bg.sortingOrder + 1;
        lsr.sharedMaterial = bg.sharedMaterial;
        var spot = new GameObject("BoxSpot");
        Undo.RegisterCreatedObjectUndo(spot, "Build puzzle");
        spot.transform.SetParent(leverGo.transform, true);
        spot.transform.position = t.TransformPoint(BoxSpot);

        var lever = leverGo.AddComponent<LeverSwitch>();
        var so = new SerializedObject(lever);
        so.FindProperty("leverRenderer").objectReferenceValue = lsr;
        so.FindProperty("box").objectReferenceValue = box;
        so.FindProperty("boxSpot").objectReferenceValue = spot.transform;
        so.FindProperty("player").objectReferenceValue = player.transform;
        so.FindProperty("interactAction").objectReferenceValue = AssetDatabase.LoadAllAssetsAtPath("Assets/Input/ArcadeControls.inputactions")
            .OfType<InputActionReference>().FirstOrDefault(r => r.action != null && r.action.name == "ButtonA");
        so.FindProperty("globalLight").objectReferenceValue = Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)
            .FirstOrDefault(l => l.lightType == Light2D.LightType.Global);
        so.ApplyModifiedProperties();
        AssignLeverSprites(lever);
        AssignBox(lever);

        Selection.activeGameObject = leverGo;
        Debug.Log("[HorrorRoom] Puzzle built: hold U next to the box to push/pull it to the cyan circle, then press U by the lever.");
    }

    // ---------------------------------------------------------------- lamp + face in the door window
    // room-image pixel (1920x1080) -> position local to the Background
    static Vector2 RoomPx(float x, float y) => new Vector2((x - 960f) / RoomPPU, (540f - y) / RoomPPU);

    [MenuItem("Tools/Horror Room/6. Add Door Lamp Scare")]
    public static void AddDoorScare()
    {
        var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("electrical_room"));
        if (bg == null) { EditorUtility.DisplayDialog("Horror Room", "Needs the Background showing electrical_room_1080p in the open scene.", "OK"); return; }
        if (Object.FindFirstObjectByType<DoorScare>() != null)
        {
            EditorUtility.DisplayDialog("Horror Room", "A DoorScare is already in this scene. Delete it to rebuild.", "OK");
            return;
        }
        ImportSprite("Assets/Images/lamp_off.png", new Vector2(0.5f, 0.5f));
        ImportSprite("Assets/Images/lamp_on.png", new Vector2(0.5f, 0.5f));
        ImportSprite("Assets/Images/door_face.png", new Vector2(0.5f, 0.5f));
        var t = bg.transform;

        var root = new GameObject("DoorScare");
        Undo.RegisterCreatedObjectUndo(root, "Add door scare");
        root.transform.position = t.position;
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        // lamp over the door
        var lampGo = new GameObject("DoorLamp");
        lampGo.transform.SetParent(root.transform, false);
        lampGo.transform.position = t.TransformPoint(RoomPx(1785f, 190f));
        lampGo.transform.localScale = t.lossyScale;
        var lampSr = lampGo.AddComponent<SpriteRenderer>();
        lampSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/lamp_off.png");
        lampSr.sortingLayerID = bg.sortingLayerID;
        lampSr.sortingOrder = bg.sortingOrder + 2;
        var light = lampGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(1f, 0.85f, 0.6f);
        light.intensity = 1.3f;
        light.pointLightInnerRadius = 0.2f;
        light.pointLightOuterRadius = 2.6f;
        light.falloffIntensity = 0.6f;

        // his face behind the door's window (hidden until the last flash)
        var faceGo = new GameObject("DoorWindowFace");
        faceGo.transform.SetParent(root.transform, false);
        faceGo.transform.position = t.TransformPoint(RoomPx(1783f, 480f));
        faceGo.transform.localScale = t.lossyScale;
        var faceSr = faceGo.AddComponent<SpriteRenderer>();
        faceSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/door_face.png");
        faceSr.sortingLayerID = bg.sortingLayerID;
        faceSr.sortingOrder = bg.sortingOrder + 1;
        faceSr.sharedMaterial = bg.sharedMaterial;
        faceSr.enabled = false;

        var scare = root.AddComponent<DoorScare>();
        var so = new SerializedObject(scare);
        so.FindProperty("lamp").objectReferenceValue = lampSr;
        so.FindProperty("lampOff").objectReferenceValue = lampSr.sprite;
        so.FindProperty("lampOn").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/lamp_on.png");
        so.FindProperty("lampLight").objectReferenceValue = light;
        so.FindProperty("face").objectReferenceValue = faceSr;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedProperties();

        // start it when the lever is pulled
        var lever = Object.FindFirstObjectByType<LeverSwitch>();
        if (lever != null)
        {
            Undo.RecordObject(lever, "Hook door scare");
            UnityEventTools.AddVoidPersistentListener(lever.onPulled, scare.Play);
            EditorUtility.SetDirty(lever);
        }
        Selection.activeGameObject = root;
        Debug.Log("[HorrorRoom] Door scare added" + (lever != null ? " and hooked to the lever's On Pulled." :
                  ". No lever found: call DoorScare.Play() from your own event.") +
                  " Drop a flicker/scare sound into the DoorScare's Sound slots if you have them.");
    }

    [MenuItem("Tools/Horror Room/7. Use Current Lever Pose as Pulled")]
    public static void UseCurrentLeverPose()
    {
        var lever = Object.FindFirstObjectByType<LeverSwitch>();
        var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("electrical_room"));
        if (lever == null || bg == null) { EditorUtility.DisplayDialog("Horror Room", "Needs the Lever and the Background in the open scene.", "OK"); return; }
        var lsr = lever.GetComponent<SpriteRenderer>();
        var lt = lsr != null ? lsr.transform : lever.transform;
        var t = bg.transform;

        // where the handle sits before it is pulled (on the panel, upright)
        Vector3 upPos = t.TransformPoint(RoomPx(208f, 487f));
        Vector3 parentUp = lt.parent != null ? lt.parent.InverseTransformPoint(upPos) : upPos;
        Vector3 upScale = t.lossyScale;

        var so = new SerializedObject(lever);
        so.FindProperty("pulledOffset").vector3Value = lt.localPosition - parentUp;
        so.FindProperty("pulledEuler").vector3Value = lt.localEulerAngles;
        so.FindProperty("pulledScale").vector3Value = new Vector3(lt.localScale.x / upScale.x, lt.localScale.y / upScale.y, lt.localScale.z / upScale.z);
        so.FindProperty("pullFrames").arraySize = 0;
        so.ApplyModifiedProperties();

        // put the handle back up, ready to be pulled
        Undo.RecordObject(lt, "Reset lever");
        lt.localPosition = parentUp;
        lt.localRotation = Quaternion.identity;
        lt.localScale = upScale;
        if (lsr != null)
        {
            Undo.RecordObject(lsr, "Reset lever");
            lsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LeverUpPath);
        }
        Selection.activeGameObject = lever.gameObject;
        Debug.Log("[HorrorRoom] Saved that pose as the pulled lever and put the handle back up. Pull Time 0 = instant snap.");
    }

    [MenuItem("Tools/Horror Room/8. Add Interact Prompt to Player")]
    public static void AddInteractPrompt()
    {
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (player == null) { EditorUtility.DisplayDialog("Horror Room", "No Player (TopDownMovement) in the open scene.", "OK"); return; }
        if (player.GetComponentInChildren<InteractPrompt>(true) != null)
        {
            Debug.Log("[HorrorRoom] The Player already has an InteractPrompt.");
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/InteractPrompt.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        Undo.RegisterCreatedObjectUndo(go, "Add interact prompt");
        go.transform.localPosition = new Vector3(0f, 2.35f, 0f);
        EditorUtility.SetDirty(player.gameObject);
        Selection.activeGameObject = go;
        Debug.Log("[HorrorRoom] Interact prompt added to the Player. If it's a prefab instance, use Overrides > Apply All.");
    }

    // ---------------------------------------------------------------- arcade buttons
    static InputActionReference ArcadeAction(string name) =>
        AssetDatabase.LoadAllAssetsAtPath("Assets/Input/ArcadeControls.inputactions")
            .OfType<InputActionReference>().FirstOrDefault(r => r.action != null && r.action.name == name);

    static void WirePlayer(GameObject root, InputActionReference b, InputActionReference c, InputActionReference d)
    {
        var move = root.GetComponentInChildren<TopDownMovement>(true);
        if (move != null)
        {
            var so = new SerializedObject(move);
            so.FindProperty("sprintAction").objectReferenceValue = b;
            so.FindProperty("crawlAction").objectReferenceValue = c;
            so.ApplyModifiedProperties();
        }
        var light = root.GetComponentsInChildren<Light2D>(true).FirstOrDefault(l => l.lightType != Light2D.LightType.Global);
        if (light == null)
        {
            var fl = new GameObject("Flashlight");
            fl.transform.SetParent(root.transform, false);
            fl.transform.localPosition = new Vector3(0f, 1f, 0f);
            light = fl.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = new Color(1f, 0.9f, 0.75f);
            light.intensity = 1.1f;
            light.pointLightInnerRadius = 0.3f;
            light.pointLightOuterRadius = 5f;
            light.falloffIntensity = 0.7f;
        }
        var flash = root.GetComponent<PlayerFlashlight>();
        if (flash == null) flash = root.AddComponent<PlayerFlashlight>();
        var fso = new SerializedObject(flash);
        fso.FindProperty("toggleAction").objectReferenceValue = d;
        fso.FindProperty("flashlight").objectReferenceValue = light;
        fso.ApplyModifiedProperties();
    }

    [MenuItem("Tools/Horror Room/9. Set Arcade Controls (A interact, B run, C crawl, D flashlight)")]
    public static void SetArcadeControls()
    {
        var a = ArcadeAction("ButtonA"); var b = ArcadeAction("ButtonB");
        var c = ArcadeAction("ButtonC"); var d = ArcadeAction("ButtonD");
        if (a == null || b == null || c == null || d == null)
        {
            EditorUtility.DisplayDialog("Horror Room", "Couldn't find ButtonA-D in Assets/Input/ArcadeControls.inputactions.", "OK");
            return;
        }
        // 1) the Player prefab itself, so every scene gets it
        const string prefabPath = "Assets/Prefabs/Player.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            WirePlayer(root, b, c, d);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
        // 2) the Player in the open scene (in case it overrides the prefab)
        foreach (var move in Object.FindObjectsByType<TopDownMovement>(FindObjectsSortMode.None))
        {
            Undo.RegisterFullObjectHierarchyUndo(move.gameObject, "Arcade controls");
            WirePlayer(move.gameObject, b, c, d);
            EditorUtility.SetDirty(move.gameObject);
        }
        // 3) A = interact for the lever and the box
        foreach (var lever in Object.FindObjectsByType<LeverSwitch>(FindObjectsSortMode.None))
        { var so = new SerializedObject(lever); so.FindProperty("interactAction").objectReferenceValue = a; so.ApplyModifiedProperties(); }
        foreach (var box in Object.FindObjectsByType<PushableBox>(FindObjectsSortMode.None))
        { var so = new SerializedObject(box); so.FindProperty("interactAction").objectReferenceValue = a; so.ApplyModifiedProperties(); }

        Debug.Log("[HorrorRoom] Arcade controls set: A = interact (U), B = run (I), C = crawl (O), D = flashlight (J). Save the scene.");
    }

    // ---------------------------------------------------------------- psycho ambush
    static Transform Marker(string name, Transform parent, Vector3 worldPos)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        return g.transform;
    }

    static SpriteRenderer SpriteObject(string name, Transform parent, string spritePath, Vector3 worldPos, int layerId, int order, Material mat)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = worldPos;
        var r = g.AddComponent<SpriteRenderer>();
        if (!string.IsNullOrEmpty(spritePath)) r.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        r.sortingLayerID = layerId; r.sortingOrder = order;
        if (mat != null) r.sharedMaterial = mat;
        return r;
    }

    static Sprite SheetSprite(string sheet, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().FirstOrDefault(sp => sp.name == name);

    [MenuItem("Tools/Horror Room/10. Build Psycho Ambush (desk, door kick, chase, death screen)")]
    public static void BuildPsychoAmbush()
    {
        var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("electrical_room"));
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var scare = Object.FindFirstObjectByType<DoorScare>();
        if (bg == null || player == null) { EditorUtility.DisplayDialog("Horror Room", "Needs the Background (electrical_room_1080p) and the Player in the open scene.", "OK"); return; }
        if (Object.FindFirstObjectByType<EntityAI>() != null) { EditorUtility.DisplayDialog("Horror Room", "The psycho is already in this scene. Delete 'PsychoAmbush' to rebuild.", "OK"); return; }

        ImportSprite("Assets/Images/desk.png", new Vector2(0.5f, 18f / 262f));        // pivot = front floor line
        ImportSprite("Assets/Images/door_open.png", new Vector2(0.5f, 0.5f));
        ImportSprite("Assets/Images/door_broken.png", new Vector2(0.5f, 0.5f));
        var t = bg.transform;
        var psr = player.GetComponentInChildren<SpriteRenderer>();
        int charLayer = psr != null ? psr.sortingLayerID : 0;
        Material lit = psr != null ? psr.sharedMaterial : bg.sharedMaterial;

        var root = new GameObject("PsychoAmbush");
        Undo.RegisterCreatedObjectUndo(root, "Build psycho ambush");

        // ---- desk in the middle of the room: solid, and low cover (hides her only while crawling)
        var desk = SpriteObject("Desk", root.transform, "Assets/Images/desk.png", t.TransformPoint(new Vector2(0f, -2.75f)), charLayer, 0, lit);
        desk.spriteSortPoint = SpriteSortPoint.Pivot;
        var deskCol = desk.gameObject.AddComponent<BoxCollider2D>();
        deskCol.size = new Vector2(2.55f, 0.5f);       // the desk's footprint on the floor
        deskCol.offset = new Vector2(0f, 0.28f);
        desk.gameObject.AddComponent<VisionCover>().lowCover = true;

        // ---- the door after the kick (hidden until then): dark doorway + the door lying on the floor
        var doorOpen = SpriteObject("DoorOpen", root.transform, "Assets/Images/door_open.png", t.TransformPoint(RoomPx(1765f, 510f)), bg.sortingLayerID, bg.sortingOrder + 1, bg.sharedMaterial);
        var broken = SpriteObject("BrokenDoor", root.transform, "Assets/Images/door_broken.png", t.TransformPoint(RoomPx(1563f, 689.5f)), bg.sortingLayerID, bg.sortingOrder + 1, bg.sharedMaterial);
        doorOpen.enabled = false; broken.enabled = false;   // decoration only: no collider

        // ---- path markers
        var markers = new GameObject("PsychoPath").transform; markers.SetParent(root.transform, false);
        var outside = Marker("DoorOutside", markers, t.TransformPoint(new Vector2(6.35f, -0.95f)));
        var inside = Marker("DoorInside", markers, t.TransformPoint(new Vector2(4.7f, -1.05f)));
        var leverStand = Marker("LeverStand", markers, t.TransformPoint(new Vector2(-3.85f, 0.3f)));
        Vector2[] patrol = { new Vector2(-3.4f, 0.9f), new Vector2(0f, 1.1f), new Vector2(4.0f, 0.5f),
                             new Vector2(4.3f, -3.0f), new Vector2(0f, -0.45f), new Vector2(-4.0f, -3.0f) };
        var patrolT = patrol.Select((p, i) => Marker("Patrol" + i, markers, t.TransformPoint(p))).ToArray();

        // ---- the psycho
        const string walkSheet = "Assets/Animations/entityAnimation/psycho-walking-sprite-sheet-transparent.png";
        var psycho = SpriteObject("Psycho", root.transform, null, outside.position, charLayer, 0, lit);
        psycho.sprite = SheetSprite(walkSheet, "psycho_walk_left_00");
        psycho.spriteSortPoint = SpriteSortPoint.Pivot;
        var pgo = psycho.gameObject;
        var animator = pgo.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Controllers/Psycho.controller");
        pgo.AddComponent<Rigidbody2D>();
        var pcol = pgo.AddComponent<BoxCollider2D>();
        pcol.size = new Vector2(0.7f, 0.3f); pcol.offset = new Vector2(0f, 0.15f);   // his feet
        var dsa = pgo.AddComponent<DirectionalSpriteAnimator>();
        var dso = new SerializedObject(dsa); dso.FindProperty("runThreshold").floatValue = 999f; dso.ApplyModifiedProperties();
        var shadow = AssetDatabase.LoadAssetAtPath<Sprite>(ShadowPath);
        if (shadow != null)
        {
            var sh = SpriteObject("Shadow", pgo.transform, ShadowPath, outside.position + new Vector3(0f, 0.02f, 0f), charLayer, -1, null);
            sh.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        }
        // vision cone: a red light that shows where he is looking
        var lightGo = new GameObject("VisionLight");
        lightGo.transform.SetParent(pgo.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var vl = lightGo.AddComponent<Light2D>();
        vl.lightType = Light2D.LightType.Point;
        vl.color = new Color(1f, 0.32f, 0.22f);
        vl.intensity = 0.9f;
        vl.pointLightInnerRadius = 0.3f;
        vl.pointLightOuterRadius = 5f;
        vl.pointLightOuterAngle = 70f;
        vl.pointLightInnerAngle = 30f;
        vl.falloffIntensity = 0.6f;
        var audio = pgo.AddComponent<AudioSource>(); audio.playOnAwake = false;

        // ---- death screen (UI)
        var canvasGo = new GameObject("DeathScreen");
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var group = canvasGo.AddComponent<CanvasGroup>();
        Image MakeImage(string n)
        {
            var g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)g.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return g.AddComponent<Image>();
        }
        var black = MakeImage("Black"); black.color = Color.black;
        var faceImg = MakeImage("Face"); faceImg.preserveAspect = true;
        const string deathSheet = "Assets/Animations/entityAnimation/psycho-death-screen-sprite-sheet.png";
        var frames = Enumerable.Range(0, 4).Select(i => SheetSprite(deathSheet, $"psycho_death_screen_{i:00}")).ToArray();
        if (frames.Any(f => f == null)) Debug.LogWarning("[HorrorRoom] Death screen frames not found: run Tools > Horror Animation > Setup Everything, then this step again.");
        var death = canvasGo.AddComponent<DeathScreen>();
        var deathAudio = canvasGo.AddComponent<AudioSource>(); deathAudio.playOnAwake = false;
        var ds = new SerializedObject(death);
        ds.FindProperty("group").objectReferenceValue = group;
        ds.FindProperty("background").objectReferenceValue = black;
        ds.FindProperty("face").objectReferenceValue = faceImg;
        var fp = ds.FindProperty("frames"); fp.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        ds.FindProperty("retryAction").objectReferenceValue = ArcadeAction("ButtonA");
        ds.FindProperty("audioSource").objectReferenceValue = deathAudio;
        ds.ApplyModifiedProperties();
        // hidden while editing (it shows itself when he catches her)
        group.alpha = 0f;
        group.blocksRaycasts = false;
        faceImg.sprite = frames.FirstOrDefault(f => f != null);
        faceImg.enabled = false;

        // ---- the AI
        var ai = pgo.AddComponent<EntityAI>();
        var so = new SerializedObject(ai);
        so.FindProperty("girl").objectReferenceValue = player.transform;
        so.FindProperty("lever").objectReferenceValue = Object.FindFirstObjectByType<LeverSwitch>();
        so.FindProperty("leverStandPoint").objectReferenceValue = leverStand;
        so.FindProperty("doorOutside").objectReferenceValue = outside;
        so.FindProperty("doorInside").objectReferenceValue = inside;
        so.FindProperty("doorOpen").objectReferenceValue = doorOpen;
        so.FindProperty("brokenDoor").objectReferenceValue = broken;
        var pp = so.FindProperty("patrolPoints"); pp.arraySize = patrolT.Length;
        for (int i = 0; i < patrolT.Length; i++) pp.GetArrayElementAtIndex(i).objectReferenceValue = patrolT[i];
        so.FindProperty("deathScreen").objectReferenceValue = death;
        so.FindProperty("visionLight").objectReferenceValue = vl;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.ApplyModifiedProperties();

        // ---- start it when the door lamp scare ends
        if (scare != null)
        {
            Undo.RecordObject(scare, "Hook psycho");
            UnityEventTools.AddVoidPersistentListener(scare.onFinished, ai.BeginEntrance);
            EditorUtility.SetDirty(scare);
        }
        Selection.activeGameObject = pgo;
        Debug.Log("[HorrorRoom] Psycho ambush built" + (scare != null ? " and hooked to the door lamp scare." : ". No DoorScare found: call EntityAI.BeginEntrance() yourself.") +
                  " Save the scene. Drag 'Desk' to move the cover; the PsychoPath markers set his route.");
    }

    [MenuItem("Tools/Horror Room/11. Add Door Exit (A at the door -> information room)")]
    public static void AddDoorExit()
    {
        var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("electrical_room"));
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (bg == null || player == null) { EditorUtility.DisplayDialog("Horror Room", "Needs the Background and the Player in the open scene.", "OK"); return; }
        if (Object.FindFirstObjectByType<DoorExit>() != null) { EditorUtility.DisplayDialog("Horror Room", "A DoorExit is already in this scene.", "OK"); return; }

        var go = new GameObject("DoorExit");
        Undo.RegisterCreatedObjectUndo(go, "Add door exit");
        go.transform.position = bg.transform.TransformPoint(new Vector2(5.2f, -1.0f));   // just inside the doorway
        var exit = go.AddComponent<DoorExit>();
        var doorOpenGo = GameObject.Find("DoorOpen");
        var so = new SerializedObject(exit);
        so.FindProperty("player").objectReferenceValue = player.transform;
        so.FindProperty("interactAction").objectReferenceValue = ArcadeAction("ButtonA");
        so.FindProperty("doorOpen").objectReferenceValue = doorOpenGo != null ? doorOpenGo.GetComponent<SpriteRenderer>() : null;
        if (doorOpenGo == null) so.FindProperty("requireDoorOpen").boolValue = false;     // no kicked door in this scene: always open
        so.ApplyModifiedProperties();
        Selection.activeGameObject = go;
        Debug.Log("[HorrorRoom] Door exit added: press A (U) next to the door once it's open. Set 'Scene Name' to the information room's scene.");
    }

    // a soft light around a character so its sprite isn't a black silhouette in the dark room
    static void AddPresenceLight(GameObject character, Color color, float intensity, float radius)
    {
        var existing = character.transform.Find("PresenceLight");
        var go = existing != null ? existing.gameObject : new GameObject("PresenceLight");
        if (existing == null) go.transform.SetParent(character.transform, false);
        go.transform.localPosition = new Vector3(0f, 1f, 0f);
        var l = go.GetComponent<Light2D>();
        if (l == null) l = go.AddComponent<Light2D>();
        l.lightType = Light2D.LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.pointLightInnerRadius = 0.2f;
        l.pointLightOuterRadius = radius;
        l.pointLightInnerAngle = 360f;
        l.pointLightOuterAngle = 360f;
        l.falloffIntensity = 0.8f;
    }

    [MenuItem("Tools/Horror Room/12. Fix Character Lighting (girl + psycho glow, desk light)")]
    public static void FixCharacterLighting()
    {
        var girlGlow = new Color(0.75f, 0.82f, 1f);      // cool, like moonlight on her
        // the Player prefab (all scenes) ...
        const string prefabPath = "Assets/Prefabs/Player.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            AddPresenceLight(root, girlGlow, 0.55f, 1.5f);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
        // ... and the scene instance (in case it overrides the prefab)
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (player != null) { Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Character light"); AddPresenceLight(player.gameObject, girlGlow, 0.55f, 1.5f); }
        // the psycho: dimmer, so he stays a dark shape
        var psycho = Object.FindFirstObjectByType<EntityAI>();
        if (psycho != null) { Undo.RegisterFullObjectHierarchyUndo(psycho.gameObject, "Character light"); AddPresenceLight(psycho.gameObject, new Color(0.85f, 0.8f, 0.8f), 0.3f, 1.3f); }
        // the desk: a soft wash of the same cool light so it reads in the dark
        var desk = Object.FindObjectsByType<VisionCover>(FindObjectsSortMode.None).Select(c => c.gameObject).FirstOrDefault(g => g.name == "Desk");
        if (desk != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(desk, "Desk light");
            var existing = desk.transform.Find("DeskLight");
            var go = existing != null ? existing.gameObject : new GameObject("DeskLight");
            if (existing == null) go.transform.SetParent(desk.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.8f, 0f);      // middle of the desk
            var l = go.GetComponent<Light2D>();
            if (l == null) l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = girlGlow;
            l.intensity = 0.5f;
            l.pointLightInnerRadius = 0.6f;
            l.pointLightOuterRadius = 2.1f;                               // covers the whole desk, fades on the floor
            l.pointLightInnerAngle = 360f;
            l.pointLightOuterAngle = 360f;
            l.falloffIntensity = 0.7f;
        }
        Debug.Log("[HorrorRoom] Character glow added" + (psycho != null ? " to the girl and the psycho" : " to the girl (no psycho in the scene)") +
                  (desk != null ? ", plus a soft light on the desk." : ".") +
                  " Tweak 'PresenceLight' / 'DeskLight' intensity if they read too bright or too dark. Save the scene.");
    }

    // ---------------------------------------------------------------- match the other hospital rooms
    [MenuItem("Tools/Horror Room/13. Match Other Rooms (door scale, camera 4.5)")]
    public static void MatchOtherRooms()
    {
        var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("electrical_room"));
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var cam = Camera.main;
        if (bg == null || player == null || cam == null) { EditorUtility.DisplayDialog("Horror Room", "Open the Electrical scene first (needs Background, Player and Main Camera).", "OK"); return; }

        var scene = bg.gameObject.scene;
        var room = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Room_Electrical");
        float factor = 1f;
        if (room == null)
        {
            room = new GameObject("Room_Electrical");
            Undo.RegisterCreatedObjectUndo(room, "Match other rooms");
            room.transform.position = new Vector3(bg.transform.position.x, bg.transform.position.y, 0f);

            // characters and the UI keep their size: take them out of anything that will be scaled
            var psycho = Object.FindFirstObjectByType<EntityAI>();
            if (psycho != null) Undo.SetTransformParent(psycho.transform, null, "Match other rooms");
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.transform.parent != null) Undo.SetTransformParent(c.transform, null, "Match other rooms");

            // everything else that belongs to the room goes under Room_Electrical
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == room || go == player.gameObject || go.GetComponent<Camera>() != null || go.GetComponent<Canvas>() != null) continue;
                if (psycho != null && go == psycho.gameObject) continue;
                var l = go.GetComponent<Light2D>();
                if (l != null && l.lightType == Light2D.LightType.Global) continue;
                Undo.SetTransformParent(go.transform, room.transform, "Match other rooms");
            }

        }
        // Scale: the other rooms were sized from their doors (girl = 2 units). Electrical's door is
        // already the same size as the Lobby's (~4.4 units), so the room stays at scale 1.
        float old = room.transform.localScale.x;
        if (Mathf.Abs(old - 1f) > 0.001f)
        {
            factor = 1f / old;
            Undo.RecordObject(room.transform, "Match other rooms");
            room.transform.localScale = Vector3.one;
            Undo.RecordObject(player.transform, "Match other rooms");
            Vector3 rel = player.transform.position - room.transform.position;
            player.transform.position = room.transform.position + new Vector3(rel.x * factor, rel.y * factor, rel.z);
        }

        // camera: same size as the other rooms, follows her and never shows past the room
        Undo.RecordObject(cam, "Match other rooms");
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;                     // like the Lobby: darkness around the room
        Undo.RecordObject(cam.transform, "Match other rooms");
        cam.transform.position = new Vector3(bg.bounds.center.x, bg.bounds.center.y, cam.transform.position.z);
        var follow = cam.GetComponent<RoomCamera>();
        if (follow == null) follow = Undo.AddComponent<RoomCamera>(cam.gameObject);
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.FindProperty("bounds").objectReferenceValue = bg;
        so.ApplyModifiedProperties();

        // Build Settings still pointed at the scene's old path (Assets/Scenes/Electrical.unity)
        string path = scene.path;
        var list = EditorBuildSettings.scenes.ToList();
        bool changed = false;
        for (int i = 0; i < list.Count; i++)
            if (list[i].path != path && System.IO.Path.GetFileName(list[i].path) == System.IO.Path.GetFileName(path) && !System.IO.File.Exists(list[i].path))
            { list[i] = new EditorBuildSettingsScene(path, true); changed = true; }
        if (!list.Any(e => e.path == path)) { list.Add(new EditorBuildSettingsScene(path, true)); changed = true; }
        if (changed) EditorBuildSettings.scenes = list.ToArray();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[HorrorRoom] Electrical now matches the other rooms: room {bg.bounds.size.x:0.##} x {bg.bounds.size.y:0.##} units" +
                  (factor != 1f ? $" (scaled x{factor:0.###})" : "") + ", camera size 4.5 with RoomCamera" +
                  (changed ? ", Build Settings path fixed." : ".") + " Save the scene.");
    }

    // the Lobby's left side door, in the Lobby background's own units (PPU 28, centred at 0,0)
    static readonly Vector2 LobbySideDoorArrival = new Vector2(-15.4f, -0.6f);

    [MenuItem("Tools/Horror Room/14. Connect Door to Lobby (arrive at the side door)")]
    public static void ConnectDoorToLobby()
    {
        var exit = Object.FindFirstObjectByType<DoorExit>();
        if (exit == null) { EditorUtility.DisplayDialog("Horror Room", "Open the Electrical scene (with its DoorExit) first. Run step 11 if it has none.", "OK"); return; }
        const string lobbyPath = "Assets/Scenes/Rooms/Lobby.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(lobbyPath) == null) { EditorUtility.DisplayDialog("Horror Room", "Can't find " + lobbyPath, "OK"); return; }

        var so = new SerializedObject(exit);
        so.FindProperty("sceneName").stringValue = "Lobby";
        so.FindProperty("entryId").stringValue = "FromElectrical";
        so.ApplyModifiedProperties();
        var electrical = exit.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(electrical);
        if (!EditorSceneManager.SaveScene(electrical)) return;
        string electricalPath = electrical.path;

        // the arrival point inside the Lobby
        var lobby = EditorSceneManager.OpenScene(lobbyPath, OpenSceneMode.Single);
        var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("Lobby"));
        var parent = lobby.GetRootGameObjects().FirstOrDefault(g => g.name == "Room_Lobby");
        var spawn = Object.FindObjectsByType<RoomSpawnPoint>(FindObjectsSortMode.None).FirstOrDefault();
        if (spawn == null)
        {
            var go = new GameObject("Spawn_FromElectrical");
            if (parent != null) go.transform.SetParent(parent.transform, false);
            spawn = go.AddComponent<RoomSpawnPoint>();
        }
        spawn.transform.position = bg != null ? bg.transform.TransformPoint(LobbySideDoorArrival) : (Vector3)LobbySideDoorArrival;
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        // both scenes must be in the build for LoadScene to work
        var list = EditorBuildSettings.scenes.ToList();
        foreach (var p in new[] { electricalPath, lobbyPath })
            if (!list.Any(e => e.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = list.ToArray();

        Vector3 arrival = spawn.transform.position;                     // read before the Lobby is unloaded
        EditorSceneManager.OpenScene(electricalPath, OpenSceneMode.Single);
        Debug.Log($"[HorrorRoom] Electrical's door now loads the Lobby; she arrives at the Lobby's side door ({arrival.x:0.##}, {arrival.y:0.##}). Both scenes saved.");
    }

    // ---------------------------------------------------------------- doors between the hospital rooms
    // positions in each room's own background units (background centred at 0,0)
    class DoorDef { public string scene, bgPrefix, exitName, toScene, entryOut; public Vector2 exitPos; public float reach;
                    public string spawnEntry; public Vector2 spawnPos, face;
                    public string art; public Vector2 artPos; public float artPPU; }

    static readonly DoorDef[] HospitalDoors =
    {
        // Lobby: main double door (top) -> SurgicalWing, side door (right) -> CorridorOffice
        new DoorDef { scene = "Lobby", bgPrefix = "Lobby", exitName = "Door_ToSurgicalWing", toScene = "SurgicalWing", entryOut = "FromLobby",
                      exitPos = new Vector2(-1.3f, 4.75f), reach = 1.2f, spawnEntry = "FromSurgicalWing", spawnPos = new Vector2(-1.3f, 3.3f), face = Vector2.down },
        new DoorDef { scene = "Lobby", bgPrefix = "Lobby", exitName = "Door_ToCorridorOffice", toScene = "CorridorOffice", entryOut = "FromLobby",
                      exitPos = new Vector2(16.0f, 0.5f), reach = 1.2f, spawnEntry = "FromCorridorOffice", spawnPos = new Vector2(15.4f, -2.0f), face = Vector2.left },
        // SurgicalWing: entrance in the bottom wall -> Lobby
        new DoorDef { scene = "SurgicalWing", bgPrefix = "SurgicalWing", exitName = "Door_ToLobby", toScene = "Lobby", entryOut = "FromSurgicalWing",
                      exitPos = new Vector2(-0.1f, -5.3f), reach = 1.0f, spawnEntry = "FromLobby", spawnPos = new Vector2(-0.1f, -4.1f), face = Vector2.up },
        // CorridorOffice: entrance door in the left end wall -> Lobby
        new DoorDef { scene = "CorridorOffice", bgPrefix = "CorridorOffice", exitName = "Door_ToLobby", toScene = "Lobby", entryOut = "FromCorridorOffice",
                      exitPos = new Vector2(-18.3f, 0.0f), reach = 1.0f, spawnEntry = "FromLobby", spawnPos = new Vector2(-17.1f, -0.2f), face = Vector2.right,
                      art = "Assets/Images/corridor_entrance_door.png", artPos = new Vector2(-18.978f, 0.978f), artPPU = 45f },
    };

    [MenuItem("Tools/Horror Room/15. Connect Hospital Rooms (Lobby <-> SurgicalWing, CorridorOffice)")]
    public static void ConnectHospitalRooms()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var interact = ArcadeAction("ButtonA");
        var report = new System.Text.StringBuilder();

        foreach (var group in HospitalDoors.GroupBy(d => d.scene))
        {
            string path = $"Assets/Scenes/Rooms/{group.Key}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { report.Append($" missing {path};"); continue; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            // look the button up again: opening a scene unloads unused assets, which empties a reference taken earlier
            interact = ArcadeAction("ButtonA");
            if (interact == null) Debug.LogWarning("[HorrorRoom] ButtonA not found in ArcadeControls.inputactions; doors will use their built-in A bindings.");
            var player = Object.FindFirstObjectByType<TopDownMovement>();
            foreach (var d in group)
            {
                var bg = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith(d.bgPrefix));
                if (bg == null) { report.Append($" no background in {group.Key};"); continue; }
                var room = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Room_" + group.Key);
                Transform parent = room != null ? room.transform : null;

                // the door (press A next to it)
                var exitGo = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<DoorExit>(true))
                    .Select(x => x.gameObject).FirstOrDefault(g => g.name == d.exitName);
                if (exitGo == null) exitGo = new GameObject(d.exitName);
                if (exitGo.transform.parent == null && parent != null) exitGo.transform.SetParent(parent, false);
                exitGo.transform.position = bg.transform.TransformPoint(d.exitPos);
                var exit = exitGo.GetComponent<DoorExit>();
                if (exit == null) exit = exitGo.AddComponent<DoorExit>();
                var so = new SerializedObject(exit);
                so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
                so.FindProperty("interactAction").objectReferenceValue = interact;
                so.FindProperty("reachDistance").floatValue = d.reach;
                so.FindProperty("sceneName").stringValue = d.toScene;
                so.FindProperty("entryId").stringValue = d.entryOut;
                so.FindProperty("requireLeverPulled").boolValue = false;   // only Electrical needs the power
                so.FindProperty("requireKickedOpen").boolValue = false;
                so.ApplyModifiedProperties();

                // door art drawn over the background (if this door has its own picture)
                if (!string.IsNullOrEmpty(d.art))
                {
                    var ti = AssetImporter.GetAtPath(d.art) as TextureImporter;
                    if (ti != null)
                    {
                        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
                        ti.spritePixelsPerUnit = d.artPPU; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
                        ti.textureCompression = TextureImporterCompression.Uncompressed; ti.filterMode = FilterMode.Bilinear;
                        ti.SaveAndReimport();
                    }
                    string artName = d.exitName + "_Art";
                    var artGo = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true))
                        .Select(x => x.gameObject).FirstOrDefault(g => g.name == artName);
                    if (artGo == null) { artGo = new GameObject(artName); if (parent != null) artGo.transform.SetParent(parent, false); }
                    artGo.transform.position = bg.transform.TransformPoint(d.artPos);
                    artGo.transform.localScale = Vector3.one;
                    var asr = artGo.GetComponent<SpriteRenderer>();
                    if (asr == null) asr = artGo.AddComponent<SpriteRenderer>();
                    asr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(d.art);
                    asr.sortingLayerID = bg.sortingLayerID; asr.sortingOrder = bg.sortingOrder + 1;
                    asr.sharedMaterial = bg.sharedMaterial;
                    // the background may be scaled under the room: match its world scale
                    var bs = bg.transform.lossyScale; var ps = artGo.transform.parent != null ? artGo.transform.parent.lossyScale : Vector3.one;
                    artGo.transform.localScale = new Vector3(bs.x / ps.x, bs.y / ps.y, 1f);
                }

                // where she appears when she comes back through this door
                string spawnName = "Spawn_" + d.spawnEntry;
                var spawnGo = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RoomSpawnPoint>(true))
                    .Select(x => x.gameObject).FirstOrDefault(g => g.name == spawnName);
                if (spawnGo == null) spawnGo = new GameObject(spawnName);
                if (spawnGo.transform.parent == null && parent != null) spawnGo.transform.SetParent(parent, false);
                spawnGo.transform.position = bg.transform.TransformPoint(d.spawnPos);
                var spawn = spawnGo.GetComponent<RoomSpawnPoint>();
                if (spawn == null) spawn = spawnGo.AddComponent<RoomSpawnPoint>();
                var sso = new SerializedObject(spawn);
                sso.FindProperty("entryId").stringValue = d.spawnEntry;
                sso.FindProperty("faceDirection").vector2Value = d.face;
                sso.ApplyModifiedProperties();
                report.Append($" {group.Key}: {d.exitName} -> {d.toScene};");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        var list = EditorBuildSettings.scenes.ToList();
        foreach (var n in new[] { "Lobby", "SurgicalWing", "CorridorOffice" })
        {
            string p = $"Assets/Scenes/Rooms/{n}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null && !list.Any(e => e.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
        }
        EditorBuildSettings.scenes = list.ToArray();

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[HorrorRoom] Rooms connected:" + report + " All scenes saved.");
    }

    [MenuItem("Tools/Horror Room/16. Lobby: Bed Bridge to the Info Desk")]
    public static void LobbyBedProp()
    {
        const string lobbyPath = "Assets/Scenes/Rooms/Lobby.unity", bedPath = "Assets/Images/Rooms/Lobby/Bed.png";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.GetActiveScene().path == lobbyPath ? EditorSceneManager.GetActiveScene()
                                                                           : EditorSceneManager.OpenScene(lobbyPath, OpenSceneMode.Single);

        // same import settings as the room builder's props (PPU 28, pivot at the foot of the bed)
        var ti = AssetImporter.GetAtPath(bedPath) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[HorrorRoom] Missing " + bedPath); return; }
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 28f; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Clamp;
        var st = new TextureImporterSettings(); ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom; st.spritePivot = new Vector2(0.4941f, 0.0153f);
        st.spriteMeshType = SpriteMeshType.Tight;
        ti.SetTextureSettings(st); ti.SaveAndReimport();

        var props = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Props");
        var sample = props != null ? props.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault() : null;   // copy the other props' settings
        var bedT = props != null ? props.Find("Bed") : null;
        var go = bedT != null ? bedT.gameObject : new GameObject("Bed");
        if (props != null) go.transform.SetParent(props, false);
        go.transform.localPosition = new Vector3(11.5714f, -1.0714f, 0f);
        var sr = go.GetComponent<SpriteRenderer>(); if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(bedPath);
        sr.spriteSortPoint = SpriteSortPoint.Pivot;
        if (sample != null) { sr.sortingLayerID = sample.sortingLayerID; sr.sortingOrder = sample.sortingOrder; sr.sharedMaterial = sample.sharedMaterial; }
        var box = go.GetComponent<BoxCollider2D>(); if (box == null) box = go.AddComponent<BoxCollider2D>();
        box.offset = new Vector2(0f, 2.25f); box.size = new Vector2(2.75f, 4.5f);
        var cover = go.GetComponent<VisionCover>(); if (cover == null) cover = go.AddComponent<VisionCover>();
        cover.lowCover = true;

        // the bed moves now: the background must not have it painted in
        const string noBedPath = "Assets/Images/Backgrounds/Lobby_NoBed.png";
        var bgSr = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpriteRenderer>(true))
            .FirstOrDefault(r => r.sprite != null && r.sprite.name.StartsWith("Lobby_"));
        var oldTi = bgSr != null ? AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(bgSr.sprite)) as TextureImporter : null;
        var newTi = AssetImporter.GetAtPath(noBedPath) as TextureImporter;
        if (bgSr != null && oldTi != null && newTi != null)
        {
            var ts = new TextureImporterSettings(); oldTi.ReadTextureSettings(ts);
            newTi.SetTextureSettings(ts);
            newTi.textureCompression = oldTi.textureCompression; newTi.maxTextureSize = oldTi.maxTextureSize;
            newTi.SaveAndReimport();
            bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(noBedPath);
        }
        else Debug.LogWarning("[HorrorRoom] Couldn't swap the Lobby background to " + noBedPath + " (the painted bed will still show).");

        // a bridge to the information desk side: climb up over the headboard, kick it to tip it and slide back
        var oldRaft = go.GetComponent<BedRaft>(); if (oldRaft != null) Object.DestroyImmediate(oldRaft);
        var bridge = go.GetComponent<BedBridge>(); if (bridge == null) bridge = go.AddComponent<BedBridge>();
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        Transform room = bgSr != null ? bgSr.transform : null;
        System.Func<Vector2, Vector2> wp = v => room != null ? (Vector2)room.TransformPoint(v) : v;
        var rso = new SerializedObject(bridge);
        rso.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        rso.FindProperty("interactAction").objectReferenceValue = ArcadeAction("ButtonA");
        rso.FindProperty("footSpot").vector2Value = wp(new Vector2(11.57f, -1.6f));   // lobby floor below the bed's foot
        rso.FindProperty("topSpot").vector2Value = wp(new Vector2(11.57f, 3.85f));    // between the desks, past the headboard
        // climbing / kicking poses from her own run frames
        rso.FindProperty("stepUpPose").objectReferenceValue = FindSprite("girl128_run_up.png", "girl_run_up_02");
        rso.FindProperty("climbPoseA").objectReferenceValue = FindSprite("girl128_run_up.png", "girl_run_up_04");
        rso.FindProperty("climbPoseB").objectReferenceValue = FindSprite("girl128_run_up.png", "girl_run_up_09");
        rso.FindProperty("kickWindUpPose").objectReferenceValue = FindSprite("girl128_run_down.png", "girl_run_down_06");
        rso.FindProperty("kickPose").objectReferenceValue = FindSprite("girl128_run_down.png", "girl_run_down_02");
        rso.FindProperty("slidePose").objectReferenceValue = FindSprite("girl128_run_down.png", "girl_run_down_00");
        rso.ApplyModifiedProperties();
        if (rso.FindProperty("kickPose").objectReferenceValue == null)
            Debug.LogWarning("[HorrorRoom] Her run frames weren't found (girl_run_up_04 ...): run Tools > Horror Animation > Setup Everything, then step 16 again.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != lobbyPath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[HorrorRoom] Lobby bed is now a bridge (Props/Bed): A at its foot climbs over to the desk side, A at the headboard kicks it and slides back. Background swapped to Lobby_NoBed. Lobby saved." +
                  (props == null ? " (No 'Props' object found: Bed was put at the scene root.)" : ""));
    }

    // walls that close the info desk area, so the bed is the only way in (Lobby background units)
    static readonly Rect[] DeskZoneWalls =
    {
        new Rect(1.20f, 4.05f, 0.42f, 2.30f),    // left end of the strip behind the info desk
        new Rect(9.45f, 2.00f, 0.80f, 1.50f),    // gap between the info desk and the bed
        new Rect(15.40f, 4.40f, 0.90f, 1.20f),   // right end, behind the side desk
    };

    [MenuItem("Tools/Horror Room/17. Lobby: Flashlight on the Red Chair")]
    public static void LobbyFlashlightOnChair()
    {
        const string lobbyPath = "Assets/Scenes/Rooms/Lobby.unity", iconPath = "Assets/Images/flashlight_item.png";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;
        var scene = returnTo == lobbyPath ? EditorSceneManager.GetActiveScene() : EditorSceneManager.OpenScene(lobbyPath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        var bgSr = all.Select(t => t.GetComponent<SpriteRenderer>()).FirstOrDefault(r => r != null && r.sprite != null && r.sprite.name.StartsWith("Lobby_"));
        if (bgSr == null) { Debug.LogWarning("[HorrorRoom] Lobby background not found."); return; }
        var bgT = bgSr.transform;

        // 1) the info desk only blocks the counter, so the strip behind it (where the chair is) is walkable
        var desk = all.FirstOrDefault(t => t.name == "InfoDesk");
        SpriteRenderer deskSr = desk != null ? desk.GetComponent<SpriteRenderer>() : null;
        var deskBox = desk != null ? desk.GetComponent<BoxCollider2D>() : null;
        if (deskBox != null) { deskBox.offset = new Vector2(deskBox.offset.x, 0.45f); deskBox.size = new Vector2(deskBox.size.x, 0.9f); }
        else Debug.LogWarning("[HorrorRoom] InfoDesk collider not found: the chair may be unreachable.");

        // the outer wall ran right along the back of the counter (y 5.39): push it back there so she fits behind it
        var wallsEdge = all.Select(t => t.GetComponent<EdgeCollider2D>()).FirstOrDefault(e => e != null && e.name == "Walls");
        if (wallsEdge != null)
        {
            var pts = wallsEdge.points.ToList();
            pts.RemoveAll(p => p.y > 5.5f && p.x > 1.0f && p.x < 9.8f);            // previous run's notch
            int i = pts.FindIndex(p => Mathf.Abs(p.x - (-4f)) < 0.05f && p.y > 5f);
            if (i >= 0 && i + 1 < pts.Count)
            {
                float y = pts[i].y;
                pts.InsertRange(i + 1, new[] { new Vector2(1.2f, y), new Vector2(1.2f, 6.25f), new Vector2(9.6f, 6.25f), new Vector2(9.6f, y) });
                wallsEdge.points = pts.ToArray();
            }
            else Debug.LogWarning("[HorrorRoom] Couldn't find the wall segment behind the info desk.");
        }
        else Debug.LogWarning("[HorrorRoom] 'Walls' edge collider not found.");

        // 2) close the info desk area so only the bed leads there
        var wallsT = all.FirstOrDefault(t => t.name == "DeskZoneWalls");
        var walls = wallsT != null ? wallsT.gameObject : new GameObject("DeskZoneWalls");
        walls.transform.SetParent(bgT.parent, false);
        walls.transform.localPosition = bgT.localPosition; walls.transform.localRotation = bgT.localRotation; walls.transform.localScale = bgT.localScale;
        foreach (var old in walls.GetComponents<BoxCollider2D>()) Object.DestroyImmediate(old);
        foreach (var r in DeskZoneWalls) { var b = walls.AddComponent<BoxCollider2D>(); b.offset = r.center; b.size = r.size; }

        // 2b) the red chair becomes its own prop (sorted from its own base), so she can stand in front of it
        System.Action<string, Vector2> importProp = (path, pivot) =>
        {
            var t2 = AssetImporter.GetAtPath(path) as TextureImporter; if (t2 == null) { Debug.LogWarning("[HorrorRoom] Missing " + path); return; }
            t2.textureType = TextureImporterType.Sprite; t2.spriteImportMode = SpriteImportMode.Single; t2.spritePixelsPerUnit = 28f;
            t2.alphaIsTransparency = true; t2.mipmapEnabled = false; t2.filterMode = FilterMode.Bilinear;
            t2.textureCompression = TextureImporterCompression.Uncompressed; t2.wrapMode = TextureWrapMode.Clamp;
            var st2 = new TextureImporterSettings(); t2.ReadTextureSettings(st2);
            st2.spriteAlignment = (int)SpriteAlignment.Custom; st2.spritePivot = pivot; st2.spriteMeshType = SpriteMeshType.Tight;
            t2.SetTextureSettings(st2); t2.SaveAndReimport();
        };
        importProp("Assets/Images/Rooms/Lobby/InfoDesk_NoChair.png", new Vector2(0.5f, 0.0102f));   // same pivot as InfoDesk.png
        importProp("Assets/Images/Rooms/Lobby/InfoChair.png", new Vector2(0.5f, 0f));
        GameObject chairGo = null;
        if (deskSr != null)
        {
            deskSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Rooms/Lobby/InfoDesk_NoChair.png");
            var chairT = all.FirstOrDefault(t => t.name == "InfoChair");
            chairGo = chairT != null ? chairT.gameObject : new GameObject("InfoChair");
            chairGo.transform.SetParent(desk.parent, false);
            chairGo.transform.localPosition = new Vector3(5.8215f, 5.0f, 0f);
            var csr = chairGo.GetComponent<SpriteRenderer>(); if (csr == null) csr = chairGo.AddComponent<SpriteRenderer>();
            csr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Rooms/Lobby/InfoChair.png");
            csr.sortingLayerID = deskSr.sortingLayerID; csr.sortingOrder = deskSr.sortingOrder;
            csr.spriteSortPoint = SpriteSortPoint.Pivot; csr.sharedMaterial = deskSr.sharedMaterial;
        }

        // 3) the flashlight on the red chair's seat
        var ti = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 160f;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
            var fst = new TextureImporterSettings(); ti.ReadTextureSettings(fst);
            fst.spriteAlignment = (int)SpriteAlignment.Custom;
            fst.spritePivot = new Vector2(0.5f, 0.5f - (5.464f - 4.99f) / (60f / 160f));   // pivot 0.47 units below the picture's centre
            ti.SetTextureSettings(fst);
            ti.SaveAndReimport();
        }
        var pickT = all.FirstOrDefault(t => t.name == "Flashlight_Pickup");
        var pick = pickT != null ? pickT.gameObject : new GameObject("Flashlight_Pickup");
        pick.transform.SetParent(bgT.parent, false);
        var psr = pick.GetComponent<SpriteRenderer>(); if (psr == null) psr = pick.AddComponent<SpriteRenderer>();
        psr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        // sorted like the chair: the pivot sits just in front of the chair's base, the picture stays on the seat
        pick.transform.position = bgT.TransformPoint(new Vector2(5.821f, 4.99f));
        psr.spriteSortPoint = SpriteSortPoint.Pivot;
        if (deskSr != null) { psr.sortingLayerID = deskSr.sortingLayerID; psr.sortingOrder = deskSr.sortingOrder; psr.sharedMaterial = deskSr.sharedMaterial; }
        var fp = pick.GetComponent<FlashlightPickup>(); if (fp == null) fp = pick.AddComponent<FlashlightPickup>();
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var so = new SerializedObject(fp);
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        so.FindProperty("interactAction").objectReferenceValue = ArcadeAction("ButtonA");
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(returnTo) && returnTo != lobbyPath) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[HorrorRoom] Red chair split into its own prop (InfoChair); flashlight placed on it; info desk area closed (bed is the only way in); she starts without the flashlight. Lobby saved.");
    }

    [MenuItem("Tools/Horror Room/18. Lobby: Boarded Door to Electrical (crowbar)")]
    public static void LobbyDoorToElectrical()
    {
        const string lobbyPath = "Assets/Scenes/Rooms/Lobby.unity", elecPath = "Assets/Scenes/Rooms/Electrical.unity";
        const string planksPath = "Assets/Images/lobby_side_door_planks.png";
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string returnTo = EditorSceneManager.GetActiveScene().path;

        // ---- Lobby: the boarded-up left side door
        var lobby = EditorSceneManager.OpenScene(lobbyPath, OpenSceneMode.Single);
        var all = lobby.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        var bgSr = all.Select(t => t.GetComponent<SpriteRenderer>()).FirstOrDefault(r => r != null && r.sprite != null && r.sprite.name.StartsWith("Lobby_"));
        if (bgSr == null) { Debug.LogWarning("[HorrorRoom] Lobby background not found."); return; }
        var bgT = bgSr.transform;
        var ti = AssetImporter.GetAtPath(planksPath) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 112f;
            ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        var doorT = all.FirstOrDefault(t => t.name == "Door_ToElectrical");
        var doorGo = doorT != null ? doorT.gameObject : new GameObject("Door_ToElectrical");
        doorGo.transform.SetParent(bgT.parent, false);
        doorGo.transform.position = bgT.TransformPoint(new Vector2(-15.9f, 0.0f));
        var exit = doorGo.GetComponent<DoorExit>(); if (exit == null) exit = doorGo.AddComponent<DoorExit>();
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        var interact = ArcadeAction("ButtonA");
        var so = new SerializedObject(exit);
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        so.FindProperty("interactAction").objectReferenceValue = interact;
        so.FindProperty("reachDistance").floatValue = 1.3f;
        so.FindProperty("sceneName").stringValue = "Electrical";
        so.FindProperty("entryId").stringValue = "FromLobby";
        so.FindProperty("requireLeverPulled").boolValue = false;
        so.FindProperty("requireKickedOpen").boolValue = false;
        so.ApplyModifiedProperties();
        exit.enabled = false;                                     // until the planks are off

        var plT = doorGo.transform.Find("Planks");
        var plGo = plT != null ? plT.gameObject : new GameObject("Planks");
        plGo.transform.SetParent(doorGo.transform, false);
        plGo.transform.position = bgT.TransformPoint(new Vector2(-16.643f, 0.536f));
        var plSr = plGo.GetComponent<SpriteRenderer>(); if (plSr == null) plSr = plGo.AddComponent<SpriteRenderer>();
        plSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(planksPath);
        plSr.sortingLayerID = bgSr.sortingLayerID; plSr.sortingOrder = bgSr.sortingOrder + 2; plSr.sharedMaterial = bgSr.sharedMaterial;

        var bar = doorGo.GetComponent<BarricadedDoor>(); if (bar == null) bar = doorGo.AddComponent<BarricadedDoor>();
        var bso = new SerializedObject(bar);
        bso.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        bso.FindProperty("interactAction").objectReferenceValue = interact;
        bso.FindProperty("planks").objectReferenceValue = plSr;
        bso.FindProperty("door").objectReferenceValue = exit;
        bso.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.SaveScene(lobby);

        // ---- Electrical: where she arrives from the Lobby (just inside its door)
        var elec = EditorSceneManager.OpenScene(elecPath, OpenSceneMode.Single);
        var eAll = elec.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        var eBg = eAll.Select(t => t.GetComponent<SpriteRenderer>()).FirstOrDefault(r => r != null && r.sprite != null && r.sprite.name.StartsWith("electrical_room"));
        var eSpawnT = eAll.FirstOrDefault(t => t.name == "Spawn_FromLobby");
        var eSpawn = eSpawnT != null ? eSpawnT.gameObject : new GameObject("Spawn_FromLobby");
        if (eBg != null) { eSpawn.transform.SetParent(eBg.transform.parent, false); eSpawn.transform.position = eBg.transform.TransformPoint(new Vector2(4.4f, -1.2f)); }
        var sp = eSpawn.GetComponent<RoomSpawnPoint>(); if (sp == null) sp = eSpawn.AddComponent<RoomSpawnPoint>();
        var sso = new SerializedObject(sp);
        sso.FindProperty("entryId").stringValue = "FromLobby";
        sso.FindProperty("faceDirection").vector2Value = Vector2.left;
        sso.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(elec);
        EditorSceneManager.SaveScene(elec);

        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[HorrorRoom] Lobby left side door -> Electrical added, boarded up (crowbar selected + A to pry the planks off). Electrical has Spawn_FromLobby. Both scenes saved.");
    }

    [MenuItem("Tools/Horror Room/3. Make Scene Dark (cold global light)")]
    public static void MakeSceneDark()
    {
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (l.lightType != Light2D.LightType.Global) continue;
            Undo.RecordObject(l, "Darken global light");
            l.color = new Color(0.55f, 0.65f, 0.9f);   // cold blue moonlight
            l.intensity = 0.35f;
            EditorUtility.SetDirty(l);
            Debug.Log("[HorrorRoom] Global light dimmed: " + l.name + " (raise Intensity if it's too dark).");
        }
    }
}
