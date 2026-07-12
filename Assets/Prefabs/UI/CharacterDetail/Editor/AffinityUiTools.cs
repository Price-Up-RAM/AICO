using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 친밀도(Affinity) UI 셋업 파이프라인 — CharacterDetail.prefab의 호감도(0/300, 3단 바) 블록을
// 친밀도(Lv.N n/100, 무지개 단일 게이지) 블록으로 변환하고, 보상 수령 모달을 베이크한다.
// 계획: Affinity_Plan.md / 작업 이력: WORKLOG.md
public static class AffinityUiTools
{
    private const string PrefabPath = "Assets/Prefabs/UI/CharacterDetail/CharacterDetail.prefab";
    private const string FontPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string JpFontPath = "Assets/FontAssets/NotoSansJP-Regular SDF.asset"; // 일본어 글리프 폴백용
    private const string SpritesDir = "Assets/Prefabs/UI/CharacterDetail/Sprites";
    private const string RainbowPath = SpritesDir + "/AffinityRainbow.png";
    private const string WhitePath = SpritesDir + "/AffinityBarWhite.png";

    // ChangeChar 카드(3중 테두리) 대상 프리팹 — 실앱이 쓰는 Root는 Root260616 하나뿐 (Root260607은 고아 백업본)
    private const string CardPrefabPath = "Assets/Migration/Root260616.prefab";
    private const string FrameSpritesDir = "Assets/Layer Lab/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/Frame/Frame_Custom";
    private const string CardBorderSpritePath = FrameSpritesDir + "/ItemFrame01_White4.png"; // 공통 테두리 — 굵은 화이트 링(내부 투명, 9-slice)
    private const string CardBorderSubSpritePath = FrameSpritesDir + "/ItemFrame01_White2.png"; // 보조 테두리 — 얇은 라인(동/은/금 틴트용)

    private static readonly Color FillYellow = new Color(1f, 0.827f, 0.357f, 1f); // 구 호감도 Yellow 게이지 색
    private static readonly Color GoldYellow = new Color(0.95f, 0.78f, 0.30f, 1f);

