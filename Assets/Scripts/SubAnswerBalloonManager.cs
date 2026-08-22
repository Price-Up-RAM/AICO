using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SubAnswerBalloonManager : MonoBehaviour
{
    [SerializeField] private Canvas _canvas; // AnswerBalloon 이미지
    [SerializeField] private GameObject answerBalloon; // AnswerBalloon 이미지
    [SerializeField] private TextMeshProUGUI answerText; // AnswerBalloon 하위의 TMP 텍스트
    [SerializeField] private RectTransform answerBalloonTransform; // AnswerBalloon의 Transform
    public TextMeshProUGUI answerBalloonText; // AnswerBalloon Text의 Transform

    // Image-Sprite
    [SerializeField] private Image answerBalloonImage;
    [SerializeField] private Sprite lightSprite;
    [SerializeField] private Sprite normalSprite;
    public bool isAnswered = false;  // 타 시스템이 해당 balloon 지워도 되는지 체크에 활용

    [SerializeField] private GameObject webImage;  // 답변에 web검색 활용했는지 여부를 보여주는 이미지

    private float hideTimer = 0f; // 타이머 변수 추가
    private bool isActive = false; // 활성화 상태

    private string textKo = "";
    private string textJp = "";
    private string textEn = "";
    private string answerLanguage = "ko";

    // 위치 설정용 변수들 (AnswerBalloonManager와 달리 유연하게)
    private Vector2 targetPosition = Vector2.zero;
    private bool useCustomPosition = false;

    // 싱글톤 인스턴스
    private static SubAnswerBalloonManager instance;

    void Start()
    {
        _canvas = FindObjectOfType<Canvas>();  // 최상위 Canvas

        // UI 최초 비활성화
        if (webImage != null)
            webImage.SetActive(false);
    }

    private void Awake()
    {
        // 싱글톤 패턴 구현
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            return;
        }

        HideAnswerBalloon(); // 시작 시 AnswerBalloon 숨기기
    }

    // 상태 갱신 로직
    private void Update()
    {
        // 타이머 갱신
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
        }

        // 타이머가 완료되면 AnswerBalloon 숨기기
        if (hideTimer <= 0f && isActive)
        {
            HideAnswerBalloon();
        }

        if (isActive)
        {
            UpdateAnswerBalloonPosition();
        }
    }

    // 싱글톤 인스턴스에 접근하는 속성
    public static SubAnswerBalloonManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SubAnswerBalloonManager>();
            }
            return instance;
        }
    }

    // 특정 위치에 AnswerBalloon을 무제한으로 보이기 (새로운 메서드)
    public void ShowAnswerBalloonInfAtPosition(Vector2 position)
    {
        useCustomPosition = true;
        targetPosition = position;
        ShowAnswerBalloonInf();
    }

    // 캐릭터 Transform 기준으로 AnswerBalloon을 무제한으로 보이기
    public void ShowAnswerBalloonInfAtCharacter(RectTransform characterTransform)
    {
        if (characterTransform != null)
        {
            useCustomPosition = true;
            targetPosition = characterTransform.anchoredPosition;
            ShowAnswerBalloonInf();
        }
    }

    // AnswerBalloon을 타이머 무제한으로 보이기
    public void ShowAnswerBalloonInf()
    {
        hideTimer = 99999f;
        answerBalloon.SetActive(true);
        // 텍스트 초기화 제거 - 어차피 ModifyAnswerBalloonText()에서 설정됨
        isActive = true; // 내부 상태 업데이트 (StatusManager 의존성 제거)
        UpdateAnswerBalloonPosition();  // AnswerBalloon 위치 조정
    }

    // 대답중 sprite
    public void ChangeAnswerBalloonSpriteLight()
    {
        if (answerBalloonImage != null && lightSprite != null)
        {
            answerBalloonImage.sprite = lightSprite;
        }
        isAnswered = false;
    }

    // 대답 완료 sprite
    public void ChangeAnswerBalloonSpriteNormal()
    {
        if (answerBalloonImage != null && normalSprite != null)
        {
            answerBalloonImage.sprite = normalSprite;
        }
        isAnswered = true;
    }

    // AnswerBalloon을 보이고 텍스트를 초기화하는 함수
    public void ShowAnswerBalloon()
    {
        answerBalloon.SetActive(true);
        // 텍스트 초기화 제거 - 어차피 ModifyAnswerBalloonText()에서 설정됨
        isActive = true; // 내부 상태 업데이트
        UpdateAnswerBalloonPosition();  // AnswerBalloon 위치 조정
    }

    // AnswerBalloon의 텍스트를 수정
    public void ModifyAnswerBalloonText()
    {
        if (answerLanguage == "ko")
        {
            answerText.text = textKo; // 텍스트 변경
        }
        else if (answerLanguage == "jp")
        {
            answerText.text = textJp; // 텍스트 변경
        }
        else
        {
            answerText.text = textEn; // 텍스트 변경
        }

        // 높이 조정
        if (answerBalloonText != null && answerBalloonTransform != null)
        {
            float textHeight = answerBalloonText.preferredHeight;
            answerBalloonTransform.sizeDelta = new Vector2(answerBalloonTransform.sizeDelta.x, textHeight + 120);
        }
    }

    // 언어전환을 고려한 string setting
    public void ModifyAnswerBalloonTextInfo(string replyKo, string replyJp, string replyEn)
    {
        answerLanguage = SettingManager.Instance.settings.ui_language; // 표시 언어 초기화[ko, en, jp]
        textKo = replyKo;
        textJp = replyJp;
        textEn = replyEn;
    }

    // 답변풍선 언어 변경
    public void changeAnswerLanguage()
    {
        if (answerLanguage == "ko")
        {
            answerLanguage = "jp";
        }
        else if (answerLanguage == "jp")
        {
            answerLanguage = "en";
        }
        else
        {
            answerLanguage = "ko";
        }
        // 바뀐 언어로 AnswerBalloon 다시 세팅
        ModifyAnswerBalloonText();
    }

    // 현재(마지막) 오디오 재생 후 AnswerBalloon을 숨기는 타이머 설정
    public void HideAnswerBalloonAfterAudio()
    {
        AudioClip clip = VoiceManager.Instance.GetAudioClip();

        if (clip != null)
        {
            hideTimer = clip.length + 0.5f; // 타이머를 오디오 재생 시간 + 0.5초로 설정
        }
    }

    // 특정 시간 후 AnswerBalloon 숨기기
    public void HideAnswerBalloonAfterDelay(float delay)
    {
        hideTimer = delay;
    }

    // AnswerBalloon을 숨기는 함수
    public void HideAnswerBalloon()
    {
        hideTimer = 0f;  // inf용 초기화
        answerBalloon.SetActive(false);
        isActive = false; // 내부 상태 업데이트
        useCustomPosition = false; // 커스텀 위치 사용 해제
    }

    // AnswerBalloon의 위치를 업데이트하는 함수 (유연한 위치 설정)
    private void UpdateAnswerBalloonPosition()
    {
        Vector2 finalPosition;

        if (useCustomPosition)
        {
            // 커스텀 위치 사용
            finalPosition = targetPosition;
        }
        else
        {
            // 기본 위치 (화면 중앙)
            finalPosition = Vector2.zero;
        }

#if UNITY_ANDROID || UNITY_EDITOR
        // MR: 배치는 MRBalloonWorldFollow가 **월드 좌표로** 전담한다.
        //
        // 여기서 anchoredPosition을 대입하면 매 프레임 말풍선을 부모 원점으로 끌어내려
        // MR 배치와 싸운다 (Kickoff Guide §4-45). Update()가 LateUpdate()보다 먼저 돌기 때문에
        // **평소에는 MRBalloonWorldFollow가 마지막에 덮어써서 화면상 멀쩡해 보이지만**,
        // 잡기(grab)로 위치 소유권이 사용자에게 넘어가 MR 쪽이 쓰기를 멈추는 순간
        // 이 대입만 남아 말풍선이 바닥(부모 원점)으로 떨어진다. 2026-08-22 실기에서 확인.
        //
        // ⚠ UIPositionManager 쪽에서 값을 (0,0)으로 바꾸는 것만으로는 부족하다 —
        //    값이 아니라 **대입 자체**를 멈춰야 한다.
        return;
#else
        answerBalloonTransform.anchoredPosition = UIPositionManager.Instance.GetBalloonAnchoredPositionByPosition(finalPosition);
#endif
    }

    // 위치를 직접 설정하는 메서드
    public void SetCustomPosition(Vector2 position)
    {
        useCustomPosition = true;
        targetPosition = position;
        if (isActive)
        {
            UpdateAnswerBalloonPosition();
        }
    }

    // 커스텀 위치 사용 해제
    public void ClearCustomPosition()
    {
        useCustomPosition = false;
    }

    // Web Image 관련 메서드들
    public void ShowWebImage()
    {
        if (webImage != null)
            webImage.SetActive(true);
    }
    
    public void HideWebImage()
    {
        if (webImage != null)
            webImage.SetActive(false);
    }

    // 활성화 상태 확인
    public bool IsActive()
    {
        return isActive;
    }
}
