using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JarvisTodoListUI : MonoBehaviour
{
    private readonly List<JarvisTodoItemRow> rows = new List<JarvisTodoItemRow>();
    private DateTime selectedDate = DateTime.Now.Date;
    private TextMeshProUGUI dateText;
    private TextMeshProUGUI countText;
    private TMP_InputField input;
    private Button addButton;
    private Button closeButton;
    private RectTransform rootRect;
    private RectTransform headerRect;
    private RectTransform panelRect;
    private RectTransform todoListParentRect;
    private TextMeshProUGUI detailText;
    private TMP_InputField detailInput;
    private RectTransform listContent;
    private Transform rowTemplate;
    private JarvisTodoItemRow draggingRow;
    private string selectedDetailItemId;
    private bool isBound;

    public void ShowDetail(JarvisTodoItemRow row)
    {
        BindExistingDetailBox();
        selectedDetailItemId = row != null ? row.ItemId : string.Empty;
        SetDetailText(row != null ? row.DetailText : string.Empty, true);
    }

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

    public void Show(DateTime date)
    {
        selectedDate = date.Date;
        gameObject.SetActive(true);
        Refresh();
    }

    public void SelectDate(DateTime date)
    {
        selectedDate = date.Date;
        Refresh();
    }

    public void BeginRowDrag(JarvisTodoItemRow row)
    {
        draggingRow = row;
    }

    public void UpdateRowDrag(JarvisTodoItemRow row, PointerEventData eventData)
    {
        if (row == null || row != draggingRow || listContent == null)
        {
            return;
        }

        TryMoveDraggingRow(eventData);
    }

    public void EndRowDrag(JarvisTodoItemRow row)
    {
        if (row == null || row != draggingRow || JarvisTodoStore.Instance == null || listContent == null)
        {
            draggingRow = null;
            return;
        }

        List<JarvisTodoItemRow> visibleRows = GetVisibleRows();
        List<string> ids = new List<string>();
        for (int i = 0; i < visibleRows.Count; i++)
        {
            if (!string.IsNullOrEmpty(visibleRows[i].ItemId))
            {
                ids.Add(visibleRows[i].ItemId);
            }
        }

        JarvisTodoStore.Instance.Reorder(selectedDate, ids);
        draggingRow = null;
    }

    private void BindExistingPrefab()
    {
        if (isBound)
        {
            return;
        }

        isBound = true;
        gameObject.name = "TODOList";
        rootRect = transform as RectTransform;

        Transform rightPanel = FindDeepChild(transform, "RightPanel");
        Transform header = FindDeepChild(transform, "Header");

        if (rightPanel != null)
        {
            rightPanel.gameObject.SetActive(true);
            panelRect = rightPanel as RectTransform;
        }

        if (header != null)
        {
            header.gameObject.SetActive(true);
            headerRect = header as RectTransform;
        }

        dateText = GetComponentInNamedChild<TextMeshProUGUI>("TxtSelectedDate");
        countText = GetComponentInNamedChild<TextMeshProUGUI>("TxtCompletionCount");
        input = GetComponentInNamedChild<TMP_InputField>("InputNewTODO");
        addButton = GetComponentInNamedChild<Button>("BtnAddTODO");
        closeButton = GetComponentInNamedChild<Button>("BtnBack");

        Button editButton = GetComponentInNamedChild<Button>("Btn Edit Mode");
        if (editButton != null)
        {
            editButton.gameObject.SetActive(false);
        }

        Transform todoListParent = FindDeepChild(transform, "TodoListParent");
        todoListParentRect = todoListParent as RectTransform;
        ScrollRect scrollRect = todoListParent != null ? todoListParent.GetComponent<ScrollRect>() : null;
        listContent = scrollRect != null && scrollRect.content != null
            ? scrollRect.content
            : todoListParent as RectTransform;

        rowTemplate = FindRowTemplate(listContent);
        if (rowTemplate != null)
        {
            rowTemplate.gameObject.SetActive(false);
        }

        BindExistingDetailBox();

        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(AddInputItem);
        }

        if (input != null)
        {
            input.onSubmit.RemoveAllListeners();
            input.onSubmit.AddListener(_ => AddInputItem());
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }
    }

    private void AddInputItem()
    {
        if (input == null || JarvisTodoStore.Instance == null)
        {
            return;
        }

        string text = input.text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        JarvisTodoStore.Instance.AddItem(selectedDate, text);
        input.text = string.Empty;
        input.ActivateInputField();
    }

    private void Refresh()
    {
        BindExistingPrefab();

        if (dateText != null)
        {
            dateText.text = selectedDate.ToString("yyyy-MM-dd");
        }

        ClearRows();

        List<JarvisTodoStore.TodoItem> items = JarvisTodoStore.Instance != null
            ? JarvisTodoStore.Instance.GetItemsByDate(selectedDate)
            : new List<JarvisTodoStore.TodoItem>();

        int completed = 0;
        JarvisTodoItemRow selectedRow = null;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].isCompleted)
            {
                completed++;
            }

            GameObject rowObject = CreateRowObject();
            if (rowObject == null)
            {
                continue;
            }

            JarvisTodoItemRow row = rowObject.GetComponent<JarvisTodoItemRow>();
            if (row == null)
            {
                Debug.LogWarning("[JarvisTodoListUI] TodoItem Sample is missing JarvisTodoItemRow.");
                Destroy(rowObject);
                continue;
            }

            row.Build();
            row.Bind(items[i], this);
            rows.Add(row);

            if (!string.IsNullOrEmpty(selectedDetailItemId) && row.ItemId == selectedDetailItemId)
            {
                selectedRow = row;
            }
        }

        if (countText != null)
        {
            countText.text = completed + "/" + items.Count + " Done";
        }

        if (selectedRow != null)
        {
            SetDetailText(selectedRow.DetailText, false);
        }
        else if (rows.Count > 0)
        {
            selectedDetailItemId = rows[0].ItemId;
            SetDetailText(rows[0].DetailText, false);
        }
        else
        {
            selectedDetailItemId = string.Empty;
            SetDetailText(string.Empty, false);
        }
    }

    private GameObject CreateRowObject()
    {
        if (rowTemplate != null)
        {
            GameObject rowObject = Instantiate(rowTemplate.gameObject, listContent, false);
            rowObject.name = "TodoItem";
            rowObject.SetActive(true);
            return rowObject;
        }

        Debug.LogWarning("[JarvisTodoListUI] Content/Sample row template is missing.");
        return null;
    }

    private void BindExistingDetailBox()
    {
        Transform detailRoot = FindDeepChild(transform, "DetailTextBox");
        if (detailRoot == null)
        {
            detailRoot = FindDeepChild(transform, "Detail");
        }

        if (detailRoot == null)
        {
            return;
        }

        detailText = detailRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        detailInput = detailRoot.GetComponent<TMP_InputField>();
        if (detailInput == null)
        {
            detailInput = detailRoot.gameObject.AddComponent<TMP_InputField>();
        }

        if (detailText != null)
        {
            detailInput.textComponent = detailText;
        }

        detailInput.lineType = TMP_InputField.LineType.MultiLineNewline;
        detailInput.contentType = TMP_InputField.ContentType.Standard;
        detailInput.targetGraphic = detailRoot.GetComponent<Graphic>();
        detailInput.onEndEdit.RemoveAllListeners();
        detailInput.onValueChanged.RemoveAllListeners();
        detailInput.onValueChanged.AddListener(SaveDetailEdit);
    }

    private void SetDetailText(string text, bool focus)
    {
        if (detailInput != null)
        {
            detailInput.SetTextWithoutNotify(text);
            if (focus)
            {
                detailInput.ActivateInputField();
                MoveDetailCaretToEnd(text);
            }
            return;
        }

        if (detailText != null)
        {
            detailText.text = text;
        }
    }

    private void SaveDetailEdit(string text)
    {
        if (string.IsNullOrEmpty(selectedDetailItemId) || JarvisTodoStore.Instance == null)
        {
            return;
        }

        JarvisTodoStore.Instance.SetContent(selectedDetailItemId, text);
    }

    private void MoveDetailCaretToEnd(string text)
    {
        if (detailInput == null)
        {
            return;
        }

        int position = string.IsNullOrEmpty(text) ? 0 : text.Length;
        detailInput.caretPosition = position;
        detailInput.selectionAnchorPosition = position;
        detailInput.selectionFocusPosition = position;
        StartCoroutine(MoveDetailCaretToEndNextFrame(position));
    }

    private IEnumerator MoveDetailCaretToEndNextFrame(int position)
    {
        yield return null;
        if (detailInput == null)
        {
            yield break;
        }

        int safePosition = Mathf.Clamp(position, 0, detailInput.text != null ? detailInput.text.Length : 0);
        detailInput.caretPosition = safePosition;
        detailInput.selectionAnchorPosition = safePosition;
        detailInput.selectionFocusPosition = safePosition;
    }

    private static Transform FindRowTemplate(Transform content)
    {
        if (content == null)
        {
            return null;
        }

        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            string lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("sample") || lowerName.Contains("template"))
            {
                return child;
            }

            if (!child.gameObject.activeSelf)
            {
                return child;
            }
        }

        return null;
    }

    private void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
            {
                Destroy(rows[i].gameObject);
            }
        }

        rows.Clear();
        if (listContent == null)
        {
            return;
        }

        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            Transform child = listContent.GetChild(i);
            if (child == rowTemplate)
            {
                continue;
            }

            if (child.GetComponent<JarvisTodoItemRow>() != null)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void TryMoveDraggingRow(PointerEventData eventData)
    {
        if (eventData == null || draggingRow == null)
        {
            return;
        }

        List<JarvisTodoItemRow> visibleRows = GetVisibleRows();
        int visibleIndex = visibleRows.IndexOf(draggingRow);
        if (visibleIndex < 0)
        {
            return;
        }

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;

        if (visibleIndex > 0)
        {
            RectTransform previousRect = visibleRows[visibleIndex - 1].transform as RectTransform;
            if (previousRect != null && PointerPassedUpperThreshold(previousRect, eventData.position.y, eventCamera))
            {
                draggingRow.transform.SetSiblingIndex(visibleRows[visibleIndex - 1].transform.GetSiblingIndex());
                Canvas.ForceUpdateCanvases();
                return;
            }
        }

        if (visibleIndex < visibleRows.Count - 1)
        {
            RectTransform nextRect = visibleRows[visibleIndex + 1].transform as RectTransform;
            if (nextRect != null && PointerPassedLowerThreshold(nextRect, eventData.position.y, eventCamera))
            {
                draggingRow.transform.SetSiblingIndex(visibleRows[visibleIndex + 1].transform.GetSiblingIndex());
                Canvas.ForceUpdateCanvases();
            }
        }
    }

    private List<JarvisTodoItemRow> GetVisibleRows()
    {
        List<JarvisTodoItemRow> visibleRows = new List<JarvisTodoItemRow>();
        if (listContent == null)
        {
            return visibleRows;
        }

        for (int i = 0; i < listContent.childCount; i++)
        {
            Transform child = listContent.GetChild(i);
            if (child == rowTemplate || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            JarvisTodoItemRow itemRow = child.GetComponent<JarvisTodoItemRow>();
            if (itemRow != null && !string.IsNullOrEmpty(itemRow.ItemId))
            {
                visibleRows.Add(itemRow);
            }
        }

        return visibleRows;
    }

    private static bool PointerPassedUpperThreshold(RectTransform target, float pointerY, Camera eventCamera)
    {
        GetScreenVerticalBounds(target, eventCamera, out float minY, out float maxY);
        float threshold = Mathf.Lerp(minY, maxY, 0.65f);
        return pointerY > threshold;
    }

    private static bool PointerPassedLowerThreshold(RectTransform target, float pointerY, Camera eventCamera)
    {
        GetScreenVerticalBounds(target, eventCamera, out float minY, out float maxY);
        float threshold = Mathf.Lerp(minY, maxY, 0.35f);
        return pointerY < threshold;
    }

    private static void GetScreenVerticalBounds(RectTransform target, Camera eventCamera, out float minY, out float maxY)
    {
        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        minY = float.MaxValue;
        maxY = float.MinValue;
        for (int i = 0; i < corners.Length; i++)
        {
            float y = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]).y;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }
    }

    private T GetComponentInNamedChild<T>(string childName) where T : Component
    {
        Transform child = FindDeepChild(transform, childName);
        return child != null ? child.GetComponent<T>() : null;
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
