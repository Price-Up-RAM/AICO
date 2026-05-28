using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TODOItemUI : MonoBehaviour
{
    [SerializeField] private Toggle toggleComplete;
    [SerializeField] private TextMeshProUGUI txtContent;
    [SerializeField] private Button btnDelete;

    [Header("순서 편집")]
    [SerializeField] private Button btnMoveUp;
    [SerializeField] private Button btnMoveDown;
    [SerializeField] private GameObject editModeButtons; // 위/아래 버튼 묶음 오브젝트

    private string m_itemId;
    private Action m_onChanged;
    private Action m_onCountChanged;

    public void Init(TODOManager.TODOItem item, Action onDeleted, Action onCountChanged)
    {
        m_itemId = item.id;
        m_onChanged = onDeleted;
        m_onCountChanged = onCountChanged;

        txtContent.text = item.content;
        toggleComplete.isOn = item.isCompleted;
        SetCompletedVisual(item.isCompleted);

        toggleComplete.onValueChanged.RemoveAllListeners();
        toggleComplete.onValueChanged.AddListener(OnToggleChanged);

        btnDelete.onClick.RemoveAllListeners();
        btnDelete.onClick.AddListener(OnDeleteClicked);

        if (btnMoveUp != null)
        {
            btnMoveUp.onClick.RemoveAllListeners();
            btnMoveUp.onClick.AddListener(() => OnMoveClicked(-1));
        }

        if (btnMoveDown != null)
        {
            btnMoveDown.onClick.RemoveAllListeners();
            btnMoveDown.onClick.AddListener(() => OnMoveClicked(1));
        }
    }

    public void SetEditMode(bool isEditMode)
    {
        if (editModeButtons != null)
            editModeButtons.SetActive(isEditMode);
    }

    private void OnMoveClicked(int direction)
    {
        TODOManager.Instance.MoveItem(m_itemId, direction);
        m_onChanged?.Invoke();
    }

    private void OnToggleChanged(bool isCompleted)
    {
        TODOManager.Instance.SetCompleted(m_itemId, isCompleted);
        SetCompletedVisual(isCompleted);
        m_onCountChanged?.Invoke();
    }

    private void OnDeleteClicked()
    {
        TODOManager.Instance.DeleteItem(m_itemId);
        m_onChanged?.Invoke();
    }

    private void SetCompletedVisual(bool isCompleted)
    {
        txtContent.fontStyle = isCompleted ? FontStyles.Strikethrough : FontStyles.Normal;
        txtContent.alpha = isCompleted ? 0.4f : 1f;
    }
}
