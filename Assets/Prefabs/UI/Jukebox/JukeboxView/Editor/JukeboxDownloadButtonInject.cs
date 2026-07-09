#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// JukeboxView.prefab 헤더에 "Download" 버튼(유튜브 모양 아이콘 + 라벨)을 주입하고
/// downloaderPrefab 참조(JukeboxDownloader.prefab)를 연결한다.
///
/// JukeboxView.prefab은 베이크 후 손질된 구조라 재베이크하면 안 된다 —
/// 이 스크립트는 LoadPrefabContents로 열어 버튼만 추가/갱신한다 (재실행 안전).
/// 클릭 동작은 JukeboxView.WireStaticControls의 BindButton("DownloadButton", ToggleDownloader)이
/// 런타임에 연결하므로 직렬화된 onClick은 필요 없다.
///
/// 사용: Unity 메뉴 → Tools/Jukebox/Inject Download Button
/// </summary>
public static class JukeboxDownloadButtonInject
{
    private const string PrefabPath = "Assets/Prefabs/UI/Jukebox/JukeboxView/Prefabs/JukeboxView.prefab";
    private const string DownloaderPrefabPath = "Assets/Prefabs/UI/JukeboxDownloader/Prefabs/JukeboxDownloader.prefab";
    private const string IconDir = "Assets/Prefabs/UI/JukeboxDownloader/Sprites";
    private const string IconPath = IconDir + "/YoutubeIcon.png";

    [MenuItem("Tools/Jukebox/Inject Download Button")]
    public static void Inject()
    {
        Sprite icon = EnsureYoutubeIcon();

        GameObject downloaderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DownloaderPrefabPath);
        if (downloaderPrefab == null)
        {
            Debug.LogError("[Jukebox][Inject] JukeboxDownloader 프리팹이 없습니다: " + DownloaderPrefabPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform header = FindDeep(root.transform, "Header");
            Transform env = header != null ? header.Find("EnvButton") : null;
            if (header == null || env == null)
            {
                Debug.LogError("[Jukebox][Inject] Header/EnvButton을 찾지 못했습니다.");
                return;
            }

            // 재실행 시 기존 버튼 제거 후 다시 만든다 (갱신 안전)
            Transform old = header.Find("DownloadButton");
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            CreateDownloadButton(header, env, icon);

            // downloaderPrefab 참조 연결
            JukeboxView view = root.GetComponent<JukeboxView>();
            if (view != null)
            {
                view.EditorSetDownloaderPrefab(downloaderPrefab);
                EditorUtility.SetDirty(view);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[Jukebox][Inject] Download 버튼 주입 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // EnvButton(SFX) 스타일을 복제해 [유튜브 아이콘 + Download] 버튼을 EnvButton 왼쪽에 만든다.
    private static void CreateDownloadButton(Transform header, Transform env, Sprite icon)
    {
        Image envImg = env.GetComponent<Image>();
        Button envBtn = env.GetComponent<Button>();
        LayoutElement envLe = env.GetComponent<LayoutElement>();
        TextMeshProUGUI envText = env.GetComponentInChildren<TextMeshProUGUI>(true);

        GameObject go = new GameObject("DownloadButton", typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(header, false);
        go.transform.SetSiblingIndex(env.GetSiblingIndex()); // SFX 버튼 바로 왼쪽

        Image img = go.AddComponent<Image>();
        if (envImg != null)
        {
            img.sprite = envImg.sprite;
            img.type = envImg.type;
            img.color = envImg.color;
            img.pixelsPerUnitMultiplier = envImg.pixelsPerUnitMultiplier;
        }

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        if (envBtn != null)
        {
            btn.colors = envBtn.colors;
        }

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 104f;
        le.minWidth = 104f;
        le.preferredHeight = envLe != null ? envLe.preferredHeight : 30f;
        le.minHeight = envLe != null ? envLe.minHeight : 30f;

        // 유튜브 아이콘 (좌측)
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.layer = 5;
        iconGo.transform.SetParent(go.transform, false);
        RectTransform iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.anchoredPosition = new Vector2(8f, 0f);
        iconRect.sizeDelta = new Vector2(20f, 14f);
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.sprite = icon;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // "Download" 라벨 (아이콘 오른쪽)
        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.layer = 5;
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(32f, 0f);
        textRect.offsetMax = new Vector2(-4f, 0f);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "Download";
        text.fontSize = 12f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        if (envText != null)
        {
            text.font = envText.font;   // SFX 버튼과 동일 폰트(SUIT-Bold)
            text.color = envText.color;
        }
    }

    // 유튜브 모양 아이콘(빨간 둥근 사각형 + 흰 재생 삼각형)을 코드로 생성해 스프라이트로 임포트.
    private static Sprite EnsureYoutubeIcon()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        if (existing != null)
        {
            return existing;
        }

        string absDir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, IconDir);
        if (!Directory.Exists(absDir))
        {
            Directory.CreateDirectory(absDir);
        }

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color red = new Color(1f, 0.15f, 0.15f, 1f);
        Color clear = new Color(0f, 0f, 0f, 0f);
        // 몸체: 64×44 둥근 사각형(라운드 12), 세로 중앙
        float cx = 32f, cy = 32f, halfW = 32f, halfH = 22f, radius = 12f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - cx) - (halfW - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - cy) - (halfH - radius), 0f);
                bool inBody = dx * dx + dy * dy <= radius * radius;
                bool inTriangle = false;
                if (inBody && x >= 25 && x <= 45)
                {
                    float t = (45f - x) / 20f;      // 1(왼쪽 변) → 0(오른쪽 꼭짓점)
                    inTriangle = Mathf.Abs(y - 32f) <= t * 11f;
                }
                tex.SetPixel(x, y, inTriangle ? Color.white : (inBody ? red : clear));
            }
        }
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, IconPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(IconPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
            Transform found = FindDeep(child, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}
#endif
