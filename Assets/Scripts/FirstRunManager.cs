using UnityEngine;
using System.Collections.Generic;

public class FirstRunManager : MonoBehaviour
{
    void Start()
    {
        int installStatus = SettingManager.Instance.GetInstallStatus();

        if (installStatus == 0)
        {
            // 최초 실행
            Debug.Log("[FirstRun] 최초 실행 감지 → 튜토리얼 시작");

            // 
            // RunInitialSetup(); // 언어 감지, InstallState 판별
            // StartFirstRunScenario(); // F01 시나리오 진입
        }
        else
        {
            // 이미 설정된 상태
            // Debug.Log("[FirstRun] 재실행 → 일반 모드 진입");
        }
    }

    // private void RunInitialSetup()
    // {
    //     // 언어 자동 감지 및 저장
    //     string lang = DetectInitialLanguage();
    //     SettingManager.Instance.SetLanguage(lang);

    //     // 설치 상태 감지
    //     var state = InstallProbe.Detect();
    //     SettingManager.Instance.SetInstallState(state);

    //     // 최초 실행 표시값 변경
    //     SettingManager.Instance.SetInstallStatus(1); // 0→1 변경
    //     SettingManager.Instance.Save();
    // }

    // private void StartFirstRunScenario()
    // {
    //     ScenarioManager.Instance.StartScenario("F01");
    // }
}
