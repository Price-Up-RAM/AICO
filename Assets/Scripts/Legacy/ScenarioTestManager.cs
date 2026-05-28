// 프롬프트 format용으로 legacy에 보관
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ScenarioTestManager : MonoBehaviour
{
    public static ScenarioTutorialManager instance;
    public static ScenarioTutorialManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ScenarioTutorialManager>();
            }
            return instance;
        }
    }

    // 튜토리얼 시작
    public void StartTutorial()
    {
        Debug.Log("튜토리얼 시작됨");

        if (!SettingManager.Instance.settings.isShowTutorialOnChat)
        {
            Debug.Log("튜토리얼 비활성화 상태");
            return;
        }

        // PC인지 아닌지 확인
        RuntimePlatform platform = Application.platform;

        if (platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor)
        {
            // PC 플랫폼
            TutorialFlowPC();
        }
        else
        {
            // 모바일 또는 기타 플랫폼
            // StartCoroutine(Scenario_00_MobilePlatformCheck());
        }
    }

    // 시나리오 선택지 반영 (콜백)
    public void OnChoiceSelected(string scenarioId, int index)
    {
        Debug.Log($"[시나리오 {scenarioId}] 선택지에서 {index}번 선택됨");

        AnswerBalloonSimpleManager.Instance.HideAnswerBalloonSimple();
        VoiceManager.Instance.StopAudio();

        switch (scenarioId)
        {
            case "X00_test_start":
                if (index == 0)
                {
                    StartCoroutine(X00_test_start_OnConfirmYes());
                }
                else if (index == 1)
                {
                    StartCoroutine(X00_test_start_OnConfirmNo());
                }
                else if (index == 2)
                {
                    Debug.Log("튜토리얼 종료");
                }
                break;

            default:
                Debug.LogWarning("정의되지 않은 시나리오 선택 분기");
                break;
        }
    }
    // PC에서는 기본 시나리오 생략 가능
    private void TutorialFlowPC()
    {
        Debug.Log("PC 플랫폼 : TutorialFlowPC Start");
        StartCoroutine(TestWorkflow());

        // float duration = Narration("01_select_compute_mode", "To talk with me, we need to connect to an AI server. Which method would you like to use?");

        // StartCoroutine(ShowChoiceAfterTime(3, "01_select_compute_mode", duration));
    }

    // 대사를 보여주고 wav 재생하고 길이 반환.
    private float Narration(string scenarioId, string dialogue)
    {
        // 안내문코드 : 01_select_compute_mode_
        // 안내문 : 저와 대화하려면 AI 서버가 꼭 필요해요. 어떤 방식으로 연결해볼까요?
        AnswerBalloonSimpleManager.Instance.ShowAnswerBalloonSimpleInf();
        AnswerBalloonSimpleManager.Instance.ModifyAnswerBalloonSimpleText(dialogue);

        // 음성재생
        string file_name = scenarioId + "_ja.wav";
        if (SettingManager.Instance.settings.ui_language == "ko")
        {
            file_name = scenarioId + "_ko.wav";
        }
        else if (SettingManager.Instance.settings.ui_language == "en")
        {
            file_name = scenarioId + "_en.wav";
        }

        string filePath = Path.Combine("Audio", file_name);
        VoiceManager.Instance.PlayWavAudioFromPath(filePath);  // 음성 재생

        // 선택지 보여주기
        float duration = 3f;
        try
        {
            duration = UtilAudio.GetWavDurationInSeconds(filePath);
            Debug.Log(file_name + " 길이 : " + duration);
        }
        catch (System.Exception)
        {

        }
        duration += 0.5f;

        return duration;
    }

    private IEnumerator ShowChoiceAfterTime(int btnNumber, string choiceScenario, float time)
    {
        yield return new WaitForSeconds(time);
        ChoiceManager.Instance.ShowChoice(btnNumber, choiceScenario);
    }

    private IEnumerator TestWorkflow()
    {
        float d1 = Narration("X00_intro_greeting", "처음 뵙겠습니다. 선생님");
        yield return new WaitForSeconds(d1);

        float d2 = Narration("X00_intro_help", "무얼 도와드릴까요?");
        yield return new WaitForSeconds(d2);

        yield return new WaitForSeconds(0.2f);
        ChoiceManager.Instance.ShowChoice(3, "X00_test_start");
    }

    private IEnumerator X00_test_start_OnConfirmYes()
    {
        float duration = Narration("X00_yes_response", "네, 오늘도 도와드릴게요");
        yield return new WaitForSeconds(duration);

        // TODO: 이후 워크플로우 연결
        Debug.Log("다음 워크플로우 연결 예정");
    }

    private IEnumerator X00_test_start_OnConfirmNo()
    {
        float duration = Narration("X00_no_response", "그러면 대기하고 있을게요");
        yield return new WaitForSeconds(duration);

        Debug.Log("튜토리얼 종료");
    }
}
