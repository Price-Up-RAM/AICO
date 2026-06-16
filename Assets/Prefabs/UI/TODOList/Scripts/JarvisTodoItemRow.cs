using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class JarvisTodoItemRow : MonoBehaviour, IPointerClickHandler
{
    private JarvisTodoStore.TodoItem item;
    private JarvisTodoListUI owner;
    private Toggle toggle;
    private TextMeshProUGUI label;
    private Button deleteButton;
    private CanvasGroup canvasGroup;
    private JarvisTodoRowDragHandle dragHandle;

    public string ItemId => item != null ? item.id : string.Empty;
    public string DetailText
    {
        get
        {
            return item != null ? item.content : string.Empty;
        }
    }

    public void Build()
    {
        dragHandle = FindNamedComponent<JarvisTodoRowDragHandle>("DragHandle");

        toggle = FindNamedComponent<Toggle>("Complete");
        if (toggle == null)
        {
            toggle = FindNamedComponent<Toggle>("Toggle");
        }
        if (toggle == null)
        {
            Debug.LogWarning("[JarvisTodoItemRow] Complete toggle is missing from TodoItem sample.");
        }

        label = FindNamedComponent<TextMeshProUGUI>("Text");
        if (label == null)
        {
            label = FindNamedComponent<TextMeshProUGUI>("Label");
        }
        if (label == null)
        {
            Debug.LogWarning("[JarvisTodoItemRow] Text label is missing from TodoItem sample.");
        }

        deleteButton = FindNamedComponent<Button>("Delete");
        if (deleteButton == null)
        {
            deleteButton = FindNamedComponent<Button>("BtnDelete");
        }
        if (deleteButton == null)
        {
            Debug.LogWarning("[JarvisTodoItemRow] Delete button is missing from TodoItem sample.");
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogWarning("[JarvisTodoItemRow] CanvasGroup is missing from TodoItem sample.");
        }
    }

    public void Bind(JarvisTodoStore.TodoItem todoItem, JarvisTodoListUI listOwner)
    {
        item = todoItem;
        owner = listOwner;
        if (dragHandle != null)
        {
            dragHandle.Bind(this);
        }

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = item != null && item.isCompleted;
            toggle.onValueChanged.AddListener(OnCompletedChanged);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteClicked);
        }

        RefreshLabel();
    }

    public void BeginHandleDrag(PointerEventData eventData)
    {
        if (owner == null)
        {
            return;
        }

        canvasGroup.alpha = 0.65f;
        canvasGroup.blocksRaycasts = false;
        owner.BeginRowDrag(this);
    }

    public void UpdateHandleDrag(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.UpdateRowDrag(this, eventData);
        }
    }

    public void EndHandleDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        if (owner != null)
        {
            owner.EndRowDrag(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.ShowDetail(this);
        }
    }

    private void OnCompletedChanged(bool isCompleted)
    {
        if (item != null && JarvisTodoStore.Instance != null)
        {
            JarvisTodoStore.Instance.SetCompleted(item.id, isCompleted);
        }
    }

    private void OnDeleteClicked()
    {
        if (item != null && JarvisTodoStore.Instance != null)
        {
            JarvisTodoStore.Instance.DeleteItem(item.id);
        }
    }

    private void RefreshLabel()
    {
        if (label == null || item == null)
        {
            return;
        }

        string prefix = string.IsNullOrEmpty(item.time) ? string.Empty : item.time + " ";
        label.text = prefix + item.content;
        label.fontStyle = item.isCompleted ? FontStyles.Strikethrough : FontStyles.Normal;
    }

    private T FindNamedComponent<T>(string childName) where T : Component
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
}
