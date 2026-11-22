#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

public static class ItemHintPopupBuilder
{
    [MenuItem("GameObject/Rebuild ItemHintPopup Here", false, 0)]
    [MenuItem("Tools/Rebuild ItemHintPopup on Selected Player")]
    public static void BuildItemHintPopup()
    {
        // Attach to selected object (e.g., Player), or find Player automatically
        GameObject parentObj = Selection.activeGameObject;
        if (parentObj == null)
        {
            parentObj = GameObject.FindGameObjectWithTag("Player");
            if (parentObj == null) parentObj = GameObject.Find("Player");
        }

        // Prevent accidental duplicate if one already exists under parent
        if (parentObj != null)
        {
            Transform existing = parentObj.transform.Find("ItemHintPopup");
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.LogWarning("[ItemHintPopupBuilder] 'ItemHintPopup' already exists on " + parentObj.name + " — selected it for you!");
                return;
            }
        }

        // 1. Root: ItemHintPopup (positioned upper-left of Player)
        GameObject root = new GameObject("ItemHintPopup");
        Undo.RegisterCreatedObjectUndo(root, "Rebuild ItemHintPopup");

        if (parentObj != null)
        {
            root.transform.SetParent(parentObj.transform, false);
        }
        root.transform.localPosition = new Vector3(-1.15f, 1.35f, 0f);
        root.transform.localScale = Vector3.one;

        // 2. BubbleBackground (SpriteRenderer at Order 50)
        GameObject bubbleBG = CreateSpriteChild("BubbleBackground", root.transform, Vector3.zero, 50);
        TryAutoAssignSprite(bubbleBG.GetComponent<SpriteRenderer>(), new[] { "thought_bubble_white", "thought_bubble", "bubble" });

        // 3. Shared '?' QuestionMark on BubbleBackground so it automatically shows with every item!
        GameObject qMarkObj = new GameObject("QuestionMark");
        qMarkObj.transform.SetParent(bubbleBG.transform, false);
        qMarkObj.transform.localPosition = new Vector3(0.32f, 0.04f, 0f);

        TextMeshPro tmp = qMarkObj.AddComponent<TextMeshPro>();
        tmp.text = "?";
        tmp.fontSize = 5.5f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.72f, 0.14f, 0.14f, 1f); // Crisp dark crimson on white bubble
        tmp.sortingOrder = 52;

        RectTransform qRT = qMarkObj.GetComponent<RectTransform>();
        if (qRT != null) qRT.sizeDelta = new Vector2(1f, 1f);

        // 4. Create the exact 5 Item Children (Order 51, slightly left inside the bubble)
        Vector3 iconOffset = new Vector3(-0.22f, 0.04f, 0f);

        GameObject crowbar   = CreateItemChild("Crowbar",   root.transform, iconOffset, 51, new[] { "crowbar" });
        GameObject keycard   = CreateItemChild("Keycard",   root.transform, iconOffset, 51, new[] { "keycard", "card", "key" });
        GameObject cloak     = CreateItemChild("Cloak",     root.transform, iconOffset, 51, new[] { "cloak", "coat" });
        GameObject lightObj  = CreateItemChild("light",     root.transform, iconOffset, 51, new[] { "flashlight", "torch", "light" });
        GameObject heightTip = CreateItemChild("HeightTip", root.transform, iconOffset, 51, new[] { "height", "crouch", "climb", "tip" });

        // 5. Attach PlayerItemHintPopup and auto-wire all serialized fields!
        PlayerItemHintPopup popupScript = root.AddComponent<PlayerItemHintPopup>();
        SerializedObject so = new SerializedObject(popupScript);
        so.FindProperty("bubbleBackground").objectReferenceValue = bubbleBG;
        so.FindProperty("crowbarObj").objectReferenceValue       = crowbar;
        so.FindProperty("keycardObj").objectReferenceValue       = keycard;
        so.FindProperty("cloakObj").objectReferenceValue         = cloak;
        so.FindProperty("lightObj").objectReferenceValue         = lightObj;
        so.FindProperty("heightTipObj").objectReferenceValue     = heightTip;
        so.ApplyModifiedProperties();

        // Hide item icons in edit mode except BubbleBackground + Keycard preview so you can adjust size easily
        crowbar.SetActive(false);
        cloak.SetActive(false);
        lightObj.SetActive(false);
        heightTip.SetActive(false);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("[ItemHintPopupBuilder] Rebuilt 'ItemHintPopup' under " + (parentObj != null ? parentObj.name : "Scene Root") + "! Check sprite slots if any prefabs/sprites need swapping.");
    }

    private static GameObject CreateItemChild(string exactName, Transform parent, Vector3 localPos, int sortOrder, string[] searchKeywords)
    {
        // First check if the user already has a Prefab with this name in Assets!
        GameObject existingPrefab = FindPrefabByKeywords(exactName, searchKeywords);
        if (existingPrefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(existingPrefab, parent);
            instance.name = exactName;
            instance.transform.localPosition = localPos;

            // Strip colliders/rigidbodies if the item prefab was a world pickup so it won't push the Player!
            foreach (var col in instance.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(col);
            foreach (var rb in instance.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(rb);

            foreach (var sr in instance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingOrder = sortOrder;
            }
            return instance;
        }

        // Otherwise create a clean SpriteRenderer object and auto-search project sprites
        GameObject child = CreateSpriteChild(exactName, parent, localPos, sortOrder);
        TryAutoAssignSprite(child.GetComponent<SpriteRenderer>(), searchKeywords);
        return child;
    }

    private static GameObject CreateSpriteChild(string name, Transform parent, Vector3 localPos, int sortOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortOrder;
        return go;
    }

    private static GameObject FindPrefabByKeywords(string exactName, string[] keywords)
    {
        // Prefer exact prefab match first
        string[] exactGuids = AssetDatabase.FindAssets(exactName + " t:Prefab");
        foreach (string guid in exactGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && string.Equals(prefab.name, exactName, System.StringComparison.OrdinalIgnoreCase))
                return prefab;
        }
        return null;
    }

    private static void TryAutoAssignSprite(SpriteRenderer sr, string[] keywords)
    {
        if (sr == null) return;
        foreach (string kw in keywords)
        {
            string[] guids = AssetDatabase.FindAssets(kw + " t:Sprite");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                Sprite found = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (found != null)
                {
                    sr.sprite = found;
                    return;
                }
            }
        }
    }
}
#endif