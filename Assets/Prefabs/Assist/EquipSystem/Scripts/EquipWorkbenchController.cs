using System.Collections.Generic;
using UnityEngine;

// EquipSystem 워크벤치 런타임 코어: 소켓 보유 캐릭터 로스터 스캔, 선택 전환,
// IMGUI 메인 패널(장착 매트릭스 + 소켓 해제 버튼 + 메시지 로그 링버퍼).
// 장착은 항상 EquipManager.Instance.Equip(target, key, out reason)을 경유한다.
public class EquipWorkbenchController : MonoBehaviour
{
    private static EquipWorkbenchController instance;  // 싱글톤 인스턴스
    public static EquipWorkbenchController Instance
    {
        get
        {
            if (instance == null)
            {
                // 인스턴스가 없으면 씬에서 찾아서 할당
                instance = FindObjectOfType<EquipWorkbenchController>();
            }

            return instance;
        }
    }

    [SerializeField] private EquipCatalog catalog;    // 아이템 카탈로그. 미지정 시 Resources에서 자동 로드
    [SerializeField] private int logCapacity = 12;    // 로그 링버퍼 최대 줄 수
    [SerializeField] private KeyCode cycleKey = KeyCode.Tab;       // 선택 캐릭터 순환
    [SerializeField] private KeyCode refreshKey = KeyCode.F5;      // 로스터 새로고침
    [SerializeField] private KeyCode togglePanelKey = KeyCode.F1;  // 패널 표시/숨김

    private readonly List<GameObject> roster = new List<GameObject>();  // 소켓 보유 캐릭터 목록
    private GameObject selected;                                        // 현재 선택 캐릭터
    private readonly List<string> logLines = new List<string>();        // 메시지 로그 링버퍼
    private readonly Dictionary<string, bool> cellResults = new Dictionary<string, bool>();  // 매트릭스 셀별 마지막 장착 결과
    private Vector2 matrixScroll;   // 장착 매트릭스 스크롤 위치
    private Vector2 rosterScroll;   // 로스터 목록 스크롤 위치
    private Vector2 logScroll;      // 로그 스크롤 위치
    private bool panelVisible = true;  // 메인 패널 표시 여부

    // 소켓 보유 캐릭터 로스터 (읽기용 — 외부에서 수정하지 말 것)
    public List<GameObject> Roster
    {
        get
        {
            return roster;
        }
    }

    // 현재 선택된 캐릭터 (없으면 null)
    public GameObject Selected
    {
        get
        {
            return selected;
        }
    }

    // 카탈로그 확보 (인스펙터 지정 우선, 없으면 Resources)
    private void Awake()
    {
        if (catalog == null)
        {
            catalog = Resources.Load<EquipCatalog>("EquipCatalog");
        }
    }

    // 시작 시 씬을 스캔해 로스터 구성
    private void Start()
    {
        RefreshRoster();
    }

    // 파괴된 캐릭터 정리 + 단축키 처리
    private void Update()
    {
        PruneRoster();

        if (Input.GetKeyDown(cycleKey))
        {
            CycleSelection();
        }

        if (Input.GetKeyDown(refreshKey))
        {
            RefreshRoster();
        }

        if (Input.GetKeyDown(togglePanelKey))
        {
            panelVisible = !panelVisible;
        }
    }

    // 씬에서 EquipSocket 보유 캐릭터를 다시 스캔 (비활성 제외). 기존 선택은 가능하면 유지.
    public void RefreshRoster()
    {
        roster.Clear();

        // FindObjectsOfType 기본 동작이 비활성 오브젝트를 제외한다
        EquipSocket[] sockets = FindObjectsOfType<EquipSocket>();
        foreach (EquipSocket socket in sockets)
        {
            if (socket == null)
            {
                continue;
            }

            GameObject root = ResolveCharacterRoot(socket);
            if (root == null)
            {
                continue;
            }

            if (root.activeInHierarchy == false)
            {
                continue;
            }

            if (roster.Contains(root) == false)
            {
                roster.Add(root);
            }
        }

        // 이름순 정렬로 매트릭스 열 순서를 안정화
        roster.Sort(CompareByName);

        if (selected == null || roster.Contains(selected) == false)
        {
            if (roster.Count > 0)
            {
                selected = roster[0];
            }
            else
            {
                selected = null;
            }
        }

        Log($"로스터 새로고침: {roster.Count}명 발견");
    }

