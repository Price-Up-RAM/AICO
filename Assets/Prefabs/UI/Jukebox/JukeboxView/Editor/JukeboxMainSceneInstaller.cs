#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class JukeboxMainSceneInstaller
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string JukeboxPrefabPath = "Assets/Prefabs/UI/Jukebox/JukeboxView/Prefabs/JukeboxView.prefab";
    private const string BgmDir = "Assets/Audio/BGM";

    [MenuItem("Tools/Jukebox/Apply To MainScene")]
    public static void ApplyToMainScene()
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        UIManager uiManager = FindSceneObject<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("[Jukebox] UIManager not found in MainScene.");
            return;
        }

        Canvas targetCanvas = ResolveCanvas();
        if (targetCanvas == null)
        {
            Debug.LogError("[Jukebox] Canvas not found in MainScene.");
            return;
        }

        GameObject jukeboxView = EnsureJukeboxView(targetCanvas.transform);
        AssignJukeboxToUIManager(uiManager, jukeboxView);
        EnsureMrJukebox();

        if (jukeboxView != null)
        {
            Selection.activeGameObject = jukeboxView;
            EditorGUIUtility.PingObject(jukeboxView);
            Debug.Log("[Jukebox] JukeboxView placed at: " + GetHierarchyPath(jukeboxView.transform));
        }
        else
        {
            Debug.LogError("[Jukebox] JukeboxView was not created. Check the prefab path: " + JukeboxPrefabPath);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Jukebox] MainScene Jukebox install complete.");
    }

    private static GameObject EnsureJukeboxView(Transform parent)
    {
        JukeboxView existing = FindSceneObject<JukeboxView>();
        if (existing != null)
        {
            existing.transform.SetParent(parent, false);
            ConfigureRect(existing.GetComponent<RectTransform>());
            existing.gameObject.SetActive(false);
            return existing.gameObject;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JukeboxPrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Jukebox] JukeboxView prefab not found: " + JukeboxPrefabPath);
            return null;
        }

        GameObject created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        if (created == null)
        {
            created = UnityEngine.Object.Instantiate(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(created, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        }

        created.name = "JukeboxView";
        ConfigureRect(created.GetComponent<RectTransform>());
        created.SetActive(false);
        return created;
    }

    private static void ConfigureRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-360f, 0f);
        rect.localScale = Vector3.one;
    }

    private static void AssignJukeboxToUIManager(UIManager uiManager, GameObject jukeboxView)
    {
        if (uiManager == null || jukeboxView == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(uiManager);
        SerializedProperty prop = so.FindProperty("jukebox");
        if (prop != null)
        {
            prop.objectReferenceValue = jukeboxView;
            so.ApplyModifiedProperties();
        }
    }

    private static void EnsureMrJukebox()
    {
        MRJukebox mr = FindSceneObject<MRJukebox>();
        if (mr == null)
        {
            GameObject go = new GameObject("MRJukebox");
            go.AddComponent<AudioSource>();
            mr = go.AddComponent<MRJukebox>();
        }

        SerializedObject so = new SerializedObject(mr);
        SerializedProperty playOnAwake = so.FindProperty("playOnAwake");
        if (playOnAwake != null)
        {
            playOnAwake.boolValue = false;
        }

        SerializedProperty playMode = so.FindProperty("playMode");
        if (playMode != null)
        {
            playMode.enumValueIndex = (int)JukeboxPlayMode.LoopAll;
        }

        SerializedProperty playlist = so.FindProperty("playlist");
        if (playlist == null)
        {
            so.ApplyModifiedProperties();
            return;
        }

        string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { BgmDir });
        Array.Sort(clipGuids, (a, b) => string.Compare(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b), StringComparison.OrdinalIgnoreCase));

        playlist.ClearArray();
        for (int i = 0; i < clipGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                continue;
            }

            int index = playlist.arraySize;
            playlist.InsertArrayElementAtIndex(index);
            SerializedProperty item = playlist.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("clip").objectReferenceValue = clip;
            item.FindPropertyRelative("trackName").stringValue = Path.GetFileNameWithoutExtension(path);

            SerializedProperty tags = item.FindPropertyRelative("tags");
            tags.ClearArray();
            foreach (string tag in TagsForTrack(path))
            {
                int tagIndex = tags.arraySize;
                tags.InsertArrayElementAtIndex(tagIndex);
                tags.GetArrayElementAtIndex(tagIndex).stringValue = tag;
            }
        }

        so.ApplyModifiedProperties();
    }

    private static IEnumerable<string> TagsForTrack(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

        yield return "bgm";
        if (name.Contains("beach")) yield return "beach";
        if (name.Contains("cafe") || name.Contains("coffee")) yield return "cafe";
        if (name.Contains("crowd")) yield return "crowd";
        if (name.Contains("firecrack")) yield return "firecrack";
        if (name.Contains("rain")) yield return "rain";
        if (name.Contains("saxophone")) yield return "saxophone";
    }

    private static Canvas ResolveCanvas()
    {
        CanvasManager canvasManager = FindSceneObject<CanvasManager>();
        if (canvasManager != null && canvasManager.canvasUI != null)
        {
            return canvasManager.canvasUI;
        }

        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.scene.IsValid() && canvas.gameObject.name == "Canvas")
            {
                return canvas;
            }
        }

        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.scene.IsValid())
            {
                return canvas;
            }
        }

        return null;
    }

    private static T FindSceneObject<T>() where T : UnityEngine.Object
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        foreach (T obj in objects)
        {
            Component component = obj as Component;
            GameObject go = obj as GameObject;
            GameObject owner = component != null ? component.gameObject : go;
            if (owner != null && owner.scene.IsValid())
            {
                return obj;
            }
        }

        return null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
#endif
