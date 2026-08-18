// JarvisCalendarUI의 MR 전용 포크.
//
// 원본은 "부모 오브젝트(Calendar)에 붙어서 자식 중 CalendarPicker를 찾는" 구조다.
// MR에서는 껍데기 부모(rect 100×100)가 잡기 띠·판정 면을 엉뚱한 크기로 만들어서
// 부모를 없애고 CalendarPicker 자체를 패널로 승격시켰다(§4-18 계열 정리).
// 그러면 원본 스크립트가 깨진다:
//
//   gameObject.name = "Calendar";                                // ① 자기 이름을 바꾸고
//   calendarPicker = FindDeepChild(transform, "CalendarPicker"); // ② 자식에서 찾음 → null
//
//   → expandedSize가 (0,0)으로 남고 SetExpanded()가 rootRect.sizeDelta = (0,0)을 대입
//   → ButtonsDaysParent(부모에 stretch)의 폭이 무너짐
//   → GridLayoutGroup이 Flexible이라 열 수 = 폭/셀폭 으로 계산 → 날짜 42개가 한 줄로 늘어섬
//     (m_ConstraintCount: 2는 Flexible에서 무시되는 죽은 값)
//
// 원본 파일은 데스크톱과 공유하므로 건드리지 않고, 이 포크를 CalendarPicker에 붙인다.
//
// 원본과 다른 점
// -------------
//  1. gameObject 이름을 바꾸지 않는다. FindDeepChild가 자기 이름부터 비교하므로
//     ("CalendarPicker" 유지 시 자기 자신을 반환) 스크립트가 자기 자신에 붙어도 동작한다.
//  2. pickerRect.anchoredPosition = Vector2.zero 를 하지 않는다.
//     MR에서는 사용자가 패널을 원하는 위치로 옮겨두므로 원점으로 끌어오면 안 된다.
//  3. 데스크톱 드래그 핸들러(DragUIHandler / UIDragHandler / JarvisCalendarToggleDragHandler)를
//     붙이지 않는다. MR 이동은 ISDK grab이 담당하며, 이들을 붙이면 §4-15처럼
//     손 드래그가 두 경로로 처리돼 충돌한다(MRSceneStripper가 끄더라도 런타임에 다시 붙는다).
//  4. 크기가 바뀌면 MRGrabFrameFitter에 재계산을 요청한다 — 접기/펼치기로 rect가 변하는데
//     잡기 띠가 옛 크기로 남으면 어긋난다(§4-26).

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MRJarvisCalendarUI : MonoBehaviour
{
    public event Action<DateTime> DateSelected;

    private DateTime visibleMonth = DateTime.Now.Date;
    private Text monthText;
    private Transform daysRoot;
    private Button previousButton;
    private Button nextButton;
    private Button closeButton;
    private Toggle calendarToggle;
    private Transform calendarPicker;
    private Transform calendarMonthHeader;
    private Transform calendarWeekDisplays;
    private Transform buttonsDaysParent;
    private Image pickerImage;
    private RectTransform rootRect;
    private RectTransform pickerRect;
    private Vector2 expandedSize;

    [Tooltip("(임시) 레이아웃 진단 로그. 캘린더 크기 문제 원인 확인 후 끄거나 지운다.")]
    [SerializeField] private bool logLayoutDiagnostics = true;
    private readonly List<JarvisCalendarDayButton> dayButtons = new List<JarvisCalendarDayButton>();
    private bool isBound;
    private bool isExpanded = true;

    private void Awake()
    {
        EnsureStore();
        BindExistingPrefab();
    }

    private void OnEnable()
    {
        EnsureStore();
        BindExistingPrefab();
        if (JarvisTodoStore.Instance != null)
        {
            JarvisTodoStore.Instance.Changed += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (JarvisTodoStore.Instance != null)
        {
            JarvisTodoStore.Instance.Changed -= Refresh;
        }
    }

    public void ShowToday()
    {
        visibleMonth = DateTime.Now.Date;
        gameObject.SetActive(true);
        SetExpanded(true);
        Refresh();
    }

    public void Refresh()
    {
        BindExistingPrefab();

        if (monthText != null)
        {
            monthText.text = visibleMonth.ToString("yyyy-MM");
        }

        EnsureDayButtons();

        // (임시·Phase 4-A 진단) 실기에서 캘린더가 위아래로 2배 늘어나 보이는 원인을 좁히기 위한 로그.
        // 레이아웃 파라미터(7열 고정, 42셀, 셀 38px)는 정상이므로, 실제 자식 수와
        // rect 크기 중 무엇이 어긋나는지 봐야 한다. 원인 확인 후 삭제할 것.
        if (logLayoutDiagnostics)
        {
            int rootChildren = 0;
            Vector2 daysSize = Vector2.zero;
            if (daysRoot != null)
            {
                rootChildren = daysRoot.childCount;
                RectTransform daysRect = daysRoot as RectTransform;
                if (daysRect != null) daysSize = daysRect.rect.size;
            }

            Vector2 pickerSize = Vector2.zero;
            if (pickerRect != null) pickerSize = pickerRect.rect.size;

            Vector2 rootSize = Vector2.zero;
            if (rootRect != null) rootSize = rootRect.rect.size;

            Debug.Log($"[MRCalendarDiag] dayButtons={dayButtons.Count} daysRoot.childCount={rootChildren} " +
                      $"daysRoot.rect={daysSize} pickerRect.rect={pickerSize} rootRect.rect={rootSize} " +
                      $"expandedSize={expandedSize} lossyScale={transform.lossyScale}");
        }

        Dictionary<string, int> counts = JarvisTodoStore.Instance != null
            ? JarvisTodoStore.Instance.GetCountsByDate(visibleMonth.Year, visibleMonth.Month)
            : new Dictionary<string, int>();

        DateTime firstDay = new DateTime(visibleMonth.Year, visibleMonth.Month, 1);
        DateTime gridStart = firstDay.AddDays(-(int)firstDay.DayOfWeek);
        for (int i = 0; i < dayButtons.Count; i++)
        {
            DateTime day = gridStart.AddDays(i);
            counts.TryGetValue(day.ToString("yyyy-MM-dd"), out int count);
            dayButtons[i].Bind(
                day,
                day.Month == visibleMonth.Month,
                day == DateTime.Now.Date,
                count,
                OnDayClicked);
        }
    }

    private void BindExistingPrefab()
    {
        if (isBound)
        {
            return;
        }

        isBound = true;

        // 원본의 `gameObject.name = "Calendar";`를 하지 않는다 — 아래 FindDeepChild가
        // 자기 이름부터 비교하므로, "CalendarPicker" 이름을 유지해야 자기 자신을 찾는다.
        rootRect = transform as RectTransform;

        calendarPicker = FindDeepChild(transform, "CalendarPicker");
        calendarMonthHeader = FindDeepChild(transform, "CalendarMonthHeader");
        calendarWeekDisplays = FindDeepChild(transform, "CalendarWeekDisplays");
        buttonsDaysParent = FindDeepChild(transform, "ButtonsDaysParent");
        Transform toggle = FindDeepChild(transform, "BtnCalendarToggle");
        Transform close = FindDeepChild(transform, "BtnCalendarClose");

        if (calendarPicker == null)
        {
            Debug.LogError($"[MRJarvisCalendarUI] '{name}' — CalendarPicker를 찾지 못했습니다. " +
                            "이 컴포넌트는 'CalendarPicker'라는 이름의 오브젝트에 직접 붙이거나, " +
                            "그 부모에 붙여야 합니다.");
            return;
        }

        calendarPicker.gameObject.SetActive(true);
        pickerImage = calendarPicker.GetComponent<Image>();
        pickerRect = calendarPicker as RectTransform;

        if (pickerRect != null)
        {
            expandedSize = pickerRect.sizeDelta;

            // 원본은 여기서 pickerRect.anchoredPosition = Vector2.zero 로 원점에 붙였다.
            // MR에서는 사용자가 옮겨둔 위치를 유지해야 하므로 하지 않는다.

            if (rootRect != null && rootRect != pickerRect)
            {
                rootRect.sizeDelta = pickerRect.sizeDelta;
                rootRect.pivot = pickerRect.pivot;
            }
        }

        if (expandedSize == Vector2.zero)
        {
            Debug.LogWarning($"[MRJarvisCalendarUI] '{name}' — 펼친 크기가 0입니다. " +
                              "CalendarPicker의 sizeDelta를 확인하세요(0이면 날짜가 한 줄로 늘어섭니다).");
        }

        if (toggle != null)
        {
            toggle.gameObject.SetActive(true);
            calendarToggle = toggle.GetComponent<Toggle>();
            if (calendarToggle != null)
            {
                calendarToggle.onValueChanged.RemoveAllListeners();
                calendarToggle.onValueChanged.AddListener(SetExpanded);
                calendarToggle.SetIsOnWithoutNotify(isExpanded);
            }
        }

        if (close != null)
        {
            close.gameObject.SetActive(true);
            closeButton = close.GetComponent<Button>();
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        // 데스크톱 드래그 핸들러는 붙이지 않는다 (클래스 주석 3번 참고).

        daysRoot = buttonsDaysParent;
        monthText = calendarMonthHeader?.GetComponentInChildren<Text>(true);
        previousButton = FindDeepChild(transform, "BtnPreviousMonth")?.GetComponent<Button>();
        nextButton = FindDeepChild(transform, "BtnNextMonth")?.GetComponent<Button>();

        if (previousButton != null)
        {
            previousButton.onClick.RemoveAllListeners();
            previousButton.onClick.AddListener(() =>
            {
                visibleMonth = visibleMonth.AddMonths(-1);
                Refresh();
            });
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() =>
            {
                visibleMonth = visibleMonth.AddMonths(1);
                Refresh();
            });
        }

        SetExpanded(isExpanded);
    }

    private void Close()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseCalendar();
            return;
        }

        gameObject.SetActive(false);
    }

    private void EnsureDayButtons()
    {
        if (daysRoot == null)
        {
            return;
        }

        // 리스트가 아니라 **실제 자식**을 진실의 원천으로 삼는다.
        //
        // 예전 구현은 dayButtons.Count만 보고 42개가 될 때까지 채웠다. 그런데 리스트는
        // 컴포넌트 인스턴스에 딸린 것이고 daysRoot의 자식은 씬에 남으므로, 리스트가
        // 비워진 채 다시 채워지면 자식만 42개 더 늘어난다.
        // 실측(2026-08-18): dayButtons=42 인데 daysRoot.childCount=84 —
        // 7열 그리드에 84칸이라 12행이 되어 캘린더 높이가 2배가 되고 두 달치처럼 보였다.
        dayButtons.Clear();
        for (int i = 0; i < daysRoot.childCount; i++)
        {
            JarvisCalendarDayButton existing = daysRoot.GetChild(i).GetComponent<JarvisCalendarDayButton>();
            if (existing != null)
            {
                dayButtons.Add(existing);
            }
        }

        // 남는 것은 지운다. 재생성보다 파괴가 먼저다 — 42개를 넘긴 채로 두면
        // 레이아웃이 계속 어긋난다.
        while (dayButtons.Count > 42)
        {
            JarvisCalendarDayButton extra = dayButtons[dayButtons.Count - 1];
            dayButtons.RemoveAt(dayButtons.Count - 1);
            if (extra == null) continue;

            // 계층에서 **먼저** 떼어낸다. Destroy는 프레임 끝에 실행되므로, 그냥 두면
            // 같은 프레임 안의 다음 Refresh()가 아직 살아 있는 자식을 다시 세어
            // 42개 초과 상태가 그대로 유지된다.
            extra.transform.SetParent(null, false);

            if (Application.isPlaying)
            {
                Destroy(extra.gameObject);
                continue;
            }

            DestroyImmediate(extra.gameObject);
        }

        while (dayButtons.Count < 42)
        {
            GameObject dayObject = new GameObject("Day", typeof(RectTransform));
            dayObject.transform.SetParent(daysRoot, false);
            JarvisCalendarDayButton dayButton = dayObject.AddComponent<JarvisCalendarDayButton>();
            dayButton.Build();
            dayButtons.Add(dayButton);
        }
    }

    private void OnDayClicked(DateTime date)
    {
        DateSelected?.Invoke(date);
        if (DateSelected == null && UIManager.Instance != null)
        {
            UIManager.Instance.OnCalendarDateSelected(date);
        }
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;

        if (calendarPicker != null)
        {
            calendarPicker.gameObject.SetActive(true);
        }

        if (pickerImage != null)
        {
            pickerImage.enabled = expanded;
        }

        if (calendarMonthHeader != null)
        {
            calendarMonthHeader.gameObject.SetActive(expanded);
        }

        if (calendarWeekDisplays != null)
        {
            calendarWeekDisplays.gameObject.SetActive(expanded);
        }

        if (buttonsDaysParent != null)
        {
            buttonsDaysParent.gameObject.SetActive(expanded);
        }

        Vector2 targetSize = expanded ? expandedSize : Vector2.zero;
        if (targetSize == Vector2.zero && pickerRect != null)
        {
            RectTransform toggleRect = calendarToggle != null ? calendarToggle.GetComponent<RectTransform>() : null;
            targetSize = expanded || toggleRect == null ? pickerRect.sizeDelta : toggleRect.sizeDelta;
        }

        // 크기가 0이면 절대 대입하지 않는다 — 원본이 이것 때문에 레이아웃을 무너뜨렸다.
        if (targetSize != Vector2.zero)
        {
            if (pickerRect != null) pickerRect.sizeDelta = targetSize;
            if (rootRect != null && rootRect != pickerRect) rootRect.sizeDelta = targetSize;
        }

        if (calendarToggle != null)
        {
            calendarToggle.gameObject.SetActive(true);
            calendarToggle.SetIsOnWithoutNotify(expanded);
        }

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(expanded);
        }

        RefitGrabFrame();
    }

    /// <summary>접기/펼치기로 rect가 바뀌었으니 잡기 띠를 다시 계산시킨다.</summary>
    private void RefitGrabFrame()
    {
        var fitter = GetComponentInChildren<MRGrabFrameFitter>(true);
        if (fitter != null) fitter.Fit();
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void EnsureStore()
    {
        if (JarvisTodoStore.Instance != null)
        {
            return;
        }

        GameObject storeObject = new GameObject("JarvisTodoStore");
        storeObject.AddComponent<JarvisTodoStore>();
        DontDestroyOnLoad(storeObject);
    }
}
