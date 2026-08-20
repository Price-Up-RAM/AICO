// 시스템 메뉴 — 빈 공간을 보며 palm-up 탭했을 때 뜨는 메뉴
// (MR_Phase4A_SystemMenu_Design.md).
//
// 역할: MR 고유 제어 + 패널 진입점 + 종료. **설정값은 담지 않는다.**
// 볼륨·캐릭터 크기·자동 스몰토크 주기 같은 것은 전부 KAI의 Tab Window_Settings(SettingManager)에
// 이미 있다. 여기에 또 두면 값 소스가 둘이 된다 — 설계서 §0-2, §0-4 참고.
//
// 왜 패널 진입점이 여기 있나
// ------------------------
// 데스크톱에는 트레이 아이콘·단축키·창 목록이 있지만 MR에는 없다.
// Calendar / TODOList / Alarm / CharChange / Jukebox / Settings를 정식으로 열 수단이
// 이 메뉴 말고는 없다. 그래서 임시 디버그 컴포넌트(MRLegacyOpenDebugTrigger)를 못 지우고 있었다.
//
// MRCharacterContextMenu와 ContextMenu 위젯을 **공유한다.**
// 저쪽도 Show()마다 Clear() 후 항목을 다시 쌓는 빌드-온-오픈 구조라 그대로 따르면 된다.
// 두 메뉴는 제스처가 배타적이라(캐릭터 조준 vs 빈 공간) 동시에 뜰 일이 없다.
//
// 배치는 건드리지 않는다 — Context Menu 위젯이 placeInFrontOnShow = 1이라
// alpha 상승 감시(§4-44)가 사용자 정면에 알아서 놓고, 닫히면 히트박스도 알아서 끈다(§8-7).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DevionGames.UIWidgets;
using ContextMenu = DevionGames.UIWidgets.ContextMenu;

