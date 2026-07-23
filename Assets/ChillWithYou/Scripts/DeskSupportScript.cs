using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DeskSupport — 책상 자체를 꾸미는 패널 (SitSupport와 동일한 공용 프리팹 규약: 본편/데모 겸용).
/// 1) 머티리얼 스킨: DeskSupportData.skinMaterials 버튼으로 책상 소품 렌더러의 스킨 계열 머티리얼을 일괄 교체.
/// 2) 장식 슬롯: Desk_Set의 DecoSlot_XX 마커에 DeskSupportData.decorations 프리팹을 배치(슬롯 버튼 클릭 = 순환).
/// 상태는 DeskSupportData(deskId별)에 기록되고 [데이터 저장]으로 디스크 영속(에디터 전용).
/// 루트는 항상 활성(패널만 숨김) — Start 후 저장 상태를 자동 적용하므로 본편에서도 스킨/장식이 복원된다.
/// 대상 책상은 ChillModeManager.deskSetRoot에서 해석(매니저가 늦게 떠도 폴링으로 1회 초기화).
/// 본편 열기: menutrigger Dev → DeskSupport. 데모: 씬 베이크가 패널을 켠 채 설치.
/// UI 참조는 프리팹 베이크 시(DeskSupportBuilder) 주입된다.
/// </summary>
public class DeskSupportScript : MonoBehaviour
{
    public static DeskSupportScript Instance { get; private set; }

    [Header("패널")]
    public GameObject panel; // 표시/숨김 대상 (루트 캔버스는 항상 활성)

    [Header("데이터 (빌더 주입)")]
    public DeskSupportData data;

    [Header("UI (빌더 주입)")]
    public RectTransform panelRect;
    public RectTransform skinContainer;
    public RectTransform decoContainer;
    public Button saveButton;
    public TMP_FontAsset uiFont;

    private static readonly Color ButtonColor = new Color(0.18f, 0.26f, 0.4f, 1f);
    private static readonly Color ButtonSelectedColor = new Color(0.3f, 0.55f, 0.95f, 1f);

    private const float RowHeight = 40f;
    private const float DecoSectionTop = 144f; // 패널 상단에서 장식 행 시작까지
    private const string SlotNamePrefix = "DecoSlot_";

