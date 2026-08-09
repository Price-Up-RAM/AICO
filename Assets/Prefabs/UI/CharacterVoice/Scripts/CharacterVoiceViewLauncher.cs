using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// UIManager 씬 참조에 종속되지 않도록 CharacterDetail 전용 창을 CanvasUI에 한 번만 생성한다.
public static class CharacterVoiceViewLauncher
{
    private static CharacterVoiceAlarmView alarmView;
    private static CharacterVoiceAlarmConfirmView alarmConfirmView;
    private static CharacterVoicePomodoroView pomodoroView;
    private static CharacterVoicePomodoroConfirmView pomodoroConfirmView;

    public static void ShowAlarm(
        string characterName,
        string refId,
        string language,
        string speed,
        CharacterAlarmVoiceCatalog catalog)
    {
        CharacterVoiceAlarmView view = GetOrCreate(
            ref alarmView,
            "CharacterVoiceAlarmView",
            "CharacterVoice/CharacterVoiceAlarmView");
        if (view == null) return;
        view.Show(characterName, refId, language, speed, catalog);
        Center(view.transform as RectTransform);
    }

    public static void ShowAlarmConfirm(
        List<string> candidates,
        string characterName,
        string refId,
        string language,
        string speed,
        Action<List<CharacterVoiceAlarmConfirmView.PreparedAlarm>> confirmed)
    {
        CharacterVoiceAlarmConfirmView view = GetOrCreate(
            ref alarmConfirmView,
            "CharacterVoiceAlarmConfirmView",
            "CharacterVoice/CharacterVoiceAlarmConfirmView");
        if (view == null) return;
        view.Open(candidates, characterName, refId, language, speed, confirmed);
        Center(view.transform as RectTransform);
    }

    public static void ShowPomodoro(
        string characterName,
        string refId,
        string language,
        string speed,
        CharacterPomodoroVoiceCatalog catalog)
    {
        CharacterVoicePomodoroView view = GetOrCreate(
            ref pomodoroView,
            "CharacterVoicePomodoroView",
            "CharacterVoice/CharacterVoicePomodoroView");
        if (view == null) return;
        view.Show(characterName, refId, language, speed, catalog);
        Center(view.transform as RectTransform);
    }

    public static void ShowPomodoroConfirm(
        List<string> candidates,
        string characterName,
        string refId,
        string language,
        string speed,
        Action<List<CharacterVoiceAlarmConfirmView.PreparedAlarm>> confirmed)
    {
        CharacterVoicePomodoroConfirmView view = GetOrCreate(
            ref pomodoroConfirmView,
            "CharacterVoicePomodoroConfirmView",
            "CharacterVoice/CharacterVoicePomodoroConfirmView");
        if (view == null) return;
        view.Open(candidates, characterName, refId, language, speed, confirmed);
        Center(view.transform as RectTransform);
    }

    private static T GetOrCreate<T>(ref T cached, string objectName, string resourcePath)
        where T : Component
    {
        if (cached != null)
        {
            return cached;
        }

        T[] existing = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene.IsValid())
            {
                cached = existing[i];
                return cached;
            }
        }

        Transform parent =
            CanvasManager.Instance != null && CanvasManager.Instance.canvasUI != null
                ? CanvasManager.Instance.canvasUI.transform
                : null;
        if (parent == null)
        {
            Debug.LogWarning($"[CharacterVoice] CanvasUI is unavailable. {objectName} was not opened.");
            return null;
        }

        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        GameObject instance;
        if (prefab != null)
        {
            instance = UnityEngine.Object.Instantiate(prefab, parent);
        }
        else
        {
            instance = new GameObject(objectName, typeof(RectTransform), typeof(CanvasGroup));
            instance.layer = 5;
            instance.transform.SetParent(parent, false);
            instance.SetActive(false);
            instance.AddComponent<T>();
        }

        Canvas panelCanvas = instance.GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = instance.AddComponent<Canvas>();
        }
        panelCanvas.overrideSorting = false;
        if (instance.GetComponent<GraphicRaycaster>() == null)
        {
            instance.AddComponent<GraphicRaycaster>();
        }

        instance.name = objectName;
        instance.transform.localScale = Vector3.one;
        cached = instance.GetComponent<T>();
        return cached;
    }

    private static void Center(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.SetAsLastSibling();
    }
}