public class MRSystemContextMenu : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private ContextMenu contextMenu;

    [Tooltip("비우면 씬에서 찾는다. 씬에 없으면 '공간' 항목을 아예 만들지 않는다.")]
    [SerializeField] private MRSpatialAnchorEditor spatialAnchorEditor;

    [Header("패널 모으기 — 부채꼴 배치")]
    [Tooltip("사용자로부터의 거리(m).")]
    [SerializeField] private float arcRadius = 1.1f;

    [Tooltip("부채꼴 전체가 덮는 각도(도).")]
    [SerializeField] private float arcSpan = 140f;

    [Header("진단")]
    [SerializeField] private bool logSkippedItems = true;

    private Transform _eye;

    private void Awake()
    {
        if (spatialAnchorEditor == null)
        {
            spatialAnchorEditor = FindFirstObjectByType<MRSpatialAnchorEditor>();
        }
    }

    // 빈 공간을 조준한 상태에서 MRIntentRouter가 호출한다.
    public void Show()
    {
        if (contextMenu == null)
        {
            Debug.LogWarning("[MRSystemContextMenu] contextMenu가 Inspector에 배선되지 않았습니다.");
            return;
        }

        contextMenu.Clear();

        string targetLang = "ko";
        if (SettingManager.Instance != null)
        {
            targetLang = SettingManager.Instance.settings.ui_language;
        }

        AddSpaceItems(targetLang);
        AddOpenItems(targetLang);

        contextMenu.AddMenuItem(LanguageData.Translate("Gather Panels", targetLang), delegate {
            GatherOpenPanels();
        });

        // 종료에 한 겹 둔다 — MR에서 오조작으로 앱이 꺼지면 복구가 비싸다.
        contextMenu.AddSubMenuItem(LanguageData.Translate("Exit", targetLang), new List<(string, UnityAction)>
        {
            (LanguageData.Translate("Confirm Exit", targetLang), delegate {
                RequestExit();
            }),
        });

        contextMenu.ShowAt(transform.position);
    }

    // 공간 앵커 — MRSpatialAnchorEditor가 씬에 있을 때만 만든다.
    //
    // 없는데 항목만 만들면 spatialAnchorEditor?.X() 널 가드에 먹혀 **눌러도 아무 일이 없다**(§4-51).
    // 회색 처리도 하지 않는다 — "눌리는데 무반응"과 "왜 못 누르지"를 사용자가 판단하게 만들 이유가 없다.
    //
    // 참고: 원본(MRSampleScene)의 앵커 UI는 버튼 2개뿐이다. "앵커 초기화"라는 라벨의 버튼이
    // 실제로는 LaunchSceneCapture(방 재스캔)에 배선돼 있었고, ResetAllAnchors는 어디에도
    // 배선돼 있지 않았다. RebuildEffectMesh는 LaunchSceneCapture 내부에서 호출된다.
    private void AddSpaceItems(string targetLang)
    {
        if (spatialAnchorEditor == null)
        {
            if (logSkippedItems)
            {
                Debug.Log("[MRSystemContextMenu] MRSpatialAnchorEditor가 씬에 없어 '공간' 항목을 건너뜁니다 (Phase 2).");
            }
            return;
        }

        contextMenu.AddSubMenuItem(LanguageData.Translate("Space", targetLang), new List<(string, UnityAction)>
        {
            (LanguageData.Translate("Edit Anchors", targetLang), delegate {
                spatialAnchorEditor.ToggleEditMode();
            }),
            (LanguageData.Translate("Rescan Room", targetLang), delegate {
                spatialAnchorEditor.LaunchSceneCapture();
            }),
        });
    }

    // 패널 열기 — 전부 UIManager의 정식 경로를 통한다(§4-37).
    private void AddOpenItems(string targetLang)
    {
        if (UIManager.Instance == null)
        {
            Debug.LogWarning("[MRSystemContextMenu] UIManager.Instance가 없어 '열기' 항목을 만들 수 없습니다.");
            return;
        }

        var items = new List<(string, UnityAction)>
        {
            (LanguageData.Translate("Calendar", targetLang), delegate {
                UIManager.Instance.ShowCalendar();
            }),
            // "TODOList"는 LanguageData에 등록된 키다 — "TODO List"로 띄어 쓰면 번역이 안 걸린다.
            (LanguageData.Translate("TODOList", targetLang), delegate {
                UIManager.Instance.ShowTODOList();
            }),
            (LanguageData.Translate("Alarm", targetLang), delegate {
                UIManager.Instance.ShowAlarm();
            }),
            // "Change Char"가 LanguageData에 이미 등록된 키다 — 중복 엔트리를 만들지 않는다.
            (LanguageData.Translate("Change Char", targetLang), delegate {
                UIManager.Instance.ShowCharChange();
            }),
        };

        // 주크박스는 씬 오브젝트로 배선됐을 때만 넣는다.
        //
        // UIManager.ResolveManagedUI는 배선된 것이 **프리팹**이면 CanvasManager.canvasUI 아래로
        // 인스턴스화하는데, 그게 메인 Canvas(월드 1920 m, §4-36)라 §4-18(캔버스 안의 캔버스)에 걸린다.
        // 씬에 미리 배치해야 current.scene.IsValid() 경로를 탄다 — 기존 패널 7개가 그렇게 되어 있다.
        if (IsSceneObject(UIManager.Instance.jukebox))
        {
            items.Add((LanguageData.Translate("Jukebox", targetLang), delegate {
                UIManager.Instance.ShowJukebox();
            }));
        }
        else if (logSkippedItems)
        {
            Debug.Log("[MRSystemContextMenu] UIManager.jukebox가 씬 오브젝트로 배선되지 않아 '주크박스'를 건너뜁니다. " +
                      "JukeboxView 프리팹을 씬에 배치하고 Tools → MR → 9로 전환한 뒤 배선하세요.");
        }

        // 설정 — 메서드 이름의 s가 소문자다(UIWidget 경로).
        items.Add((LanguageData.Translate("Settings", targetLang), delegate {
            UIManager.Instance.showSettings();
        }));

        contextMenu.AddSubMenuItem(LanguageData.Translate("Open", targetLang), items);
    }

    // 열려 있는 패널을 전부 사용자 앞 부채꼴로 다시 놓는다 (설계서 §3-2, §7-2 해소).
    //
    // 전부 PlaceInFront()만 부르면 같은 자리에 겹치므로 부채꼴로 흩는다.
    // 배치 계산은 폐기하는 MRWorldUIDebugToggle.ArcPosition()에서 가져왔다.
    //
    // §4-27("사용자가 옮기면 그 자리에 남는다")을 의도적으로 덮어쓴다 —
    // 사용자가 명시적으로 부른 회수 동작이므로 맞다.
    public void GatherOpenPanels()
    {
        // 닫힌 패널은 Close()가 SetActive(false)를 하므로 이 API에 잡히지 않는다 — 의도된 동작이다.
        MRFloatingPanel[] all = FindObjectsByType<MRFloatingPanel>(FindObjectsSortMode.None);

        var open = new List<MRFloatingPanel>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (!all[i].IsOpen) continue;

            open.Add(all[i]);
        }

        if (open.Count == 0)
        {
            Debug.Log("[MRSystemContextMenu] 열려 있는 패널이 없습니다.");
            return;
        }

        for (int i = 0; i < open.Count; i++)
        {
            open[i].OpenAt(ArcPosition(i, open.Count));
        }

        Debug.Log($"[MRSystemContextMenu] 패널 {open.Count}개를 눈앞으로 모았습니다.");
    }

    private Vector3 ArcPosition(int index, int total)
    {
        Transform eye = ResolveEye();
        if (eye == null) return transform.position;

        float t = 0.5f;
        if (total > 1)
        {
            t = (float)index / (total - 1);
        }

        float angle = Mathf.Lerp(-arcSpan * 0.5f, arcSpan * 0.5f, t);

        Vector3 forward = eye.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
        Vector3 pos = eye.position + dir * arcRadius;

        // 개수가 많으면 위아래 두 줄로 나눠 겹침을 더 줄인다.
        if (index % 2 == 0)
        {
            pos.y = eye.position.y - 0.15f;
        }
        else
        {
            pos.y = eye.position.y + 0.2f;
        }
        return pos;
    }

    // Camera.main은 OVR 리그에서 null인 프레임이 있어 CenterEyeAnchor를 우선한다
    // (MRFloatingPanel.ResolveEye와 같은 규약, §4-28 정정 참고).
    private Transform ResolveEye()
    {
        if (_eye != null) return _eye;

        GameObject center = GameObject.Find("CenterEyeAnchor");
        if (center != null)
        {
            _eye = center.transform;
            return _eye;
        }

        if (Camera.main != null)
        {
            _eye = Camera.main.transform;
            return _eye;
        }

        return null;
    }

    // 씬에 실재하는 오브젝트인가 — 프리팹 에셋이면 false다.
    private bool IsSceneObject(GameObject go)
    {
        if (go == null) return false;

        return go.scene.IsValid();
    }

    // 이 메뉴를 닫는다. MRIntentRouter의 "빈 공간 + 더블탭 = 전부 닫기"가 부른다.
    public void CloseAll()
    {
        if (contextMenu != null) contextMenu.Close();
    }

    public bool IsAnyOpen
    {
        get
        {
            if (contextMenu != null && contextMenu.IsVisible) return true;

            return false;
        }
    }

    private void RequestExit()
    {
        Debug.Log("[MRSystemContextMenu] Exit 요청됨.");
        Application.Quit();
    }
}
