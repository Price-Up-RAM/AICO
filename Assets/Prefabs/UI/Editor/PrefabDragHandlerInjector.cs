#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 기존 프리팹을 재생성하지 않고 JukeboxView 규격의 드래그 영역만 추가/보정한다.
[InitializeOnLoad]
public static class PrefabDragHandlerInjector
{
    private const string SquareSpritePath = "Assets/Sprites/Square.png";
    private static bool conformanceCheckQueued;

    private static readonly string[] MemoryPrefabPaths =
    {
        "Assets/Prefabs/UI/MemoryUser/Prefabs/MemoryUserView.prefab",
        "Assets/Prefabs/UI/MemoryUser/Prefabs/MemoryUserLearningConfirmView.prefab",
        "Assets/Prefabs/UI/MemoryArchive/Prefabs/MemoryArchiveView.prefab"
    };

    static PrefabDragHandlerInjector()
    {
        if (!conformanceCheckQueued)
        {
            conformanceCheckQueued = true;
            EditorApplication.delayCall += ApplyMissingHandlers;
        }
    }

    public static void ApplyToMemoryPrefabs()
    {
        Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        if (squareSprite == null)
        {
            Debug.LogError("[UI Handler] Square 스프라이트를 찾지 못했습니다: " + SquareSpritePath);
            return;
        }

        foreach (string prefabPath in MemoryPrefabPaths)
        {
            ApplyToPrefab(prefabPath, squareSprite);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void RepairMissingHandlers()
    {
        ApplyMissingHandlers();
    }

    private static void ApplyMissingHandlers()
    {
        Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        if (squareSprite == null)
        {
            return;
        }

        bool changed = false;
        foreach (string prefabPath in MemoryPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || HasRequiredHandlers(prefab))
            {
                continue;
            }

            ApplyToPrefab(prefabPath, squareSprite);
            changed = true;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static bool HasRequiredHandlers(GameObject prefab)
    {
        Transform handler = prefab.transform.Find("Handler");
        Transform header = prefab.transform.Find("Header");
        if (handler == null || header == null)
        {
            return false;
        }

        RectTransform handlerRect = handler as RectTransform;
        LayoutElement handlerLayout = handler.GetComponent<LayoutElement>();
        return handler.GetSiblingIndex() == 0 &&
               handlerRect != null &&
               handlerRect.anchorMin == Vector2.zero &&
               handlerRect.anchorMax == Vector2.one &&
               handlerRect.offsetMin == Vector2.zero &&
               handlerRect.offsetMax == Vector2.zero &&
               handler.GetComponent<Image>() != null &&
               handler.GetComponent<DragUIHandler>() != null &&
               handlerLayout != null &&
               handlerLayout.ignoreLayout &&
               header.GetComponent<Image>() != null &&
               header.GetComponent<DragUIHandler>() != null;
    }

    public static void ApplyToPrefab(string prefabPath)
    {
        Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        if (squareSprite == null)
        {
            Debug.LogError("[UI Handler] Square 스프라이트를 찾지 못했습니다: " + SquareSpritePath);
            return;
        }

        ApplyToPrefab(prefabPath, squareSprite);
    }

    public static void EnsureOnRoot(GameObject root)
    {
        Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        if (squareSprite == null)
        {
            Debug.LogError("[UI Handler] Square 스프라이트를 찾지 못했습니다: " + SquareSpritePath);
            return;
        }

        EnsureOnRoot(root, squareSprite);
    }

    private static void ApplyToPrefab(string prefabPath, Sprite squareSprite)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            Debug.LogError("[UI Handler] 프리팹을 열지 못했습니다: " + prefabPath);
            return;
        }

        try
        {
            EnsureOnRoot(root, squareSprite);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[UI Handler] 기존 프리팹을 보존하며 드래그 영역 적용: " + prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureOnRoot(GameObject root, Sprite squareSprite)
    {
        if (root == null)
        {
            return;
        }

        Transform handlerTransform = root.transform.Find("Handler");
        GameObject handler;
        if (handlerTransform == null)
        {
            handler = new GameObject("Handler", typeof(RectTransform));
            handler.layer = root.layer;
            handler.transform.SetParent(root.transform, false);
        }
        else
        {
            handler = handlerTransform.gameObject;
        }

        handler.layer = root.layer;
        handler.transform.SetSiblingIndex(0);
        RectTransform handlerRect = handler.GetComponent<RectTransform>();
        handlerRect.anchorMin = Vector2.zero;
        handlerRect.anchorMax = Vector2.one;
        handlerRect.pivot = new Vector2(0.5f, 0.5f);
        handlerRect.anchoredPosition = Vector2.zero;
        handlerRect.sizeDelta = Vector2.zero;
        handlerRect.offsetMin = Vector2.zero;
        handlerRect.offsetMax = Vector2.zero;

        Image handlerImage = handler.GetComponent<Image>();
        if (handlerImage == null)
        {
            handlerImage = handler.AddComponent<Image>();
        }
        ConfigureTransparentSquare(handlerImage, squareSprite);

        if (handler.GetComponent<DragUIHandler>() == null)
        {
            handler.AddComponent<DragUIHandler>();
        }

        LayoutElement handlerLayout = handler.GetComponent<LayoutElement>();
        if (handlerLayout == null)
        {
            handlerLayout = handler.AddComponent<LayoutElement>();
        }
        handlerLayout.ignoreLayout = true;

        Transform header = root.transform.Find("Header");
        if (header == null)
        {
            Debug.LogError("[UI Handler] 루트 직속 Header를 찾지 못했습니다: " + root.name);
            return;
        }

        Image headerImage = header.GetComponent<Image>();
        if (headerImage == null)
        {
            headerImage = header.gameObject.AddComponent<Image>();
            ConfigureTransparentSquare(headerImage, squareSprite);
        }
        else
        {
            headerImage.raycastTarget = true;
        }

        if (header.GetComponent<DragUIHandler>() == null)
        {
            header.gameObject.AddComponent<DragUIHandler>();
        }

        EditorUtility.SetDirty(handler);
        EditorUtility.SetDirty(header.gameObject);
        EditorUtility.SetDirty(root);
    }

    private static void ConfigureTransparentSquare(Image image, Sprite squareSprite)
    {
        image.sprite = squareSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
    }
}
#endif
