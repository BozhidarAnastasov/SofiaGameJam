// Tools > Horror Animation > Setup Everything
// 1) Import settings + clean grid slicing (pivot on the feet) for every character sheet.
// 2) Animation clips (each clip also owns SpriteRenderer.flipX, so no script ever flips sprites).
// 3) Animator controllers for the girl and the psycho (only created if they do not exist yet).
//
// Change WalkPPU to make every character bigger/smaller in the world; all other sheets follow.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class HorrorSpriteAnimationSetup
{
    // 51 px per unit -> the girl's walk sprite (102 px tall) is 2 world units tall.
    const float WalkPPU = 51f;

    const string Girl = "Assets/Animations/girlAnimation/";
    const string Psycho = "Assets/Animations/entityAnimation/";
    const string ClipDir = "Assets/Animations/Clips/";
    const string CtrlDir = "Assets/Animations/Controllers/";

    class Sheet
    {
        public string path; public int cellW, cellH; public (string name, int count)[] rows;
        public float ppu, pivotX = 0.5f, pivotY; public int maxSize = 4096;
    }

    // The girl was drawn at a different size in every sheet; these ratios make her the same
    // height on screen in all of them (girl's standing height in px / 102 px of the walk sheet).
    static readonly Sheet[] Sheets =
    {
        // Matched walk/run set (girl_sprites_128.zip): 128x128 cells, feet 8 px from the bottom, same height everywhere.
        // run_down was 4% taller than the walk and has been scaled to match.
        G128("girl128_walk_down.png", "girl_walk_down", 12), G128("girl128_walk_up.png", "girl_walk_up", 12),
        G128("girl128_walk_left.png", "girl_walk_left", 16, 58.5f / 128f), G128("girl128_walk_right.png", "girl_walk_right", 16, 68.5f / 128f),
        G128("girl128_run_down.png", "girl_run_down", 12), G128("girl128_run_up.png", "girl_run_up", 12),
        G128("girl128_run_left.png", "girl_run_left", 14, 57f / 128f), G128("girl128_run_right.png", "girl_run_right", 14, 69f / 128f),
        // Idles cut from the walk sprites (same art): down = frame 0 with mirrored planted leg, up = frame 4,
        // sides = frame 6 (legs together). 8 frames: upper body rises 1 px = one slow breath.
        G128("girl128_idle_down.png", "girl_idle_down", 8), G128("girl128_idle_up.png", "girl_idle_up", 8),
        G128("girl128_idle_left.png", "girl_idle_left", 8, 58.5f / 128f), G128("girl128_idle_right.png", "girl_idle_right", 8, 68.5f / 128f),
        new Sheet { path = Girl + "girl_crawl_11f.png", cellW = 228, cellH = 144,
            rows = new[] { ("girl_crawl_right", 11) }, ppu = WalkPPU * 224f / 102f, pivotY = 8f / 144f },
        new Sheet { path = Girl + "girl_pickup_10f.png", cellW = 228, cellH = 440,
            rows = new[] { ("girl_pickup_right", 10) }, ppu = WalkPPU * 421f / 102f, pivotY = 8f / 440f },

        new Sheet { path = Psycho + "psycho-walking-sprite-sheet-transparent.png", cellW = 160, cellH = 160,
            rows = new[] { ("psycho_walk_down", 12), ("psycho_walk_left", 16), ("psycho_walk_right", 16), ("psycho_walk_up", 12) },
            ppu = WalkPPU, pivotY = 3f / 160f },
        new Sheet { path = Psycho + "psycho-grab-sequence-sprite-sheet-transparent.png", cellW = 224, cellH = 192,
            rows = new[] { ("psycho_grab_left", 4), ("psycho_grab_right", 4) }, ppu = WalkPPU, pivotY = 3f / 192f },
        new Sheet { path = Psycho + "psycho-death-screen-sprite-sheet.png", cellW = 1536, cellH = 1024,
            rows = new[] { ("psycho_death_screen", 4) }, ppu = 100f, pivotY = 0.5f, maxSize = 8192 },
    };

    // pivotX: side views are not centred in their cells, so the pivot sits under her body instead
    static Sheet G128(string file, string spriteName, int frames, float pivotX = 0.5f) => new Sheet
    {
        path = Girl + "Sprites128/" + file, cellW = 128, cellH = 128,
        rows = new[] { (spriteName, frames) }, ppu = WalkPPU, pivotX = pivotX, pivotY = 8f / 128f,
    };

    [MenuItem("Tools/Horror Animation/Setup Everything")]
    public static void SetupEverything()
    {
        SliceAllSheets();
        var clips = BuildClips();
        string[] dirs = { "Down", "Up", "Left", "Right" };
        if (HasAll(clips, dirs.SelectMany(d => new[] { "Girl_Idle_" + d, "Girl_Walk_" + d, "Girl_Run_" + d })
                .Concat(new[] { "Girl_Crawl_Left", "Girl_Crawl_Right", "Girl_PickUp_Left", "Girl_PickUp_Right" })))
            BuildGirlController(clips);
        if (HasAll(clips, dirs.SelectMany(d => new[] { "Psycho_Idle_" + d, "Psycho_Walk_" + d })
                .Concat(new[] { "Psycho_Grab_Left", "Psycho_Grab_Right" })))
            BuildPsychoController(clips);
        AssetDatabase.SaveAssets();
        Debug.Log("[HorrorAnimation] Done. Controllers are in " + CtrlDir);
    }

    static bool HasAll(Dictionary<string, AnimationClip> clips, IEnumerable<string> names)
    {
        var missing = names.Where(n => !clips.ContainsKey(n)).ToList();
        if (missing.Count > 0)
            Debug.LogError("[HorrorAnimation] Controller not built, missing clips: " + string.Join(", ", missing));
        return missing.Count == 0;
    }

    [MenuItem("Tools/Horror Animation/1. Slice Sheets Only")]
    public static void SliceAllSheets()
    {
        foreach (var s in Sheets) Slice(s);
    }

    static void Slice(Sheet s)
    {
        var importer = AssetImporter.GetAtPath(s.path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("[HorrorAnimation] Missing sheet: " + s.path); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = s.ppu;
        importer.filterMode = FilterMode.Bilinear;                       // painted art, not pixel art
        importer.textureCompression = TextureImporterCompression.Uncompressed; // no blocky artefacts
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;                       // no bleeding from neighbour frames
        importer.alphaIsTransparency = true;                             // no dark halos around edges
        importer.maxTextureSize = s.maxSize;                             // never let Unity downscale the sheet
        importer.isReadable = false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
        dp.InitSpriteEditorDataProvider();

        // keep the IDs of sprites that already have the same name, so references survive re-running
        var oldIds = dp.GetSpriteRects().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First().spriteID);

        int texH = s.cellH * s.rows.Length;
        var rects = new List<SpriteRect>();
        for (int r = 0; r < s.rows.Length; r++)
        {
            for (int c = 0; c < s.rows[r].count; c++)
            {
                string name = $"{s.rows[r].name}_{c:00}";
                rects.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(c * s.cellW, texH - (r + 1) * s.cellH, s.cellW, s.cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(s.pivotX, s.pivotY),
                    spriteID = oldIds.TryGetValue(name, out var id) ? id : GUID.Generate(),
                });
            }
        }
        dp.SetSpriteRects(rects.ToArray());
        var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        names?.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
        dp.Apply();
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- clips
    static Dictionary<string, Sprite> sprites;

    static Sprite[] Frames(string prefix, IEnumerable<int> indices = null)
    {
        var all = sprites.Where(kv => kv.Key.StartsWith(prefix + "_")).OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray();
        if (all.Length == 0) Debug.LogWarning("[HorrorAnimation] No sprites named " + prefix + "_xx");
        return indices == null ? all : indices.Where(i => i < all.Length).Select(i => all[i]).ToArray();
    }

    static IEnumerable<int> Even(int n) => Enumerable.Range(0, n).Where(i => i % 2 == 0);

    public static Dictionary<string, AnimationClip> BuildClips()
    {
        sprites = new Dictionary<string, Sprite>();
        foreach (var s in Sheets)
            foreach (var sp in AssetDatabase.LoadAllAssetsAtPath(s.path).OfType<Sprite>())
                sprites[sp.name] = sp;

        var c = new Dictionary<string, AnimationClip>();
        void Add(string folder, string name, Sprite[] f, float cycle, bool loop, bool flip = false, bool ui = false)
        {
            if (f.Length > 0) c[name] = MakeClip(folder, name, f, cycle, loop, flip, ui);
        }

        // ---- Girl. Cycle = seconds for the whole loop. Tune these, not the frame count.
        Add("Girl", "Girl_Idle_Down", Frames("girl_idle_down"), 2.4f, true);               // one slow breath
        Add("Girl", "Girl_Idle_Up", Frames("girl_idle_up"), 2.4f, true);
        Add("Girl", "Girl_Idle_Left", Frames("girl_idle_left"), 2.4f, true);               // one slow breath
        Add("Girl", "Girl_Idle_Right", Frames("girl_idle_right"), 2.4f, true);
        Add("Girl", "Girl_Walk_Down", Frames("girl_walk_down"), 1.0f, true);   // 12 f -> 12 fps
        Add("Girl", "Girl_Walk_Up", Frames("girl_walk_up"), 1.0f, true);
        Add("Girl", "Girl_Walk_Left", Frames("girl_walk_left"), 1.0f, true);   // 16 f -> 16 fps, same rhythm
        Add("Girl", "Girl_Walk_Right", Frames("girl_walk_right"), 1.0f, true);
        Add("Girl", "Girl_Run_Down", Frames("girl_run_down"), 0.66f, true);
        Add("Girl", "Girl_Run_Up", Frames("girl_run_up"), 0.66f, true);
        Add("Girl", "Girl_Run_Right", Frames("girl_run_right"), 0.7f, true);
        Add("Girl", "Girl_Run_Left", Frames("girl_run_left"), 0.7f, true);
        Add("Girl", "Girl_Crawl_Right", Frames("girl_crawl_right"), 1.1f, true);
        Add("Girl", "Girl_Crawl_Left", Frames("girl_crawl_right"), 1.1f, true, flip: true);
        Add("Girl", "Girl_PickUp_Right", Frames("girl_pickup_right"), 0.85f, false);
        Add("Girl", "Girl_PickUp_Left", Frames("girl_pickup_right"), 0.85f, false, flip: true);

        // ---- Psycho. His side walk holds every pose for 2 cells, so only the 8 unique poses are used.
        Add("Psycho", "Psycho_Idle_Down", Frames("psycho_walk_down", new[] { 0 }), 0.5f, true);
        Add("Psycho", "Psycho_Idle_Up", Frames("psycho_walk_up", new[] { 0 }), 0.5f, true);
        Add("Psycho", "Psycho_Idle_Left", Frames("psycho_walk_left", new[] { 0 }), 0.5f, true);
        Add("Psycho", "Psycho_Idle_Right", Frames("psycho_walk_right", new[] { 0 }), 0.5f, true);
        Add("Psycho", "Psycho_Walk_Down", Frames("psycho_walk_down"), 1.2f, true);
        Add("Psycho", "Psycho_Walk_Up", Frames("psycho_walk_up"), 1.2f, true);
        Add("Psycho", "Psycho_Walk_Left", Frames("psycho_walk_left", Even(16)), 1.2f, true);
        Add("Psycho", "Psycho_Walk_Right", Frames("psycho_walk_right", Even(16)), 1.2f, true);
        Add("Psycho", "Psycho_Grab_Left", Frames("psycho_grab_left"), 1.0f, false);
        Add("Psycho", "Psycho_Grab_Right", Frames("psycho_grab_right"), 1.0f, false);
        // For a UI Image (full-screen jump scare), not a SpriteRenderer.
        Add("Psycho", "Psycho_DeathScreen_UI", Frames("psycho_death_screen"), 1.2f, false, ui: true);
        return c;
    }

    static AnimationClip MakeClip(string folder, string name, Sprite[] frames, float cycle, bool loop, bool flipX, bool ui)
    {
        EnsureFolder(ClipDir + folder);
        string path = $"{ClipDir}{folder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        else clip.ClearCurves();

        int n = frames.Length;
        float step = cycle / n;
        clip.frameRate = 1f / step;
        var type = ui ? typeof(UnityEngine.UI.Image) : typeof(SpriteRenderer);

        // n frames + 1 closing key: the last frame is shown for a full step instead of ~0 s
        // (the classic "last frame flashes / loop hitches" bug of hand-made sprite clips).
        var keys = new ObjectReferenceKeyframe[n + 1];
        for (int i = 0; i < n; i++) keys[i] = new ObjectReferenceKeyframe { time = i * step, value = frames[i] };
        keys[n] = new ObjectReferenceKeyframe { time = n * step, value = frames[n - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", type, "m_Sprite"), keys);

        if (!ui)
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_FlipX"),
                AnimationCurve.Constant(0f, n * step, flipX ? 1f : 0f));

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // ---------------------------------------------------------------- controllers
    static void BuildGirlController(Dictionary<string, AnimationClip> c)
    {
        var ctrl = NewController("Girl", withPickUp: true);
        if (ctrl == null) return;
        var sm = ctrl.layers[0].stateMachine;

        var idle = Directional(ctrl, sm, "Idle", new Vector3(300, 0), c, "Girl_Idle", false);
        var walk = Directional(ctrl, sm, "Walk", new Vector3(300, 100), c, "Girl_Walk", true);
        var run = Directional(ctrl, sm, "Run", new Vector3(300, 200), c, "Girl_Run", true);
        var crawl = Sideways(ctrl, sm, "Crawl", new Vector3(300, 300), c["Girl_Crawl_Left"], c["Girl_Crawl_Right"], true);
        sm.defaultState = idle;
        LinkAll(new[] { idle, walk, run, crawl });

        var pick = Sideways(ctrl, sm, "PickUp", new Vector3(650, 100), c["Girl_PickUp_Left"], c["Girl_PickUp_Right"], false);
        var any = sm.AddAnyStateTransition(pick);
        Instant(any); any.canTransitionToSelf = false; any.AddCondition(AnimatorConditionMode.If, 0, "PickUp");
        var back = pick.AddTransition(idle);
        back.hasExitTime = true; back.exitTime = 1f; back.duration = 0f; back.hasFixedDuration = true;
    }

    static void BuildPsychoController(Dictionary<string, AnimationClip> c)
    {
        var ctrl = NewController("Psycho", withPickUp: false);
        if (ctrl == null) return;
        ctrl.AddParameter("Grab", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;

        var idle = Directional(ctrl, sm, "Idle", new Vector3(300, 0), c, "Psycho_Idle", false);
        var walk = Directional(ctrl, sm, "Walk", new Vector3(300, 100), c, "Psycho_Walk", true);
        sm.defaultState = idle;
        LinkAll(new[] { idle, walk });

        // Grab is not looped and has no way out: it holds the last frame until you show the death screen.
        var grab = Sideways(ctrl, sm, "Grab", new Vector3(650, 50), c["Psycho_Grab_Left"], c["Psycho_Grab_Right"], false);
        var any = sm.AddAnyStateTransition(grab);
        Instant(any); any.canTransitionToSelf = false; any.AddCondition(AnimatorConditionMode.If, 0, "Grab");
    }

    static AnimatorController NewController(string name, bool withPickUp)
    {
        EnsureFolder(CtrlDir);
        string path = CtrlDir + name + ".controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
        {
            Debug.Log($"[HorrorAnimation] {path} already exists, kept as is (clips were still updated). Delete it to rebuild.");
            return null;
        }
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "DirX", type = AnimatorControllerParameterType.Float, defaultFloat = 0 });
        ctrl.AddParameter(new AnimatorControllerParameter { name = "DirY", type = AnimatorControllerParameterType.Float, defaultFloat = -1 });
        ctrl.AddParameter(new AnimatorControllerParameter { name = "SideX", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        ctrl.AddParameter(new AnimatorControllerParameter { name = "State", type = AnimatorControllerParameterType.Int, defaultInt = 0 });
        ctrl.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        if (withPickUp) ctrl.AddParameter("PickUp", AnimatorControllerParameterType.Trigger);
        return ctrl;
    }

    // 4-direction blend tree. The runtime script always feeds exact cardinal values, so exactly one
    // child has full weight: no flicker between two sprites on diagonals.
    static AnimatorState Directional(AnimatorController ctrl, AnimatorStateMachine sm, string name, Vector3 pos,
        Dictionary<string, AnimationClip> c, string prefix, bool speedParam)
    {
        var tree = NewTree(ctrl, name, BlendTreeType.SimpleDirectional2D, "DirX");
        tree.blendParameterY = "DirY";
        tree.AddChild(c[prefix + "_Down"], new Vector2(0, -1));
        tree.AddChild(c[prefix + "_Up"], new Vector2(0, 1));
        tree.AddChild(c[prefix + "_Left"], new Vector2(-1, 0));
        tree.AddChild(c[prefix + "_Right"], new Vector2(1, 0));
        return StateFor(sm, name, pos, tree, speedParam);
    }

    // Left/right only. Uses SideX (never 0) so facing up/down can't produce a 50/50 tie.
    static AnimatorState Sideways(AnimatorController ctrl, AnimatorStateMachine sm, string name, Vector3 pos,
        AnimationClip left, AnimationClip right, bool speedParam)
    {
        var tree = NewTree(ctrl, name, BlendTreeType.Simple1D, "SideX");
        tree.useAutomaticThresholds = false;
        tree.AddChild(left, -1f);
        tree.AddChild(right, 1f);
        return StateFor(sm, name, pos, tree, speedParam);
    }

    static BlendTree NewTree(AnimatorController ctrl, string name, BlendTreeType type, string param)
    {
        var tree = new BlendTree { name = name, blendType = type, blendParameter = param, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        return tree;
    }

    static AnimatorState StateFor(AnimatorStateMachine sm, string name, Vector3 pos, BlendTree tree, bool speedParam)
    {
        var st = sm.AddState(name, pos);
        st.motion = tree;
        st.writeDefaultValues = true;
        if (speedParam) { st.speedParameter = "AnimSpeed"; st.speedParameterActive = true; }
        return st;
    }

    // Every locomotion state can go to every other one instantly when State == its index.
    static void LinkAll(AnimatorState[] states)
    {
        for (int a = 0; a < states.Length; a++)
            for (int b = 0; b < states.Length; b++)
                if (a != b)
                {
                    var t = states[a].AddTransition(states[b]);
                    Instant(t);
                    t.AddCondition(AnimatorConditionMode.Equals, b, "State");
                }
    }

    // Sprite animation can't cross-fade: any transition time > 0 shows two clips fighting.
    static void Instant(AnimatorStateTransition t)
    {
        t.hasExitTime = false; t.exitTime = 0f; t.duration = 0f; t.hasFixedDuration = true; t.offset = 0f;
    }

    static void EnsureFolder(string path)
    {
        path = path.TrimEnd('/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
