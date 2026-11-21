// Tools > Hospital Rooms > ...
// Builds one scene per hospital room from the cut-out sprites in Assets/Images/Rooms/<Room>/
// and the <Room>_layout.json next to them (positions, pivots at the feet, colliders, fade zones).
//
//   Build All Room Scenes : Lobby, SurgicalWing, CorridorOffice -> Assets/Scenes/Rooms/<Room>.unity
//   Build <Room>          : just that one
//   Reimport Room Sprites : only fixes the sprite import settings (PPU, pivots), no scene changes
//
// Layout of every scene:
//   Background (sorting layer Background) : the full room painting, walls + floor
//   Props      (sorting layer Enviornment, Sort Point = Pivot) : furniture cut out of the painting, pivot at the feet,
//              BoxCollider2D on the footprint, VisionCover (low = desks/chairs, tall = cabinets/plants)
//   SouthWall  (sorting layer Foreground) : front wall, fades to 35% while she is behind it (WallFade)
//   Walls      : EdgeCollider2D around the walkable floor + PolygonCollider2D blockers (the lobby hole, rubble)
//   Markers    : empty spots for doors / the keycard, to hang scripts on later
// Re-running a build asks before it overwrites an existing scene.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class HospitalRoomsBuilder
{
    static readonly string[] Rooms = { "Lobby", "SurgicalWing", "CorridorOffice" };
    const string RoomsFolder = "Assets/Images/Rooms";
    const string ScenesFolder = "Assets/Scenes/Rooms";
    const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    const string PropLayer = "Enviornment";       // same sorting layer as the girl, so Y-sorting works between them
    const string BackgroundLayer = "Background";
    const string ForegroundLayer = "Foreground";

    // ------------------------------------------------------------------ json layout
    [Serializable] class V2 { public float x, y; public Vector2 V => new Vector2(x, y); }
    [Serializable] class RectJ { public float x, y, width, height; public Rect R => new Rect(x, y, width, height); }
    [Serializable] class Col { public V2 offset; public V2 size; }
    [Serializable] class Prop { public string name; public string sprite; public V2 pivot; public V2 position; public Col[] colliders; public bool lowCover; }
    [Serializable] class Blocker { public string name; public V2[] points; }
    [Serializable] class Marker { public string name; public V2 position; }
    [Serializable]
    class Layout
    {
        public string room, background, southWall;
        public float pixelsPerUnit, cameraSize;
        public V2 spawn;
        public V2[] floor;
        public Blocker[] blockers;
        public RectJ[] fadeZones;
        public Marker[] markers;
        public Prop[] props;
    }

    static Layout Load(string room)
    {
        string path = $"{RoomsFolder}/{room}/{room}_layout.json";
        var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (ta == null) { Debug.LogError("[HospitalRooms] Missing " + path); return null; }
        return JsonUtility.FromJson<Layout>(ta.text);
    }

    // ------------------------------------------------------------------ menu
    [MenuItem("Tools/Hospital Rooms/Build All Room Scenes")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var built = new List<string>();
        foreach (var r in Rooms) if (Build(r, false)) built.Add(r);
        Debug.Log("[HospitalRooms] Built: " + string.Join(", ", built) + ". Scenes are in " + ScenesFolder + " and in Build Settings.");
        if (built.Count > 0) EditorSceneManager.OpenScene($"{ScenesFolder}/{built[0]}.unity");
    }

    // Command line: Unity -batchmode -quit -projectPath ... -executeMethod HospitalRoomsBuilder.BuildAllBatch
    public static void BuildAllBatch()
    {
        try
        {
            var built = new List<string>();
            foreach (var r in Rooms) if (Build(r, false)) built.Add(r);
            Debug.Log("[HospitalRooms] Batch built: " + string.Join(", ", built));
            if (built.Count != Rooms.Length)
            {
                Debug.LogError("[HospitalRooms] Not all rooms were built.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    [MenuItem("Tools/Hospital Rooms/Build Lobby")] public static void BuildLobby() => BuildOne("Lobby");
    [MenuItem("Tools/Hospital Rooms/Build Surgical Wing")] public static void BuildSurgical() => BuildOne("SurgicalWing");
    [MenuItem("Tools/Hospital Rooms/Build Corridor + Office")] public static void BuildCorridor() => BuildOne("CorridorOffice");

    [MenuItem("Tools/Hospital Rooms/Reimport Room Sprites")]
    public static void ReimportAll()
    {
        foreach (var r in Rooms) { var l = Load(r); if (l != null) ImportRoom(l); }
        Debug.Log("[HospitalRooms] Sprite import settings refreshed.");
    }

    static void BuildOne(string room)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build(room, true);
    }

    // ------------------------------------------------------------------ import settings
    static void ImportSprite(string path, float ppu, Vector2 pivot, bool fullRect)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning("[HospitalRooms] Missing " + path); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;
        var st = new TextureImporterSettings();
        ti.ReadTextureSettings(st);
        st.spriteAlignment = (int)SpriteAlignment.Custom;
        st.spritePivot = pivot;
        st.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        ti.SetTextureSettings(st);
        ti.SaveAndReimport();
    }

    static void ImportRoom(Layout l)
    {
        ImportSprite(l.background, l.pixelsPerUnit, new Vector2(0.5f, 0.5f), true);
        ImportSprite(l.southWall, l.pixelsPerUnit, new Vector2(0.5f, 0.5f), false);
        foreach (var p in l.props) ImportSprite(p.sprite, l.pixelsPerUnit, p.pivot.V, false);
    }

    // ------------------------------------------------------------------ scene
    static bool Build(string room, bool openAfter)
    {
        var l = Load(room);
        if (l == null) return false;

        string scenePath = $"{ScenesFolder}/{room}.unity";
        if (File.Exists(scenePath) && !Application.isBatchMode &&
            !EditorUtility.DisplayDialog("Hospital Rooms", $"{scenePath} already exists. Rebuild it from scratch?\n(Anything you added to it by hand is lost.)", "Rebuild", "Skip"))
            return false;

        ImportRoom(l);
        EnsureFolder(ScenesFolder);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // the girl (prefab instance) - gives us the sorting layer + material the props must share
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject player = null;
        SpriteRenderer playerSr = null;
        if (playerPrefab != null)
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(l.spawn.x, l.spawn.y, 0f);
            var move = player.GetComponentInChildren<TopDownMovement>();
            playerSr = move != null ? move.GetComponent<SpriteRenderer>() : null;
            if (playerSr == null) playerSr = player.GetComponentInChildren<SpriteRenderer>();
        }
        else Debug.LogWarning("[HospitalRooms] " + PlayerPrefabPath + " not found: scene built without the girl.");
        Material spriteMat = playerSr != null ? playerSr.sharedMaterial : null;
        int propLayerId = SortingLayerId(PropLayer, playerSr != null ? playerSr.sortingLayerID : 0);

        // light: same cold moonlight as the electrical room
        var lightGo = new GameObject("Global Light 2D");
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = new Color(0.55f, 0.65f, 0.9f);
        light.intensity = 0.35f;
        var lso = new SerializedObject(light);
        var layersProp = lso.FindProperty("m_ApplyToSortingLayers");
        if (layersProp != null)
        {
            var layers = SortingLayer.layers;
            layersProp.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++) layersProp.GetArrayElementAtIndex(i).intValue = layers[i].id;
            lso.ApplyModifiedPropertiesWithoutUndo();
        }

        var root = new GameObject("Room_" + room).transform;

        // background
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(root, false);
        var bg = bgGo.AddComponent<SpriteRenderer>();
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(l.background);
        bg.sortingLayerID = SortingLayerId(BackgroundLayer, 0);
        bg.sortingOrder = 0;
        if (spriteMat != null) bg.sharedMaterial = spriteMat;

        // walls: edge loop around the walkable floor
        var wallsGo = new GameObject("Walls");
        wallsGo.transform.SetParent(root, false);
        var edge = wallsGo.AddComponent<EdgeCollider2D>();
        var pts = l.floor.Select(v => v.V).ToList();
        pts.Add(pts[0]);
        edge.points = pts.ToArray();
        edge.edgeRadius = 0.05f;
        if (l.blockers != null)
            foreach (var b in l.blockers)
            {
                var bGo = new GameObject("Blocker_" + b.name);
                bGo.transform.SetParent(wallsGo.transform, false);
                var poly = bGo.AddComponent<PolygonCollider2D>();
                poly.points = b.points.Select(v => v.V).ToArray();
            }

        // props
        var propsRoot = new GameObject("Props").transform;
        propsRoot.SetParent(root, false);
        foreach (var p in l.props)
        {
            var go = new GameObject(p.name);
            go.transform.SetParent(propsRoot, false);
            go.transform.localPosition = new Vector3(p.position.x, p.position.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p.sprite);
            sr.sortingLayerID = propLayerId;
            sr.sortingOrder = playerSr != null ? playerSr.sortingOrder : 0;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            if (spriteMat != null) sr.sharedMaterial = spriteMat;
            if (p.colliders != null)
                foreach (var c in p.colliders)
                {
                    var box = go.AddComponent<BoxCollider2D>();
                    box.offset = c.offset.V;
                    box.size = c.size.V;
                }
            var cover = go.AddComponent<VisionCover>();
            cover.lowCover = p.lowCover;
        }

        // south wall in front of everything, fades while she is behind it
        var swGo = new GameObject("SouthWall");
        swGo.transform.SetParent(root, false);
        var sw = swGo.AddComponent<SpriteRenderer>();
        sw.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(l.southWall);
        sw.sortingLayerID = SortingLayerId(ForegroundLayer, propLayerId);
        sw.sortingOrder = 10;
        if (spriteMat != null) sw.sharedMaterial = spriteMat;
        var fade = swGo.AddComponent<WallFade>();
        var fso = new SerializedObject(fade);
        if (player != null) fso.FindProperty("player").objectReferenceValue = player.transform;
        var zones = fso.FindProperty("fadeZones");
        zones.arraySize = l.fadeZones.Length;
        for (int i = 0; i < l.fadeZones.Length; i++) zones.GetArrayElementAtIndex(i).rectValue = l.fadeZones[i].R;
        fso.ApplyModifiedPropertiesWithoutUndo();

        // markers (doors, keycard spot...)
        var markers = new GameObject("Markers").transform;
        markers.SetParent(root, false);
        var spawn = new GameObject("PlayerSpawn").transform;
        spawn.SetParent(markers, false);
        spawn.localPosition = new Vector3(l.spawn.x, l.spawn.y, 0f);
        if (l.markers != null)
            foreach (var m in l.markers)
            {
                var t = new GameObject(m.name).transform;
                t.SetParent(markers, false);
                t.localPosition = new Vector3(m.position.x, m.position.y, 0f);
            }

        // camera follows the girl, clamped to the room
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = l.cameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        camGo.transform.position = new Vector3(l.spawn.x, l.spawn.y, -10f);
        camGo.AddComponent<AudioListener>();
        var follow = camGo.AddComponent<RoomCamera>();
        var cso = new SerializedObject(follow);
        if (player != null) cso.FindProperty("target").objectReferenceValue = player.transform;
        cso.FindProperty("bounds").objectReferenceValue = bg;
        cso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, scenePath);
        AddToBuildSettings(scenePath);
        Debug.Log($"[HospitalRooms] {room}: {l.props.Length} props, saved to {scenePath}. " +
                  $"Room is {bg.bounds.size.x:0.#} x {bg.bounds.size.y:0.#} units at {l.pixelsPerUnit} px/unit.");
        if (!openAfter) return true;
        Selection.activeGameObject = root.gameObject;
        return true;
    }

    static int SortingLayerId(string name, int fallback)
    {
        foreach (var sl in SortingLayer.layers) if (sl.name == name) return sl.id;
        Debug.LogWarning($"[HospitalRooms] Sorting layer '{name}' not found, using fallback.");
        return fallback;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void AddToBuildSettings(string scenePath)
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == scenePath)) return;
        list.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
