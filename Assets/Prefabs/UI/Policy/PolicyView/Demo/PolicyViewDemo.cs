using UnityEngine;

/// <summary>
/// PolicyView 데모 컨트롤러 (데모 씬 전용). SettingManager 없이 언어 전환을 검증한다.
///
/// 검증 포인트:
///  - 좌측 탭 4개(이용약관/개인정보처리방침/AI 고지/AI 운영정책) 전환
///  - 우측 본문 스크롤(휠/드래그/스크롤바)
///  - 헤더 언어 버튼(× 왼쪽) 클릭 = ko→jp→en 순환 / 키 1/2/3 = ko/en/jp 직접 지정
///  - 데모 씬엔 SettingManager가 없으므로 최초 표시 언어는 en 폴백이 정상
/// </summary>
public class PolicyViewDemo : MonoBehaviour
{
    [SerializeField] private PolicyView view;

    private void Start()
    {
        if (view == null)
        {
            view = FindObjectOfType<PolicyView>();
        }
        if (view == null)
        {
            Debug.LogWarning("[PolicyViewDemo] PolicyView를 찾지 못했습니다.");
            return;
        }
        view.Show();
    }

    private void Update()
    {
        if (view == null)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            view.SetLanguageOverride("ko");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            view.SetLanguageOverride("en");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            view.SetLanguageOverride("jp");
        }
    }

#if UNITY_EDITOR
    public void EditorSet(PolicyView target)
    {
        view = target;
    }
#endif
}
