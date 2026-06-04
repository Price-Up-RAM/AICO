using UnityEngine;
using UnityEngine.UI;

public class ToggleObjectSwapper : MonoBehaviour
{
    [Header("UI 연결")]
    public Toggle targetToggle;

    [Header("상태별 게임오브젝트")]
    [Tooltip("토글이 켜졌을 때(ON) 보여줄 최상위 오브젝트 (배경+체크+텍스트 등)")]
    public GameObject onStateObject;  
    
    [Tooltip("토글이 꺼졌을 때(OFF) 보여줄 최상위 오브젝트 (배경+텍스트 등)")]
    public GameObject offStateObject; 

    void Start()
    {
        targetToggle = GetComponent<Toggle>(); // 같은 게임오브젝트에 Toggle 컴포넌트가 있다고 가정
        if (targetToggle != null)
        {
            // 1단계: 시작할 때 현재 토글의 체크 상태에 맞춰 오브젝트 초기화
            UpdateObjectState(targetToggle.isOn);
            
            // 2단계: 토글 값이 변경될 때마다 실행되도록 리스너 연결
            targetToggle.onValueChanged.AddListener(UpdateObjectState);
        }
    }

    // 토글 상태에 따라 게임오브젝트를 껐다 켜는 함수
    public void UpdateObjectState(bool isOn)
    {
        // 토글이 켜지면 ON 오브젝트 활성화, OFF 오브젝트 비활성화
        if (onStateObject != null) onStateObject.SetActive(isOn);
        if (offStateObject != null) offStateObject.SetActive(!isOn);
    }
}