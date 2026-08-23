using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class AnswerBalloonManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static AnswerBalloonManager instance;
    public static AnswerBalloonManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AnswerBalloonManager>();
            }
            return instance;
        }
    }

    [SerializeField] private GameObject answerBalloon; // AnswerBalloon 이미지
    [SerializeField] private TextMeshProUGUI answerText; // AnswerBalloon 하위의 TMP 텍스트
    [SerializeField] public RectTransform characterTransform; // AnswerBalloon이 표시될 캐릭터의 Transform
    [SerializeField] private RectTransform answerBalloonTransform; // AnswerBalloon의 Transform
    public TextMeshProUGUI answerBalloonText; // AnswerBalloon Text의 Transform

    // Image-Sprite
    [SerializeField] private Image answerBalloonImage;
    [SerializeField] private Sprite lightSprite;
    [SerializeField] private Sprite normalSprite;
    public bool isAnswered = false;  // 타 시스템이 해당 balloon 지워도 되는지 체크에 활용

    [SerializeField] private GameObject webImage;  // 답변에 web검색 활용했는지 여부를 보여주는 이미지

    public float hideTimer = 0f; // 타이머 변수 추가

    private string textKo = "";
    private string textJp = "";
    private string textEn = "";
    private string answerLanguage = "ko";

    void Start()
    {
        // UI 최초 비활성화
        webImage.SetActive(false);
    }

    private void Awake()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        // MR에서는 Game20QPanel 등 비활성 UI 부모에 묶여 같이 꺼지는 것을 막기 위해 최상단으로 뺌
        transform.SetParent(null);
#endif

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
        if (hideTimer <= 0f && StatusManager.Instance.IsAnswering)
        {
            HideAnswerBalloon();
        }

        if (StatusManager.Instance.IsAnswering)
        {
            UpdateAnswerBalloonPosition();
        }

#if UNITY_ANDROID || UNITY_EDITOR
        // MR에서는 드래그(IsPicking) 중에 말풍선을 끄지 않고 따라가게 둔다.
        if (StatusManager.Instance.IsListening || StatusManager.Instance.IsAsking)
        {
            HideAnswerBalloon();
        }
#else
        if (StatusManager.Instance.IsPicking || StatusManager.Instance.IsListening || StatusManager.Instance.IsAsking)
        {
            HideAnswerBalloon();
        }
