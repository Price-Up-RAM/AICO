using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 친밀도 레벨 보상 수령 모달 (프로토타입).
// - 계층은 에디터 툴(AffinityUiTools)이 CharacterDetail.prefab 안에 베이크하고, Awake의 BindExisting이
//   이름 계약으로 재연결한다 (StoreConfirmView 패턴). 표시/숨김은 CanvasGroup만 조작(SetActive 금지).
// - 레벨 행(Row)은 열 때마다 코드로 생성한다(SkillView 방식) — 폰트는 베이크된 타이틀에서 캡처.
// - 보상 정의는 AffinityData.RewardsFor(타입 확장 모델), 수령/해금 저장은 SettingCharManager,
//   지급은 GrantRewards 라우터(Currency→CurrencyManager, Item→ItemSystemManager, Border/Title→해금 기록).
public class AffinityRewardModalView : MonoBehaviour
{
    private static readonly Color RowBg = new Color(0.176f, 0.196f, 0.243f, 1f);
    private static readonly Color RowBgLocked = new Color(0.137f, 0.153f, 0.188f, 1f);
    private static readonly Color AccentBlue = new Color(0.306f, 0.404f, 0.608f, 1f);
    private static readonly Color TextWhite = new Color(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted = new Color(0.6f, 0.62f, 0.66f, 1f);
    private static readonly Color GoldYellow = new Color(0.95f, 0.78f, 0.30f, 1f);

    private CanvasGroup canvasGroup;
    private Button backdropButton;
    private Button closeButton;
    private Button claimAllButton;
    private Button debugAddButton;
    private Button debugResetButton;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI summaryText;
    private RectTransform rewardContent;

    private string currentCharacterId;
    private bool bound;

    // UI 언어 번역 헬퍼 — 매니저 부재 시 원문 반환 (LanguageData.Translate는 미등록 문자열이면 원문 반환)
    private static string T(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (LanguageManager.Instance == null || SettingManager.Instance == null || SettingManager.Instance.settings == null) return text;
        // 레거시 settings.json에는 ui_language가 없을 수 있음 — null이면 Translate 내부 TryGetValue(null) 예외
        if (string.IsNullOrEmpty(SettingManager.Instance.settings.ui_language)) return text;
        return LanguageManager.Instance.Translate(text);
    }

    private void Awake()
    {
        BindExisting();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (backdropButton != null) backdropButton.onClick.RemoveListener(Close);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (claimAllButton != null) claimAllButton.onClick.RemoveListener(OnClaimAllClicked);
        if (debugAddButton != null) debugAddButton.onClick.RemoveListener(OnDebugAddClicked);
        if (debugResetButton != null) debugResetButton.onClick.RemoveListener(OnDebugResetClicked);
    }

    public void Open(string characterId)
    {
        currentCharacterId = characterId;
        BindExisting();
        RefreshRows();
        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void BindExisting()
    {
        if (bound) return;

        canvasGroup = GetComponent<CanvasGroup>();
        titleText = FindComponent<TextMeshProUGUI>("AffinityModalTitleText");
        summaryText = FindComponent<TextMeshProUGUI>("AffinityModalSummaryText");
        backdropButton = FindComponent<Button>("AffinityModalBackdrop");
        closeButton = FindComponent<Button>("AffinityModalCloseButton");
        claimAllButton = FindComponent<Button>("AffinityClaimAllButton");
        debugAddButton = FindComponent<Button>("AffinityDebugAddButton");
        debugResetButton = FindComponent<Button>("AffinityDebugResetButton");
        Transform content = FindDeepChild(transform, "AffinityRewardContent");
        rewardContent = content as RectTransform;

        if (rewardContent == null)
        {
            Debug.LogWarning("[CharacterDetail][AffinityModal] 베이크된 계층이 없습니다. Tools/CharacterDetail/Setup All을 실행하세요.");
            return;
        }

        if (backdropButton != null) backdropButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (claimAllButton != null) claimAllButton.onClick.AddListener(OnClaimAllClicked);
        if (debugAddButton != null) debugAddButton.onClick.AddListener(OnDebugAddClicked);
        if (debugResetButton != null) debugResetButton.onClick.AddListener(OnDebugResetClicked);
        bound = true;
    }

    // ── 행 구성 ──────────────────────────────────────────────

    private void RefreshRows()
    {
        if (rewardContent == null) return;

        for (int i = rewardContent.childCount - 1; i >= 0; i--)
        {
            Destroy(rewardContent.GetChild(i).gameObject);
        }

        int points = 0;
        var setting = SettingCharManager.Instance != null ? SettingCharManager.Instance.GetCharCodeSetting(currentCharacterId) : null;
        if (setting != null) points = setting.affinityPoints;

        int level = AffinityData.LevelFor(points);
        string levelLabel = level >= AffinityData.MaxLevel ? "Lv.MAX" : "Lv." + level.ToString("00");
        SetText(summaryText, levelLabel + " · " + T(AffinityData.StageNameFor(level)) + " · " + points + " / " + AffinityData.MaxPoints);

        for (int rewardLevel = 1; rewardLevel <= AffinityData.MaxLevel; rewardLevel++)
        {
            CreateRow(rewardLevel, level);
        }
    }

    private void CreateRow(int rewardLevel, int currentLevel)
    {
        bool reached = rewardLevel <= currentLevel;
        bool claimed = reached && SettingCharManager.Instance != null &&
                       SettingCharManager.Instance.IsAffinityRewardClaimed(currentCharacterId, rewardLevel);

        GameObject row = CreateUIObject("Row_Lv" + rewardLevel, rewardContent);
        Image bg = row.AddComponent<Image>();
        bg.color = reached ? RowBg : RowBgLocked;
        bg.raycastTarget = false;
        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;

        TextMeshProUGUI levelText = CreateText("LevelText", row.transform, "Lv." + rewardLevel.ToString("00"), 16,
            reached ? GoldYellow : TextMuted, TextAlignmentOptions.MidlineLeft);
        SetRect(levelText.rectTransform, new Vector2(12f, 0f), new Vector2(56f, 40f));

        TextMeshProUGUI descText = CreateText("DescText", row.transform, T(AffinityData.RewardDescFor(rewardLevel)), 14,
            reached ? TextWhite : TextMuted, TextAlignmentOptions.MidlineLeft);
        SetRect(descText.rectTransform, new Vector2(70f, 0f), new Vector2(170f, 40f));

        if (claimed)
        {
            TextMeshProUGUI done = CreateText("StateText", row.transform, T("수령 완료"), 13, TextMuted, TextAlignmentOptions.Midline);
            SetRectRight(done.rectTransform, new Vector2(-10f, 0f), new Vector2(76f, 26f));
        }
        else if (reached)
        {
            GameObject buttonObject = CreateUIObject("ClaimButton", row.transform);
            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = AccentBlue;
            SetRectRight(buttonObject.GetComponent<RectTransform>(), new Vector2(-10f, 0f), new Vector2(76f, 26f));

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            int captured = rewardLevel;
            button.onClick.AddListener(() => OnClaimClicked(captured));

            TextMeshProUGUI label = CreateText("ClaimButton_Text", buttonObject.transform, T("수령"), 13, TextWhite, TextAlignmentOptions.Midline);
            StretchFull(label.rectTransform);
        }
        else
        {
            TextMeshProUGUI lockedText = CreateText("StateText", row.transform, T("미도달"), 13, TextMuted, TextAlignmentOptions.Midline);
            SetRectRight(lockedText.rectTransform, new Vector2(-10f, 0f), new Vector2(76f, 26f));
        }
    }

    private void OnClaimClicked(int rewardLevel)
    {
        if (SettingCharManager.Instance == null) return;

        var setting = SettingCharManager.Instance.GetCharCodeSetting(currentCharacterId);
        if (setting == null || AffinityData.LevelFor(setting.affinityPoints) < rewardLevel) return;

        if (!SettingCharManager.Instance.ClaimAffinityReward(currentCharacterId, rewardLevel)) return;

        GrantRewards(AffinityData.RewardsFor(rewardLevel));

        Debug.Log("[CharacterDetail][AffinityModal] 보상 수령: char=" + currentCharacterId + " Lv." + rewardLevel +
                  " gold=" + AffinityData.RewardGoldFor(rewardLevel));
        RefreshRows();
    }

    // 도달한 레벨의 미수령 보상을 한 번에 수령
    private void OnClaimAllClicked()
    {
        if (SettingCharManager.Instance == null) return;

        var setting = SettingCharManager.Instance.GetCharCodeSetting(currentCharacterId);
        if (setting == null) return;

        int level = AffinityData.LevelFor(setting.affinityPoints);
        int claimedCount = 0;
        for (int rewardLevel = 1; rewardLevel <= level; rewardLevel++)
        {
            if (SettingCharManager.Instance.ClaimAffinityReward(currentCharacterId, rewardLevel))
            {
                GrantRewards(AffinityData.RewardsFor(rewardLevel));
                claimedCount++;
            }
        }

        if (claimedCount == 0) return;

        Debug.Log("[CharacterDetail][AffinityModal] 전부 수령: char=" + currentCharacterId + " levels=" + claimedCount);
        RefreshRows();
    }

    // 보상 지급 라우터 — 타입별 지급 경로.
    // Currency: CurrencyManager(골드 포함 전 재화 단일 지갑 — 미션 집계는 CurrencyChanged 구독) / Item: ItemSystemManager /
    // Border·Title: 캐릭터 단위 해금(settings_char.json의 affinityUnlockedIds).
    private void GrantRewards(System.Collections.Generic.List<AffinityRewardDef> rewards)
    {
        if (rewards == null) return;

        foreach (AffinityRewardDef def in rewards)
        {
            switch (def.type)
            {
                case AffinityRewardType.Currency:
                    // Earn은 실패할 수 있다(재화 카탈로그 부재/미등재 키) — 실패면 유실 경고
                    bool earned = CurrencyManager.Instance != null && CurrencyManager.Instance.Earn(def.id, def.amount);
                    if (!earned)
                    {
                        Debug.LogError("[CharacterDetail][AffinityModal] 재화 지급 실패(유실) — key=" + def.id + " amount=" + def.amount);
                    }
                    break;

                case AffinityRewardType.Item:
                    if (string.IsNullOrEmpty(def.id))
                    {
                        // 전용 장신구 키 미정 — 수령 이력을 남겨 카탈로그 확정 후 백필 가능하게 한다
                        if (SettingCharManager.Instance != null)
                        {
                            SettingCharManager.Instance.UnlockAffinityReward(currentCharacterId, AffinityData.PendingSignatureItemId);
                        }
                        Debug.Log("[CharacterDetail][AffinityModal] 아이템 보상 보류 — 키 미정 (전용 장신구 카탈로그 후속, pending 기록됨)");
                        break;
                    }
                    if (ItemSystemManager.Instance == null || !ItemSystemManager.Instance.GrantItem(def.id, def.amount))
                    {
                        Debug.LogError("[CharacterDetail][AffinityModal] 아이템 지급 실패(유실) — key=" + def.id + " amount=" + def.amount);
                    }
                    break;

                case AffinityRewardType.Border:
                case AffinityRewardType.Title:
                    if (SettingCharManager.Instance != null)
                    {
                        SettingCharManager.Instance.UnlockAffinityReward(currentCharacterId, def.id);
                    }
                    break;
            }
        }
    }

    // 프로토타입 테스트용 — 포인트 획득 규칙이 확정되면 제거한다.
    private void OnDebugAddClicked()
    {
        if (string.IsNullOrEmpty(currentCharacterId)) return;
        CharacterDetailStateManager.Instance.AddAffinityPoints(currentCharacterId, 40);
        RefreshRows();
    }

    // 프로토타입 테스트용 — 인연도 포인트만 0으로 (수령 상태는 유지)
    private void OnDebugResetClicked()
    {
        if (string.IsNullOrEmpty(currentCharacterId)) return;
        CharacterDetailStateManager.Instance.AddAffinityPoints(currentCharacterId, -AffinityData.MaxPoints);
        RefreshRows();
    }

    // ── UI 헬퍼 ──────────────────────────────────────────────

    private TMP_FontAsset RowFont()
    {
        return titleText != null ? titleText.font : null;
    }

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = CreateUIObject(objectName, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        TMP_FontAsset font = RowFont();
        if (font != null) text.font = font;
        return text;
    }

    // 좌상단 기준 배치 (행 높이 40 안에서 세로 중앙)
    private static void SetRect(RectTransform rect, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    // 우측 기준 배치
    private static void SetRectRight(RectTransform rect, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null) target.text = value;
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        Transform found = FindDeepChild(transform, objectName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private static Transform FindDeepChild(Transform parent, string objectName)
    {
        if (parent == null) return null;
        if (parent.name == objectName) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), objectName);
            if (found != null) return found;
        }
        return null;
    }
}
