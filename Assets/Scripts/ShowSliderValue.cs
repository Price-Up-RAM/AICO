using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro를 사용하기 위한 네임스페이스

public class ShowSliderValue : MonoBehaviour
{
    [Header("UI 연결")]
    public Slider targetSlider;       // 값을 가져올 슬라이더
    public TextMeshProUGUI valueText; // 값을 표시할 TMPro 텍스트

    [Header("텍스트 설정")]
    public string prefix = "";        // 숫자 앞에 붙을 문자 (예: "Volume: ")
    public string postfix = "";       // 숫자 뒤에 붙을 문자 (예: "%")
    
    // 소수점 표시 방식 설정 (F0 = 정수, F1 = 소수점 첫째 자리까지 등)
    public string numberFormat = "F0"; 

    void Start()
    {
        // 1단계: 연결된 슬라이더가 있는지 검증
        if (targetSlider != null)
        {
            // 시작할 때 현재 슬라이더 값으로 텍스트 초기화
            UpdateText(targetSlider.value);
            
            // 슬라이더 값이 변경될 때마다 UpdateText 함수가 자동으로 실행되도록 이벤트 리스너 추가
            targetSlider.onValueChanged.AddListener(UpdateText);
        }
    }

    // 슬라이더 값이 변경될 때 호출되는 함수
    public void UpdateText(float value)
    {
        // 2단계: 연결된 텍스트 컴포넌트가 있는지 검증
        if (valueText != null)
        {
            // prefix + 포맷이 적용된 숫자 문자열 + postfix 형태로 결합하여 출력
            valueText.text = prefix + value.ToString(numberFormat) + postfix;
        }
    }
}