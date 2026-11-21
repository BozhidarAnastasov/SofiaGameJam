// Tools > Horror Room > ...
// 1. Setup Electrical Room Import : import settings for the 1080p room so the girl (2 units) is to scale.
// 2. Add Shadow + Flashlight      : select a character (Player / Psycho) first.
// 3. Make Scene Dark              : dims the Global Light 2D to a cold blue so the flashlight matters.
// 5. Build Box + Lever Puzzle     : pushable box (cut out of the room art) + breaker lever on the left wall.
// 6. Add Door Lamp Scare          : lamp over the door flickers after the lever; last flash shows his face in the window.
// 7. Use Current Lever Pose as Pulled : pose the Lever by hand, run this, it becomes the pulled state.
// 8. Add Interact Prompt (U)      : key icon over the girl's head when she can use something.
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

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

    [MenuItem("Tools/Horror Room/8. Add Interact Prompt (U) to Player")]
    public static void AddInteractPrompt()
    {
        var player = Object.FindFirstObjectByType<TopDownMovement>();
        if (player == null) { EditorUtility.DisplayDialog("Horror Room", "No Player (TopDownMovement) in the open scene.", "OK"); return; }
        if (player.GetComponentInChildren<InteractPrompt>(true) != null)
        {
            Debug.Log("[HorrorRoom] The Player already has an InteractPrompt.");
            return;
        }
        const string iconPath = "Assets/Images/prompt_key_u.png";
        var ti = AssetImporter.GetAtPath(iconPath) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 160f;            // ~0.45 units wide
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
        }
        var psr = player.GetComponentInChildren<SpriteRenderer>();

        var go = new GameObject("InteractPrompt");
        Undo.RegisterCreatedObjectUndo(go, "Add interact prompt");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = new Vector3(0f, 2.35f, 0f);    // just above her head (she is ~2 units tall)
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (psr != null) sr.sortingLayerID = psr.sortingLayerID;
        sr.sortingOrder = 100;                                      // always on top
        // unlit: stays readable in the dark room
        var unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        if (unlit != null) sr.sharedMaterial = unlit;
        var prompt = go.AddComponent<InteractPrompt>();
        var so = new SerializedObject(prompt);
        so.FindProperty("icon").objectReferenceValue = sr;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(player.gameObject);
        Selection.activeGameObject = go;
        Debug.Log("[HorrorRoom] Interact prompt added to the Player. If it's a prefab instance, use Overrides > Apply All.");
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