    private static readonly Color PanelBg = new Color(0.113f, 0.125f, 0.153f, 0.98f);
    private static readonly Color ButtonBg = new Color(0.22f, 0.25f, 0.31f, 1f);
    private static readonly Color BackdropDim = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);
    private static readonly Color TrackDark = new Color(0.047f, 0.055f, 0.071f, 0.9f);

    [MenuItem("Tools/CharacterDetail/Setup All (rainbow + affinity UI + font + card border + jp fallback)")]
    public static void SetupAll()
    {
        BakeRainbowSprite();
        ConvertAffinityUi();
        ApplyFont();
        InjectCardBorder();
        EnsureJpFontFallback();
        AssetDatabase.SaveAssets();
        Debug.Log("[CharacterDetail][AffinityUiTools] Setup All 완료.");
    }

    // batchmode -executeMethod 진입점 (다이얼로그 없음)
    public static void BatchBuildAll()
    {
        SetupAll();
    }

    // ── 1) 무지개 그라데이션 스프라이트 베이크 (StoreTools EnsureNoImageSprite 레시피) ──
    [MenuItem("Tools/CharacterDetail/1. Bake Rainbow Sprite")]
    public static void BakeRainbowSprite()
    {
        EnsureRainbowSprite();
        EnsureWhiteSprite();
    }

    // Filled 타입은 sprite가 있어야 클리핑이 동작 — 단색 게이지용 화이트 스프라이트
    private static Sprite EnsureWhiteSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(SpritesDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/UI/CharacterDetail", "Sprites");
        }

        const int size = 8;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();

        WriteAndImportSpritePng(tex, WhitePath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePath);
    }

    private static Sprite EnsureRainbowSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(RainbowPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(SpritesDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/UI/CharacterDetail", "Sprites");
        }

        const int width = 256;
        const int height = 16;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int x = 0; x < width; x++)
        {
            // 좌(빨강 hue 0) → 우(보라 hue 0.8) 무지개 스윕. Filled 이미지라 진행할수록 색이 드러난다.
            float t = x / (float)(width - 1);
            Color color = Color.HSVToRGB(t * 0.8f, 0.85f, 1f);
            for (int y = 0; y < height; y++)
            {
                tex.SetPixel(x, y, color);
            }
        }
        tex.Apply();

        WriteAndImportSpritePng(tex, RainbowPath);
        Debug.Log("[CharacterDetail][AffinityUiTools] 무지개 스프라이트 베이크: " + RainbowPath);
        return AssetDatabase.LoadAssetAtPath<Sprite>(RainbowPath);
    }

    // 텍스처를 PNG로 저장하고 Single 스프라이트로 임포트 (StoreTools.WriteAndImportSpritePng 레시피)
    private static void WriteAndImportSpritePng(Texture2D tex, string assetPath)
    {
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    // ── 2) 프리팹 변환: 호감도 블록 → 친밀도 블록 + 보상 모달 베이크 ──
    [MenuItem("Tools/CharacterDetail/2. Convert Affinity UI (prefab)")]
    public static void ConvertAffinityUi()
    {
        Sprite rainbow = EnsureRainbowSprite();
        Sprite white = EnsureWhiteSprite();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (rainbow == null || white == null || font == null)
        {
            Debug.LogError("[CharacterDetail][AffinityUiTools] 게이지 스프라이트 또는 SUIT-Bold 폰트를 찾지 못했습니다.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform container = FindDeep(root.transform, "AffinityContainer") ?? FindDeep(root.transform, "AffectionContainer");
            if (container == null)
            {
                Debug.LogError("[CharacterDetail][AffinityUiTools] AffectionContainer/AffinityContainer를 찾지 못했습니다.");
                return;
            }

            // 2-1. 리네임 + 텍스트 교체
            container.name = "AffinityContainer";

            // 가로 배치: [AffinityLevelText "Lv.00"] [AffinityValueText "0/100"]
            Transform valueText = FindDeep(container, "AffinityValueText") ?? FindDeep(container, "AffectionValueText");
            if (valueText != null)
            {
                valueText.name = "AffinityValueText";
                RectTransform valueRect = valueText as RectTransform;
                SetTopLeft(valueRect, new Vector2(84f, -10f), new Vector2(120f, 26f));
                TMP_Text tmp = valueText.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = "0/100";
                    tmp.fontSize = 16f;
                }
            }

            Transform levelText = FindDeep(container, "AffinityLevelText");
            if (levelText == null)
            {
                TextMeshProUGUI created = CreateText("AffinityLevelText", container, "Lv.00", 20, GoldYellow, font, TextAlignmentOptions.MidlineLeft, true);
                SetTopLeft(created.rectTransform, new Vector2(16f, -10f), new Vector2(64f, 26f));
                created.transform.SetSiblingIndex(0);
                levelText = created.transform;
            }

            Transform labelText = FindDeep(container, "AffinityLabelText") ?? FindDeep(container, "AffectionLabelText");
            if (labelText != null)
            {
                labelText.name = "AffinityLabelText";
                TMP_Text tmp = labelText.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = "낯선 사이"; // 색상은 현행(핑크) 유지
            }

            Transform barBackground = FindDeep(container, "AffinityBarBackground") ?? FindDeep(container, "AffectionBarBackground");
            Image fillImage = null;
            Image fillMaxImage = null;
            if (barBackground != null)
            {
                barBackground.name = "AffinityBarBackground";

                // 2-2. 3단 fill 제거 → 평시 연노랑 + MAX 무지개 2중 Filled 게이지
                //      트랙(배경)이 테두리로 보이도록 fill을 2px 인셋
                DestroyChildIfExists(barBackground, "AffectionBarFillYellow");
                DestroyChildIfExists(barBackground, "AffectionBarFillOrange");
                DestroyChildIfExists(barBackground, "AffectionBarFillRed");
                DestroyChildIfExists(barBackground, "AffinityBarFill"); // 재실행 대비 재생성
                DestroyChildIfExists(barBackground, "AffinityBarFillMax");

                fillImage = CreateBarFill("AffinityBarFill", barBackground, white, FillYellow);
                fillMaxImage = CreateBarFill("AffinityBarFillMax", barBackground, rainbow, Color.white);
                fillMaxImage.fillAmount = 1f;
                fillMaxImage.gameObject.SetActive(false); // MAX에서만 컨트롤러가 켠다
            }

            // 2-3. 컨테이너 클릭 → 보상 모달 (Button, 배경 Image가 이미 raycastTarget=true)
            Button affinityButton = container.GetComponent<Button>();
            if (affinityButton == null) affinityButton = container.gameObject.AddComponent<Button>();
            affinityButton.targetGraphic = container.GetComponent<Image>();
            affinityButton.transition = Selectable.Transition.ColorTint;

            // 2-4. FeatureTag 구 표기 → 친밀도 (베이크 기본값 — 런타임 표시는 컨트롤러가 치환)
            //      폴백 체인: 신명칭 우선, 구명칭 2종(호감도/인연도)은 리베이크 멱등성을 위해 후순위 유지
            Transform featureTag = FindDeep(root.transform, "FeatureTag_친밀도보유")
                ?? FindDeep(root.transform, "FeatureTag_호감도보유")
                ?? FindDeep(root.transform, "FeatureTag_인연도보유");
            if (featureTag != null)
            {
                featureTag.name = "FeatureTag_친밀도보유";
                TMP_Text tagText = featureTag.GetComponentInChildren<TMP_Text>(true);
                if (tagText != null)
                {
                    tagText.gameObject.name = "FeatureTag_친밀도보유_Text";
                    tagText.text = "친밀도 보유";
                }
            }

            // 2-4b. 볼 태그 오타 교정 — 코드 표준 어휘는 "볼당기기" (CheekPullHandler.requiredFeatureTag)
            Transform cheekTag = FindDeep(root.transform, "FeatureTag_볼당기기") ?? FindDeep(root.transform, "FeatureTag_볼땡기기");
            if (cheekTag != null)
            {
                cheekTag.name = "FeatureTag_볼당기기";
                TMP_Text cheekText = cheekTag.GetComponentInChildren<TMP_Text>(true);
                if (cheekText != null)
                {
                    cheekText.gameObject.name = "FeatureTag_볼당기기_Text";
                    cheekText.text = "볼당기기";
                }
            }

            // 2-5. 보상 수령 모달 베이크 (최상단 형제 — 모든 UI 위에 그려짐, 기본 숨김)
            Transform oldModal = FindDeep(root.transform, "AffinityRewardModal");
            if (oldModal != null) Object.DestroyImmediate(oldModal.gameObject);
            AffinityRewardModalView modal = BuildRewardModal(root.transform, font);

            // 2-6. 컨트롤러 직렬화 참조 갱신
            CharacterDetailController controller = root.GetComponent<CharacterDetailController>();
            if (controller != null)
            {
                SerializedObject so = new SerializedObject(controller);
                SetRef(so, "affinityLevelText", levelText != null ? levelText.GetComponent<TMP_Text>() : null);
                SetRef(so, "affinityValueText", valueText != null ? valueText.GetComponent<TMP_Text>() : null);
                SetRef(so, "affinityLabelText", labelText != null ? labelText.GetComponent<TMP_Text>() : null);
                SetRef(so, "affinityBarFill", fillImage);
                SetRef(so, "affinityBarFillMax", fillMaxImage);
                SetRef(so, "affinityButton", affinityButton);
                SetRef(so, "affinityRewardModal", modal);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[CharacterDetail][AffinityUiTools] 프리팹 변환 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static AffinityRewardModalView BuildRewardModal(Transform prefabRoot, TMP_FontAsset font)
    {
        GameObject modalRoot = CreateUIObject("AffinityRewardModal", prefabRoot);
        StretchFull(modalRoot.GetComponent<RectTransform>());
        CanvasGroup canvasGroup = modalRoot.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // 백드롭 — 클릭 시 닫힘(코드 바인딩), 뒤 UI 입력 차단
        GameObject backdrop = CreateUIObject("AffinityModalBackdrop", modalRoot.transform);
        StretchFull(backdrop.GetComponent<RectTransform>());
        Image backdropImage = backdrop.AddComponent<Image>();
        backdropImage.color = BackdropDim;
        Button backdropButton = backdrop.AddComponent<Button>();
        backdropButton.targetGraphic = backdropImage;
        backdropButton.transition = Selectable.Transition.None;

        // 중앙 패널
        GameObject panel = CreateUIObject("AffinityModalPanel", modalRoot.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(400f, 470f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = BuiltinUISprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = PanelBg;

        TextMeshProUGUI title = CreateText("AffinityModalTitleText", panel.transform, "친밀도 보상", 20, TextWhite, font, TextAlignmentOptions.MidlineLeft, true);
        SetTopLeft(title.rectTransform, new Vector2(16f, -12f), new Vector2(220f, 28f));

        // 닫기 버튼
        GameObject close = CreateUIObject("AffinityModalCloseButton", panel.transform);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-10f, -10f);
        closeRect.sizeDelta = new Vector2(28f, 28f);
        Image closeImage = close.AddComponent<Image>();
        closeImage.sprite = BuiltinUISprite();
        closeImage.type = Image.Type.Sliced;
        closeImage.color = ButtonBg;
        Button closeButton = close.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        TextMeshProUGUI closeText = CreateText("AffinityModalCloseButton_Text", close.transform, "X", 14, TextWhite, font, TextAlignmentOptions.Midline, true);
        StretchFull(closeText.rectTransform);

        // 헤더 우측(X 왼쪽) — 도달한 미수령 보상 일괄 수령
        GameObject claimAll = CreateUIObject("AffinityClaimAllButton", panel.transform);
        RectTransform claimAllRect = claimAll.GetComponent<RectTransform>();
        claimAllRect.anchorMin = claimAllRect.anchorMax = new Vector2(1f, 1f);
        claimAllRect.pivot = new Vector2(1f, 1f);
        claimAllRect.anchoredPosition = new Vector2(-46f, -10f);
        claimAllRect.sizeDelta = new Vector2(84f, 28f);
        Image claimAllImage = claimAll.AddComponent<Image>();
        claimAllImage.sprite = BuiltinUISprite();
        claimAllImage.type = Image.Type.Sliced;
        claimAllImage.color = new Color(0.306f, 0.404f, 0.608f, 1f);
        Button claimAllButton = claimAll.AddComponent<Button>();
        claimAllButton.targetGraphic = claimAllImage;
        TextMeshProUGUI claimAllText = CreateText("AffinityClaimAllButton_Text", claimAll.transform, "전부 수령", 12, TextWhite, font, TextAlignmentOptions.Midline, true);
        StretchFull(claimAllText.rectTransform);

        TextMeshProUGUI summary = CreateText("AffinityModalSummaryText", panel.transform, "Lv.0 · 낯선 사이 · 0 / 1000", 14, TextMuted, font, TextAlignmentOptions.MidlineLeft, false);
        SetTopLeft(summary.rectTransform, new Vector2(16f, -46f), new Vector2(368f, 22f));

        // 스크롤 영역 (상단 76 ~ 하단 52)
        GameObject scrollObject = CreateUIObject("AffinityRewardScroll", panel.transform);
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.pivot = new Vector2(0.5f, 0.5f);
        scrollRect.offsetMin = new Vector2(12f, 52f);
        scrollRect.offsetMax = new Vector2(-12f, -76f);
        Image scrollBg = scrollObject.AddComponent<Image>();
        scrollBg.sprite = BuiltinUISprite();
        scrollBg.type = Image.Type.Sliced;
        scrollBg.color = TrackDark;

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        GameObject viewport = CreateUIObject("AffinityRewardViewport", scrollObject.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.pivot = new Vector2(0.5f, 0.5f);
        viewportRect.offsetMin = new Vector2(6f, 6f);
        viewportRect.offsetMax = new Vector2(-20f, -6f); // 우측 스크롤바 레인
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUIObject("AffinityRewardContent", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 우측 스크롤바 레인 (Mission 관용구)
        GameObject sb = CreateUIObject("AffinityRewardScrollbar", scrollObject.transform);
        RectTransform sbRect = sb.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(1f, 0f);
        sbRect.anchorMax = new Vector2(1f, 1f);
        sbRect.pivot = new Vector2(1f, 0.5f);
        sbRect.sizeDelta = new Vector2(10f, -12f);
        sbRect.anchoredPosition = new Vector2(-4f, 0f);
        Image sbImage = sb.AddComponent<Image>();
        sbImage.color = new Color(0f, 0f, 0f, 0.3f);
        Scrollbar scrollbar = sb.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = CreateUIObject("Sliding Area", sb.transform);
        StretchFull(slidingArea.GetComponent<RectTransform>());
        GameObject handle = CreateUIObject("Handle", slidingArea.transform);
        StretchFull(handle.GetComponent<RectTransform>());
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = ButtonBg;
        scrollbar.handleRect = handle.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handleImage;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // 프로토타입 테스트용 버튼 2종 (포인트 획득 규칙 확정 시 제거): +40 / 친밀도 초기화
        CreateFooterButton(panel.transform, font, "AffinityDebugAddButton", "+40 (테스트)", new Vector2(12f, 12f));
        CreateFooterButton(panel.transform, font, "AffinityDebugResetButton", "초기화 (테스트)", new Vector2(130f, 12f));

        return modalRoot.AddComponent<AffinityRewardModalView>();
    }

    // ── 3) 폰트 일괄 적용 (베이크 후 필수 마지막 단계) ──
    [MenuItem("Tools/CharacterDetail/3. Apply SUIT-Bold Font")]
    public static void ApplyFont()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            Debug.LogError("[CharacterDetail][AffinityUiTools] SUIT-Bold 폰트를 찾지 못했습니다: " + FontPath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                text.font = font;
                EditorUtility.SetDirty(text);
            }
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[CharacterDetail][AffinityUiTools] SUIT-Bold 적용: " + texts.Length + "개 TMP_Text");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 4) ChangeChar 카드 3중 테두리 베이크 (Root260616.prefab) ──
    // 계약: 카드 루트 마지막 자식으로 CardBorderImage → CardBorderSubImage → CardBorderOriginalImage 순서 append,
    //       3개 전부 비활성 베이크. 활성/틴트 판정은 런타임 컨트롤러(ChangeCharCardController)가 담당.
    //       이 프리팹의 TMP 폰트는 건드리지 않는다(테두리는 Image뿐).
    [MenuItem("Tools/CharacterDetail/4. Inject Card Border (ChangeChar)")]
    public static void InjectCardBorder()
    {
        Sprite borderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardBorderSpritePath);
        Sprite subSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardBorderSubSpritePath);
        if (borderSprite == null || subSprite == null)
        {
            Debug.LogError("[CharacterDetail][AffinityUiTools] 카드 테두리 스프라이트를 찾지 못했습니다: "
                + CardBorderSpritePath + " / " + CardBorderSubSpritePath);
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CardPrefabPath);
        try
        {
            ChangeCharCardController[] controllers = root.GetComponentsInChildren<ChangeCharCardController>(true);
            if (controllers == null || controllers.Length == 0)
            {
                Debug.LogError("[CharacterDetail][AffinityUiTools] ChangeCharCardController를 찾지 못했습니다: " + CardPrefabPath);
                return;
            }

            // 미싱 스크립트(죽은 guid 컴포넌트)가 있으면 SaveAsPrefabAsset이 저장을 거부한다.
            // 런타임에 아무 동작도 하지 않는 직렬화 껍데기이므로 제거하되, 대상을 전부 로그로 남긴다.
            RemoveMissingScripts(root);

            foreach (ChangeCharCardController controller in controllers)
            {
                Transform card = controller.transform;

                // 재실행 대비 — 기존 테두리 노드 제거 후 재생성.
                // 직계 자식만 스캔 (FindDeep 재귀는 Icon 하위의 동명 노드를 오폭할 수 있음)
                DestroyDirectChildren(card, "CardBorderImage");
                DestroyDirectChildren(card, "CardBorderSubImage");
                DestroyDirectChildren(card, "CardBorderOriginalImage");

                // 계약 순서대로 마지막 자식으로 append
                Image borderImage = CreateCardBorderNode("CardBorderImage", card, borderSprite);         // 공통 테두리 (외부 이미지 기반)
                Image borderSubImage = CreateCardBorderNode("CardBorderSubImage", card, subSprite);      // 보조 테두리 (White + 동/은/금 틴트)
                Image borderOriginalImage = CreateCardBorderNode("CardBorderOriginalImage", card, null); // 전용 테두리 (전용 PNG용, 베이크 시 sprite=null)

                // 컨트롤러 직렬화 참조 연결
                SerializedObject so = new SerializedObject(controller);
                SetRef(so, "cardBorderImage", borderImage);
                SetRef(so, "cardBorderSubImage", borderSubImage);
                SetRef(so, "cardBorderOriginalImage", borderOriginalImage);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            bool saved;
            PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath, out saved);
            if (saved)
            {
                Debug.Log("[CharacterDetail][AffinityUiTools] 카드 테두리 베이크 완료: " + controllers.Length + "개 카드 / " + CardPrefabPath);
            }
            else
            {
                Debug.LogError("[CharacterDetail][AffinityUiTools] 카드 테두리 프리팹 저장 실패: " + CardPrefabPath
                    + " — 위 SaveAsPrefabAsset 에러 로그를 확인하세요 (프리팹에 저장 불가 컴포넌트가 있으면 거부됩니다).");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 5) SUIT-Bold → NotoSansJP 폴백 등록 (jp 문자열 □ 렌더 방지) ──
    // SUIT-Bold는 한글/라틴만 보유(일본어 글리프 0) → fallbackFontAssetTable에 NotoSansJP를 추가하면
    // SUIT-Bold를 쓰는 모든 UI가 일본어를 표시할 수 있다.
    // (NotoSansJP의 폴백에 SUIT-Bold가 이미 있어 상호 참조가 되지만, TMP는 방문 목록으로 순환을 방어한다.)
    [MenuItem("Tools/CharacterDetail/5. Ensure JP Font Fallback (SUIT→NotoJP)")]
    public static void EnsureJpFontFallback()
    {
        TMP_FontAsset suit = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TMP_FontAsset noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JpFontPath);
        if (suit == null || noto == null)
        {
            Debug.LogError("[CharacterDetail][AffinityUiTools] 폴백용 폰트 에셋을 찾지 못했습니다: "
                + FontPath + " / " + JpFontPath);
            return;
        }

        if (suit.fallbackFontAssetTable == null)
        {
            suit.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }

        if (suit.fallbackFontAssetTable.Contains(noto))
        {
            Debug.Log("[CharacterDetail][AffinityUiTools] SUIT-Bold 폴백에 NotoSansJP가 이미 등록되어 있습니다 — 스킵.");
            return;
        }

        suit.fallbackFontAssetTable.Add(noto);
        EditorUtility.SetDirty(suit);
        AssetDatabase.SaveAssets();
        Debug.Log("[CharacterDetail][AffinityUiTools] SUIT-Bold 폴백에 NotoSansJP 추가 완료 — jp 글리프 렌더 가능.");
    }

    // 프리팹 전체에서 미싱 스크립트 컴포넌트를 제거하고 대상 오브젝트 경로를 로그로 남긴다
    private static void RemoveMissingScripts(GameObject root)
    {
        int total = 0;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if (count > 0)
            {
                Debug.LogWarning("[CharacterDetail][AffinityUiTools] 미싱 스크립트 " + count + "개 제거: " + HierarchyPath(t));
                total += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            }
        }

        if (total > 0)
        {
            Debug.LogWarning("[CharacterDetail][AffinityUiTools] 미싱 스크립트 총 " + total + "개 제거 완료 (제거하지 않으면 프리팹 저장 불가).");
        }
    }

    private static string HierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    // 직계 자식 중 이름이 일치하는 노드를 전부 제거 (재귀 없음 — InjectCardBorder 전용 스코프)
    private static void DestroyDirectChildren(Transform parent, string childName)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) Object.DestroyImmediate(child.gameObject);
        }
    }

    // 풀스트레치 Sliced 테두리 이미지 노드 — 비활성으로 베이크, layer는 부모 따라감
    private static Image CreateCardBorderNode(string objectName, Transform card, Sprite sprite)
    {
        GameObject go = CreateUIObject(objectName, card);
        go.layer = card.gameObject.layer;
        StretchFull(go.GetComponent<RectTransform>());
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
        go.SetActive(false);
        return image;
    }

    // ── 헬퍼 ──────────────────────────────────────────────

    private static Sprite BuiltinUISprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }

    private static void CreateFooterButton(Transform panel, TMP_FontAsset font, string objectName, string label, Vector2 pos)
    {
        GameObject go = CreateUIObject(objectName, panel);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(110f, 28f);
        Image image = go.AddComponent<Image>();
        image.sprite = BuiltinUISprite();
        image.type = Image.Type.Sliced;
        image.color = ButtonBg;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = CreateText(objectName + "_Text", go.transform, label, 12, TextMuted, font, TextAlignmentOptions.Midline, false);
        StretchFull(text.rectTransform);
    }

    // 트랙 안쪽 2px 인셋 Filled 게이지 (테두리는 트랙 배경이 담당)
    private static Image CreateBarFill(string objectName, Transform barBackground, Sprite sprite, Color color)
    {
        GameObject fill = CreateUIObject(objectName, barBackground);
        RectTransform rect = fill.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(2f, 2f);
        rect.offsetMax = new Vector2(-2f, -2f);
        Image image = fill.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 0f;
        image.raycastTarget = false;
        return image;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = 5; // UI
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align, bool bold)
    {
        GameObject go = CreateUIObject(objectName, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.font = font;
        if (bold) text.fontStyle = FontStyles.Bold;
        return text;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform child = FindDeep(parent, childName);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    private static void SetRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null) prop.objectReferenceValue = value;
        else Debug.LogWarning("[CharacterDetail][AffinityUiTools] 직렬화 필드 없음: " + propertyName);
    }

    private static Transform FindDeep(Transform parent, string objectName)
    {
        if (parent == null) return null;
        if (parent.name == objectName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), objectName);
            if (found != null) return found;
        }
        return null;
    }
}