#endif
    }

    // AnswerBalloon을 타이머 무제한으로 보이기
    public void ShowAnswerBalloonInf()
    {
        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.ShowInf();
            return;
        }

        hideTimer = 99999f;
        answerBalloon.SetActive(true);
        answerText.text = string.Empty; // 텍스트 초기화
        StatusManager.Instance.IsAnswering = true; // StatusManager 상태 업데이트
        UpdateAnswerBalloonPosition();  // AnswerBalloon 위치 조정하
    }

    // 대답중 sprite
    public void ChangeAnswerBalloonSpriteLight()
    {
        answerBalloonImage.sprite = lightSprite;
        isAnswered = false;
    }

    // 대답 완료 sprite
    public void ChangeAnswerBalloonSpriteNormal()
    {
        answerBalloonImage.sprite = normalSprite;
        isAnswered = true;
    }

    // AnswerBalloon을 보이고 텍스트를 초기화하는 함수
    public void ShowAnswerBalloon()
    {
        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.Show();
            return;
        }

        answerBalloon.SetActive(true);
        answerText.text = string.Empty; // 텍스트 초기화
        StatusManager.Instance.IsAnswering = true; // StatusManager 상태 업데이트
        UpdateAnswerBalloonPosition();  // AnswerBalloon 위치 조정하
    }

    // AnswerBalloon의 텍스트를 수정
    public void ModifyAnswerBalloonText()
    {
        // 표시할 텍스트 선택
        string displayText = textEn;
        if (answerLanguage == "ko")
        {
            displayText = textKo;
        }
        else if (answerLanguage == "jp")
        {
            displayText = textJp;
        }

        // 표시언어 답변이 비어있으면(번역 스킵/실패 시 빈값 계약) 비어있지 않은 언어로 폴백
        if (string.IsNullOrEmpty(displayText))
        {
            if (!string.IsNullOrEmpty(textKo)) displayText = textKo;
            else if (!string.IsNullOrEmpty(textJp)) displayText = textJp;
            else if (!string.IsNullOrEmpty(textEn)) displayText = textEn;
        }

        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.ModifyText(displayText);
            return;
        }

        answerText.text = displayText;

        // 높이 조정
        float textHeight = answerBalloonText.preferredHeight;
        answerBalloonTransform.sizeDelta = new Vector2(answerBalloonTransform.sizeDelta.x, textHeight + 120);
    }

    // 언어전환을 고려한 string setting
    public void ModifyAnswerBalloonTextInfo(string replyKo, string replyJp, string replyEn)
    {
        // Debug.Log("ModifyAnswerBalloonTextInfo Start : " + replyEn);
        answerLanguage = SettingManager.Instance.settings.ui_language; // 표시 언어 초기화[ko, en, jp]
        textKo = replyKo;
        textJp = replyJp;
        textEn = replyEn;
    }

    // 답변풍선 언어 변경 (번역 스킵 등으로 답변이 비어있는 언어는 순환에서 제외)
    public void changeAnswerLanguage()
    {
        string[] langOrder = { "ko", "jp", "en" };
        int currentIdx = System.Array.IndexOf(langOrder, answerLanguage);
        if (currentIdx < 0)
        {
            currentIdx = 2;  // 알 수 없는 언어면 en으로 간주
        }

        // 다음 언어부터 순환하며 텍스트가 있는 언어 탐색 (모두 비어있으면 현재 언어 유지)
        for (int i = 1; i <= langOrder.Length; i++)
        {
            string nextLang = langOrder[(currentIdx + i) % langOrder.Length];
            string nextText = nextLang == "ko" ? textKo : (nextLang == "jp" ? textJp : textEn);
            if (!string.IsNullOrEmpty(nextText))
            {
                answerLanguage = nextLang;
                break;
            }
        }
        // 바뀐 언어로 AnswerBalloon 다시 세팅
        ModifyAnswerBalloonText();
    }

    // 최근대화 삭제후 창 종료하기
    public void DeleteRecentDialogue()
    {
        MemoryManager.Instance.DeleteRecentDialogue();
        MemoryManager.Instance.DeleteRecentDialogue();

        HideAnswerBalloon();
    }

    // 대화 재생성
    public void ChatRegenerate()
    {
        // 기존 음성 중지 및 초기화 (세션까지 취소 — 새 대화 세션 시작 전 재공급 차단)
        TTSManager.Instance.CancelTtsSession();

        string input = APIManager.Instance.GetQueryOrigin(GameManager.Instance.chatIdx);
        GameManager.Instance.chatIdx += 1;
        GameManager.Instance.chatIdxRegenerateCount += 1;
        Debug.Log("Regenerate 텍스트 (" + GameManager.Instance.chatIdx.ToString() + ") : " + input);
        APIManager.Instance.CallConversationStream(input, GameManager.Instance.chatIdx.ToString());

        // 이미 대화 저장했을 경우 삭제
        if (isAnswered)
        {
            DeleteRecentDialogue();
        }
        HideAnswerBalloon();
    }

    // 채팅로그 창 열기
    public void ShowChatHistory()
    {
        UIManager.Instance.ShowChatHistory();
    }

    // 현재(마지막) 오디오 재생 후 AnswerBalloon을 숨기는 코루틴 호출
    public void HideAnswerBalloonAfterAudio()
    {
        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.HideAfterAudio();
            return;
        }

        AudioClip clip = VoiceManager.Instance.GetAudioClip();

        if (clip != null)
        {
            hideTimer = clip.length + 0.5f; // 타이머를 오디오 재생 시간 + 0.5초로 설정
        }
    }

    // AnswerBalloon을 숨기는 함수
    public void HideAnswerBalloon()
    {
        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.Hide();
            StatusManager.Instance.IsAnswering = false;
            return;
        }

        hideTimer = 0f;  // inf용 초기화
        answerBalloon.SetActive(false);
        StatusManager.Instance.IsAnswering = false;
    }

    // AnswerBalloon의 위치를 캐릭터 바로 위로 조정하는 함수
    private void UpdateAnswerBalloonPosition()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        // MR: 배치는 MRBalloonWorldFollow가 **월드 좌표로** 전담한다.
        //
        // 여기서 anchoredPosition을 대입하면 매 프레임 말풍선을 부모 원점으로 끌어내려
        // MR 배치와 싸운다 (Kickoff Guide §4-45). Update()가 LateUpdate()보다 먼저 돌기 때문에
        // **평소에는 MRBalloonWorldFollow가 마지막에 덮어써서 화면상 멀쩡해 보이지만**,
        // 잡기(grab)로 위치 소유권이 사용자에게 넘어가 MR 쪽이 쓰기를 멈추는 순간
        // 이 대입만 남아 말풍선이 바닥(부모 원점)으로 떨어진다. 2026-08-22 실기에서 확인.
        //
        // 2026-08-22 변경: 예전에는 `FindFirstObjectByType<MRCharacterWorldRoot>() != null`로
        // 판정했는데, 이게 **매 프레임 씬 전수 검색**이라 비쌌다. 이 저장소는 MR 전용이므로
        // (§3 분리 정책) 전처리기 분기로 충분하다.
        return;
#else
        answerBalloonTransform.anchoredPosition = UIPositionManager.Instance.GetBalloonAnchoredPosition(characterTransform);
#endif
    }

    public void ShowWebImage()
    { 
        webImage.SetActive(true);
    }
    
    public void HideWebImage()
    {
        webImage.SetActive(false);
    }

    // AI Choice 닫힌 후 다시 열기 위한 함수
    public void ShowAIChoicesAgain()
    {
        ChoiceManager.Instance.ShowLastAIChoices();
    }
}
