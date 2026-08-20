// 캐릭터 메뉴 — 캐릭터를 조준하고 트리거했을 때 뜨는 메뉴 (SampleSceneKAI_MR_Port_Plan.md §7).
//
// MenuTriggerKAI(데스크톱 KAI 씬의 컨텍스트 메뉴)에서 "캐릭터와 관련된 항목"만 추려 분리했다.
// Settings / Function(Inventory·Store·Skill) / Control / Exit은 시스템 메뉴 쪽으로 갔다.
// (2026-08-19: 시스템 메뉴가 MRSystemMenuController → MRSystemContextMenu로 바뀌었다.
//  두 메뉴는 같은 ContextMenu 위젯을 공유한다 — MR_Phase4A_SystemMenu_Design.md §2.)
//
// 이 스크립트는 "무엇이 들어있는가"만 확정한다 — 메뉴를 여는 트리거(캐릭터 조준 + 탭 두 번)는
// Phase 4의 MRIntentRouter가 담당한다. 지금은 Show()를 외부에서 호출하면 내용이 뜨는 수준까지만.
//
// DevionGames ContextMenu를 그대로 재사용한다 — MenuItem이 IPointerClickHandler라
// PointableCanvasModule(ISDK)이 클릭을 알아서 배달해준다 (MR_Phase3-2_Canvas_Plan.md §3-2-D 참고).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DevionGames.UIWidgets;
using ContextMenu = DevionGames.UIWidgets.ContextMenu;

