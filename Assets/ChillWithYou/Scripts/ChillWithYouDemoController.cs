using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ChillWithYouSample 데모 전용 컨트롤러 — 캐릭터 교체 버튼 UI만 담당.
/// 데이터(캐릭터 목록)와 교체/재착석 로직은 ChillWithYouSampleManager(싱글톤)가 소유하고,
/// 이 컨트롤러는 그 목록으로 버튼을 동적 생성해 SwitchCharacter(index)를 호출할 뿐이다.
/// 목록이 바뀌면(OnCharactersChanged) 버튼을 다시 그린다 — 캐릭터 추가는 매니저 인스펙터/API로.
/// 착석 오프셋 튜닝 UI는 공용 SitSupport 패널(SitSupportScript)이 맡는다.
/// 참조는 씬 베이크 시(ChillWithYouSampleBuilder) 주입된다.
/// </summary>
public class ChillWithYouDemoController : MonoBehaviour
{
    [Header("UI (빌더 주입)")]
    public RectTransform panelRect;        // DemoCharPanel — 버튼 행 수에 맞춰 높이 조절
    public RectTransform buttonContainer;  // 버튼이 생성될 컨테이너
    public TMP_FontAsset buttonFont;       // SUIT-Bold

    // 빌더(SitSupportBuilder/ChillWithYouSampleBuilder)와 동일한 다크 테마 값
    private static readonly Color ButtonColor = new Color(0.18f, 0.26f, 0.4f, 1f);

    private const float PanelHeaderHeight = 42f;  // 타이틀 영역
    private const float RowHeight = 40f;
    private const int ButtonsPerRow = 3;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private void Start()
    {
        if (ChillWithYouSampleManager.Instance != null)
        {
            ChillWithYouSampleManager.Instance.OnCharactersChanged += RebuildButtons;
        }
        RebuildButtons();
    }

    private void OnDestroy()
    {
        if (ChillWithYouSampleManager.Instance != null)
        {
            ChillWithYouSampleManager.Instance.OnCharactersChanged -= RebuildButtons;
        }
    }

    /// <summary>매니저의 캐릭터 목록으로 버튼 전체 재생성 (3개/행, 패널 높이 자동).</summary>
    private void RebuildButtons()
    {
        foreach (GameObject go in spawnedButtons)
        {
            if (go != null) Destroy(go);
        }
        spawnedButtons.Clear();

        ChillWithYouSampleManager manager = ChillWithYouSampleManager.Instance;
        if (manager == null || buttonContainer == null) return;

        int count = manager.Count;
        for (int i = 0; i < count; i++)
        {
            int index = i; // 클로저 캡처용 복사
            Button button = CreateButton(manager.GetLabel(i),
                new Vector2(14f + (i % ButtonsPerRow) * 120f, -(i / ButtonsPerRow) * RowHeight));
            button.onClick.AddListener(() => manager.SwitchCharacter(index));
        }

        // 행 수에 맞춰 패널 높이 조절 (버튼 0개여도 1행 높이 확보)
        if (panelRect != null)
        {
            int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)ButtonsPerRow));
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, PanelHeaderHeight + rows * RowHeight + 8f);
        }
    }

    private Button CreateButton(string label, Vector2 pos)
    {
        GameObject go = new GameObject("CharButton_" + label, typeof(RectTransform));
        go.layer = buttonContainer.gameObject.layer;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(buttonContainer, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(112f, 34f);

        Image img = go.AddComponent<Image>();
        img.color = ButtonColor;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = img;

        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.layer = go.layer;
        RectTransform textRt = textGO.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        if (buttonFont != null) tmp.font = buttonFont;
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        spawnedButtons.Add(go);
        return button;
    }
}
