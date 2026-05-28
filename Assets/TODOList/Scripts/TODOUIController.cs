using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TODOUIController : MonoBehaviour
{
    [Header("헤더")]
    [SerializeField] private Button btnBack;
    [SerializeField] private TextMeshProUGUI txtDate;
    [SerializeField] private TextMeshProUGUI txtCompletionCount;

    [Header("TODO 입력")]
    [SerializeField] private TMP_InputField inputNewTODO;
    [SerializeField] private Button btnAddTODO;

    [Header("TODO 리스트")]
    [SerializeField] private Transform todoListParent;
    [SerializeField] private GameObject todoItemPrefab;
    [SerializeField] private Button btnEditMode;
    [SerializeField] private TextMeshProUGUI txtEditMode;

    [Header("달력 팝업")]
    [SerializeField] private Toggle toggleCalendar;
    [SerializeField] private UniversalCalendarPopUp calendarPopUp;

    private DateTime m_selectedDate = DateTime.Now.Date;
    private List<GameObject> m_spawnedItems = new List<GameObject>();
    private bool m_isEditMode = false;

    private void Start()
    {
        btnBack.onClick.AddListener(OnBackClicked);
        btnAddTODO.onClick.AddListener(OnAddTODO);

        if (toggleCalendar != null)
            toggleCalendar.onValueChanged.AddListener(OnCalendarToggled);

        if (btnEditMode != null)
            btnEditMode.onClick.AddListener(OnToggleEditMode);

        UpdateDateDisplay();
        RefreshTODOList();
    }

    private void OnBackClicked()
    {
        Debug.Log("돌아가기");
    }

    private void OnCalendarToggled(bool isOn)
    {
        var popup = calendarPopUp != null ? calendarPopUp : UniversalCalendarPopUp.Instance;
        if (popup == null) return;

        if (isOn)
            popup.ShowCalendarPopUp(OnDateSelected);
        else
            popup.HideCalendar();
    }

    private void OnDateSelected(DateTime selectedDate)
    {
        m_selectedDate = selectedDate.Date;
        UpdateDateDisplay();
        RefreshTODOList();
    }

    private void UpdateDateDisplay()
    {
        txtDate.text = m_selectedDate.ToString("yyyy년 MM월 dd일");
    }

    private void OnAddTODO()
    {
        string content = inputNewTODO.text.Trim();
        if (string.IsNullOrEmpty(content)) return;

        TODOManager.Instance.AddItem(content, m_selectedDate);
        inputNewTODO.text = string.Empty;
        inputNewTODO.ActivateInputField();
        RefreshTODOList();
        RefreshCalendarDots();
    }

    private void OnToggleEditMode()
    {
        m_isEditMode = !m_isEditMode;
        if (txtEditMode != null)
            txtEditMode.text = m_isEditMode ? "완료" : "편집";
        ApplyEditMode();
    }

    private void ApplyEditMode()
    {
        foreach (var obj in m_spawnedItems)
            obj.GetComponent<TODOItemUI>()?.SetEditMode(m_isEditMode);
    }

    private void RefreshTODOList()
    {
        foreach (var obj in m_spawnedItems)
            Destroy(obj);
        m_spawnedItems.Clear();

        var items = TODOManager.Instance.GetItemsByDate(m_selectedDate);

        foreach (var item in items)
        {
            var obj = Instantiate(todoItemPrefab, todoListParent);
            obj.GetComponent<TODOItemUI>().Init(item, OnTODODeleted, RefreshTODOList);
            obj.GetComponent<TODOItemUI>().SetEditMode(m_isEditMode);
            m_spawnedItems.Add(obj);
        }

        // 스폰 직후 레이아웃 강제 갱신 (Content Size Fitter 타이밍 문제 방지)
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(todoListParent as RectTransform);

        RefreshCompletionCount();
    }

    private void RefreshCompletionCount()
    {
        var items = TODOManager.Instance.GetItemsByDate(m_selectedDate);
        int completed = items.FindAll(i => i.isCompleted).Count;
        txtCompletionCount.text = $"{completed}/{items.Count} 완료";
        RefreshCalendarDots();
    }

    private void OnTODODeleted()
    {
        RefreshTODOList();
        RefreshCalendarDots();
    }

    private void RefreshCalendarDots()
    {
        UniversalCalendarPopUp popup = calendarPopUp != null ? calendarPopUp : UniversalCalendarPopUp.Instance;
        popup?.RefreshDots();
    }
}
