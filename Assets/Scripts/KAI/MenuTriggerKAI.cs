// KAI 제출용 프로토타입 전용 — MenuTrigger.cs 사본 (원본 Assets/Scripts/MenuTrigger.cs는 수정 금지)
// SampleSceneKAI에서 KAIManager가 기존 MenuTrigger를 이 컴포넌트로 in-place 교체한다.
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using DevionGames.UIWidgets;
using ContextMenu = DevionGames.UIWidgets.ContextMenu;
using UnityEngine.EventSystems;
using System;
using UnityEngine.Events;

public class MenuTriggerKAI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private TransparentWindow _transparentWindow;
    private CharAttributes _charAttributes;
    private ContextMenu m_ContextMenu;
    private ContextMenu m_ContextMenuSub;
    private bool itemChkFlag = false;
    private float chkTimer = 0f; // 타이머 변수 추가
    private bool isLeftClickHeld = false; // 좌클릭 상태
    private float leftClickHoldTime = 0f; // 좌클릭 누른 시간

    // 더블클릭 감지용 변수들
    private float lastClickTime = 0f;
    private int clickCount = 0;
    private const float doubleClickTime = 0.3f; // 더블클릭 판정 시간

    private RadialMenu m_RadialMenuAction;

    // Start is called before the first frame update
    private void Start()
    {
        this.m_ContextMenu = WidgetUtility.Find<ContextMenu>("ContextMenu");
        this.m_ContextMenuSub = WidgetUtility.Find<ContextMenu>("ContextMenuSub"); // SubMenu 인스턴스 직접 참조
        this._transparentWindow = FindObjectOfType<TransparentWindow>();  // GameObject에 있음
        this._charAttributes = GetComponent<CharAttributes>();
        if (this._charAttributes == null)
        {
            this._charAttributes = GetComponentInParent<CharAttributes>();
        }
        if (this._charAttributes == null)
        {
            this._charAttributes = FindObjectOfType<CharAttributes>();
        }
        this.m_RadialMenuAction = WidgetUtility.Find<RadialMenu>("RadialMenuAction");

    }

    private void Update()
    {
        // MR 포팅 널 가드:
        // m_ContextMenu / m_RadialMenuAction 은 Start()에서 WidgetUtility.Find 로 Canvases/Canvas 하위 위젯을 찾는다.
        // 해당 캔버스가 비활성이거나(성능 테스트·월드스페이스 전환 중) 위젯이 없는 씬에서는 null이 되는데,
        // 가드가 없으면 매 프레임 NullReferenceException이 발생하고 스택 트레이스 문자열 생성으로
        // 프레임당 수 KB의 GC 할당이 폭주한다. (Quest 실측: 프레임당 2회, 초당 ~800KB)
        // itemCheck가 null이 아닌데, active가 아님 = 메뉴가 꺼짐
        if (itemChkFlag)
        {
            // 타이머 갱신
            if (chkTimer > 0f)
            {
                chkTimer -= Time.deltaTime;
                return;
            }

            // 메뉴가 보이는 중 (메뉴가 아예 없으면 '꺼짐'으로 간주해 플래그를 해제한다)
            if (m_ContextMenu == null || !m_ContextMenu.IsVisible)  // 자체제공함수
            {
                if (StatusManager.Instance != null)
                {
                    StatusManager.Instance.IsOptioning = false;
                }
                itemChkFlag = false; // 한번 처리 후 flag 초기화
            }
        }

        // 좌클릭 상태 체크
        if (isLeftClickHeld && StatusManager.Instance != null && !StatusManager.Instance.IsDragging)
        {
            leftClickHoldTime += Time.deltaTime;
            if (leftClickHoldTime >= 0.5f) // 0.5초 이상 누르면 우클릭 동작 실행
            {
                isLeftClickHeld = false; // 상태 초기화
                leftClickHoldTime = 0f;
                TriggerMenu();
            }
        }

        // 더블클릭 타이머 관리
        if (clickCount > 0 && Time.time - lastClickTime > doubleClickTime)
        {
            clickCount = 0; // 더블클릭 시간 초과 시 리셋
        }

        // Radial Menu Action이 보이는 중 (위젯이 없는 씬에서는 건너뛴다)
        if (m_RadialMenuAction != null && m_RadialMenuAction.IsVisible)  // 자체제공함수
        {
            UpdateRadialMenuActionPosition();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            TriggerMenu();
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            // 더블클릭 감지 로직
            if (Time.time - lastClickTime < doubleClickTime && clickCount == 1)
            {
                OnDoubleClick();
                clickCount = 0; // 더블클릭 처리 후 리셋
            }
            else
            {
                clickCount = 1;
                lastClickTime = Time.time;
            }

            isLeftClickHeld = true;
            leftClickHoldTime = 0f; // 타이머 초기화
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            isLeftClickHeld = false;
            leftClickHoldTime = 0f; // 타이머 초기화
        }
    }

    /*
    Context Menu (KAI 제출용 — 완성 기능만 노출)
    ├ Settings (세팅 창 열기)
    ├ Character Detail (현재 캐릭터(AICO) 상세 패널)
    ├ Action (원형 메뉴: 이동/행동 등)
    ├ Chat
    │   ├ New Chat (대화 메모리 초기화)
    │   ├ Chat History (채팅 이력 보기)
    │   └ Idle Talk (자율 발화 시작)
    ├ Mode
    │   └ Chat / Pomodoro / Operator (현재 모드는 회색)
    ├ Control
    │   ├ Show Voice Panel (음성입력 리모컨)
    │   ├ Show/Hide TalkInfo (음성 텍스트 토글)
    │   └ Set Screenshot Area (스크린샷 범위 설정)
    └ Exit (프로그램 종료)

    원본 대비 제외: Character(변경/소환/의상), Guideline, Situation, OCR, 기능(Function),
    Experiment/Dev/Debug, Version
    */
    private void TriggerMenu()
    {
        this.m_ContextMenu.Clear();

        string targetLang = SettingManager.Instance.settings.ui_language; // 0 : ko, 1 : jp, 2: en

        // setting
        // Setting은 언어 상관없이 영어로 (원본과 동일)
        m_ContextMenu.AddMenuItem("Settings", delegate {
            UIManager.Instance.showSettings();
        });

        bool isLocalAiInstalled =
            InstallerManager.Instance.IsJarvisServerInstalled() &&
            InstallStatusManager.Instance.IsStatus("full");

        if (!isLocalAiInstalled)
        {
            m_ContextMenu.AddMenuItem(GetLocalAiInstallMenuLabel(targetLang), delegate {
                InstallerManager.Instance.RunInstaller();
            });
        }

        // Character Detail — 현재 캐릭터(AICO) 상세
        m_ContextMenu.AddMenuItem(
            LanguageData.Translate("Character", targetLang) + " " + LanguageData.Translate("Detail", targetLang),
            delegate { ShowCurrentCharacterDetail(); });

        // Action — 원형 액션 메뉴
        m_ContextMenu.AddMenuItem(LanguageData.Translate("Action", targetLang), delegate {
            OnPointerDownRadialMenuAction();
        });

        // Chat - 채팅
        m_ContextMenu.AddSubMenuItem(LanguageData.Translate("Chat", targetLang), new List<(string, UnityAction)>
        {
            (LanguageData.Translate("New Chat", targetLang), delegate {
                MemoryManager.Instance.ResetConversationMemoryAndGuide();
            }),
            (LanguageData.Translate("Chat History", targetLang), delegate { UIManager.Instance.ShowChatHistory(); }),
            (LanguageData.Translate("Idle Talk", targetLang), async delegate {
                if (!await InstallStatusManager.Instance.CheckAndOperateFullAsync())
                {
                    return;
                }
                string purpose = "잡담"; // 기본 목적
                APIManager.Instance.CallSmallTalkStream(purpose);
            }), // 잡담
        });

        // Mode - 대화 모드 전환 (일반/포모도로/오퍼레이터). 현재 모드는 회색 표시.
        ChatMode currentChatMode = ChatModeManager.Instance != null ? ChatModeManager.Instance.CurrentMode : ChatMode.Chat;
        m_ContextMenu.AddSubMenuItem(LanguageData.Translate("Mode", targetLang), new List<(string, UnityAction)>
        {
            (LanguageData.Translate("Chat Mode", targetLang),
                currentChatMode != ChatMode.Chat
                ? (UnityAction)(() => { ChatModeManager.Instance.SetMode(ChatMode.Chat); })
                : null  // 회색 글씨 (현재 모드)
            ),
            (LanguageData.Translate("Pomodoro Mode", targetLang),
                currentChatMode != ChatMode.Pomodoro
                ? (UnityAction)(() => { ChatModeManager.Instance.SetMode(ChatMode.Pomodoro); })
                : null  // 회색 글씨 (현재 모드)
            ),
            // (LanguageData.Translate("OPERATOR MODE", targetLang),
            //     currentChatMode != ChatMode.Operator
            //     ? (UnityAction)(() => { ChatModeManager.Instance.SetMode(ChatMode.Operator); })
            //     : null  // 회색 글씨 (현재 모드)
            // ),
        });

        // Control - 제어
        m_ContextMenu.AddSubMenuItem(LanguageData.Translate("Control", targetLang), new List<(string, UnityAction)>
        {
            (LanguageData.Translate("Show Voice Panel", targetLang), delegate { TalkMenuManager.Instance.ShowTalkMenu(); }),
            (
                NoticeBalloonManager.Instance.noticeBalloon.activeSelf ?
                LanguageData.Translate("Hide TalkInfo", targetLang) :
                LanguageData.Translate("Show TalkInfo", targetLang),
                delegate {
                    if (NoticeBalloonManager.Instance.noticeBalloon.activeSelf)
                    {
                        NoticeBalloonManager.Instance.HideNoticeBalloon();
                    }
                    else
                    {
                        NoticeBalloonManager.Instance.ShowNoticeBalloon();
                    }
                }
            ),
            // 접근성 모드에서는 메뉴 항목을 유지하되 ContextMenu의 Disabled 색상으로 표시한다.
            (LanguageData.Translate("Set Screenshot Area", targetLang), (UnityAction)null),
        });

        // Exit
        m_ContextMenu.AddMenuItem(LanguageData.Translate("Exit", targetLang), delegate {
            if (_transparentWindow != null)
            {
                _transparentWindow.Quit();
            }
            else
            {
                Application.Quit();
            }
        });

        // 메뉴 보이기
        this.m_ContextMenu.ShowAtScreenPosition(Input.mousePosition);

        // StatusManager 관리 (1초 후)
        StatusManager.Instance.IsOptioning = true;

        chkTimer = 1f;
        itemChkFlag = true;
    }

    // 현재 캐릭터의 character_database.json 엔트리를 찾아 CharacterDetail 패널 표시
    // (characterId 규칙은 CharacterDetailStateManager.BuildCharacterId와 동일: charcode 소문자, 없으면 nickname)
    private static string GetLocalAiInstallMenuLabel(string language)
    {
        switch (language)
        {
            case "ko": return "로컬 AI 설치";
            case "jp": return "ローカルAIをインストール";
            default: return "Install Local AI";
        }
    }

    private void ShowCurrentCharacterDetail()
    {
        GameObject currentChar = CharManager.Instance != null ? CharManager.Instance.GetCurrentCharacter() : null;
        CharAttributes attrs = currentChar != null ? currentChar.GetComponent<CharAttributes>() : _charAttributes;

        string characterId = null;
        if (attrs != null)
        {
            if (!string.IsNullOrEmpty(attrs.charcode))
            {
                characterId = attrs.charcode.ToLower();
            }
            else if (!string.IsNullOrEmpty(attrs.nickname))
            {
                characterId = attrs.nickname.ToLower();
            }
        }

        ChangeCharInfo charInfo = characterId != null ? CharManager.Instance.FindCharacterInfoByCharacterId(characterId) : null;
        if (charInfo == null)
        {
            Debug.LogWarning($"[MenuTriggerKAI] character_database에서 캐릭터를 찾지 못했습니다: {characterId}");
            return;
        }

        ChangeCharClothesInfo clothes = (charInfo.clothesList != null && charInfo.clothesList.Count > 0) ? charInfo.clothesList[0] : null;
        UIManager.Instance.ShowCharacterDetail(charInfo, clothes);
    }

    // 더블클릭 시 호출되는 메서드 (현재는 메뉴를 띄우지만, 나중에 다른 기능으로 변경될 수 있음)
    private void OnDoubleClick()
    {
        TriggerMenu();
    }

    // Sub - RadialMenu를 위한 전용 함수들

    // Action
    private void OnPointerDownRadialMenuAction() {
        Vector2 characterTransformPos = StatusManager.Instance.characterTransform.anchoredPosition;
        m_RadialMenuAction.characterTransformPos = new Vector2(characterTransformPos.x, characterTransformPos.y + 200 * SettingManager.Instance.settings.char_size / 100f + 100);
        m_RadialMenuAction.Show();
    }

    // AnswerBalloon의 위치를 캐릭터 바로 위로 조정하는 함수
    private void UpdateRadialMenuActionPosition()
    {
        Vector2 characterTransformPos = StatusManager.Instance.characterTransform.anchoredPosition;
        m_RadialMenuAction.GetComponent<RectTransform>().anchoredPosition = new Vector2(characterTransformPos.x, characterTransformPos.y + 200 * SettingManager.Instance.settings.char_size / 100f + 100);
    }
}