    // 로스터 인덱스로 캐릭터 선택
    public void Select(int index)
    {
        if (index < 0 || index >= roster.Count)
        {
            Log($"선택 실패: 잘못된 인덱스 {index} (로스터 {roster.Count}명)");
            return;
        }

        selected = roster[index];
        Log($"선택: {selected.name}");
    }

    // 메시지 로그 링버퍼에 추가 (콘솔에도 남김)
    public void Log(string msg)
    {
        string line = $"[{System.DateTime.Now:HH:mm:ss}] {msg}";
        logLines.Add(line);

        while (logLines.Count > logCapacity)
        {
            logLines.RemoveAt(0);
        }

        // 새 줄이 보이도록 로그 스크롤을 바닥으로
        logScroll.y = float.MaxValue;
        Debug.Log("[EquipWorkbench] " + msg);
    }

    // 소켓에서 캐릭터 루트를 결정 (Animator 우선, 없으면 transform.root)
    private GameObject ResolveCharacterRoot(EquipSocket socket)
    {
        Animator anim = socket.GetComponentInParent<Animator>();
        if (anim != null)
        {
            return anim.gameObject;
        }

        return socket.transform.root.gameObject;
    }

    // 이름순 비교 (로스터 정렬용)
    private static int CompareByName(GameObject a, GameObject b)
    {
        if (a == null && b == null)
        {
            return 0;
        }
        if (a == null)
        {
            return 1;
        }
        if (b == null)
        {
            return -1;
        }

        return string.CompareOrdinal(a.name, b.name);
    }

    // 파괴된 캐릭터를 로스터에서 제거하고 선택을 복구
    private void PruneRoster()
    {
        for (int i = roster.Count - 1; i >= 0; i--)
        {
            if (roster[i] == null)
            {
                roster.RemoveAt(i);
            }
        }

        if (selected == null && roster.Count > 0)
        {
            selected = roster[0];
        }
    }

    // 선택을 다음 캐릭터로 순환
    private void CycleSelection()
    {
        if (roster.Count == 0)
        {
            Log("순환 실패: 로스터가 비어 있습니다 — 캐릭터 프리팹을 씬에 끌어다 놓고 F5로 새로고침");
            return;
        }

        int index = roster.IndexOf(selected);
        int next = (index + 1) % roster.Count;
        Select(next);
    }

    // 장착 실행 — 실패해도 침묵하지 않고 사유를 로그에 남긴다 (씬에 매니저 없음 안내 가드는 데모 선례상 허용)
    private void DoEquip(GameObject target, string key)
    {
        if (target == null)
        {
            Log("장착 실패: 대상 캐릭터 없음");
            return;
        }

        if (EquipManager.Instance == null)
        {
            Log("씬에 EquipManager 없음 — 빈 GameObject에 EquipManager를 추가하세요");
            return;
        }

        string reason;
        bool ok = EquipManager.Instance.Equip(target, key, out reason);
        cellResults[CellKey(target, key)] = ok;

        if (ok)
        {
            Log($"장착 성공: {key} → {target.name}");
        }
        else
        {
            Log($"장착 실패: {key} → {target.name} — {reason}");
        }
    }

    // 선택 캐릭터의 특정 슬롯 해제
    private void DoUnequip(GameObject target, string slotId)
    {
        if (target == null)
        {
            Log("해제 실패: 대상 캐릭터 없음");
            return;
        }

        if (EquipManager.Instance == null)
        {
            Log("씬에 EquipManager 없음 — 빈 GameObject에 EquipManager를 추가하세요");
            return;
        }

        EquipManager.Instance.Unequip(target, slotId);
        Log($"해제: {slotId} on {target.name}");
    }

    // 매트릭스 셀 결과 키 (캐릭터 인스턴스 × 카탈로그 키)
    private string CellKey(GameObject target, string key)
    {
        return target.GetInstanceID() + "::" + key;
    }