public class MRCharacterContextMenu : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ContextMenu contextMenu;
    [SerializeField] private RadialMenu radialMenuAction;

    /// <summary>캐릭터를 조준한 상태에서 호출한다. 메뉴는 이 오브젝트의 위치(보통 캐릭터 근처)에 뜬다.</summary>
    public void Show()
    {
        if (contextMenu == null)
        {
            Debug.LogWarning("[MRCharacterContextMenu] contextMenu가 Inspector에 배선되지 않았습니다.");
            return;
        }
        if (SettingManager.Instance == null)
        {
            Debug.LogWarning("[MRCharacterContextMenu] SettingManager.Instance가 없습니다.");
            return;
        }

        contextMenu.Clear();
        string targetLang = SettingManager.Instance.settings.ui_language; // 0: ko, 1: jp, 2: en

        // Character Detail
        contextMenu.AddMenuItem(LanguageData.Translate("Character Detail", targetLang), delegate {
            ShowCurrentCharacterDetail();
        });

        // Chat
        contextMenu.AddSubMenuItem(LanguageData.Translate("Chat", targetLang), new List<(string, UnityAction)>
        {
            (LanguageData.Translate("New Chat", targetLang), delegate {
                MemoryManager.Instance.ResetConversationMemoryAndGuide();
            }),
            (LanguageData.Translate("Chat History", targetLang), delegate {
                UIManager.Instance.ShowChatHistory();
            }),
            (LanguageData.Translate("Idle Talk", targetLang), async delegate {
                if (!await InstallStatusManager.Instance.CheckAndOperateFullAsync()) return;
                APIManager.Instance.CallSmallTalkStream("잡담");
            }),
        });

        // Action — 원형 메뉴(이동/행동)
        contextMenu.AddMenuItem(LanguageData.Translate("Action", targetLang), delegate {
            ShowRadialMenuAction();
        });

        // Mode — Chat / Aropla만. Pomodoro·Operator는 이 메뉴에서 제외한다(Port Plan §7).
        // Operator 자체는 유지되지만 손목 보기 제스처 전용 진입점이다(§4-B) — 여기 넣지 않는다.
        ChatMode currentChatMode = ChatModeManager.Instance != null ? ChatModeManager.Instance.CurrentMode : ChatMode.Chat;
        contextMenu.AddSubMenuItem(LanguageData.Translate("Mode", targetLang), new List<(string, UnityAction)>
        {
            (LanguageData.Translate("Chat Mode", targetLang),
                currentChatMode != ChatMode.Chat
                ? (UnityAction)(() => { ChatModeManager.Instance.SetMode(ChatMode.Chat); })
                : null),
            (LanguageData.Translate("Aropla Mode", targetLang),
                currentChatMode != ChatMode.Aropla
                ? (UnityAction)(() => { ChatModeManager.Instance.SetMode(ChatMode.Aropla); })
                : null),
        });

        contextMenu.ShowAt(transform.position);
    }

    public void ShowRadialMenuAction()
    {
        if (radialMenuAction == null) return;
        // MR: 이 캔버스는 캐릭터 전용 월드 스페이스 캔버스이므로 로컬 원점이 곧 캐릭터 위치다.
        // 데스크톱처럼 StatusManager.characterTransform.anchoredPosition을 읽을 필요가 없다.
        radialMenuAction.characterTransformPos = Vector2.zero;
        radialMenuAction.Show();

        // RadialMenu.Show()는 **매번** anchoredPosition을 캔버스 로컬 원점으로 되돌린다
        // (characterTransformPos가 zero일 때의 폴백). 그래서 MRFloatingPanel이 한 번 배치해도
        // 다음 Show에서 다시 원점(y=0, 바닥)으로 끌려간다 — 실기 로그에서 매 회차
        // `RadialMenuAction 위치 = (0.00, 0.00, ...)`로 확인됐다(2026-08-18).
        // alpha 감시로는 못 잡는다: 이 메뉴는 닫히지 않아 alpha가 다시 0으로 내려가지 않는다.
        // 그러므로 Show() **뒤에** 명시적으로 배치한다.
        var radialPanel = radialMenuAction.GetComponent<MRFloatingPanel>();
        if (radialPanel != null) radialPanel.PlaceInFront();
    }

    /// <summary>이 메뉴 계열을 전부 닫는다.
    ///
    /// MR에서는 DevionGames의 "메뉴 밖 클릭 시 닫기"가 성립하지 않는다 —
    /// `PointableCanvasModule.WhenSelected`가 **허공 선택에는 발생하지 않기 때문이다**(§4-13).
    /// 그래서 허공 판정을 `MRIntentRouter` 한 곳에만 두고, 그 신호로 여기를 부른다.
    /// 위젯 쪽에 닫기 버튼이나 타임아웃 같은 임시 수단을 만들지 않기 위해서다.</summary>
    public void CloseAll()
    {
        if (contextMenu != null) contextMenu.Close();
        if (radialMenuAction != null) radialMenuAction.Close();
    }

    /// <summary>지금 이 계열 중 하나라도 열려 있는가.</summary>
    public bool IsAnyOpen
    {
        get
        {
            if (contextMenu != null && contextMenu.IsVisible) return true;
            if (radialMenuAction != null && radialMenuAction.IsVisible) return true;
            return false;
        }
    }

    private void ShowCurrentCharacterDetail()
    {
        GameObject currentChar = CharManager.Instance != null ? CharManager.Instance.GetCurrentCharacter() : null;
        if (currentChar == null)
        {
            MRCharacterWorldRoot worldRoot = FindFirstObjectByType<MRCharacterWorldRoot>();
            if (worldRoot != null) currentChar = worldRoot.CurrentCharacter;
        }

        CharAttributes attrs = currentChar != null ? currentChar.GetComponent<CharAttributes>() : null;

        string characterId = "arona";
        if (attrs != null)
        {
            if (!string.IsNullOrEmpty(attrs.charcode)) characterId = attrs.charcode.ToLower();
            else if (!string.IsNullOrEmpty(attrs.nickname)) characterId = attrs.nickname.ToLower();
        }

        ChangeCharInfo charInfo = CharManager.Instance != null ? CharManager.Instance.FindCharacterInfoByCharacterId(characterId) : null;
        if (charInfo == null)
        {
            Debug.LogWarning($"[MRCharacterContextMenu] character_database에서 캐릭터를 찾지 못했습니다: {characterId}");
            return;
        }

        ChangeCharClothesInfo clothes = (charInfo.clothesList != null && charInfo.clothesList.Count > 0) ? charInfo.clothesList[0] : null;
        UIManager.Instance.ShowCharacterDetail(charInfo, clothes);
    }
}
