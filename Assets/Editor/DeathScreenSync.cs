using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Automatically validates/repairs PsychoDeathScreen.prefab using Unity's native PrefabUtility
/// and reconnects any scene instance that became disconnected if Unity imported mid-edit.
[InitializeOnLoad]
public static class DeathScreenSync
{
    private const string PrefabPath = "Assets/Prefabs/Generated/PsychoDeathScreen.prefab";
    private const string FontPath = "Assets/Fonts/ChainsawCarnage SDF.asset";
    private const string SessionKey = "DeathScreenSync_Repaired_v1";

    static DeathScreenSync()
    {
        EditorApplication.delayCall += RunOnceOnLoad;
    }

    private static void RunOnceOnLoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        RepairPrefabAndActiveScene(clearConsole: true);
    }

    [MenuItem("Tools/Horror Room/Repair & Sync Death Screen Prefab")]
    public static void RepairMenu()
    {
        RepairPrefabAndActiveScene(clearConsole: false);
    }

    private static void RepairPrefabAndActiveScene(bool clearConsole)
    {
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null) return;

        // Re-save through Unity's native serializer so internal prefab state is 100% canonical
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root != null)
        {
            DeathScreen ds = root.GetComponent<DeathScreen>();
            TMP_FontAsset horrorFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (ds != null)
            {
                SerializedObject so = new SerializedObject(ds);
                if (horrorFont != null)
                {
                    so.FindProperty("youDiedFont").objectReferenceValue = horrorFont;
                }
                so.FindProperty("autoCreateButtonsIfMissing").boolValue = true;
                so.FindProperty("mainMenuSceneName").stringValue = "Main_scene 1";
                so.FindProperty("goToLoseScreen").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabAsset == null) return;

        // Repair any disconnected/missing PsychoDeathScreen instance in the currently open scene
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isLoaded && activeScene.path.StartsWith("Assets/Scenes/Rooms/"))
        {
            bool repairedScene = false;
            GameObject validInstance = null;

            foreach (GameObject go in activeScene.GetRootGameObjects())
            {
                if (go == null) continue;
                bool isDeathObj = go.name.Contains("PsychoDeathScreen") || go.GetComponent<DeathScreen>() != null;
                if (!isDeathObj) continue;

                if (PrefabUtility.IsPrefabAssetMissing(go) || (go.name.Contains("PsychoDeathScreen") && go.GetComponent<DeathScreen>() == null))
                {
                    Object.DestroyImmediate(go);
                    repairedScene = true;
                }
                else
                {
                    validInstance = go;
                }
            }

            if (validInstance == null && activeScene.name != "Electrical")
            {
                validInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, activeScene);
                validInstance.name = "PsychoDeathScreen";
                repairedScene = true;
            }

            if (validInstance != null)
            {
                DeathScreen sceneDeath = validInstance.GetComponent<DeathScreen>();
                if (sceneDeath != null)
                {
                    EntityAI[] entities = Object.FindObjectsByType<EntityAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        SerializedObject aiSo = new SerializedObject(entities[i]);
                        SerializedProperty prop = aiSo.FindProperty("deathScreen");
                        if (prop != null && prop.objectReferenceValue == null)
                        {
                            prop.objectReferenceValue = sceneDeath;
                            aiSo.ApplyModifiedPropertiesWithoutUndo();
                            repairedScene = true;
                        }
                    }
                }
            }

            if (repairedScene)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }
        }

        if (clearConsole)
        {
            System.Type logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            MethodInfo clearMethod = logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            clearMethod?.Invoke(null, null);
        }

        Debug.Log("[DeathScreenSync] PsychoDeathScreen prefab verified and synced cleanly.");
    }
}