    private bool _initialized;                 // deskSetRoot 해석 + 저장 상태 적용 완료 여부
    private Transform[] _slotMarkers = new Transform[0];
    private GameObject[] _slotInstances = new GameObject[0];
    private readonly List<GameObject> _skinButtons = new List<GameObject>();
    private readonly List<TMP_Text> _slotButtonLabels = new List<TMP_Text>();
    private readonly List<Image> _skinButtonImages = new List<Image>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (saveButton != null) saveButton.onClick.AddListener(SaveData);
    }

    private void Update()
    {
        // ChillModeManager가 늦게 뜨는 씬 대비 — 초기화될 때까지 가벼운 폴링
        if (!_initialized)
        {
            TryInitialize();
        }
    }

    // ---------------------------------------------------------------- 패널 표시

    public void TogglePanel()
    {
        if (panel == null) return;
        if (panel.activeSelf) HidePanel();
        else ShowPanel();
    }

    public void ShowPanel()
    {
        if (panel == null) return;
        panel.SetActive(true);
        RefreshSkinButtonStates();
        RefreshSlotButtonLabels();
    }

    public void HidePanel()
    {
        if (panel == null) return;
        panel.SetActive(false);
    }

    // ---------------------------------------------------------------- 초기화/상태 적용

    private Transform DeskRoot
    {
        get
        {
            ChillModeManager manager = ChillModeManager.Instance;
            return manager != null ? manager.deskSetRoot : null;
        }
    }

    private void TryInitialize()
    {
        if (data == null) return;
        Transform deskRoot = DeskRoot;
        if (deskRoot == null) return;

        // 슬롯 마커 수집 (DecoSlot_01, DecoSlot_02, ... 이름순)
        List<Transform> markers = new List<Transform>();
        foreach (Transform tr in deskRoot.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name.StartsWith(SlotNamePrefix))
            {
                markers.Add(tr);
            }
        }
        markers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        _slotMarkers = markers.ToArray();
        _slotInstances = new GameObject[_slotMarkers.Length];

        // 저장 상태 적용 (스킨 + 장식)
        DeskSupportData.DeskState state = data.GetOrCreateDesk(DeskSupportData.DefaultDeskId);
        ApplySkinToRenderers(state.skinIndex);
        for (int i = 0; i < _slotMarkers.Length; i++)
        {
            string decoId = i < state.slotDecoIds.Count ? state.slotDecoIds[i] : "";
            SpawnSlotDeco(i, decoId);
        }

        BuildSkinButtons();
        BuildSlotButtons();
        _initialized = true;
    }

    // ---------------------------------------------------------------- 머티리얼 스킨

    /// <summary>스킨 선택: 상태 기록 + 책상 렌더러 일괄 교체 (디스크 저장은 [데이터 저장]).</summary>
    public void ApplySkin(int skinIndex)
    {
        if (data == null || skinIndex < 0 || skinIndex >= data.skinMaterials.Count) return;
        data.GetOrCreateDesk(DeskSupportData.DefaultDeskId).skinIndex = skinIndex;
        ApplySkinToRenderers(skinIndex);
        RefreshSkinButtonStates();
    }

    // 책상 하위 렌더러의 sharedMaterials 중 "스킨 후보에 속한" 원소만 선택 스킨으로 교체.
    // 유리/스크린 등 스킨 후보 밖 머티리얼과 장식 슬롯 하위는 건드리지 않는다.
    private void ApplySkinToRenderers(int skinIndex)
    {
        Transform deskRoot = DeskRoot;
        if (deskRoot == null || skinIndex < 0 || skinIndex >= data.skinMaterials.Count) return;
        Material target = data.skinMaterials[skinIndex];
        if (target == null) return;

        foreach (Renderer renderer in deskRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (IsUnderSlotMarker(renderer.transform)) continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i] != target && data.skinMaterials.Contains(materials[i]))
                {
                    materials[i] = target;
                    changed = true;
                }
            }
            if (changed)
            {
                renderer.sharedMaterials = materials;
            }
        }
    }

    private bool IsUnderSlotMarker(Transform tr)
    {
        foreach (Transform marker in _slotMarkers)
        {
            if (marker != null && tr.IsChildOf(marker))
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- 장식 슬롯

    /// <summary>슬롯 버튼 클릭: 없음 → 장식1 → 장식2 → ... → 없음 순환.</summary>
    public void CycleSlot(int slotIndex)
    {
        if (data == null || slotIndex < 0 || slotIndex >= _slotMarkers.Length) return;

        DeskSupportData.DeskState state = data.GetOrCreateDesk(DeskSupportData.DefaultDeskId);
        while (state.slotDecoIds.Count <= slotIndex)
        {
            state.slotDecoIds.Add("");
        }

        string current = state.slotDecoIds[slotIndex];
        int currentIdx = -1; // -1 = 없음
        for (int i = 0; i < data.decorations.Count; i++)
        {
            if (data.decorations[i].id == current)
            {
                currentIdx = i;
                break;
            }
        }
        int nextIdx = currentIdx + 1;
        string nextId = nextIdx < data.decorations.Count ? data.decorations[nextIdx].id : "";

        state.slotDecoIds[slotIndex] = nextId;
        SpawnSlotDeco(slotIndex, nextId);
        RefreshSlotButtonLabels();
    }

    private void SpawnSlotDeco(int slotIndex, string decoId)
    {
        if (slotIndex < 0 || slotIndex >= _slotMarkers.Length || _slotMarkers[slotIndex] == null) return;

        if (_slotInstances[slotIndex] != null)
        {
            Destroy(_slotInstances[slotIndex]);
            _slotInstances[slotIndex] = null;
        }

        DeskSupportData.DecoDef deco = data.FindDecoration(decoId);
        if (deco == null || deco.prefab == null) return;

        GameObject instance = Instantiate(deco.prefab, _slotMarkers[slotIndex]);
        instance.name = deco.prefab.name;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        SetLayerRecursive(instance, _slotMarkers[slotIndex].gameObject.layer);
        _slotInstances[slotIndex] = instance;
    }

    // ---------------------------------------------------------------- 저장

    private void SaveData()
    {
        if (data == null)
        {
            Debug.LogWarning("[DeskSupport] DeskSupportData가 연결되지 않았습니다.");
            return;
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(data);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("[DeskSupport] DeskSupportData 저장 완료 (스킨/장식 상태)");
#else
        Debug.LogWarning("[DeskSupport] 빌드에서는 DeskSupportData가 디스크에 저장되지 않습니다 (세션 한정 반영).");
#endif
    }

    // ---------------------------------------------------------------- UI 동적 생성

    private void BuildSkinButtons()
    {
        foreach (GameObject go in _skinButtons)
        {
            if (go != null) Destroy(go);
        }
        _skinButtons.Clear();
        _skinButtonImages.Clear();
        if (skinContainer == null || data == null) return;

        for (int i = 0; i < data.skinMaterials.Count; i++)
        {
            int index = i; // 클로저 캡처용 복사
            Button button = CreateButton(skinContainer, "Skin" + (i + 1), (i + 1).ToString(),
                new Vector2(14f + i * 90f, 0f), new Vector2(84f, 34f), out Image image, out TMP_Text _);
            button.onClick.AddListener(() => ApplySkin(index));
            _skinButtons.Add(button.gameObject);
            _skinButtonImages.Add(image);
        }
        RefreshSkinButtonStates();
    }

    private void BuildSlotButtons()
    {
        _slotButtonLabels.Clear();
        if (decoContainer == null) return;
        for (int i = decoContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(decoContainer.GetChild(i).gameObject);
        }

        for (int i = 0; i < _slotMarkers.Length; i++)
        {
            int index = i; // 클로저 캡처용 복사
            Button button = CreateButton(decoContainer, "Slot" + (i + 1), "",
                new Vector2(14f, -(i * RowHeight)), new Vector2(352f, 34f), out Image _, out TMP_Text label);
            button.onClick.AddListener(() => CycleSlot(index));
            _slotButtonLabels.Add(label);
        }
        RefreshSlotButtonLabels();
        AdjustLayout();
    }

    // 선택 중 스킨 버튼만 강조색
    private void RefreshSkinButtonStates()
    {
        if (data == null) return;
        int selected = data.GetOrCreateDesk(DeskSupportData.DefaultDeskId).skinIndex;
        for (int i = 0; i < _skinButtonImages.Count; i++)
        {
            if (_skinButtonImages[i] != null)
            {
                _skinButtonImages[i].color = i == selected ? ButtonSelectedColor : ButtonColor;
            }
        }
    }

    private void RefreshSlotButtonLabels()
    {
        if (data == null) return;
        DeskSupportData.DeskState state = data.GetOrCreateDesk(DeskSupportData.DefaultDeskId);
        for (int i = 0; i < _slotButtonLabels.Count; i++)
        {
            if (_slotButtonLabels[i] == null) continue;
            string decoId = i < state.slotDecoIds.Count ? state.slotDecoIds[i] : "";
            DeskSupportData.DecoDef deco = data.FindDecoration(decoId);
            _slotButtonLabels[i].text = "슬롯 " + (i + 1) + " : " + (deco != null ? deco.label : "없음");
        }
    }

    // 슬롯 수에 맞춰 저장 버튼 위치/패널 높이 조절
    private void AdjustLayout()
    {
        float decoBottom = DecoSectionTop + Mathf.Max(1, _slotMarkers.Length) * RowHeight;
        if (saveButton != null)
        {
            RectTransform saveRt = saveButton.GetComponent<RectTransform>();
            saveRt.anchoredPosition = new Vector2(14f, -(decoBottom + 8f));
        }
        if (panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, decoBottom + 8f + 32f + 12f);
        }
    }

    private Button CreateButton(RectTransform parent, string name, string label,
        Vector2 pos, Vector2 size, out Image image, out TMP_Text labelText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        image = go.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

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
        if (uiFont != null) tmp.font = uiFont;
        tmp.text = label;
        tmp.fontSize = 20f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        labelText = tmp;

        return button;
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}
