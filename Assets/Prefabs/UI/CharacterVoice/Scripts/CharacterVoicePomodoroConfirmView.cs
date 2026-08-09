using TMPro;
using UnityEngine;

// Alarm Confirm의 음성 준비·선택·재생성 동작을 Pomodoro 문구와 별도 프리팹으로 제공한다.
public class CharacterVoicePomodoroConfirmView : CharacterVoiceAlarmConfirmView
{
    protected override void Awake()
    {
        pomodoroMode = true;
        base.Awake();
    }

#if UNITY_EDITOR
    public void EditorBuildPomodoro(
        Sprite roundedSprite = null,
        TMP_FontAsset fontAsset = null,
        Sprite selectionCheckmarkSprite = null)
    {
        EditorBuild(
            roundedSprite,
            fontAsset,
            true,
            selectionCheckmarkSprite);
    }
#endif
}
