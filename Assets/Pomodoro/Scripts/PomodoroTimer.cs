using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class PomodoroTimer : MonoBehaviour
{
    [Header("UI 연결")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI stateText;
    public Button startButton;
    public Button pauseButton;
    public Button resetButton;
    public TMP_InputField workInputField;   // 집중 시간 (분)
    public TMP_InputField breakInputField;  // 휴식 시간 (분)
    public Image tomatoImage;
    public AudioClip alarmClip;

    [Header("색상 설정")]
    [SerializeField] private Color workStartColor = Color.green;
    [SerializeField] private Color workEndColor   = new Color(1f, 0.4f, 0.2667f);
    [SerializeField] private AnimationCurve colorCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("타이머 기본값")]
    public int defaultWorkMinutes  = 25;
    public int defaultBreakMinutes = 5;
    private const int TotalCycles  = 4;

    [Header("급속 기능")]
    [SerializeField] private Button boostButton;
    [SerializeField] private float boostRotateSpeed = 360f;

    // 상태 메시지
    [Header("상태 메시지")]
    [SerializeField] private string[] idleMessages    = { "집중할 준비 됐나요?", "오늘도 파이팅!", "잠깐, 숨 고르고 시작해요." };
    [SerializeField] private string[] workMessages    = { "지금 이 순간에 집중!", "조금만 더!", "You got this." };
    [SerializeField] private string[] breakMessages   = { "토마토가 회복 중... 🍅", "잠깐 쉬어요!", "숨 한번 크게 쉬어요." };
    [SerializeField] private string[] pausedMessages  = { "잠깐 쉬는 중...", "곧 돌아올게요.", "숨 한번 크게 쉬어요." };
    private int lastMessageIndex = -1;

    // 내부 상태
    private enum Phase { Work, Break }
    private bool running   = false;
    private bool boosting  = false;
    private Phase phase    = Phase.Work;
    private int currentCycle = 0;   // 완료된 집중 사이클 수
    private int totalSeconds;
    private int remaining;
    private Coroutine timerCoroutine;
    private AudioSource audioSource;

    // 급속 가속
    private float boostHeldTime      = 0f;
    private float currentRotateSpeed = 0f;

    // ───────────────────────────────────────────
    private void Awake()
    {
        startButton.onClick.AddListener(OnStart);
        pauseButton.onClick.AddListener(OnPause);
        resetButton.onClick.AddListener(OnReset);

        var trigger = boostButton.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(_ => { boosting = true; boostHeldTime = 0f; });
        trigger.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener(_ => { boosting = false; boostHeldTime = 0f; currentRotateSpeed = 0f; });
        trigger.triggers.Add(up);
    }

    private void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        workInputField.text  = defaultWorkMinutes.ToString();
        breakInputField.text = defaultBreakMinutes.ToString();

        workInputField.onEndEdit.AddListener(_  => OnReset());
        breakInputField.onEndEdit.AddListener(_ => OnReset());

        workInputField.DeactivateInputField();
        breakInputField.DeactivateInputField();

        OnReset();
    }

    // ───────────────────────────────────────────
    public void OnStart()
    {
        if (running) return;
        running = true;
        timerCoroutine = StartCoroutine(Tick());
        UpdateButtonStates();
        ShowWorkOrBreakMessage();
    }

    public void OnPause()
    {
        if (!running) return;
        running = false;
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        UpdateButtonStates();
        ShowMessage(pausedMessages);
    }

    public void OnReset()
    {
        running = false;
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);

        phase        = Phase.Work;
        currentCycle = 0;
        totalSeconds = GetWorkSeconds();
        remaining    = totalSeconds;

        Refresh();
        UpdateButtonStates();
        ShowMessage(idleMessages);
    }

    // ───────────────────────────────────────────
    private IEnumerator Tick()
    {
        while (remaining > 0 && running)
        {
            float interval = boosting ? Mathf.Max(0.1f, 1f - boostHeldTime * 0.3f) : 1f;
            yield return new WaitForSeconds(interval);
            remaining = Mathf.Max(0, remaining - 1);
            Refresh();
        }

        if (remaining <= 0)
            yield return StartCoroutine(OnPhaseComplete());
    }

    private IEnumerator OnPhaseComplete()
    {
        running = false;
        if (alarmClip != null) audioSource.PlayOneShot(alarmClip);

        if (phase == Phase.Work)
        {
            currentCycle++;

            // 4사이클 완료
            if (currentCycle >= TotalCycles)
            {
                timerText.text = "(*^□^*)";
                if (stateText != null) stateText.text = $"🎉 {TotalCycles}사이클 완료! 오늘 집중 끝!";
                UpdateButtonStates();
                yield break;
            }

            // 집중 완료 → 휴식 전환
            if (stateText != null) stateText.text = $"(*^□^*) {currentCycle}/{TotalCycles} 완료! 잠깐 쉬어요";
            timerText.text = "(*^□^*)";
            yield return new WaitForSeconds(2f);

            phase        = Phase.Break;
            totalSeconds = GetBreakSeconds();
            remaining    = totalSeconds;
        }
        else
        {
            // 휴식 완료 → 집중 전환
            if (stateText != null) stateText.text = $"자, 다시 시작! ({currentCycle + 1}/{TotalCycles})";
            yield return new WaitForSeconds(2f);

            phase        = Phase.Work;
            totalSeconds = GetWorkSeconds();
            remaining    = totalSeconds;
        }

        // 자동 시작
        running        = true;
        timerCoroutine = StartCoroutine(Tick());
        UpdateButtonStates();
        ShowWorkOrBreakMessage();
    }

    // ───────────────────────────────────────────
    private void Update()
    {
        if (boosting && running)
        {
            boostHeldTime     += Time.deltaTime;
            currentRotateSpeed = boostRotateSpeed * Mathf.Clamp(1f + boostHeldTime * 2f, 1f, 10f);
            boostButton.transform.Rotate(0f, 0f, -currentRotateSpeed * Time.deltaTime);
        }
    }

    private void UpdateButtonStates()
    {
        startButton.interactable = !running;
        pauseButton.interactable = running;
        resetButton.interactable = running || remaining != totalSeconds || currentCycle > 0;
    }

    private void Refresh()
    {
        timerText.text = string.Format("{0:D2}:{1:D2}", remaining / 60, remaining % 60);

        // 집중: 초록→빨강 / 휴식: 빨강→초록
        float t = totalSeconds > 0 ? 1f - (remaining / (float)totalSeconds) : 0f;
        if (phase == Phase.Work)
            tomatoImage.color = Color.Lerp(workStartColor, workEndColor, colorCurve.Evaluate(t));
        else
            tomatoImage.color = Color.Lerp(workEndColor, workStartColor, colorCurve.Evaluate(t));
    }

    private void ShowWorkOrBreakMessage()
    {
        if (phase == Phase.Work)
        {
            string[] msgs = { $"집중! ({currentCycle + 1}/{TotalCycles})" };
            // 랜덤 메시지 + 사이클 정보 조합
            string random = workMessages[Random.Range(0, workMessages.Length)];
            if (stateText != null) stateText.text = $"{random}  ({currentCycle + 1}/{TotalCycles})";
        }
        else
        {
            string random = breakMessages[Random.Range(0, breakMessages.Length)];
            if (stateText != null) stateText.text = random;
        }
    }

    private void ShowMessage(string[] messages)
    {
        if (stateText == null || messages.Length == 0) return;
        int index;
        do { index = Random.Range(0, messages.Length); }
        while (messages.Length > 1 && index == lastMessageIndex);
        lastMessageIndex = index;
        stateText.text = messages[index];
    }

    private int GetWorkSeconds()
    {
        if (workInputField != null && int.TryParse(workInputField.text, out int m) && m > 0)
            return m * 60;
        return defaultWorkMinutes * 60;
    }

    private int GetBreakSeconds()
    {
        if (breakInputField != null && int.TryParse(breakInputField.text, out int m) && m > 0)
            return m * 60;
        return defaultBreakMinutes * 60;
    }
}
