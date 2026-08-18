using System.Collections;
using System.Collections.Generic;
using DevionGames.UIWidgets;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] public GameObject charChange; // CharChange
    [SerializeField] public GameObject characterDetail; // CharacterDetail
    [SerializeField] public GameObject charSummon; // charSummon
    [SerializeField] public GameObject version; // version+thanks
    [SerializeField] public Text versionThanksContent; // version+thanks
    [SerializeField] public GameObject settings; // settings
    [SerializeField] public GameObject chatHistory; // chatHistory
    [SerializeField] public GameObject guideLine; // guideLine
    [SerializeField] public GameObject situation; // UIChatSituation
    [SerializeField] public GameObject ocrAutoMapper; // OCRAutoMapper
    [SerializeField] public GameObject choiceInputImage; // APIInput
    [SerializeField] public GameObject pomodoro; // Pomodoro
    [SerializeField] public GameObject alarm; // Alarm
    [SerializeField] public GameObject skill; // SkillView
    [SerializeField] public GameObject mission; // MissionView
    [SerializeField] public GameObject todoList; // TODOList
    [SerializeField] public GameObject calendar; // Calendar
    [SerializeField] public GameObject aiStatus; // AIStatusView
    [SerializeField] public GameObject jukebox; // JukeboxView
    [SerializeField] public GameObject inventoryPanel; // InventorySystem 메인 패널 (프리팹 에셋 할당 시 canvasUI 아래 인스턴스화)
    [SerializeField] public GameObject inventoryPanelChar; // InventorySystem 캐릭터 패널 (프리팹 에셋 할당 시 canvasUI 아래 인스턴스화)
    [SerializeField] public AlarmMiniView alarmMiniPrefab; // AlarmMini prefab

    [SerializeField] public GameObject debugBalloon2; // VL, Web 등 정보 보여주기

    // 싱글톤 인스턴스
    private AlarmMiniView alarmMiniInstance;
    private string alarmMiniAlarmId = string.Empty;
    private float alarmMiniRefreshProgress;
    private bool alarmMiniPositionInitialized;

    // 인벤토리 패널 상태 플래그 (최초 표시 위치 지정 / 캐릭터 섹션 지정 1회 / 미할당 경고 1회)
    private bool inventoryPositionInitialized;
    private bool inventoryCharPositionInitialized;
    private bool inventoryCharSectionConfigured;
    private bool inventoryMissingWarned;

    private static UIManager instance;
    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindBestUIManager();
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        // 싱글톤 패턴 구현
        if (instance == null || ShouldReplaceInstance(instance, this))
        {
            instance = this;
        }
        else
        {
            // Destroy(gameObject);
            return;
        }

        // Prefab 선언. 자체 함수로 비활성화
        pomodoro = ResolveManagedUI(pomodoro, "Pomodoro");
        alarm = ResolveManagedUI(alarm, "Alarm");
        skill = ResolveManagedUI(skill, "SkillView");
        mission = ResolveManagedUI(mission, "MissionView");
        todoList = ResolveManagedUI(todoList, "TODOList");
        calendar = ResolveManagedUI(calendar, "Calendar");
        aiStatus = ResolveManagedUI(aiStatus, "AIStatusView");
        jukebox = ResolveManagedUI(jukebox, "JukeboxView");
        characterDetail = ResolveManagedUI(characterDetail, "CharacterDetail");
        inventoryPanel = ResolveManagedUI(inventoryPanel, "InventoryPanel");
        inventoryPanelChar = ResolveManagedUI(inventoryPanelChar, "InventoryPanelChar");

        // UIWidget 패널은 Awake 실행 순서와 무관하게 동일한 닫힘 상태(inactive+alpha0)로 강제 리셋
        // — SetActive(false) 후 Close() 방식은 위젯 Awake 선행 여부에 따라 초기 alpha가 0/1로 갈라져,
        //   최초 Show 때 이월된 OnDelayedStart가 패널을 꺼버리는 고착(m_IsShowing 잠김)의 원인이 됨
        ForceResetWidget(charChange);
        SetInitialInactive(characterDetail);
        ForceResetWidget(charSummon);
        ForceResetWidget(version);
        ForceResetWidget(settings);
        ForceResetWidget(chatHistory);
        // guideLine.SetActive(false);
        // situation.SetActive(false);
        ForceResetWidget(ocrAutoMapper);
        choiceInputImage.SetActive(false);
        SetInitialInactive(pomodoro);
        SetInitialInactive(alarm);
        SetInitialInactive(skill);
        SetInitialInactive(mission);
        SetInitialInactive(todoList);
        SetInitialInactive(calendar);
        SetInitialInactive(aiStatus);
        SetInitialInactive(jukebox);
        // InventoryView는 CanvasGroup으로 표시를 제어하므로 SetActive 대신 Hide로 초기 숨김
        HideInventoryViewIfPresent(inventoryPanel);
        HideInventoryViewIfPresent(inventoryPanelChar);
        debugBalloon2.SetActive(false);

        // UIWidget 존재하면 Close (ForceResetWidget 미적용 패널만 — guideLine/situation은 시작 시 활성 유지가 기존 의도)
        TryCloseWidget(characterDetail);
        TryCloseWidget(guideLine);
        TryCloseWidget(situation);

        //         // 안드로이드 or 테스트용
        // #if UNITY_ANDROID || UNITY_EDITOR
        //         charChange.SetActive(true);
        //         settings.SetActive(true);
        // #endif
    }



    // GameObject에 UIWidget이 있으면 Close() 호출
    private static UIManager FindBestUIManager()
    {
        UIManager[] managers = Resources.FindObjectsOfTypeAll<UIManager>();
        UIManager fallback = null;

        for (int i = 0; i < managers.Length; i++)
        {
            UIManager manager = managers[i];
            if (manager == null || !manager.gameObject.scene.IsValid())
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = manager;
            }

            if (manager.calendar != null || manager.todoList != null)
            {
                return manager;
            }
        }

        return fallback;
    }

    private static bool ShouldReplaceInstance(UIManager current, UIManager candidate)
    {
        if (current == null)
        {
            return true;
        }

        if (candidate == null || !candidate.gameObject.scene.IsValid())
        {
            return false;
        }

        bool currentHasTodoCalendar = current.todoList != null || current.calendar != null;
        bool candidateHasTodoCalendar = candidate.todoList != null || candidate.calendar != null;
        return !currentHasTodoCalendar && candidateHasTodoCalendar;
    }

    private void Update()
    {
        if (alarmMiniInstance == null || !alarmMiniInstance.gameObject.activeSelf)
        {
            return;
        }

        alarmMiniRefreshProgress += Time.unscaledDeltaTime;
        if (alarmMiniRefreshProgress < 1f)
        {
            return;
        }

        alarmMiniRefreshProgress = 0f;
        RefreshAlarmMini();
    }

    private void TryCloseWidget(GameObject target)
    {
        if (target == null) return;

        UIWidget widget = target.GetComponent<UIWidget>();
        if (widget != null)
        {
            widget.Close();
        }
    }

    // 시작 시 위젯을 트윈/이월된 자동 닫힘에 의존하지 않고 즉시 닫힘 상태로 강제 리셋
    private void ForceResetWidget(GameObject target)
    {
        if (target == null) return;

        UIWidget widget = target.GetComponent<UIWidget>();
        if (widget != null)
        {
            widget.Close(); // 위젯 Awake가 이미 돌았다면 m_IsShowing 리셋 (아니면 no-op)

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            target.transform.localScale = Vector3.zero;
        }

        target.SetActive(false);
    }

    // charChange-UIWidget의 Show 작동
    private void SetInitialInactive(GameObject target)
    {
        if (target != null)
        {
            target.SetActive(false);
        }
    }

    // InventoryView가 붙어 있으면 CanvasGroup 관례에 따라 Hide()로 초기 숨김
    private void HideInventoryViewIfPresent(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        InventoryView view = target.GetComponent<InventoryView>();
        if (view != null)
        {
            view.Hide();
        }
    }

    private void ShowManagedUI(GameObject target, string menuName)
    {
        if (target == null)
        {
            return;
        }

        if (!target.activeSelf)
        {
            RectTransform targetRect = target.GetComponent<RectTransform>();
            if (targetRect != null)
            {
                targetRect.position = UIPositionManager.Instance.GetMenuPosition(menuName);
            }

            target.SetActive(true);
        }

        UIWidget widget = target.GetComponent<UIWidget>();
        if (widget != null)
        {
            widget.Show();
        }
    }

    private void CloseManagedUI(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        UIWidget widget = target.GetComponent<UIWidget>();
        if (widget != null)
        {
            widget.Close();
        }
        else
        {
            target.SetActive(false);
        }
    }

    private void ShowSimpleUI(GameObject target, string menuName)
    {
        if (target == null)
        {
            return;
        }

        if (!target.activeSelf)
        {
            RectTransform targetRect = target.GetComponent<RectTransform>();
            if (targetRect != null)
            {
                targetRect.position = UIPositionManager.Instance.GetMenuPosition(menuName);
            }
        }

        target.SetActive(true);
    }

    private void CloseSimpleUI(GameObject target)
    {
        if (target != null)
        {
            target.SetActive(false);
        }
    }

    public void ShowCharChange()
    {
        UIWidget uIWidget = charChange.GetComponent<UIWidget>();

        // 이미 활성화되어 있지 않은 경우라면 위치 조정
        if (!charChange.activeSelf)
        {
            // Vector3 position = UIPositionManager.Instance.GetCanvasPositionRight();
            Vector3 position = UIPositionManager.Instance.GetMenuPosition("charChange");
            charChange.GetComponent<RectTransform>().position = position;
        }

        uIWidget.Show();
    }

    // charChange-UIWidget의 Close 작동
    public void CloseCharChange()
    {
        UIWidget uIWidget = charChange.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // charChange-UIWidget의 Toggle 작동
    // activeSelf 판정은 닫힘 트윈(0.7초) 동안 무반응이 되므로 위젯의 표시 의도(m_IsShowing) 기준으로 판정
    public void ToggleCharChange()
    {
        UIWidget uIWidget = charChange.GetComponent<UIWidget>();
        bool isShowing = uIWidget != null ? uIWidget.IsM_IsShowing : charChange.activeSelf;
        if (isShowing)
        {
            CloseCharChange();
        }
        else
        {
            ShowCharChange();
        }
    }

    // GuideLine-UIWidget의 Show 작동
    public void ShowCharacterDetail(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)
    {
        Debug.Log($"[CharacterDetail][UIManager] Show requested. char={charInfo?.name} clothes={clothesInfo?.text} currentAssigned={(characterDetail != null ? characterDetail.name : "null")}");

        characterDetail = ResolveManagedUI(characterDetail, "CharacterDetail");
        if (characterDetail == null)
        {
            Debug.LogWarning("[UIManager] CharacterDetail prefab or scene object is not assigned.");
            return;
        }

        Debug.Log($"[CharacterDetail][UIManager] Resolved target name={characterDetail.name} active={characterDetail.activeSelf} sceneValid={characterDetail.scene.IsValid()}");

        if (!characterDetail.activeSelf)
        {
            RectTransform targetRect = characterDetail.GetComponent<RectTransform>();
            if (targetRect != null)
            {
                targetRect.position = UIPositionManager.Instance.GetMenuPosition("characterDetail");
                Debug.Log($"[CharacterDetail][UIManager] Position applied. world={targetRect.position} anchored={targetRect.anchoredPosition}");
            }

            characterDetail.SetActive(true);
            Debug.Log("[CharacterDetail][UIManager] Target activated.");
        }

        CharacterDetailController controller = characterDetail.GetComponent<CharacterDetailController>();
        if (controller == null)
        {
            controller = characterDetail.AddComponent<CharacterDetailController>();
            Debug.Log("[CharacterDetail][UIManager] CharacterDetailController was missing and has been added at runtime.");
        }

        controller.Show(charInfo, clothesInfo);
    }

    public void CloseCharacterDetail()
    {
        characterDetail = ResolveManagedUI(characterDetail, "CharacterDetail");
        if (characterDetail == null)
        {
            return;
        }

        CharacterDetailController controller = characterDetail.GetComponent<CharacterDetailController>();
        if (controller != null)
        {
            controller.Hide();
        }
        else
        {
            characterDetail.SetActive(false);
        }
    }

    public void ToggleCharacterDetail(ChangeCharInfo charInfo, ChangeCharClothesInfo clothesInfo = null)
    {
        characterDetail = ResolveManagedUI(characterDetail, "CharacterDetail");
        if (characterDetail != null && characterDetail.activeSelf)
        {
            CloseCharacterDetail();
            return;
        }

        ShowCharacterDetail(charInfo, clothesInfo);
    }

    public void ShowGuideLine()
    {
        UIWidget uIWidget = guideLine.GetComponent<UIWidget>();

        // 이미 활성화되어 있지 않은 경우라면 위치 조정
        if (!guideLine.activeSelf)
        {
            // Vector3 position = UIPositionManager.Instance.GetCanvasPositionRight();
            Vector3 position = UIPositionManager.Instance.GetMenuPosition("guideline");
            guideLine.GetComponent<RectTransform>().position = position;
        }

        // 값이 없으면 초기값 선언하게 선언
        UIUserCardManager.Instance.InitUserCard();

        uIWidget.Show();
    }

    // GuideLine-UIWidget의 Close 작동
    public void CloseGuideLine()
    {
        UIWidget uIWidget = guideLine.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // GuideLine-UIWidget의 Toggle 작동
    public void ToggleGuideLine()
    {
        if (guideLine.activeSelf)
        {
            CloseGuideLine();
        }
        else
        {
            ShowGuideLine();
        }
    }

    // ChatSituation 활성화 후 -UIWidget의 Show 작동
    public void ShowUIChatSituation()
    {

        UIWidget uIWidget = situation.GetComponent<UIWidget>();

        // 이미 활성화되어 있지 않은 경우라면 위치 조정
        if (!situation.activeSelf)
        {
            situation.SetActive(true);  // 활성화 해야 Load 가능
            UIChatSituationManager.Instance.LoadChatSituationData();  // 언어 ui 변경가능성 있으니 그냥 load (data가 아직은 가벼움)

            Vector3 position = UIPositionManager.Instance.GetCanvasPositionCenter();
            // Vector3 position = UIPositionManager.Instance.GetMenuPosition("situation");
            situation.GetComponent<RectTransform>().position = position;
        }

        uIWidget.Show();

        // 스크롤 강제 초기화
        UIChatSituationManager.Instance.ResetScrollPosition();
    }

    // ChatSituation-UIWidget의 Close 작동
    public void CloseUIChatSituation()
    {
        UIWidget uIWidget = situation.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // ChatSituation-UIWidget의 Toggle 작동
    public void ToggleUIChatSituation()
    {
        if (situation.activeSelf)
        {
            CloseUIChatSituation();
        }
        else
        {
            ShowUIChatSituation();
        }
    }


    // charSummon-UIWidget의 Show 작동
    public void ShowCharSummon()
    {
        UIWidget uIWidget = charSummon.GetComponent<UIWidget>();

        // 이미 활성화되어 있지 않은 경우라면 위치 조정
        if (!charSummon.activeSelf)
        {
            // Vector3 position = UIPositionManager.Instance.GetCanvasPositionRight();
            Vector3 position = UIPositionManager.Instance.GetMenuPosition("charSummon");
            Debug.Log(position);
            charSummon.GetComponent<RectTransform>().position = position;
        }

        uIWidget.Show();
    }

    // charSummon-UIWidget의 Close 작동
    public void CloseCharSummon()
    {
        UIWidget uIWidget = charSummon.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // charSummon-UIWidget의 Toggle 작동
    public void ToggleCharSummon()
    {
        if (charSummon.activeSelf)
        {
            CloseCharSummon();
        }
        else
        {
            ShowCharSummon();
        }
    }

    // version-UIWidget의 Show 작동
    public void ShowVersion()
    {
        UIWidget uIWidget = version.GetComponent<UIWidget>();

        // Special Thanks 문자
        string answerLanguage = SettingManager.Instance.settings.ui_language; // 표시 언어 초기화[ko, en, jp]
        // 언어에 따른 텍스트 설정
        if (answerLanguage == "ko")
        {
            versionThanksContent.text = "이 프로그램은 무료로 사용할 수 있으며\n많은 기부자들의 후원으로 제작되고 있습니다.";
        }
        else if (answerLanguage == "jp")
        {
            versionThanksContent.text = "このプログラムは無料で利用することができ、\n多くのパトロンの後援で制作されています。";
        }
        else
        {
            versionThanksContent.text = "This program is FREE TO USE\nand is supported by many generous donors.";
        }

        // 이미 활성화되어 있지 않은 경우라면 위치 조정
        if (!version.activeSelf)
        {
            // Vector3 position = UIPositionManager.Instance.GetCanvasPositionRight();
            Vector3 position = UIPositionManager.Instance.GetMenuPosition("version");
            Debug.Log(position);
            version.GetComponent<RectTransform>().position = position;
        }
    
        uIWidget.Show();
    }

    // version-UIWidget의 Close 작동
    public void CloseVersion()
    {
        UIWidget uIWidget = version.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // version-UIWidget의 Toggle 작동
    public void ToggleVersion()
    {
        if (version.activeSelf)
        {
            CloseVersion();
        }
        else
        {
            ShowVersion();
        }
    }

    // settings-UIWidget의 Show 작동
    public void showSettings()
    {
        UIWidget uIWidget = settings.GetComponent<UIWidget>();
        uIWidget.Show();
    }

    // settings-UIWidget의 Close 작동
    public void CloseSettings()
    {
        UIWidget uIWidget = settings.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // settings-UIWidget의 Toggle 작동
    public void ToggleSettings()
    {
        if (settings.activeSelf)
        {
            CloseSettings();
        }
        else
        {
            showSettings();
        }
    }

    // chatHistory-UIWidget의 Show 작동
    public void ShowChatHistory()
    {
        UIChatHistoryManager uIChatHistoryManager = chatHistory.GetComponent<UIChatHistoryManager>();
        uIChatHistoryManager.LoadChatHistory();

        UIWidget uIWidget = chatHistory.GetComponent<UIWidget>();

        // 이미 활성화되어 있지 않은 경우라면 위치 조정
        if (!chatHistory.activeSelf)
        {
            // Vector3 position = UIPositionManager.Instance.GetCanvasPositionRight();
            Vector3 position = UIPositionManager.Instance.GetMenuPosition("chatHistory");
            Debug.Log(position);
            chatHistory.GetComponent<RectTransform>().position = position;
        }

        uIWidget.Show();
    }

    // chatHistory-UIWidget의 Close 작동
    public void CloseChatHistory()
    {
        UIWidget uIWidget = chatHistory.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // chatHistory-UIWidget의 Toggle 작동
    public void ToggleChatHistory()
    {
        if (chatHistory.activeSelf)
        {
            CloseChatHistory();
        }
        else
        {
            ShowChatHistory();
        }
    }

    // OCRAutoMapper-UIWidget의 Show 작동
    public void ShowOCRAutoMapper()
    {
        UIWidget uIWidget = ocrAutoMapper.GetComponent<UIWidget>();

        // 이미 활성화되어 있지 않은 경우라면 위치 조정
        if (!ocrAutoMapper.activeSelf)
        {
            Vector3 position = UIPositionManager.Instance.GetMenuPosition("ocrAutoMapper");
            ocrAutoMapper.GetComponent<RectTransform>().position = position;
        }

        uIWidget.Show();
    }

    // OCRAutoMapper-UIWidget의 Close 작동
    public void CloseOCRAutoMapper()
    {
        UIWidget uIWidget = ocrAutoMapper.GetComponent<UIWidget>();
        
        uIWidget.Close();
    }

    // OCRAutoMapper-UIWidget의 Toggle 작동
    public void ToggleOCRAutoMapper()
    {
        if (ocrAutoMapper.activeSelf)
        {
            CloseOCRAutoMapper();
        }
        else
        {
            ShowOCRAutoMapper();
        }
    }

    public void ShowPomodoro()
    {
        pomodoro = ResolveManagedUI(pomodoro, "Pomodoro");
        ShowSimpleUI(pomodoro, "pomodoro");
    }

    public void ClosePomodoro()
    {
        CloseSimpleUI(pomodoro);
    }

    public void TogglePomodoro()
    {
        if (pomodoro != null && pomodoro.activeSelf)
        {
            ClosePomodoro();
        }
        else
        {
            ShowPomodoro();
        }
    }

    public void ShowInventory()
    {
        inventoryPanel = ResolveManagedUI(inventoryPanel, "InventoryPanel");
        inventoryPanelChar = ResolveManagedUI(inventoryPanelChar, "InventoryPanelChar");

        if ((inventoryPanel == null || inventoryPanelChar == null) && inventoryMissingWarned == false)
        {
            Debug.LogWarning("[UIManager] Inventory 패널 프리팹이 할당되지 않았습니다. inventoryPanel/inventoryPanelChar를 확인하세요.");
            inventoryMissingWarned = true;
        }

        if (inventoryPanel != null)
        {
            if (inventoryPositionInitialized == false && UIPositionManager.Instance != null)
            {
                RepositionSimpleUI(inventoryPanel, "inventory");
                inventoryPositionInitialized = true;
            }

            if (inventoryPanel.activeSelf == false)
            {
                inventoryPanel.SetActive(true);
            }

            InventoryView mainView = inventoryPanel.GetComponent<InventoryView>();
            if (mainView != null)
            {
                mainView.Show();
            }
        }

        if (inventoryPanelChar != null)
        {
            if (inventoryCharPositionInitialized == false && UIPositionManager.Instance != null)
            {
                RepositionSimpleUI(inventoryPanelChar, "inventoryChar");
                inventoryCharPositionInitialized = true;
            }

            InventoryView charView = inventoryPanelChar.GetComponent<InventoryView>();

            // 섹션 지정은 SetActive(true)보다 먼저 — 활성화 시 OnEnable→Rebuild가
            // 프리팹 기본값(Main)으로 그려버리는 최초 오픈 버그 방지 (캐릭터 창은 최초 1회만 지정)
            if (charView != null && inventoryCharSectionConfigured == false)
            {
                charView.ConfigureSection(InventorySection.Char);
                inventoryCharSectionConfigured = true;
            }

            if (inventoryPanelChar.activeSelf == false)
            {
                inventoryPanelChar.SetActive(true);
            }

            if (charView != null)
            {
                charView.Show();
            }
        }
    }

    public void CloseInventory()
    {
        if (inventoryPanel != null)
        {
            InventoryView mainView = inventoryPanel.GetComponent<InventoryView>();
            if (mainView != null)
            {
                mainView.Hide();
            }
        }

        if (inventoryPanelChar != null)
        {
            InventoryView charView = inventoryPanelChar.GetComponent<InventoryView>();
            if (charView != null)
            {
                charView.Hide();
            }
        }
    }

    public void ToggleInventory()
    {
        bool isVisible = false;
        if (inventoryPanel != null)
        {
            InventoryView mainView = inventoryPanel.GetComponent<InventoryView>();
            if (mainView != null && mainView.IsVisible == true)
            {
                isVisible = true;
            }
        }

        if (isVisible == true)
        {
            CloseInventory();
        }
        else
        {
            ShowInventory();
        }
    }

    public void ShowAlarm()
    {
        alarm = ResolveManagedUI(alarm, "Alarm");
        ShowSimpleUI(alarm, "alarm");
    }

    public void CloseAlarm()
    {
        CloseSimpleUI(alarm);
    }

    public void ToggleAlarm()
    {
        if (alarm != null && alarm.activeSelf)
        {
            CloseAlarm();
        }
        else
        {
            ShowAlarm();
        }
    }

    public void ShowSkill()
    {
        skill = ResolveManagedUI(skill, "SkillView");
        bool wasActive = skill != null && skill.activeSelf;
        ShowSimpleUI(skill, "skill");
        if (wasActive)
        {
            SkillCatalogClient client = skill.GetComponent<SkillCatalogClient>();
            if (client != null)
            {
                client.ReloadCatalog();
            }
        }
    }

    public void CloseSkill()
    {
        CloseSimpleUI(skill);
    }

    public void ToggleSkill()
    {
        if (skill != null && skill.activeSelf)
        {
            CloseSkill();
        }
        else
        {
            ShowSkill();
        }
    }

    public void ShowMission()
    {
        mission = ResolveManagedUI(mission, "MissionView");
        ShowSimpleUI(mission, "mission");

        MissionView missionView = mission != null ? mission.GetComponent<MissionView>() : null;
        if (missionView != null)
        {
            missionView.Show();
        }
    }

    public void CloseMission()
    {
        MissionView missionView = mission != null ? mission.GetComponent<MissionView>() : null;
        if (missionView != null)
        {
            missionView.Hide();
        }
        else
        {
            CloseSimpleUI(mission);
        }
    }

    public void ToggleMission()
    {
        if (mission != null && mission.activeSelf)
        {
            CloseMission();
        }
        else
        {
            ShowMission();
        }
    }

    public void ShowTODOList()
    {
        ShowTODOList(System.DateTime.Now.Date);
    }

    public void ShowTODOList(System.DateTime date)
    {
        JarvisTodoListUI controller = GetOrCreateTypedManagedUI<JarvisTodoListUI>(ref todoList, "TODOList", "todolist");
        if (controller == null)
        {
            return;
        }

        controller.Show(date);
    }

    public void CloseTODOList()
    {
        CloseSimpleUI(todoList);
    }

    public void ToggleTODOList()
    {
        if (todoList != null && todoList.activeSelf)
        {
            CloseTODOList();
        }
        else
        {
            ShowTODOList();
        }
    }

    public void ShowCalendar()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // MR: 반드시 MR 포크를 요구한다.
        //
        // EnsureTypedComponent<T>()는 T가 없으면 AddComponent<T>()로 **런타임에 붙인다.**
        // 여기서 원본 JarvisCalendarUI를 요구하면, 씬에 MRJarvisCalendarUI가 있어도
        // 별개 클래스라 GetComponent<JarvisCalendarUI>()가 못 찾아 데스크톱 원본이
        // 추가되고 MR 포크와 나란히 돌아간다. 그러면 원본의 데스크톱 가정이 되살아난다:
        //   · gameObject.name = "Calendar" 로 이름을 바꿈
        //   · SetExpanded()가 pickerRect.sizeDelta = (0,0) 을 대입 → 레이아웃 붕괴
        //   · 날짜 버튼을 따로 42개 더 생성 → 84개, 캘린더 높이 2배
        // 실기에서 위 세 증상이 동시에 관측됐다(2026-08-18). Kickoff Guide §4-31/§4-42.
        MRJarvisCalendarUI calendarUI = GetOrCreateTypedManagedUI<MRJarvisCalendarUI>(ref calendar, "Calendar", "calendar");
#else
        JarvisCalendarUI calendarUI = GetOrCreateTypedManagedUI<JarvisCalendarUI>(ref calendar, "Calendar", "calendar");
#endif
        if (calendarUI == null)
        {
            return;
        }

        calendarUI.DateSelected -= OnCalendarDateSelected;
        calendarUI.DateSelected += OnCalendarDateSelected;
        calendarUI.ShowToday();
    }

    public void CloseCalendar()
    {
        CloseSimpleUI(calendar);
    }

    public void ToggleCalendar()
    {
        if (calendar != null && calendar.activeSelf)
        {
            CloseCalendar();
        }
        else
        {
            ShowCalendar();
        }
    }

    public void ShowAIStatus()
    {
        aiStatus = ResolveManagedUI(aiStatus, "AIStatusView");
        ShowSimpleUI(aiStatus, "aistatus");

        AIStatusView view = aiStatus != null ? aiStatus.GetComponent<AIStatusView>() : null;
        if (view != null)
        {
            view.Show();
        }
    }

    public void CloseAIStatus()
    {
        AIStatusView view = aiStatus != null ? aiStatus.GetComponent<AIStatusView>() : null;
        if (view != null)
        {
            view.Hide();
        }
        else
        {
            CloseSimpleUI(aiStatus);
        }
    }

    public void ToggleAIStatus()
    {
        if (aiStatus != null && aiStatus.activeSelf)
        {
            CloseAIStatus();
        }
        else
        {
            ShowAIStatus();
        }
    }

    public void ShowJukebox()
    {
        jukebox = ResolveManagedUI(jukebox, "JukeboxView");
        ShowSimpleUI(jukebox, "jukebox");

        JukeboxView view = jukebox != null ? jukebox.GetComponent<JukeboxView>() : null;
        if (view != null)
        {
            view.Show();
        }
    }

    public void CloseJukebox()
    {
        JukeboxView view = jukebox != null ? jukebox.GetComponent<JukeboxView>() : null;
        if (view != null)
        {
            view.Hide();
        }
        else
        {
            CloseSimpleUI(jukebox);
        }
    }

    public void ToggleJukebox()
    {
        if (jukebox != null && jukebox.activeSelf)
        {
            CloseJukebox();
        }
        else
        {
            ShowJukebox();
        }
    }

    public void ShowAlarmMini()
    {
        AlarmManager alarmManager = GetAlarmManager();
        if (alarmManager == null)
        {
            Debug.LogWarning("[UIManager] AlarmManager is not available.");
            return;
        }

        AlarmItem targetAlarm = GetAlarmMiniTarget(alarmManager);
        if (targetAlarm == null)
        {
            targetAlarm = alarmManager.AddRelativeTimer(string.Empty, 600, "default_alarm");
        }

        AlarmMiniView mini = GetOrCreateAlarmMini();
        if (mini == null)
        {
            return;
        }

        alarmMiniAlarmId = targetAlarm.id;
        mini.Bind(targetAlarm);
        mini.RefreshFromManager(alarmManager, targetAlarm);
        if (!alarmMiniPositionInitialized)
        {
            PositionAlarmMini(mini);
            alarmMiniPositionInitialized = true;
        }

        mini.Show();
        EnsureAlarmMiniOnTop(mini);
        RefreshAlarmUIRuntime();
    }

    public void CloseAlarmMini()
    {
        if (alarmMiniInstance != null)
        {
            alarmMiniInstance.Hide();
        }
    }

    public void ToggleAlarmMini()
    {
        if (alarmMiniInstance != null && alarmMiniInstance.gameObject.activeSelf)
        {
            CloseAlarmMini();
        }
        else
        {
            ShowAlarmMini();
        }
    }

    private AlarmMiniView GetOrCreateAlarmMini()
    {
        if (alarmMiniInstance != null)
        {
            return alarmMiniInstance;
        }

        if (alarmMiniPrefab == null)
        {
            Debug.LogWarning("[UIManager] AlarmMini prefab is not assigned.");
            return null;
        }

        Transform parent = null;
        if (CanvasManager.Instance != null && CanvasManager.Instance.canvasUI != null)
        {
            parent = CanvasManager.Instance.canvasUI.transform;
        }

        alarmMiniInstance = parent != null ? Instantiate(alarmMiniPrefab, parent) : Instantiate(alarmMiniPrefab);
        alarmMiniInstance.name = "AlarmMini_Global";
        alarmMiniInstance.StartRequested += OnAlarmMiniStartRequested;
        alarmMiniInstance.PauseRequested += OnAlarmMiniPauseRequested;
        alarmMiniInstance.ResetRequested += OnAlarmMiniResetRequested;
        alarmMiniInstance.CloseRequested += OnAlarmMiniCloseRequested;
        return alarmMiniInstance;
    }

    private void PositionAlarmMini(AlarmMiniView mini)
    {
        if (mini == null)
        {
            return;
        }

        RectTransform miniRect = mini.transform as RectTransform;
        if (miniRect == null)
        {
            return;
        }

        miniRect.position = UIPositionManager.Instance.GetMenuPosition("alarmmini");
        Vector3 localPosition = miniRect.localPosition;
        localPosition.z = 10f;
        miniRect.localPosition = localPosition;
    }

    private void EnsureAlarmMiniOnTop(AlarmMiniView mini)
    {
        if (mini == null)
        {
            return;
        }

        mini.transform.SetAsLastSibling();

        RectTransform miniRect = mini.transform as RectTransform;
        if (miniRect != null)
        {
            Vector3 localPosition = miniRect.localPosition;
            localPosition.z = 10f;
            miniRect.localPosition = localPosition;
        }
    }

    private void RefreshAlarmMini()
    {
        if (alarmMiniInstance == null || !alarmMiniInstance.gameObject.activeSelf)
        {
            return;
        }

        AlarmManager alarmManager = GetAlarmManager();
        if (alarmManager == null)
        {
            return;
        }

        AlarmItem alarmItem = FindAlarmById(alarmManager, alarmMiniAlarmId);
        if (alarmItem == null || alarmItem.alarmType != AlarmType.RelativeTimer)
        {
            alarmMiniInstance.Hide();
            alarmMiniAlarmId = string.Empty;
            return;
        }

        alarmMiniInstance.RefreshFromManager(alarmManager, alarmItem);
    }

    private AlarmItem GetAlarmMiniTarget(AlarmManager alarmManager)
    {
        if (alarmManager == null)
        {
            return null;
        }

        List<AlarmItem> alarms = alarmManager.GetAlarms();
        AlarmItem firstTimer = null;
        AlarmItem bestRunningTimer = null;
        int bestRemainingSeconds = int.MaxValue;

        for (int i = 0; i < alarms.Count; i++)
        {
            AlarmItem alarmItem = alarms[i];
            if (alarmItem == null || alarmItem.alarmType != AlarmType.RelativeTimer)
            {
                continue;
            }

            if (firstTimer == null)
            {
                firstTimer = alarmItem;
            }

            string state = alarmManager.GetRelativeTimerState(alarmItem.id);
            bool isRunning = state == AlarmRuntimeState.Running || alarmManager.IsAlarmRinging(alarmItem.id);
            if (!isRunning)
            {
                continue;
            }

            int remainingSeconds = alarmManager.GetRemainingSeconds(alarmItem);
            if (remainingSeconds < bestRemainingSeconds)
            {
                bestRemainingSeconds = remainingSeconds;
                bestRunningTimer = alarmItem;
            }
        }

        if (bestRunningTimer != null)
        {
            return bestRunningTimer;
        }

        return firstTimer;
    }

    private AlarmItem FindAlarmById(AlarmManager alarmManager, string alarmId)
    {
        if (alarmManager == null || string.IsNullOrEmpty(alarmId))
        {
            return null;
        }

        List<AlarmItem> alarms = alarmManager.GetAlarms();
        for (int i = 0; i < alarms.Count; i++)
        {
            AlarmItem alarmItem = alarms[i];
            if (alarmItem != null && alarmItem.id == alarmId)
            {
                return alarmItem;
            }
        }

        return null;
    }

    private AlarmManager GetAlarmManager()
    {
        alarm = ResolveManagedUI(alarm, "Alarm");
        if (alarm != null)
        {
            AlarmManager manager = alarm.GetComponent<AlarmManager>();
            if (manager != null)
            {
                return manager;
            }
        }

        return FindSceneComponentIncludingInactive<AlarmManager>();
    }

    private AlarmUI GetAlarmUI()
    {
        alarm = ResolveManagedUI(alarm, "Alarm");
        if (alarm != null)
        {
            AlarmUI alarmUI = alarm.GetComponent<AlarmUI>();
            if (alarmUI != null)
            {
                return alarmUI;
            }
        }

        return FindSceneComponentIncludingInactive<AlarmUI>();
    }

    public void OnCalendarDateSelected(System.DateTime date)
    {
        ShowTODOList(date);
        RepositionTODOListBesideCalendar();
    }

    private void RepositionTODOListBesideCalendar()
    {
        if (todoList == null || calendar == null)
        {
            RepositionSimpleUI(todoList, "todolist");
            return;
        }

        RectTransform calendarRect = calendar.GetComponent<RectTransform>();
        if (calendarRect == null)
        {
            calendarRect = GetChildRect(calendar.transform, "CalendarPicker");
        }

        RectTransform rootRect = todoList.GetComponent<RectTransform>();
        if (rootRect == null || calendarRect == null)
        {
            RepositionSimpleUI(todoList, "todolist");
            return;
        }

        Canvas.ForceUpdateCanvases();

#if UNITY_ANDROID && !UNITY_EDITOR
        // MR: 이 오프셋은 캔버스 픽셀 단위다. 월드 스페이스로 전환된 패널들은 각자
        // 독립 캔버스라 anchoredPosition이 사실상 부모(WorldUI/Panels, scale 1) 기준
        // 로컬 위치가 된다 — 340을 그대로 대입하면 340 m 밖으로 날아간다.
        // 실측(2026-08-18): 캘린더에서 날짜를 누른 뒤 TODOList가 눈에서 339.65 m에 놓였다.
        // 미터로 환산해 캘린더 오른쪽에 붙인다.
        const float TodoOffsetMetersX = 0.34f;
        const float TodoOffsetMetersY = -0.01f;

        Vector3 calendarWorld = calendarRect.position;
        Vector3 right = calendarRect.right;
        Vector3 up = calendarRect.up;

        rootRect.position = calendarWorld
                          + right * TodoOffsetMetersX
                          + up * TodoOffsetMetersY;
        rootRect.rotation = calendarRect.rotation;
#else
        float todoOffsetX = 340f;
        float todoOffsetY = -10f;
        float todoOffsetZ = 0f;
        Vector2 todoListCalendarOffset = new Vector2(todoOffsetX, todoOffsetY);
        rootRect.anchoredPosition = calendarRect.anchoredPosition + todoListCalendarOffset;

        Vector3 localPosition = rootRect.localPosition;
        localPosition.z = todoOffsetZ;
        rootRect.localPosition = localPosition;
#endif
    }

    private RectTransform GetChildRect(Transform parent, string childName)
    {
        Transform child = FindDeepChild(parent, childName);
        return child as RectTransform;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void RepositionSimpleUI(GameObject target, string menuName)
    {
        if (target == null || UIPositionManager.Instance == null)
        {
            return;
        }

        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (targetRect != null)
        {
            targetRect.position = UIPositionManager.Instance.GetMenuPosition(menuName);
        }
    }

    private T GetOrCreateTypedManagedUI<T>(ref GameObject current, string objectName, string menuName) where T : Component
    {
        GameObject resolved = ResolveManagedUI(current, objectName);
        if (resolved == null)
        {
            Debug.LogWarning("[UIManager] " + objectName + " prefab or scene object is not assigned.");
            return null;
        }

        current = resolved;
        ShowSimpleUI(current, menuName);
        return EnsureTypedComponent<T>(current);
    }

    private GameObject InstantiateManagedPrefab(GameObject prefab, string objectName)
    {
        Transform parent = null;
        if (CanvasManager.Instance != null && CanvasManager.Instance.canvasUI != null)
        {
            parent = CanvasManager.Instance.canvasUI.transform;
        }

        GameObject obj = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
        obj.name = objectName;
        obj.transform.localScale = Vector3.one;
        obj.SetActive(false);
        return obj;
    }

    private T EnsureTypedComponent<T>(GameObject obj) where T : Component
    {
        if (obj == null)
        {
            return null;
        }

        T component = obj.GetComponent<T>();
        if (component == null)
        {
            component = obj.AddComponent<T>();
        }

        return component;
    }

    private GameObject ResolveManagedUI(GameObject current, string objectName)
    {
        if (current != null && current.scene.IsValid())
        {
            Debug.Log($"[UIManager] ResolveManagedUI({objectName}) using assigned scene object: {current.name}");
            return current;
        }

        if (current != null && !current.scene.IsValid())
        {
            Debug.Log($"[UIManager] ResolveManagedUI({objectName}) instantiating assigned prefab: {current.name}");
            Transform parent = null;
            if (CanvasManager.Instance != null && CanvasManager.Instance.canvasUI != null)
            {
                parent = CanvasManager.Instance.canvasUI.transform;
            }

            GameObject instanceObject = parent != null ? Instantiate(current, parent) : Instantiate(current);
            instanceObject.name = objectName;
            instanceObject.transform.localScale = Vector3.one;
            instanceObject.SetActive(false);
            return instanceObject;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (candidate.gameObject.name != objectName)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid())
            {
                continue;
            }

            Debug.Log($"[UIManager] ResolveManagedUI({objectName}) found scene object: {candidate.gameObject.name}");
            return candidate.gameObject;
        }

        Debug.LogWarning($"[UIManager] ResolveManagedUI({objectName}) failed. Assign it in UIManager or place a scene object named {objectName}.");
        return null;
    }

    private T FindSceneComponentIncludingInactive<T>() where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || component.gameObject == null)
            {
                continue;
            }

            if (!component.gameObject.scene.IsValid())
            {
                continue;
            }

            return component;
        }

        return null;
    }

    private void RefreshAlarmUIRuntime()
    {
        AlarmUI alarmUI = GetAlarmUI();
        if (alarmUI != null)
        {
            alarmUI.RefreshRuntimeViews();
        }
    }

    private void OnAlarmMiniStartRequested(string alarmId)
    {
        AlarmManager alarmManager = GetAlarmManager();
        if (alarmManager == null)
        {
            return;
        }

        alarmManager.StartRelativeTimer(alarmId);
        alarmMiniAlarmId = alarmId;
        RefreshAlarmMini();
        RefreshAlarmUIRuntime();
    }

    private void OnAlarmMiniPauseRequested(string alarmId)
    {
        AlarmManager alarmManager = GetAlarmManager();
        if (alarmManager == null)
        {
            return;
        }

        alarmManager.PauseRelativeTimer(alarmId);
        alarmMiniAlarmId = alarmId;
        RefreshAlarmMini();
        RefreshAlarmUIRuntime();
    }

    private void OnAlarmMiniResetRequested(string alarmId)
    {
        AlarmManager alarmManager = GetAlarmManager();
        if (alarmManager == null)
        {
            return;
        }

        alarmManager.ResetRelativeTimer(alarmId);
        alarmMiniAlarmId = alarmId;
        RefreshAlarmMini();
        RefreshAlarmUIRuntime();
    }

    private void OnAlarmMiniCloseRequested(string alarmId)
    {
        CloseAlarmMini();
    }

    // choiceInputImage Show
    public void ShowChoiceInput()
    {
        if (!choiceInputImage.activeSelf)
        {
            Vector3 position = UIPositionManager.Instance.GetMenuPosition("choiceInput");
            choiceInputImage.GetComponent<RectTransform>().position = position;
        }
        choiceInputImage.SetActive(true);
    }

    // choiceInputImage Hide
    public void HideChoiceInput()
    {
        choiceInputImage.SetActive(false);
    }
}
