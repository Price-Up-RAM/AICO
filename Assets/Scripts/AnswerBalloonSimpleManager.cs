using System.Collections;
using UnityEngine;
using TMPro;
using System;

public class AnswerBalloonSimpleManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static AnswerBalloonSimpleManager instance;
    public static AnswerBalloonSimpleManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AnswerBalloonSimpleManager>();
            }
            return instance;
        }
    }

    [SerializeField] private GameObject answerBalloonSimple; // AnswerBalloonSimple 이미지
    [SerializeField] private TextMeshProUGUI answerText; // AnswerBalloonSimple 하위의 TMP 텍스트
    [SerializeField] public RectTransform characterTransform; // AnswerBalloonSimple이 표시될 캐릭터의 Transform
    [SerializeField] private RectTransform answerBalloonSimpleTransform; // AnswerBalloonSimple의 Transform
    public TextMeshProUGUI answerBalloonSimpleText; // AnswerBalloonSimple Text의 Transform

    private float hideTimer = 0f; // 타이머 변수 추가

    private string textKo = "";
    private string textJa = "";
    private string textEn = "";
    private Coroutine timedHideCoroutine;

    private void Awake()
    {
#if UNITY_ANDROID || UNITY_EDITOR
        transform.SetParent(null);
#endif
        HideAnswerBalloonSimple(); // 시작 시 AnswerBalloonSimple 숨기기
    }

    // 상태 갱신 로직
    private void Update()
    {
        // 타이머 갱신
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;
        }

        // 타이머가 완료되면 AnswerBalloonSimple 숨기기
        if (hideTimer <= 0f && StatusManager.Instance.IsAnsweringSimple)
        {
            HideAnswerBalloonSimple();
        }

        if (StatusManager.Instance.IsAnsweringSimple)
        {
            UpdateAnswerBalloonSimplePosition();
        }

#if UNITY_ANDROID || UNITY_EDITOR
        if (StatusManager.Instance.IsListening || StatusManager.Instance.IsAsking )
        {
            HideAnswerBalloonSimple();
        }
#else
        if (StatusManager.Instance.IsPicking || StatusManager.Instance.IsListening || StatusManager.Instance.IsAsking )
        {
            HideAnswerBalloonSimple();
        }
#endif
    }

    // AnswerBalloonSimple을 타이머 무제한으로 보이기
    public void ShowAnswerBalloonSimpleInf()
    {
        // 기존의 balloon이 있을경우 Hide
        if (AnswerBalloonManager.Instance.isAnswered) AnswerBalloonManager.Instance.HideAnswerBalloon();
        ChatBalloonManager.Instance.HideChatBalloon();

        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.ShowInf();
            return;
        }

        hideTimer = 99999f;
        answerBalloonSimple.SetActive(true);
        answerText.text = string.Empty; // 텍스트 초기화
        StatusManager.Instance.IsAnsweringSimple = true; // StatusManager 상태 업데이트
        UpdateAnswerBalloonSimplePosition();  // AnswerBalloonSimple 위치 조정하
    }

    // AnswerBalloonSimple을 보이고 텍스트를 초기화하는 함수
    public void ShowAnswerBalloonSimple()
    {
        // 기존의 balloon이 있을경우 Hide
        if (AnswerBalloonManager.Instance.isAnswered) AnswerBalloonManager.Instance.HideAnswerBalloon();
        ChatBalloonManager.Instance.HideChatBalloon();

        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.Show();
            return;
        }

        answerBalloonSimple.SetActive(true);
        answerText.text = string.Empty; // 텍스트 초기화
        StatusManager.Instance.IsAnsweringSimple = true; // StatusManager 상태 업데이트
        UpdateAnswerBalloonSimplePosition();  // AnswerBalloonSimple 위치 조정하
    }

    // AnswerBalloonSimple의 텍스트를 수정하고 오디오를 재생하는 함수
    public void ModifyAnswerBalloonSimpleText(string text)
    {
        // 자동번역 시도
        text = LanguageManager.Instance.Translate(text);

        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.ModifyText(text);
            return;
        }

        answerText.text = text; // 텍스트 변경

        // 높이 조정
        float textHeight = answerBalloonSimpleText.preferredHeight;
        answerBalloonSimpleTransform.sizeDelta = new Vector2(answerBalloonSimpleTransform.sizeDelta.x, textHeight + 60);
    }

    // 언어전환을 고려한 string setting
    public void ModifyAnswerBalloonSimpleTextInfo(string replyKo, string replyJa, string replyEn)
    {
        textKo = replyKo;
        textJa = replyJa;
        textEn = replyEn;
    }

    public void ShowAnswerBalloonSimpleForSeconds(string text, float seconds)
    {
        ShowAnswerBalloonSimpleInf();
        ModifyAnswerBalloonSimpleText(text);

        if (timedHideCoroutine != null)
        {
            StopCoroutine(timedHideCoroutine);
        }

        timedHideCoroutine = StartCoroutine(HideAfterSeconds(Mathf.Max(1f, seconds)));
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        timedHideCoroutine = null;
        HideAnswerBalloonSimple();
    }
    
    // 현재(마지막) 오디오 재생 후 AnswerBalloonSimple을 숨기는 코루틴 호출
    public void HideAnswerBalloonSimpleAfterAudio()
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

    // AnswerBalloonSimple을 숨기는 함수
    public void HideAnswerBalloonSimple()
    {
        if (timedHideCoroutine != null)
        {
            StopCoroutine(timedHideCoroutine);
            timedHideCoroutine = null;
        }

        // 서브 캐릭터 라우팅
        GameObject activeChar = CharManager.Instance?.GetActiveCharacter();
        if (activeChar != null && CharManager.Instance != null && CharManager.Instance.activeCharacter != null)
        {
            SubAnswerBalloonSimpleController subController = SubAnswerBalloonSimpleManager.Instance?.GetOrCreateController(CharManager.Instance.activeCharacter);
            if (subController != null)
            {
                subController.HideAnswerBalloonSimple();
                return;
            }
        }

        // Operator 모드일 경우 PortraitBalloonSimpleManager로 라우팅
        if (ChatModeManager.Instance.IsOperatorMode())
        {
            PortraitBalloonSimpleManager.Instance.Hide();
            StatusManager.Instance.IsAnsweringSimple = false;
            return;
        }

        hideTimer = 0f;  // inf용 초기화
        answerBalloonSimple.SetActive(false);
        StatusManager.Instance.IsAnsweringSimple = false; 
    }

    // AnswerBalloonSimple의 위치를 캐릭터 바로 위로 조정하는 함수
    private void UpdateAnswerBalloonSimplePosition()
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
        // ⚠ UIPositionManager 쪽에서 값을 (0,0)으로 바꾸는 것만으로는 부족하다 —
        //    값이 아니라 **대입 자체**를 멈춰야 한다.
        return;
#else
        answerBalloonSimpleTransform.anchoredPosition = UIPositionManager.Instance.GetBalloonAnchoredPosition(characterTransform);
#endif
    }
}