    // 셀 표시 문자: 미시도 "·" / 성공 "○" / 실패 "×"
    private string CellLabel(GameObject target, string key)
    {
        bool ok;
        if (cellResults.TryGetValue(CellKey(target, key), out ok))
        {
            if (ok)
            {
                return "○";
            }
            return "×";
        }

        return "·";
    }

    // 긴 이름 자르기 (패널 표시용)
    private string ShortName(string name, int max)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "?";
        }

        if (name.Length <= max)
        {
            return name;
        }

        return name.Substring(0, max);
    }

    // 메인 패널: 로스터 / 장착 매트릭스 / 선택 캐릭터 소켓 해제 / 메시지 로그
    private void OnGUI()
    {
        if (panelVisible == false)
        {
            GUI.Label(new Rect(10f, 10f, 300f, 22f), $"{togglePanelKey}: 워크벤치 패널 표시");
            return;
        }

        float panelWidth = 470f;
        float panelHeight = Screen.height - 20f;
        GUILayout.BeginArea(new Rect(10f, 10f, panelWidth, panelHeight), GUI.skin.box);

        GUILayout.Label("Equip Workbench");
        GUILayout.Label($"{cycleKey}: 캐릭터 순환 / {refreshKey}: 로스터 새로고침 / {togglePanelKey}: 패널 숨김");

        // 환경 경고 (없어도 패널은 뜨되 원인을 알린다)
        if (EquipManager.Instance == null)
        {
            GUILayout.Label("경고: 씬에 EquipManager 없음 — 장착 불가");
        }
        if (catalog == null)
        {
            GUILayout.Label("경고: EquipCatalog 없음 — Resources/EquipCatalog.asset 확인");
        }

        DrawRosterSection();
        DrawMatrixSection();
        DrawSelectedSection();
        DrawToolsSection();
        DrawLogSection();

        GUILayout.EndArea();
    }

    // 로스터 목록 + 선택 버튼
    private void DrawRosterSection()
    {
        GUILayout.Space(6f);
        GUILayout.Label($"로스터 ({roster.Count}명)");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("새로고침", GUILayout.Width(90f)))
        {
            RefreshRoster();
        }
        GUILayout.EndHorizontal();

        if (roster.Count == 0)
        {
            GUILayout.Label("캐릭터 없음 — 소켓 있는 캐릭터 프리팹을 씬에 끌어다 놓고 새로고침");
            return;
        }

        rosterScroll = GUILayout.BeginScrollView(rosterScroll, GUILayout.Height(90f));
        for (int i = 0; i < roster.Count; i++)
        {
            GameObject character = roster[i];
            if (character == null)
            {
                continue;
            }

            string marker = "    ";
            if (character == selected)
            {
                marker = "▶ ";
            }

            if (GUILayout.Button($"{marker}[{i + 1}] {character.name}"))
            {
                Select(i);
            }
        }
        GUILayout.EndScrollView();
    }

    // 장착 매트릭스: 행 = 카탈로그 엔트리, 열 = 로스터 캐릭터. 셀 클릭 = 해당 캐릭터에 장착 시도.
    private void DrawMatrixSection()
    {
        GUILayout.Space(6f);
        GUILayout.Label("장착 매트릭스 (셀 클릭 = 장착 시도, ○ 성공 / × 실패 / · 미시도)");

        if (catalog == null)
        {
            GUILayout.Label("카탈로그 없음 — 매트릭스 표시 불가");
            return;
        }

        IReadOnlyList<EquipEntry> entries = catalog.Entries;
        if (entries == null || entries.Count == 0)
        {
            GUILayout.Label("카탈로그가 비어 있습니다");
            return;
        }

        if (roster.Count == 0)
        {
            GUILayout.Label("로스터가 비어 있습니다");
            return;
        }

        matrixScroll = GUILayout.BeginScrollView(matrixScroll, GUILayout.Height(200f));

        // 헤더 행: 캐릭터 인덱스 (이름은 로스터 목록에서 대응)
        GUILayout.BeginHorizontal();
        GUILayout.Label("키", GUILayout.Width(170f));
        for (int c = 0; c < roster.Count; c++)
        {
            GUILayout.Label($"{c + 1}", GUILayout.Width(34f));
        }
        GUILayout.EndHorizontal();

        // 엔트리 행
        foreach (EquipEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key))
            {
                continue;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(ShortName(entry.key, 22), GUILayout.Width(170f));

            for (int c = 0; c < roster.Count; c++)
            {
                GameObject character = roster[c];
                if (character == null)
                {
                    GUILayout.Label("-", GUILayout.Width(34f));
                    continue;
                }

                if (GUILayout.Button(CellLabel(character, entry.key), GUILayout.Width(34f)))
                {
                    DoEquip(character, entry.key);
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();
    }

    // 선택 캐릭터의 소켓 목록 + 슬롯별 해제 버튼
    private void DrawSelectedSection()
    {
        GUILayout.Space(6f);

        if (selected == null)
        {
            GUILayout.Label("선택 캐릭터: (없음)");
            return;
        }

        GUILayout.Label($"선택 캐릭터: {selected.name}");

        EquipSocket[] sockets = selected.GetComponentsInChildren<EquipSocket>(true);
        if (sockets.Length == 0)
        {
            GUILayout.Label("소켓 없음");
            return;
        }

        foreach (EquipSocket socket in sockets)
        {
            if (socket == null)
            {
                continue;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(ShortName(socket.slotId, 26), GUILayout.Width(220f));
            if (GUILayout.Button("해제", GUILayout.Width(60f)))
            {
                DoUnequip(selected, socket.slotId);
            }
            GUILayout.EndHorizontal();
        }
    }

    // 도구 섹션: 스모크 회귀 테스트 / 코디 / 스케일 테스트 (EquipWorkbenchTools 경유)
    private void DrawToolsSection()
    {
        GUILayout.Space(6f);
        GUILayout.Label("도구 (스모크는 전 로스터, 코디/스케일은 선택 캐릭터)");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("스모크 테스트"))
        {
            Log("스모크 시작 — 전 캐릭터의 기존 코디가 전부 해제됩니다");
            List<string> report = EquipWorkbenchTools.RunSmokeTest(roster);
            LogReportSummary(report);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("전부 장착"))
        {
            LogReportSummary(EquipWorkbenchTools.EquipAll(selected));
        }
        if (GUILayout.Button("전부 해제"))
        {
            Log(EquipWorkbenchTools.UnequipAll(selected));
        }
        if (GUILayout.Button("랜덤 코디"))
        {
            LogReportSummary(EquipWorkbenchTools.EquipRandom(selected));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("스케일 ×10"))
        {
            Log(EquipWorkbenchTools.ApplyScale(selected, 10f));
        }
        if (GUILayout.Button("스케일 ×0.1"))
        {
            Log(EquipWorkbenchTools.ApplyScale(selected, 0.1f));
        }
        // 버튼을 조건부로 없앴다 붙이면 같은 프레임 Layout/Repaint 컨트롤 수가 어긋나 IMGUI 예외가 날 수 있어
        // 항상 그리되 비활성화로 처리
        bool canRestore = selected != null && EquipWorkbenchTools.HasSavedScale(selected);
        bool prevEnabled = GUI.enabled;
        GUI.enabled = prevEnabled && canRestore;
        if (GUILayout.Button("스케일 복원"))
        {
            Log(EquipWorkbenchTools.RestoreScale(selected));
        }
        GUI.enabled = prevEnabled;
        GUILayout.EndHorizontal();
    }

    // 도구 리포트를 링버퍼에 반영 — 성공/복구 라인은 생략하고 실패·요약만 남긴다 (전체는 Tools가 콘솔에 이미 기록)
    private void LogReportSummary(List<string> report)
    {
        if (report == null)
        {
            return;
        }

        foreach (string line in report)
        {
            if (line.StartsWith("OK"))
            {
                continue;
            }
            if (line.StartsWith("복구"))
            {
                continue;
            }

            Log(line);
        }
    }

    // 메시지 로그 링버퍼 표시
    private void DrawLogSection()
    {
        GUILayout.Space(6f);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"로그 (최근 {logCapacity}줄)");
        if (GUILayout.Button("지우기", GUILayout.Width(60f)))
        {
            logLines.Clear();
        }
        GUILayout.EndHorizontal();

        logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(140f));
        foreach (string line in logLines)
        {
            GUILayout.Label(line);
        }
        GUILayout.EndScrollView();
    }
}
