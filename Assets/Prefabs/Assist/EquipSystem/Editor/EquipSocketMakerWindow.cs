using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Socket Maker: "클릭 = 후보 → 승인 = 소켓". 커서 레이가 메시에 처음 맞는 곳이 소켓 자리다.
// 악세서리 고스트로 결과를 미리 보며 클릭 = 후보 고정(검수 — 자유 카메라/턴테이블로 확인),
// Enter/[승인] = socket_N 생성(지배 본 자동, 크기 기준 refDist 베이크). Esc = 조준 복귀.
// 기존 placeholder의 재조정(BeginRepick)도 같은 픽 세션으로 — 승인 시 신규 생성 대신 그 소켓을 덮어쓴다.
// 캡슐/콜라이더/표준 슬롯 시퀀스 없음 — 소켓 이름(slotId)만 나중에 지어주면 된다.
public class EquipSocketMakerWindow : EditorWindow
{
    private GameObject target;  // 대상 캐릭터 (자동 인식)

    [MenuItem("Tools/EquipSystem/Socket Maker")]
    public static void Open()
    {
        GetWindow<EquipSocketMakerWindow>(false, "Socket Maker", true);
    }

    private void OnDisable()
    {
        StopPick();
        ClearTestEquip();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("[+ 소켓] → 씬에서 캐릭터 표면 클릭 = 후보 고정(검수) → Enter/[승인] = 소켓 생성.\n악세서리를 골라두면 고스트가 커서를 따라다니며 결과를 미리 보여줍니다.", MessageType.Info);

        // 대상 (자동 인식: 프리팹 스테이지 루트 → 씬의 유일 스킨 캐릭터)
        // 세션 중 대상 교체 잠금: 교체되면 소켓 회전 규약(charRoot.rotation)·기존 소켓 탐색 루트가 오염된다
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(pickPhase != PickPhase.Off))
        {
            target = (GameObject)EditorGUILayout.ObjectField("대상 캐릭터", target, typeof(GameObject), true);
            if (GUILayout.Button("선택에서", GUILayout.Width(64)))
            {
                if (Selection.activeGameObject != null)
                {
                    target = Selection.activeGameObject.transform.root.gameObject;
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        if (target == null)
        {
            TryAutoTarget();
        }
        if (target == null)
        {
            EditorGUILayout.HelpBox("씬 인스턴스(또는 프리팹 스테이지 루트)를 지정하세요.\n(씬에 스킨 캐릭터가 1명뿐이면 자동으로 잡힙니다)", MessageType.Warning);
            return;
        }

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드 — 지금 만드는 소켓은 정지 시 사라집니다! 실제 저작은 에딧 모드에서.", MessageType.Error);
        }

        // 프리팹 스테이지 안내: 격리 프리뷰 씬이라 Game 뷰에는 아무것도(캐릭터 본체 포함) 렌더링되지 않음 — Unity 설계
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorGUILayout.HelpBox("프리팹 모드: Scene 뷰에서만 보입니다 — Game 뷰에는 스테이지 내용이 원래 렌더링되지 않습니다(Unity 격리 규칙).\nGame 뷰로 확인하려면 씬 인스턴스를 대상으로 작업하고, 완성 후 Overrides → Apply 하세요.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 악세서리 선택 (고스트 미리보기 + 테스트용) + 크기(카탈로그 sizeRatio 직접 편집)
        // 잠금: Reviewing 중 팝업 변경은 DestroyGhost로 후보(동결 고스트)를 오염시키고,
        // repick(재조정)은 악세서리 변경이 스코프 밖이라 Picking 단계부터 잠근다
        EditorGUILayout.LabelField("악세서리 (고스트 미리보기)", EditorStyles.boldLabel);
        LoadPickCatalog();
        using (new EditorGUI.DisabledScope(pickPhase == PickPhase.Reviewing || repickMode))
        {
            EditorGUILayout.BeginHorizontal();
            if (pickKeys != null && pickKeys.Length > 0)
            {
                int newKeyIndex = EditorGUILayout.Popup(pickKeyIndex, pickKeys);
                if (newKeyIndex != pickKeyIndex)
                {
                    pickKeyIndex = newKeyIndex;
                    DestroyGhost();
                }

                EquipEntry ghostEntry = GetPickEntry();
                if (ghostEntry != null)
                {
                    EditorGUI.BeginChangeCheck();
                    float newRatio = EditorGUILayout.FloatField(ghostEntry.sizeRatio, GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(pickCatalog, "Edit Size Ratio");
                        ghostEntry.sizeRatio = Mathf.Max(0.01f, newRatio);
                        EditorUtility.SetDirty(pickCatalog);
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("카탈로그 없음", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            // 배치 = 장착 정보 저장 (선택한 악세서리의 targetSlotId를 새 소켓으로)
            linkCatalogOnPlace = EditorGUILayout.ToggleLeft("배치 시 이 악세서리를 소켓에 연결 (카탈로그 저장)", linkCatalogOnPlace);

            // 접촉 기준: Pivot=모델 원점(0,0,0)을 클릭점에(기본, 레거시 equip 감각) /
            // BottomAlign=표면 위에 얹음(핀/모자) / Center=바운드 중심을 클릭점에
            pickContactAnchor = (EquipContactAnchor)EditorGUILayout.EnumPopup("접촉 기준", pickContactAnchor);
        }

        EditorGUILayout.Space();

        // 핵심 버튼: 3분기 — Off=[+ 소켓] / Picking=취소 / Reviewing=검수 패널(씬 오버레이와 이중화)
        Color prevBg = GUI.backgroundColor;
        if (pickPhase == PickPhase.Picking)
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
            string pickLabel;
            if (repickMode)
            {
                pickLabel = $"재조정 중: '{pickSlotId}' — 클릭=후보 지정, Esc=취소";
            }
            else
            {
                pickLabel = $"클릭 대기 중… '{pickSlotId}' (Esc 또는 여기 클릭으로 취소)";
            }
            if (GUILayout.Button(pickLabel, GUILayout.Height(34)))
            {
                StopPick();
            }
        }
        else
        {
            if (pickPhase == PickPhase.Reviewing)
            {
                DrawReviewWindowPanel();
            }
            else
            {
                GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
                if (GUILayout.Button("[+ 소켓] — 클릭한 자리에 socket_N 생성 (이름은 나중에)", GUILayout.Height(34)))
                {
                    StartPick();
                }
            }
        }
        GUI.backgroundColor = prevBg;

        // 베이크 (접힘 기본): 손으로 배치해둔 오브젝트 + 본 → 소켓/placeholder
        EditorGUILayout.Space();
        showBake = EditorGUILayout.Foldout(showBake, "베이크 — 배치한 오브젝트 + 본 → 소켓", true);
        if (showBake)
        {
            bakeSource = (GameObject)EditorGUILayout.ObjectField("위치 소스 (배치된 오브젝트)", bakeSource, typeof(GameObject), true);
            bakeBone = (Transform)EditorGUILayout.ObjectField("본 (Transform)", bakeBone, typeof(Transform), true);
            bakeSlotId = EditorGUILayout.TextField("Slot Id", bakeSlotId);

            // 픽 세션(Picking/Reviewing) 중 베이크 금지: 세션 상태로 CreateSocketAtHit(fromPick)가 돌면
            // ghostLift 위치·현재 선택 악세서리 링크·고스트 Record가 베이크 결과를 3중 오염시킨다
            if (pickPhase != PickPhase.Off)
            {
                EditorGUILayout.HelpBox("픽 세션 중에는 베이크할 수 없습니다 — 승인/취소로 세션을 끝낸 뒤 실행하세요.", MessageType.Info);
            }
            bool canBake = pickPhase == PickPhase.Off && bakeSource != null && bakeBone != null && string.IsNullOrEmpty(bakeSlotId) == false;
            using (new EditorGUI.DisabledScope(canBake == false))
            {
                if (GUILayout.Button("베이크"))
                {
                    BakeFromObject();
                }
            }
        }

        // 카탈로그 연결 현황: 각 악세서리가 "이 캐릭터"의 어느 소켓에 붙는지 / 소켓이 없는지 한눈에
        EditorGUILayout.Space();
        showLinks = EditorGUILayout.Foldout(showLinks, "카탈로그 연결 현황 (이 캐릭터 기준)", true);
        if (showLinks && pickCatalog != null)
        {
            foreach (EquipEntry entry in pickCatalog.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                // 해석 사다리(런타임과 동일 코드): ① key 이름 소켓 ② targetSlotId ③ 폴백 ④ 장착 불가
                string state;
                Color rowColor;
                string matched;
                int priority;
                EquipSocket linked = EquipSlotResolver.Resolve(target, entry, out matched, out priority);
                if (linked != null)
                {
                    rowColor = new Color(0.6f, 1f, 0.7f);
                    if (priority == 1)
                    {
                        state = "→ " + matched + "  ✓ (키 이름 소켓)";
                    }
                    else
                    {
                        if (priority == 2)
                        {
                            state = "→ " + matched + "  ✓";
                        }
                        else
                        {
                            state = "→ " + matched + "  ✓ (폴백)";
                        }
                    }
                }
                else
                {
                    List<string> candidates = EquipSlotResolver.Candidates(entry);
                    if (candidates.Count == 0)
                    {
                        state = "슬롯 미지정 (key/targetSlotId/폴백 전부 비어 있음)";
                        rowColor = new Color(1f, 0.9f, 0.4f);
                    }
                    else
                    {
                        state = "장착 불가 — 후보(" + string.Join("/", candidates) + ") 모두 없음";
                        rowColor = new Color(1f, 0.55f, 0.55f);
                    }
                }

                EditorGUILayout.BeginHorizontal();
                Color prevRow = GUI.color;
                GUI.color = rowColor;
                EditorGUILayout.LabelField(entry.key, state);
                GUI.color = prevRow;
                if (linked != null)
                {
                    // 플레이 없이 장착 확인 — 실장착과 동일 함수(FitToPlaceholder/Fit)로 배치 (저장 안 됨)
                    // Reviewing 중 잠금: 검수 대기 중 테스트 인스턴스가 씬/후보 관찰을 흐린다
                    using (new EditorGUI.DisabledScope(pickPhase == PickPhase.Reviewing))
                    {
                        if (GUILayout.Button("테스트", GUILayout.Width(48)))
                        {
                            TestEquip(entry, linked);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (testInstance != null)
            {
                if (GUILayout.Button("테스트 장착 지우기"))
                {
                    ClearTestEquip();
                }
            }
        }

        // Reviewing 키 이중 라우팅: 창이 포커스를 가진 상태에서도 Enter=승인 / Esc=재조정 (씬 뷰와 동일 계약)
        if (pickPhase == PickPhase.Reviewing && Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
            {
                Event.current.Use();  // 모달이 열리기 전에 소비 — 같은 이벤트의 재배달/이중 처리 봉쇄
                ApproveCandidate();
            }
            else
            {
                if (Event.current.keyCode == KeyCode.Escape)
                {
                    BackToPicking();
                    Event.current.Use();
                }
            }
        }
    }

    // 창 안의 검수 패널 (씬 오버레이와 이중화 — 어느 쪽에서도 승인/재조정 가능)
    private void DrawReviewWindowPanel()
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("검수 중 — Enter=승인 · Esc=재조정 (소켓은 승인 시 생성)", EditorStyles.boldLabel);

        string slotLine = $"예정 slotId: {candidate.slotId}";
        if (repickMode)
        {
            slotLine = slotLine + " (기존 소켓 덮어쓰기)";
        }
        EditorGUILayout.LabelField(slotLine);

        bool prefabBlocked = IsRepickBoneMovePrefabBlocked();
        if (candidate.bone != null)
        {
            if (repickMode && repickSocket != null && candidate.bone != repickSocket.transform.parent)
            {
                string oldBoneName = "?";
                if (repickSocket.transform.parent != null)
                {
                    oldBoneName = repickSocket.transform.parent.name;
                }
                EditorGUILayout.LabelField($"본 이사 예정: {oldBoneName} → {candidate.bone.name}");
                if (prefabBlocked)
                {
                    EditorGUILayout.HelpBox("프리팹 인스턴스 소켓은 본 이사 불가 — 프리팹 모드에서 재조정하세요.", MessageType.Error);
                }
            }
            else
            {
                EditorGUILayout.LabelField($"지배 본: {candidate.bone.name}");
            }
            float refLocal = candidate.hitDist / EquipAuthoringUtil.LossyAvg(candidate.bone);
            EditorGUILayout.LabelField($"예상 refDist: {candidate.hitDist:F2} (월드) / {refLocal:F4} (로컬)");
        }
        bool ghostLive = IsReviewGhostLive();
        if (candidate.hasGhostPose)
        {
            EditorGUILayout.LabelField($"회전 ZX {candidate.yaw:F0}° · YZ {candidate.tilt:F0}° · XY {candidate.roll:F0}° · 거리 +{candidate.lift * 100f:F0}%");
            EditorGUILayout.LabelField($"접촉 기준: {pickContactAnchor} · 악세서리: {candidate.accessoryKey}");
            EditorGUILayout.LabelField("씬 뷰에서 Ctrl=ZX·Shift=YZ·Ctrl+Shift=XY 회전, Alt+휠=거리, R=전부 리셋", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField($"회전 미적용 — 고스트 없음(표면 기준으로 커밋) · 거리 +{candidate.lift * 100f:F0}%는 적용");
            EditorGUILayout.LabelField($"접촉 기준: {pickContactAnchor} · 악세서리: {candidate.accessoryKey}");
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(prefabBlocked))
        {
            if (GUILayout.Button("승인 (Enter)"))
            {
                ApproveCandidate();
            }
        }
        if (GUILayout.Button("재조정 (Esc)"))
        {
            BackToPicking();
        }
        if (GUILayout.Button("세션 취소"))
        {
            StopPick();
        }
        EditorGUILayout.EndHorizontal();

        // 조정 행: 거리(항상 커밋 반영) / 크기(카탈로그 sizeRatio — 고스트 있어야 의미)
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("거리 +5%"))
        {
            AdjustReviewLift(0.05f);
        }
        if (GUILayout.Button("거리 −5%"))
        {
            AdjustReviewLift(-0.05f);
        }
        using (new EditorGUI.DisabledScope(ghostLive == false))
        {
            if (GUILayout.Button("크기 +0.1"))
            {
                AdjustReviewSize(0.1f);
            }
            if (GUILayout.Button("크기 −0.1"))
            {
                AdjustReviewSize(-0.1f);
            }
        }
        EditorGUILayout.EndHorizontal();

        // 카메라 행: 턴테이블/시점 — lastActiveSceneView만 제어 (다중 씬뷰에서는 마지막 활성 뷰)
        SceneView sv = SceneView.lastActiveSceneView;
        bool camBlocked = sv == null || sv.in2DMode || sv.isRotationLocked;
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(camBlocked))
        {
            bool newTurn = GUILayout.Toggle(turntableOn, "턴테이블", GUI.skin.button);
            if (newTurn != turntableOn)
            {
                ToggleTurntable();
            }
            if (GUILayout.Button("정면"))
            {
                SetViewpoint(0);
            }
            if (GUILayout.Button("후면"))
            {
                SetViewpoint(1);
            }
            if (GUILayout.Button("좌"))
            {
                SetViewpoint(2);
            }
            if (GUILayout.Button("우"))
            {
                SetViewpoint(3);
            }
            if (GUILayout.Button("상"))
            {
                SetViewpoint(4);
            }
        }
        EditorGUILayout.EndHorizontal();
        if (camBlocked)
        {
            EditorGUILayout.LabelField("씬 뷰 없음/2D 모드/회전 잠금 — 턴테이블·시점 버튼 사용 불가", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }

    // ── 장착 테스트 (에딧 모드, 저장 안 됨) ──
    private GameObject testInstance;

    private void TestEquip(EquipEntry entry, EquipSocket socket)
    {
        ClearTestEquip();

        if (entry.prefab == null)
        {
            Debug.LogWarning($"[SocketMaker] 테스트 불가: '{entry.key}' 프리팹이 비어 있습니다.");
            return;
        }

        testInstance = (GameObject)Instantiate(entry.prefab);
        testInstance.name = "__EquipPreview__Test_" + entry.key;  // 레이캐스터 제외 규약
        testInstance.hideFlags = HideFlags.DontSave;

        // 실장착과 동일 라우팅: placeholder (구명 spot 별칭 호환)
        EquipPlaceholder ph = socket.FindPlaceholder("placeholder");
        if (ph == null)
        {
            DestroyImmediate(testInstance);
            testInstance = null;
            Debug.LogWarning($"[SocketMaker] '{socket.slotId}' 소켓에 부착점(placeholder) 없음 — Socket Maker로 재저작하세요.");
            return;
        }

        bool fitted = EquipPlacement.FitToPlaceholder(testInstance, socket, ph, entry);
        if (fitted == false)
        {
            // 거부 경로(refDist 미베이크)에서는 인스턴스가 내부에서 파괴됨
            testInstance = null;
            Debug.LogWarning("[SocketMaker] 테스트 장착이 거부되었습니다 — 콘솔 경고를 확인하세요.");
            return;
        }

        Debug.Log($"[SocketMaker] 테스트 장착: '{entry.key}' → '{socket.slotId}' (저장 안 됨 — 창의 [테스트 장착 지우기]로 제거).");
    }

    private void ClearTestEquip()
    {
        if (testInstance != null)
        {
            DestroyImmediate(testInstance);
            testInstance = null;
        }
    }

    // 자동 타깃: 프리팹 스테이지 루트 → 씬의 유일한 스킨 캐릭터
    private void TryAutoTarget()
    {
        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            target = stage.prefabContentsRoot;
            return;
        }

        List<GameObject> candidates = new List<GameObject>();
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
        for (int s = 0; s < sceneCount; s++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (scene.isLoaded == false)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.activeInHierarchy && root.GetComponentInChildren<SkinnedMeshRenderer>(false) != null)
                {
                    candidates.Add(root);
                }
            }
        }

        if (candidates.Count == 1)
        {
            target = candidates[0];
        }
    }

    // ── 픽 모드 (클릭 = 후보 → 검수 → 승인 시 소켓) ──
    // 상태 머신: Off(세션 없음) / Picking(고스트 커서 추적) / Reviewing(클릭 지점 동결 + 검수).
    // 소켓은 Reviewing에서 승인(Enter/[승인])해야 생성된다 — 클릭 즉시 생성 아님.
    private enum PickPhase
    {
        Off,
        Picking,
        Reviewing,
    }

    // 검수 후보: 클릭 순간의 스냅샷. hit는 struct 값 복사(EquipMeshHit)라 레이캐스터 캐시 무효화와 무관.
    private struct PickCandidate
    {
        public bool valid;
        public EquipMeshHit hit;            // point/normal/distance/renderer/triangleIndex
        public Transform bone;              // 클릭 시점 지배 본 — 참조라 생존 가드 필수
        public string slotId;               // 예정 slotId (repick은 승인 시 live 재확인)
        public float hitDist;               // 정보 패널 표시용 파생값 (커밋은 CreateSocketAtHit에서 재계산)
        public string accessoryKey;         // 엔트리 key 박제 — 커밋은 이 key로 재해석 (인덱스 밀림 면역)
        public float sizeRatioAtCapture;    // 검수-커밋 괴리 경고용
        public float lift;                  // 패널 표시용 (커밋은 라이브 필드 — Reviewing 동안 동결이라 동일값)
        public float yaw;
        public float tilt;
        public float roll;
        public Vector3 ghostWorldPos;       // Record 동결값 — 고스트 파괴 내성
        public Quaternion ghostWorldRot;    //   = displayRot (rotationOffset 합성값)
        public float ghostWorldScale;       //   = pickGhost.localScale.x
        public bool hasGhostPose;
    }

    private PickPhase pickPhase = PickPhase.Off;  // 현재 페이즈 (비직렬화 — 도메인 리로드 시 Off로 리셋, 기존 계약과 동일)
    private PickCandidate candidate;              // 현재 검수 후보
    private bool sizeAdjustedInReview;            // 검수 중 [크기±] 버튼으로 sizeRatio를 바꿨는지 — 승인 경고 억제 + R 원복 대상
    private bool approveInProgress;               // 승인(모달 포함) 진행 중 — 키 리핏/이중 라우팅의 재진입 커밋 차단
    private Vector3 lastCandidatePoint;           // Esc 복귀 후 직전 후보 마커 (회색)
    private Vector3 lastCandidateNormal;
    private bool lastCandidateShown;

    // 재조정(repick) 세션: 기존 소켓의 고스트 재배치 — 승인 시 신규 생성 대신 그 소켓을 덮어쓴다
    private bool repickMode;                    // 신규/덮어쓰기 직교 플래그
    private EquipSocket repickSocket;           // 덮어쓰기 대상 — 커밋에 직접 참조로 전달 (리네임 면역)
    private EquipPlaceholder repickPh;          // 대상 부착점
    private Vector3 repickRefPos;               // 진입 시점 기존 배치 (참조 표시용)
    private Quaternion repickRefRot;
    private float repickRefDistWorld;
    private float repickPrevRefDist;            // 승인 로그의 refDist 변화율용
    private GameObject repickRefGhost;          // 기존 배치 실물 참조 (DontSave, 외관 무변조)
    private EquipContactAnchor sessionPrevContactAnchor;  // repick 종료 시 창 설정 원복
    private int sessionPrevKeyIndex;

    // 검수 카메라 (EquipWorkbenchCamera 동형 모델 — SceneView는 LookAtDirect만 사용)
    private bool turntableOn;
    private double turntablePrevTime;
    private float orbitYaw;
    private float orbitPitch;
    private Vector3 reviewFocus;      // 후보 지점 (프레이밍 피벗)
    private float reviewFrameSize;    // SceneView.size = 프레이밍 반경 그 자체 (거리 환산 불필요)
    private bool turntableHasLastSet; // 사용자 개입 감지용 last-set 유효 여부
    private Quaternion turntableLastRot;
    private Vector3 turntableLastPivot;
    private float turntableLastSize;

    private string pickSlotId;       // 이번에 만들 소켓의 임시 slotId (socket_N)
    private Vector2 pickMousePos;    // 호버용 최근 마우스 위치
    private double nextPickRepaint;  // 리페인트 스로틀
    private int hoverDiagCount;      // 호버 진단 로그 횟수 (초탄 센터 레이 1회 + 첫 실호버 1회 = 최대 2회)
    private bool pickMouseMoved;     // 실제 마우스 이벤트가 들어왔는지 (초탄 miss와 진짜 miss 구분)
    private bool lastHoverSuccess;   // 마지막 호버 히트 여부 (배지 표시용)
    private bool ghostDiagLogged;    // 픽 세션당 1회 고스트 배치 진단 로그

    // socket_N 자동 넘버링
    private string NextAutoSlotId()
    {
        int n = 1;
        while (EquipAuthoringUtil.FindSocketBySlotId(target.transform, "socket_" + n) != null)
        {
            n = n + 1;
        }
        return "socket_" + n;
    }

    private void StartPick()
    {
        StopPick();

        // 사전 점검: 대상에서 레이캐스트 가능한 메시가 있는지 (씬/스테이지 어디서든 즉시 원인 보고)
        if (EquipMeshRaycaster.Instance.HasCache(target.transform) == false)
        {
            Debug.LogWarning($"[SocketMaker] 대상 '{target.name}'에서 레이캐스트할 메시를 찾지 못했습니다 — 활성 렌더러/enabled 상태를 확인하세요. (씬 '{target.scene.name}')");
            return;
        }

        pickPhase = PickPhase.Picking;
        repickMode = false;
        repickSocket = null;
        repickPh = null;
        candidate = default;
        lastCandidateShown = false;
        sizeAdjustedInReview = false;
        turntableOn = false;
        pickSlotId = NextAutoSlotId();
        hoverDiagCount = 0;
        pickMouseMoved = false;
        lastHoverSuccess = false;
        ghostDiagLogged = false;
        ghostLift = 0f;  // 새 픽 세션은 표면에서 시작 (회전은 세션 간 유지)
        lastGhostRotValid = false;  // 이전 픽 세션의 회전이 새 소켓에 베이크되는 것 방지

        SceneView.duringSceneGui += OnPickSceneGUI;
        EditorApplication.update += OnPickUpdate;
        // 세션 정리 콜백: 플레이 진입(도메인 리로드 off 환경 포함)·프리팹 스테이지 닫힘 → 세션 종료
        EditorApplication.playModeStateChanged += OnPickPlayModeChanged;
        UnityEditor.SceneManagement.PrefabStage.prefabStageClosing += OnPickPrefabStageClosing;
        // 검수 중 Ctrl+Z/Y(예: [크기±]의 카탈로그 원복) 관측 — 고스트·후보 동결값 재동기 (표시=커밋 유지)
        Undo.undoRedoPerformed += OnPickUndoRedo;

        SceneView sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.Focus();
            // 초기값 = 뷰포트 중앙 (sv.position은 탭+툴바 포함 rect라 높이에서 툴바만큼 보정)
            pickMousePos = new Vector2(sv.position.width * 0.5f, (sv.position.height - 21f) * 0.5f);
        }
        SceneView.RepaintAll();
    }

    // StopPick = 세션 완전 종료 전용. Reviewing 진입/이탈에서 호출 금지 (DestroyGhost 무조건 실행됨).
    private void StopPick()
    {
        if (pickPhase != PickPhase.Off)
        {
            SceneView.duringSceneGui -= OnPickSceneGUI;
            EditorApplication.update -= OnPickUpdate;
            EditorApplication.playModeStateChanged -= OnPickPlayModeChanged;
            UnityEditor.SceneManagement.PrefabStage.prefabStageClosing -= OnPickPrefabStageClosing;
            Undo.undoRedoPerformed -= OnPickUndoRedo;
            // repick 세션이 덮었던 창 설정 원복 — 다음 신규 세션이 이전 재조정 설정을 물려받는 누출 방지
            if (repickMode)
            {
                pickContactAnchor = sessionPrevContactAnchor;
                pickKeyIndex = sessionPrevKeyIndex;
            }
            pickPhase = PickPhase.Off;
            candidate = default;
            lastCandidateShown = false;
            repickMode = false;
            repickSocket = null;
            repickPh = null;
            turntableOn = false;
            turntableHasLastSet = false;
            SceneView.RepaintAll();
        }
        DestroyGhost();
        DestroyRepickRefGhost();  // repick 참조 실물 파괴 (없으면 no-op)
        Repaint();
    }

    // 플레이 진입 = 세션 종료 (Enter Play Mode Options로 도메인 리로드가 꺼진 환경의 정리 구멍 봉합)
    private void OnPickPlayModeChanged(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingEditMode)
        {
            StopPick();
        }
    }

    // 프리팹 스테이지 닫힘 = 세션 종료 — 후보의 hit.renderer/bone은 스테이지 인스턴스에 결합되어
    // 씬 컨텍스트로 이월 금지 (레이캐스터의 prefabStageClosing 캐시 파기와 동조)
    private void OnPickPrefabStageClosing(UnityEditor.SceneManagement.PrefabStage stage)
    {
        StopPick();
    }

    // 검수 중 Undo/Redo 관측: [크기±]의 카탈로그 편집이 Ctrl+Z로 되돌아오면 고스트 스케일·후보 동결값이
    // stale로 남아 "검수한 모습 ≠ 커밋값"이 무경고 통과한다 — 재핏+재동결로 재동기
    private void OnPickUndoRedo()
    {
        if (pickPhase != PickPhase.Reviewing || candidate.valid == false)
        {
            return;
        }
        // 카탈로그가 캡처값으로 되돌아왔으면 세션 변경 플래그 해제 — 이후 외부 편집 괴리 경고 감시 원상 복구
        EquipEntry entry = ResolveCommitEntry();
        if (entry != null && Mathf.Abs(entry.sizeRatio - candidate.sizeRatioAtCapture) <= 1e-6f)
        {
            sizeAdjustedInReview = false;
        }
        RefreshReviewAfterAdjust();
    }

    // 픽 모드 중 씬 뷰 상시 리페인트 (~30fps) + 검수 턴테이블 틱 (틱은 스로틀 밖 — 부드러운 궤도)
    private void OnPickUpdate()
    {
        if (pickPhase == PickPhase.Reviewing && turntableOn)
        {
            TurntableTick();
        }
        if (EditorApplication.timeSinceStartup < nextPickRepaint)
        {
            return;
        }
        nextPickRepaint = EditorApplication.timeSinceStartup + 0.033;
        SceneView.RepaintAll();
    }

    // 씬 처리: (Picking) 고스트 호버 + 클릭=후보 지정 / (Reviewing) 검수 오버레이로 위임
    private void OnPickSceneGUI(SceneView sceneView)
    {
        if (pickPhase == PickPhase.Off || target == null)
        {
            StopPick();
            return;
        }

        // repick 생존 가드: 대상 소켓/부착점이 파괴되면 세션 종료 (신규 생성으로 자동 전환 금지 — 의도 왜곡 방지)
        if (repickMode && (repickSocket == null || repickPh == null))
        {
            Debug.LogWarning("[SocketMaker] 재조정 대상 소켓/부착점이 사라져 세션을 종료합니다.");
            StopPick();
            return;
        }

        // 두 페이즈 공통 참조 표시: 기존 배치(repick) + 직전 후보 마커(Esc 복귀 후)
        if (repickMode)
        {
            DrawRepickReference();
        }
        if (lastCandidateShown)
        {
            DrawLastCandidateMarker();
        }

        if (pickPhase == PickPhase.Reviewing)
        {
            OnReviewSceneGUI(sceneView);
            return;
        }

        Event e = Event.current;
        Transform charRoot = target.transform;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        sceneView.wantsMouseMove = true;

        if (e.isMouse)
        {
            pickMousePos = e.mousePosition;
            pickMouseMoved = true;
            if (e.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }
        }

        // 고스트 조작: 휠 = 거리(표면에서 띄우기), Ctrl+휠 = ZX 회전(법선축), Shift+휠 = YZ 회전(기울임), Ctrl+Shift+휠 = XY 회전(롤), R = 리셋
        if (HandleGhostWheel(e, true))
        {
            e.Use();
            sceneView.Repaint();
        }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            ghostYaw = 0f;
            ghostTilt = 0f;
            ghostRoll = 0f;
            ghostLift = 0f;
            e.Use();
            sceneView.Repaint();
        }

        // 안내 배지
        string rotInfo = "";
        if (ghostYaw != 0f || ghostTilt != 0f || ghostRoll != 0f)
        {
            rotInfo = $"  회전 ZX{ghostYaw:F0}°·YZ{ghostTilt:F0}°·XY{ghostRoll:F0}°";
        }
        if (ghostLift > 0f)
        {
            rotInfo = rotInfo + $"  거리 +{ghostLift * 100f:F0}%";
        }
        if (rotInfo != "")
        {
            rotInfo = rotInfo + " (R=리셋)";
        }
        if (lastHoverSuccess == false && pickMouseMoved)
        {
            rotInfo = rotInfo + "  [miss — 캐릭터 표면 위로]";
        }
        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 560, 24));
        GUILayout.Label($"클릭 = 후보 [{pickSlotId}] — 클릭=후보 지정(검수), 휠=거리, Ctrl=ZX·Shift=YZ·Ctrl+Shift=XY 회전, Esc=취소{rotInfo}", EditorStyles.helpBox);
        GUILayout.EndArea();
        Handles.EndGUI();

        // 호버: 히트 원 + 노멀 + 악세서리 고스트
        if (e.type == EventType.Repaint)
        {
            Ray hoverRay = HandleUtility.GUIPointToWorldRay(pickMousePos);
            EquipMeshHit hoverHit;
            int hoverCount;
            bool hoverSuccess = EquipMeshRaycaster.Instance.RaycastCursor(charRoot, hoverRay, 0, out hoverHit, out hoverCount);

            lastHoverSuccess = hoverSuccess;

            // 진단 2단계: 초탄(마우스 이동 전 센터 레이 — miss여도 정상일 수 있음) + 첫 실호버.
            // entries>0인데 miss = 조준 문제 / entries=0 = 필터 전멸 — 즉시 판별 가능.
            if (hoverDiagCount < 2 && (hoverDiagCount == 0 || pickMouseMoved))
            {
                hoverDiagCount = hoverDiagCount + 1;
                int entries = EquipMeshRaycaster.Instance.GetEntryCount(charRoot);
                string diagPhase;
                if (pickMouseMoved)
                {
                    diagPhase = "실호버";
                }
                else
                {
                    diagPhase = "초기센터레이(miss여도 정상)";
                }
                Debug.Log($"[SocketMaker] 호버 진단({diagPhase}): 히트={hoverSuccess}, hitCount={hoverCount}, entries={entries}, 대상='{charRoot.name}' 씬='{charRoot.gameObject.scene.name}'");
            }

            if (hoverSuccess)
            {
                float size = HandleUtility.GetHandleSize(hoverHit.point) * 0.12f;
                Handles.color = new Color(0.4f, 1f, 0.6f, 0.9f);
                Handles.DrawWireDisc(hoverHit.point, hoverHit.normal, size);
                Handles.DrawLine(hoverHit.point, hoverHit.point + hoverHit.normal * size * 2f);

                UpdateGhost(charRoot, hoverHit);
            }
            else
            {
                HideGhost();

                // miss 표시: 커서 자리 회색 십자 — "픽 모드 죽음"과 "조준 miss"를 눈으로 구분
                Handles.BeginGUI();
                Color prevColor = GUI.color;
                GUI.color = new Color(0.75f, 0.75f, 0.75f, 0.85f);
                GUI.Label(new Rect(pickMousePos.x - 7f, pickMousePos.y - 12f, 24f, 24f), "+", EditorStyles.boldLabel);
                GUI.color = prevColor;
                Handles.EndGUI();
            }
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            StopPick();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            EquipMeshHit hit;
            int hitCount;
            if (EquipMeshRaycaster.Instance.RaycastCursor(charRoot, ray, 0, out hit, out hitCount))
            {
                Transform bone = EquipMeshRaycaster.Instance.QueryDominantBone(charRoot, hit);
                if (bone != null)
                {
                    // 즉시 생성하지 않고 검수(Reviewing)로 — 소켓은 승인 시점에 만든다
                    UpdateGhost(charRoot, hit);   // 클릭 레이 기준 재동기 (호버 1프레임 어긋남 제거, lastGhostRot도 갱신)
                    CaptureCandidate(bone, hit);
                    EnterReview();
                    e.Use();
                    return;
                }
                Debug.LogWarning("[SocketMaker] 히트 지점의 지배 본을 찾지 못했습니다 — 다시 클릭하세요.");
            }
            else
            {
                Debug.LogWarning("[SocketMaker] 메시 히트 없음 — 캐릭터 표면을 클릭하세요.");
            }
            e.Use();
        }
    }

    // ── 검수(Reviewing) 전이 ──

    // 클릭 순간의 후보 스냅샷 캡처 (UpdateGhost 직후 호출 — 고스트 TRS가 클릭 히트 기준으로 재동기된 상태)
    private void CaptureCandidate(Transform bone, EquipMeshHit hit)
    {
        candidate = new PickCandidate();
        candidate.valid = true;
        candidate.hit = hit;
        candidate.bone = bone;
        candidate.slotId = pickSlotId;
        candidate.hitDist = (hit.point - bone.position).magnitude;
        if (candidate.hitDist <= 1e-6f)
        {
            // 커밋(CreateSocketAtHit)과 동일 폴백 — 본 원점 클릭 등 퇴화 케이스
            candidate.hitDist = EquipAuthoringUtil.MeasureCharHeight(target) * 0.05f;
        }
        candidate.lift = ghostLift;
        candidate.yaw = ghostYaw;
        candidate.tilt = ghostTilt;
        candidate.roll = ghostRoll;
        sizeAdjustedInReview = false;  // 새 후보 = 크기 조정 세션 리셋
        EquipEntry entry = GetPickEntry();
        if (entry != null)
        {
            candidate.accessoryKey = entry.key;
            candidate.sizeRatioAtCapture = entry.sizeRatio;
        }
        if (pickGhost != null && lastGhostRotValid)
        {
            // 고스트 TRS 동결: 승인 시 Record는 이 값으로 산출 — 고스트 파괴/변형 내성
            candidate.ghostWorldPos = pickGhost.transform.position;
            candidate.ghostWorldRot = pickGhost.transform.rotation;
            candidate.ghostWorldScale = pickGhost.transform.localScale.x;
            candidate.hasGhostPose = true;
        }
    }

    // 검수 중 조정용 재캡처: 회전·거리 표시값과 고스트 동결 TRS만 갱신 —
    // accessoryKey/sizeRatioAtCapture/slotId/bone/hit/hitDist는 클릭 시점 박제를 유지한다
    // (검수 중 카탈로그 외부 편집으로 pickKeyIndex가 밀려도 커밋 key가 흔들리지 않는 계약 보존)
    private void RecaptureCandidatePose()
    {
        candidate.yaw = ghostYaw;
        candidate.tilt = ghostTilt;
        candidate.roll = ghostRoll;
        candidate.lift = ghostLift;
        if (pickGhost != null && lastGhostRotValid)
        {
            candidate.ghostWorldPos = pickGhost.transform.position;
            candidate.ghostWorldRot = pickGhost.transform.rotation;
            candidate.ghostWorldScale = pickGhost.transform.localScale.x;
            candidate.hasGhostPose = true;
        }
    }

    // 검수 중 고스트가 살아 있고 라이브 엔트리가 박제 key와 일치하는지 — 회전/크기 조정의 허용 조건.
    // (불일치 = 악세서리 미지정이거나 검수 중 카탈로그 외부 편집으로 드리프트 — 조정해도 커밋에 반영 안 됨)
    private bool IsReviewGhostLive()
    {
        EquipEntry entry = GetPickEntry();
        return entry != null && entry.prefab != null
            && string.IsNullOrEmpty(candidate.accessoryKey) == false && entry.key == candidate.accessoryKey;
    }

    // 검수 중 조정(회전/거리/크기) 공통 후처리: 고스트 재핏 + 후보 재동결 + 프레이밍 갱신(거리·크기가 초점/반경을 움직인다)
    private void RefreshReviewAfterAdjust()
    {
        if (IsReviewGhostLive())
        {
            UpdateGhost(target.transform, candidate.hit);  // 동결 히트 기준 재핏 — lastGhostRot(커밋 회전)도 갱신
        }
        else
        {
            // 재핏 불가(고스트 없음/카탈로그 드리프트)여도 거리 조정은 커밋(라이브 ghostLift)에 반영되므로,
            // 동결 고스트만이라도 같은 양만큼 노멀 방향으로 이동 — 비주얼·Record 동결 TRS·커밋 placeholder 정합 유지
            float liftDelta = ghostLift - candidate.lift;
            if (pickGhost != null && Mathf.Abs(liftDelta) > 1e-6f)
            {
                pickGhost.transform.position = pickGhost.transform.position + candidate.hit.normal * (liftDelta * candidate.hitDist);
            }
        }
        RecaptureCandidatePose();
        reviewFocus = candidate.hit.point + candidate.hit.normal * (ghostLift * candidate.hitDist);
        reviewFrameSize = ComputeReviewFrameSize();
        Repaint();
        SceneView.RepaintAll();
    }

    // 검수 중 거리 조정 (Alt+휠과 동일 스텝의 버튼용). 고스트 유무와 무관 — lift는 커밋에 항상 반영된다.
    private void AdjustReviewLift(float delta)
    {
        ghostLift = Mathf.Max(0f, ghostLift + delta);
        RefreshReviewAfterAdjust();
    }

    // 검수 중 크기 조정: 카탈로그 sizeRatio 직편집 (아이템 공용 값 — 창의 sizeRatio 필드와 동일 경로)
    private void AdjustReviewSize(float delta)
    {
        if (IsReviewGhostLive() == false)
        {
            return;
        }
        EquipEntry entry = ResolveCommitEntry();
        if (entry == null || pickCatalog == null)
        {
            return;
        }
        Undo.RecordObject(pickCatalog, "Edit Size Ratio");
        entry.sizeRatio = Mathf.Max(0.01f, entry.sizeRatio + delta);
        EditorUtility.SetDirty(pickCatalog);
        sizeAdjustedInReview = true;  // 의도적 세션 변경 — 승인 시 괴리 경고 억제, R로 원복 가능
        RefreshReviewAfterAdjust();
    }

    // 검수 R = 전부 초기화: 회전 3축·거리는 0으로, (세션 중 버튼으로 바꾼) 크기는 클릭 시점 값으로 원복.
    // 회전 리셋은 재핏 가능(고스트 유효)할 때만 — 재핏 없이 필드만 0이 되면 커밋(stale lastGhostRot)과 표시가 어긋난다
    private void ResetReviewAdjust()
    {
        if (IsReviewGhostLive())
        {
            ghostYaw = 0f;
            ghostTilt = 0f;
            ghostRoll = 0f;
        }
        ghostLift = 0f;
        if (sizeAdjustedInReview)
        {
            EquipEntry entry = ResolveCommitEntry();
            if (entry != null && pickCatalog != null && candidate.sizeRatioAtCapture > 0f)
            {
                Undo.RecordObject(pickCatalog, "Edit Size Ratio");
                entry.sizeRatio = candidate.sizeRatioAtCapture;
                EditorUtility.SetDirty(pickCatalog);
            }
            sizeAdjustedInReview = false;
        }
        RefreshReviewAfterAdjust();
    }

    private void EnterReview()
    {
        pickPhase = PickPhase.Reviewing;
        lastCandidateShown = false;
        // 프레이밍 피벗 = 부착점 예정 위치 (히트점 + lift 띄우기 — placeholder 위치식과 동일)
        reviewFocus = candidate.hit.point + candidate.hit.normal * (candidate.lift * candidate.hitDist);
        reviewFrameSize = ComputeReviewFrameSize();
        // 카메라는 여기서 움직이지 않는다 — 프레이밍은 턴테이블/시점 버튼이 담당
        Repaint();
        SceneView.RepaintAll();
    }

    // SceneView.size = 프레이밍 반경 그 자체 — 거리 환산(tan) 불필요. 스케일 상대값만 사용 (극단 스케일 20000 대응).
    // Renderer.bounds stale 회피: identity 측정치(ghostNaturalExtents) × 현재 스케일
    private float ComputeReviewFrameSize()
    {
        float radius = candidate.hitDist * 2.5f;
        if (pickGhost != null && ghostMeasured)
        {
            radius = Mathf.Max(radius, ghostNaturalExtents.magnitude * pickGhost.transform.localScale.x * 1.5f);
        }
        if (radius < 1e-4f)
        {
            radius = Mathf.Max(EquipAuthoringUtil.MeasureCharHeight(target) * 0.05f, 1e-4f);
        }
        return radius * 1.3f;
    }

    // Reviewing → Picking 복귀: 후보 무효화(직전 후보 마커는 보존), yaw/tilt/lift·lastGhostRot은
    // 필드 무접촉 = 자동 보존 → 고스트가 그 회전/거리 그대로 커서 추적을 재개한다
    private void BackToPicking()
    {
        if (pickPhase != PickPhase.Reviewing)
        {
            // 세션 종료(Off) 후 뒤늦게 도착한 호출이 "유령 Picking"(씬 구독 없는 Picking 상태)을 만드는 것 방지
            return;
        }
        lastCandidatePoint = candidate.hit.point;
        lastCandidateNormal = candidate.hit.normal;
        lastCandidateShown = true;
        candidate.valid = false;
        turntableOn = false;
        pickPhase = PickPhase.Picking;
        Repaint();
        SceneView.RepaintAll();
    }

    // ── 검수(Reviewing) 씬 처리 ──

    private void OnReviewSceneGUI(SceneView sv)
    {
        if (candidate.valid == false || candidate.bone == null)
        {
            Debug.LogWarning("[SocketMaker] 후보의 지배 본이 사라져 조준으로 복귀합니다.");
            BackToPicking();
            return;
        }

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));  // 씬 선택만 차단 — 휠/우클릭/중클릭/Alt 내비는 통과

        DrawReviewOverlay(sv);
        if (pickPhase != PickPhase.Reviewing)
        {
            // 오버레이 버튼(승인/재조정/취소)이 상태를 바꿨으면 즉시 종료
            return;
        }

        if (e.type == EventType.Repaint)
        {
            // 후보 마킹 (동결 고스트는 이미 씬에 있음)
            float size = HandleUtility.GetHandleSize(candidate.hit.point) * 0.12f;
            Handles.color = new Color(1f, 0.8f, 0.3f, 0.9f);
            Handles.DrawWireDisc(candidate.hit.point, candidate.hit.normal, size);
            Handles.DrawLine(candidate.hit.point, candidate.hit.point + candidate.hit.normal * size * 2f);
        }

        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            e.Use();  // 모달(연결 다이얼로그)이 열리기 전에 소비 — 같은 이벤트의 재배달/이중 처리 봉쇄
            ApproveCandidate();
            return;
        }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            BackToPicking();
            e.Use();
            return;
        }

        // 검수 중 고스트 계속 조정: 표면 지점만 동결, 회전(Ctrl/Shift/Ctrl+Shift+휠)·거리(Alt+휠)는 갱신.
        // 맨휠은 HandleGhostWheel이 건드리지 않는다(카메라 줌 통과). 턴테이블 도는 중에도 조정 가능(의도).
        // 회전은 고스트가 없으면(악세서리 미지정/카탈로그 드리프트) 커밋에 반영되지 않으므로 소비하지 않고
        // 통과시킨다 (표시-커밋 괴리 방지). 거리(Alt+휠)는 고스트 유무와 무관하게 커밋에 반영되므로 항상 허용.
        if (e.type == EventType.ScrollWheel)
        {
            bool rotationWheel = e.control || e.shift;
            if (rotationWheel == false || IsReviewGhostLive())
            {
                if (HandleGhostWheel(e, false))
                {
                    RefreshReviewAfterAdjust();
                    e.Use();
                    return;
                }
            }
        }
        // R = 전부 초기화: 회전 3축 + 거리 + (세션 중 바꾼) 크기 — 조준 단계의 R과 동일 감각
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            ResetReviewAdjust();
            e.Use();
            return;
        }

        // 턴테이블 자동 해제 보조 감지 — 이벤트는 절대 소비하지 않는다 (자유 내비 유지)
        if (turntableOn && (e.type == EventType.ScrollWheel
            || (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2 || e.alt))))
        {
            turntableOn = false;
            Repaint();
        }
        // 오클릭 무해화: Alt 없는 좌클릭만 소비 (후보 재지정 오해 방지) — 우/중클릭·Alt 드래그는 통과
        if (e.type == EventType.MouseDown && e.button == 0 && e.alt == false)
        {
            e.Use();
        }
    }

    // 씬 좌상단 검수 오버레이. 다중 씬뷰: 오버레이는 모든 뷰에 그려짐(무해, 의도),
    // 키는 포커스 뷰만 수신, 턴테이블은 lastActiveSceneView만 제어.
    private void DrawReviewOverlay(SceneView sv)
    {
        int action = 0;  // 0=없음 1=승인 2=재조정 3=세션취소 4=턴테이블 토글 10~14=시점 (GUI 블록 밖에서 집행 — 레이아웃 정합)
        bool prefabBlocked = IsRepickBoneMovePrefabBlocked();
        bool camBlocked = sv.in2DMode || sv.isRotationLocked;

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(10, 10, 360, 270), GUI.skin.box);
        GUILayout.Label("검수 중 — Enter=승인 · Esc=재조정 (소켓은 승인 시 생성)", EditorStyles.boldLabel);

        string slotLine = $"예정 slotId: {candidate.slotId}";
        if (repickMode)
        {
            slotLine = slotLine + " (기존 소켓 덮어쓰기)";
        }
        GUILayout.Label(slotLine);

        Color prevColor = GUI.color;
        if (repickMode && repickSocket != null && candidate.bone != repickSocket.transform.parent)
        {
            string oldBoneName = "?";
            if (repickSocket.transform.parent != null)
            {
                oldBoneName = repickSocket.transform.parent.name;
            }
            GUI.color = new Color(1f, 0.8f, 0.3f);
            GUILayout.Label($"본 이사 예정: {oldBoneName} → {candidate.bone.name}");
            if (prefabBlocked)
            {
                GUI.color = new Color(1f, 0.45f, 0.45f);
                GUILayout.Label("프리팹 인스턴스 — 본 이사 불가 (프리팹 모드에서 재조정)");
            }
            GUI.color = prevColor;
        }
        else
        {
            GUILayout.Label($"지배 본: {candidate.bone.name}");
        }

        // 소켓은 본 자식·scale 1이라 본 lossy=소켓 lossy — 커밋값(bakedRefDistLocal)과 일치
        float refLocal = candidate.hitDist / EquipAuthoringUtil.LossyAvg(candidate.bone);
        GUILayout.Label($"예상 refDist: {candidate.hitDist:F2} (월드) / {refLocal:F4} (로컬)");
        bool ghostLive = IsReviewGhostLive();
        if (candidate.hasGhostPose)
        {
            GUILayout.Label($"회전 ZX {candidate.yaw:F0}° · YZ {candidate.tilt:F0}° · XY {candidate.roll:F0}° · 거리 +{candidate.lift * 100f:F0}%");
            GUILayout.Label($"접촉 기준: {pickContactAnchor} · 악세서리: {candidate.accessoryKey}");
            GUILayout.Label("Ctrl=ZX · Shift=YZ · Ctrl+Shift=XY 회전 · Alt+휠=거리 · R=전부 리셋 · 맨휠=줌", EditorStyles.miniLabel);
        }
        else
        {
            // 고스트 없음 = 커밋 회전은 표면 기준 폴백 — 회전 숫자를 보여주면 반영되는 것으로 오해한다
            GUILayout.Label($"회전 미적용 — 고스트 없음(표면 기준으로 커밋) · 거리 +{candidate.lift * 100f:F0}%는 적용");
            GUILayout.Label($"접촉 기준: {pickContactAnchor} · 악세서리: {candidate.accessoryKey}");
        }

        GUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(prefabBlocked))
        {
            if (GUILayout.Button("승인 (Enter)"))
            {
                action = 1;
            }
        }
        if (GUILayout.Button("재조정 (Esc)"))
        {
            action = 2;
        }
        if (GUILayout.Button("세션 취소"))
        {
            action = 3;
        }
        GUILayout.EndHorizontal();

        // 조정 행: 거리(항상 커밋 반영) / 크기(카탈로그 sizeRatio — 고스트 있어야 의미)
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("거리 +5%"))
        {
            action = 5;
        }
        if (GUILayout.Button("거리 −5%"))
        {
            action = 6;
        }
        using (new EditorGUI.DisabledScope(ghostLive == false))
        {
            if (GUILayout.Button("크기 +0.1"))
            {
                action = 7;
            }
            if (GUILayout.Button("크기 −0.1"))
            {
                action = 8;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(camBlocked))
        {
            bool newTurn = GUILayout.Toggle(turntableOn, "턴테이블", GUI.skin.button);
            if (newTurn != turntableOn)
            {
                action = 4;
            }
            if (GUILayout.Button("정면"))
            {
                action = 10;
            }
            if (GUILayout.Button("후면"))
            {
                action = 11;
            }
            if (GUILayout.Button("좌"))
            {
                action = 12;
            }
            if (GUILayout.Button("우"))
            {
                action = 13;
            }
            if (GUILayout.Button("상"))
            {
                action = 14;
            }
        }
        GUILayout.EndHorizontal();
        if (camBlocked)
        {
            GUILayout.Label("2D 모드/회전 잠금 뷰 — 턴테이블·시점 버튼 사용 불가", EditorStyles.miniLabel);
        }
        if (sv.cameraSettings.dynamicClip == false)
        {
            GUILayout.Label("경고: dynamicClip이 꺼져 있어 극단 스케일에서 잘릴 수 있음", EditorStyles.miniLabel);
        }
        GUILayout.EndArea();
        Handles.EndGUI();

        if (action == 1)
        {
            ApproveCandidate();
        }
        if (action == 2)
        {
            BackToPicking();
        }
        if (action == 3)
        {
            StopPick();
        }
        if (action == 4)
        {
            ToggleTurntable();
        }
        if (action == 5)
        {
            AdjustReviewLift(0.05f);
        }
        if (action == 6)
        {
            AdjustReviewLift(-0.05f);
        }
        if (action == 7)
        {
            AdjustReviewSize(0.1f);
        }
        if (action == 8)
        {
            AdjustReviewSize(-0.1f);
        }
        if (action >= 10)
        {
            SetViewpoint(action - 10);
        }
    }

    // repick 본 이사 대상이 프리팹 인스턴스인지 — 승인 사전 차단용 (커밋의 프리팹 가드를 사전화)
    private bool IsRepickBoneMovePrefabBlocked()
    {
        if (repickMode == false || repickSocket == null || candidate.bone == null)
        {
            return false;
        }
        if (candidate.bone == repickSocket.transform.parent)
        {
            return false;
        }
        return PrefabUtility.IsPartOfPrefabInstance(repickSocket.gameObject);
    }

    // Esc 복귀 후 직전 후보 자리 마커 (회색 — Repaint 전용)
    private void DrawLastCandidateMarker()
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }
        float size = HandleUtility.GetHandleSize(lastCandidatePoint) * 0.1f;
        Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        Handles.DrawWireDisc(lastCandidatePoint, lastCandidateNormal, size);
    }

    // ── 승인 (커밋) ──

    // 승인 = 재검증 → 정확히 1 Undo 그룹으로 커밋 → 세션 종료. 실패 시 Reviewing 잔류.
    private void ApproveCandidate()
    {
        if (pickPhase != PickPhase.Reviewing)
        {
            // 세션 종료 후 도착한 중복 트리거(키 리핏/이중 라우팅) 무시 — 이중 커밋 봉쇄
            return;
        }
        if (approveInProgress)
        {
            // 모달(연결 다이얼로그)이 떠 있는 동안의 재진입 차단
            return;
        }
        if (candidate.valid == false || candidate.bone == null || target == null)
        {
            Debug.LogWarning("[SocketMaker] 후보가 유효하지 않습니다 — 조준으로 복귀.");
            BackToPicking();
            return;
        }

        // 모달이 떠 있는 동안 재진입 금지 (예외에도 반드시 해제 — 아니면 승인이 영구 잠김)
        approveInProgress = true;
        try
        {
            ApproveCandidateCore();
        }
        finally
        {
            approveInProgress = false;
        }
    }

    // ApproveCandidate 본체 (가드 통과 후): 커밋 직전 재검증 + 연결 다이얼로그 + 1그룹 커밋
    private void ApproveCandidateCore()
    {
        if (repickMode && repickSocket == null)
        {
            Debug.LogWarning("[SocketMaker] 재조정 대상 소켓이 사라져 세션을 종료합니다.");
            StopPick();
            return;
        }
        if (repickMode && candidate.bone != repickSocket.transform.parent
            && PrefabUtility.IsPartOfPrefabInstance(repickSocket.gameObject))
        {
            // Enter 우회 방지 재검사 — Reviewing 잔류 ([세션 취소]로 탈출 가능)
            Debug.LogWarning("[SocketMaker] 프리팹 인스턴스 소켓은 본 이사 불가 — 프리팹 모드에서 재조정하세요.");
            return;
        }

        // 커밋 직전 재검증
        string commitSlotId;
        EquipSocket overwrite = null;
        EquipPlaceholder overwritePh = null;  // 다이얼로그 덮어쓰기 경로의 refDist 변화 로그용 (repickPrevRefDist와 동일 취지)
        float overwritePrevRefDist = 0f;
        if (repickMode)
        {
            // Reviewing 중 리네임됐어도 그 소켓을 정확히 덮는다 (직접 참조 + live slotId)
            commitSlotId = repickSocket.slotId;
            overwrite = repickSocket;
        }
        else
        {
            commitSlotId = candidate.slotId;
            // Reviewing 대기 중 같은 slotId 소켓이 생겼으면(수동 생성/전파/Undo 부활) 번호 재발급 — 남의 소켓 파괴 방지
            if (EquipAuthoringUtil.FindSocketBySlotId(target.transform, commitSlotId) != null)
            {
                commitSlotId = NextAutoSlotId();
                Debug.Log($"[SocketMaker] '{candidate.slotId}'가 검수 중 생겨 '{commitSlotId}'로 재발급.");
            }

            // 카탈로그 연결 3지선다: 아이템이 이미 의미 있는 자리 이름(head1 등)에 등록된 경우.
            // 카탈로그는 읽기 전용 — 어떤 선택도 등록(targetSlotId)을 고치지 않고 "소켓 쪽"을 맞춘다.
            // (등록 변경은 다른 캐릭터의 같은 자리 연결 자산을 끊는 고위험 조작이라 경로 자체를 없앰)
            if (linkCatalogOnPlace)
            {
                EquipEntry dialogEntry = ResolveCommitEntry();
                if (dialogEntry != null
                    && string.IsNullOrEmpty(dialogEntry.targetSlotId) == false
                    && dialogEntry.targetSlotId.StartsWith("socket_") == false)
                {
                    // 기존 소켓 조회 + 덮어쓰기 가능 여부 사전 판정 (프리팹 본 이사 가드의 사전화 — repick 선검사와 동일 조건)
                    bool targetBlocked;
                    EquipSocket existingTarget = FindDialogSocket(dialogEntry.targetSlotId, out targetBlocked);
                    bool keyBlocked;
                    EquipSocket existingKey = FindDialogSocket(dialogEntry.key, out keyBlocked);

                    string header = $"'{dialogEntry.key}'는 카탈로그(전역 장부)에서 '{dialogEntry.targetSlotId}' 자리에 등록되어 있습니다.\n";

                    // 선택지 2의 실체 고지: key 이름 소켓이 이미 있으면 "새 소켓"이 아니라 그 소켓 덮어쓰기다 (미고지 덮어쓰기 방지)
                    string opt2;
                    if (existingKey != null)
                    {
                        opt2 = $"2. '{dialogEntry.key}' 소켓 덮어쓰기 — 기존 key 소켓의 부착점을 지금 검수한 위치로 갱신";
                        if (keyBlocked)
                        {
                            opt2 = opt2 + " (프리팹 본 이사 불가 — 선택 시 임시 이름으로 진행)";
                        }
                    }
                    else
                    {
                        opt2 = $"2. '{dialogEntry.key}' 이름으로 새 소켓 — 이 캐릭터 전용 자리 (해석 사다리 1순위)";
                    }
                    string opt3 = $"3. 임시 이름('{commitSlotId}')으로 새 소켓 — 연결 없이, 이름은 나중에";
                    string footer = $"\n\n카탈로그 등록('{dialogEntry.targetSlotId}')은 어떤 선택에서도 바뀌지 않습니다.";

                    if (existingTarget != null && targetBlocked == false)
                    {
                        // Case A: 자리 소켓 존재 — 1 = 그 소켓 덮어쓰기
                        string warnLadder = "";
                        if (existingKey != null && existingKey != existingTarget)
                        {
                            // key 소켓이 사다리 1순위라 targetSlotId 소켓 갱신은 이 아이템 장착에 안 보인다 — 고지
                            warnLadder = $"\n주의: 이 캐릭터에 '{dialogEntry.key}' 소켓이 있어 해석 사다리 1순위로 먼저 매칭됩니다 — 선택지 1의 갱신은 이 아이템 장착에는 반영되지 않습니다.";
                        }
                        int choice = EditorUtility.DisplayDialogComplex(
                            "카탈로그 연결",
                            header +
                            $"이 캐릭터에 '{dialogEntry.targetSlotId}' 소켓이 존재합니다.\n\n" +
                            $"1. '{dialogEntry.targetSlotId}' 덮어쓰기 — 기존 소켓의 부착점을 지금 검수한 위치로 갱신\n" +
                            opt2 + "\n" + opt3 + warnLadder + footer,
                            "1 수행", "3 수행", "2 수행");
                        // DisplayDialogComplex 반환: 첫 버튼=0, 둘째(cancel·Esc)=1, 셋째=2
                        if (choice == 0)
                        {
                            commitSlotId = dialogEntry.targetSlotId;
                            overwrite = existingTarget;  // 기존 소켓 덮어쓰기 (repick과 동일 커밋 경로)
                        }
                        if (choice == 2)
                        {
                            ApplyDialogKeyOption(dialogEntry, existingKey, keyBlocked, ref commitSlotId, ref overwrite);
                        }
                        // choice 1 (Esc 포함) = 3. 임시 이름 유지 — 링크 없음
                    }
                    else if (existingTarget != null)
                    {
                        // Case A': 자리 소켓이 있으나 프리팹에 구워져 있고 검수한 본과 달라 덮어쓰기가 반드시 실패 —
                        // 항상 실패하는 선택지 1을 제거한 2지선다 (구 코드의 우아한 강등을 사전 차단 방식으로 계승)
                        bool doKey = EditorUtility.DisplayDialog(
                            "카탈로그 연결",
                            header +
                            $"이 캐릭터의 '{dialogEntry.targetSlotId}' 소켓은 프리팹에 구워져 있고 지금 검수한 본과 달라 덮어쓸 수 없습니다 (프리팹 모드에서 재조정하세요).\n\n" +
                            opt2 + "\n" + opt3 + footer,
                            "2 수행", "3 수행");
                        if (doKey)
                        {
                            ApplyDialogKeyOption(dialogEntry, existingKey, keyBlocked, ref commitSlotId, ref overwrite);
                        }
                        // 아니오(Esc 포함) = 3. 임시 이름 유지
                    }
                    else
                    {
                        // Case B: 자리 소켓 부재 — 1 = 이 소켓을 그 이름으로
                        int choice = EditorUtility.DisplayDialogComplex(
                            "카탈로그 연결",
                            header +
                            $"이 캐릭터에 '{dialogEntry.targetSlotId}' 소켓이 없습니다.\n\n" +
                            $"1. 이 소켓을 '{dialogEntry.targetSlotId}'(으)로 만들기 — 기존 등록을 그대로 사용 (권장)\n" +
                            opt2 + "\n" + opt3 + footer,
                            "1 수행", "3 수행", "2 수행");
                        if (choice == 0)
                        {
                            commitSlotId = dialogEntry.targetSlotId;
                        }
                        if (choice == 2)
                        {
                            ApplyDialogKeyOption(dialogEntry, existingKey, keyBlocked, ref commitSlotId, ref overwrite);
                        }
                        // choice 1 (Esc 포함) = 3. 임시 이름 유지 — 링크 없음
                    }

                    // 선택 결과 명시 — 어떤 버튼이 눌려 어떤 이름으로 커밋되는지 콘솔에 남긴다 (오선택/이중 커밋 진단용)
                    string overwriteName = "없음(새 소켓)";
                    if (overwrite != null)
                    {
                        overwriteName = "'" + overwrite.slotId + "' 소켓";
                    }
                    Debug.Log($"[SocketMaker] 연결 다이얼로그 선택 → 커밋 slotId '{commitSlotId}', 덮어쓰기 대상: {overwriteName}");

                    // 덮어쓰기 경로의 refDist 변화 로그용 전값 캡처 (전파 손보정 <1% 구멍 보완 — repick과 동일 취지)
                    if (overwrite != null)
                    {
                        overwritePh = overwrite.FindPlaceholder("placeholder");
                        if (overwritePh != null)
                        {
                            overwritePrevRefDist = overwritePh.bakedRefDistLocal;
                        }
                    }
                }
            }
        }
        // sizeRatio 괴리 경고 (검수한 모습 ≠ 기록될 값 — 기록은 장착 시 실제 쓰일 라이브 값 기준).
        // 검수 패널의 [크기±] 버튼으로 바꾼 것은 의도적 변경이라 경고하지 않는다 (sizeAdjustedInReview).
        EquipEntry liveEntry = ResolveCommitEntry();
        if (liveEntry != null && sizeAdjustedInReview == false
            && Mathf.Abs(liveEntry.sizeRatio - candidate.sizeRatioAtCapture) > 1e-6f)
        {
            Debug.LogWarning($"[SocketMaker] 검수 중 sizeRatio가 바뀌었습니다({candidate.sizeRatioAtCapture:G3}→{liveEntry.sizeRatio:G3}) — 기록은 현재 값 기준.");
        }

        Undo.IncrementCurrentGroup();
        if (repickMode)
        {
            Undo.SetCurrentGroupName("소켓 재조정 (검수 승인)");
        }
        else
        {
            if (overwrite != null)
            {
                Undo.SetCurrentGroupName("소켓 덮어쓰기 (검수 승인)");
            }
            else
            {
                Undo.SetCurrentGroupName("소켓 생성 (검수 승인)");
            }
        }
        int group = Undo.GetCurrentGroup();
        bool ok = CreateSocketAtHit(commitSlotId, candidate.bone, candidate.hit, target.transform, true, overwrite);
        Undo.CollapseUndoOperations(group);  // Ctrl+Z 1회 = 승인 전체 복원

        if (ok)
        {
            if (repickMode && repickPh != null)
            {
                // refDist 변화율 가시화 (<1% 변화는 전파 손보정 판정이 못 잡는 기지의 구멍 — 로그로 보완)
                Debug.Log($"[SocketMaker] 재조정 완료: refDist {repickPrevRefDist:F4} → {repickPh.bakedRefDistLocal:F4}");
            }
            else
            {
                if (overwrite != null && overwritePh != null)
                {
                    // 다이얼로그 덮어쓰기도 refDist 재베이크 — repick과 같은 로그로 전파 손보정 구멍 보완
                    Debug.Log($"[SocketMaker] '{commitSlotId}' 덮어쓰기 완료: refDist {overwritePrevRefDist:F4} → {overwritePh.bakedRefDistLocal:F4}");
                }
            }
            StopPick();  // 반드시 커밋 "후" (StopPick = 고스트 파괴)
        }
        else
        {
            Debug.LogWarning("[SocketMaker] 승인 실패 — 검수 상태 유지. [세션 취소]로 나갈 수 있습니다.");
        }
    }

    // 승인 다이얼로그용: 이름으로 기존 소켓을 찾고, 덮어쓰기 가능 여부를 사전 판정
    // (프리팹 인스턴스 소켓 + 검수한 본과 다름 = 커밋의 본 이사 가드에서 반드시 실패 — 선택지에서 미리 제외)
    private EquipSocket FindDialogSocket(string slotName, out bool blocked)
    {
        blocked = false;
        EquipSocket s = EquipAuthoringUtil.FindSocketBySlotId(target.transform, slotName);
        if (s != null && s.transform.parent != candidate.bone
            && PrefabUtility.IsPartOfPrefabInstance(s.gameObject))
        {
            blocked = true;
        }
        return s;
    }

    // 다이얼로그 선택지 2(key 이름) 집행: key 소켓이 있으면 그 소켓을 직접 참조로 덮어쓰기(리네임 면역),
    // 프리팹 차단이면 경고 후 임시 이름 유지, 없으면 key 이름 새 소켓
    private void ApplyDialogKeyOption(EquipEntry entry, EquipSocket existingKey, bool keyBlocked, ref string commitSlotId, ref EquipSocket overwrite)
    {
        if (existingKey != null && keyBlocked)
        {
            Debug.LogWarning($"[SocketMaker] '{entry.key}' 소켓은 프리팹에 구워져 있고 검수한 본과 달라 덮어쓸 수 없습니다 — 임시 이름('{commitSlotId}')으로 진행합니다.");
            return;
        }
        commitSlotId = entry.key;
        if (existingKey != null)
        {
            overwrite = existingKey;
        }
    }

    // 박제 key 재해석: pickKeys가 매 OnGUI 재구성돼 인덱스가 밀려도 커밋이 흔들리지 않는다
    private EquipEntry ResolveCommitEntry()
    {
        if (pickCatalog == null || string.IsNullOrEmpty(candidate.accessoryKey))
        {
            return null;
        }
        return pickCatalog.Get(candidate.accessoryKey);
    }

    // ── 검수 카메라 (턴테이블/시점) — sv.camera.transform 직접 대입 금지, LookAtDirect만 사용 ──

    private void ToggleTurntable()
    {
        if (turntableOn)
        {
            turntableOn = false;
            return;
        }
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.in2DMode || sv.isRotationLocked)
        {
            Debug.LogWarning("[SocketMaker] 씬 뷰가 없거나 2D/회전 잠금 상태 — 턴테이블을 켤 수 없습니다.");
            return;
        }
        // 현재 카메라 각도에서 이어받아 시작 (pitch는 -180~180 정규화 — if로)
        orbitYaw = sv.rotation.eulerAngles.y;
        orbitPitch = sv.rotation.eulerAngles.x;
        if (orbitPitch > 180f)
        {
            orbitPitch = orbitPitch - 360f;
        }
        turntablePrevTime = EditorApplication.timeSinceStartup;
        turntableHasLastSet = false;  // 첫 틱 개입 오탐 방지
        turntableOn = true;
        // 첫 프레이밍 즉시 적용 + last* 기록
        Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        sv.LookAtDirect(reviewFocus, rot, reviewFrameSize);
        turntableLastRot = rot;
        turntableLastPivot = reviewFocus;
        turntableLastSize = reviewFrameSize;
        turntableHasLastSet = true;
        sv.Repaint();
    }

    private void TurntableTick()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.in2DMode || sv.isRotationLocked)
        {
            return;  // null/2D = 스킵 (정지 아님 — 뷰가 돌아오면 재개)
        }
        double now = EditorApplication.timeSinceStartup;  // Time.deltaTime 불신 (에디터 틱)
        float dt = Mathf.Min((float)(now - turntablePrevTime), 0.1f);
        turntablePrevTime = now;
        if (turntableHasLastSet)
        {
            // 사용자 개입 감지: 직전에 "내가 세팅한" 값과 현재값을 상대 epsilon 비교 → 자동 해제
            bool moved = Quaternion.Angle(sv.rotation, turntableLastRot) > 0.5f
                || (sv.pivot - turntableLastPivot).magnitude > turntableLastSize * 0.005f
                || Mathf.Abs(sv.size - turntableLastSize) > turntableLastSize * 0.005f;
            if (moved)
            {
                turntableOn = false;
                Repaint();
                return;
            }
        }
        orbitYaw = orbitYaw + 20f * dt;  // 20°/s — 워크벤치 턴테이블과 동일 감각, pitch 불변
        Quaternion rot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
        sv.LookAtDirect(reviewFocus, rot, reviewFrameSize);
        turntableLastRot = rot;
        turntableLastPivot = reviewFocus;
        turntableLastSize = reviewFrameSize;
        turntableHasLastSet = true;
        sv.Repaint();
    }

    // 시점 버튼: 0정면 1후면 2좌 3우 4상 — 캐릭터 루트 기준, 후보 지점 프레이밍
    private void SetViewpoint(int which)
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv == null || sv.in2DMode || sv.isRotationLocked)
        {
            return;
        }
        if (target == null)
        {
            return;
        }
        Transform t = target.transform;
        Vector3 dir;
        Vector3 up = Vector3.up;
        if (which == 0)
        {
            dir = -t.forward;
        }
        else
        {
            if (which == 1)
            {
                dir = t.forward;
            }
            else
            {
                if (which == 2)
                {
                    dir = t.right;
                }
                else
                {
                    if (which == 3)
                    {
                        dir = -t.right;
                    }
                    else
                    {
                        // 상: up=t.forward (Vector3.up 평행 특이점 회피)
                        dir = -t.up;
                        up = t.forward;
                    }
                }
            }
        }
        if (Mathf.Abs(Vector3.Dot(dir.normalized, up.normalized)) > 0.999f)
        {
            // 퇴화 폴백 (접선 폴백 선례와 동일 발상)
            up = Vector3.forward;
        }
        Quaternion rot = Quaternion.LookRotation(dir, up);
        sv.LookAtDirect(reviewFocus, rot, reviewFrameSize);
        // 턴테이블 연속성: yaw/pitch 역산 + last* 기록 (버튼이 개입 감지로 오인되지 않게)
        orbitYaw = rot.eulerAngles.y;
        orbitPitch = rot.eulerAngles.x;
        if (orbitPitch > 180f)
        {
            orbitPitch = orbitPitch - 360f;
        }
        turntableLastRot = rot;
        turntableLastPivot = reviewFocus;
        turntableLastSize = reviewFrameSize;
        turntableHasLastSet = true;
        sv.Repaint();
    }

    // ── 재조정 (repick — 기존 placeholder의 고스트 재배치) ──

    // placeholder/소켓 인스펙터의 진입점: 기존 소켓을 대상으로 픽 세션을 열고,
    // 승인 시 신규 생성 대신 그 소켓을 덮어쓴다 (slotId·카탈로그 연결·스탬프 무접촉).
    public static void BeginRepick(EquipPlaceholder ph)
    {
        if (ph == null)
        {
            return;
        }
        if (Application.isPlaying)
        {
            Debug.LogWarning("[SocketMaker] 플레이 모드에서는 재조정할 수 없습니다.");
            return;
        }
        EquipSocket socket = ph.OwnerSocket;
        if (socket == null)
        {
            Debug.LogWarning("[SocketMaker] 부모에 EquipSocket이 없습니다.");
            return;
        }
        // 커밋은 FindPlaceholder("placeholder")로 부착점을 다시 찾는다 — 버튼 누른 ph와 불일치하면 중단
        if (socket.FindPlaceholder("placeholder") != ph)
        {
            EditorUtility.DisplayDialog("고스트 재조정", "이 부착점은 소켓의 표준 부착점(placeholder)이 아니어서 재조정할 수 없습니다.", "확인");
            return;
        }

        EquipSocketMakerWindow w = GetWindow<EquipSocketMakerWindow>(false, "Socket Maker", true);

        // 세션 재진입 확인 — StartPick 첫 줄의 StopPick이 진행 중 후보를 조용히 폐기하는 사고 방지
        if (w.pickPhase != PickPhase.Off)
        {
            bool proceed = EditorUtility.DisplayDialog("고스트 재조정",
                "진행 중인 픽 세션이 있습니다. 버리고 재조정을 시작할까요?", "시작", "취소");
            if (proceed == false)
            {
                return;
            }
        }

        // 대상 루트 해석: 스테이지 소속이면 스테이지 루트, 아니면 씬 루트 (창의 target 규약 + 소켓 회전 규약 charRoot.rotation).
        // 로컬에만 담는다 — 아래 가드에서 실패하면 창 상태(target 포함)를 일절 건드리지 않아야 한다 (진입 실패 = 창 무변조 계약)
        GameObject resolvedRoot;
        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && socket.transform.IsChildOf(stage.prefabContentsRoot.transform))
        {
            resolvedRoot = stage.prefabContentsRoot;
        }
        else
        {
            resolvedRoot = socket.transform.root.gameObject;
        }
        // 규약 검증: 기존 소켓 회전이 해석 루트와 0.5° 이상 다르면 경고만 (승인 시 루트 기준으로 재정립되므로 진행 허용)
        float rootAngle = Quaternion.Angle(socket.transform.rotation, resolvedRoot.transform.rotation);
        if (rootAngle > 0.5f)
        {
            Debug.LogWarning($"[SocketMaker] '{socket.slotId}' 소켓 회전이 루트 규약과 {rootAngle:F1}° 어긋나 있습니다 — 승인 시 루트 기준으로 재정립됩니다.");
        }

        // 악세서리 key 해석 (Record → key==slotId → targetSlotId==slotId) + prefab 가드 — target/세션 무접촉 읽기 전용
        w.LoadPickCatalog();
        string key = w.ResolveRepickAccessoryKey(socket);
        if (key == null)
        {
            EditorUtility.DisplayDialog("고스트 재조정",
                $"'{socket.slotId}' 소켓에 연결된 악세서리를 찾지 못했습니다.\n배치 기록도, 카탈로그의 key/targetSlotId 일치 엔트리도 없습니다.\n카탈로그에서 이 슬롯에 악세서리를 먼저 연결하세요.", "확인");
            return;
        }
        EquipEntry keyEntry = w.pickCatalog.Get(key);
        if (keyEntry == null || keyEntry.prefab == null)
        {
            EditorUtility.DisplayDialog("고스트 재조정", $"'{key}' 엔트리의 프리팹이 비어 있어 고스트를 띄울 수 없습니다.", "확인");
            return;
        }

        // 모든 가드 통과 — 이제부터 창 상태 변경 (StartPick이 HasCache 점검에 target을 쓰므로 대입이 먼저)
        w.target = resolvedRoot;
        w.StartPick();  // 첫 줄의 StopPick이 이전 repick 세션의 창 설정 원복까지 수행 (HasCache 사전 점검 내장)
        if (w.pickPhase != PickPhase.Picking)
        {
            return;  // 캐시 실패 등 미시작
        }

        // 원복 백업은 StartPick "후" — 이전 repick 세션이 덮었던 값이 아니라 사용자의 원래 설정을 캡처
        w.sessionPrevContactAnchor = w.pickContactAnchor;
        w.sessionPrevKeyIndex = w.pickKeyIndex;

        // ── StartPick 이후 프리셋 (pickSlotId/ghostLift/lastGhostRotValid 리셋 뒤여야 함 — 순서 필수) ──
        w.pickSlotId = socket.slotId;
        w.pickContactAnchor = ph.contactAnchor;
        w.pickKeyIndex = System.Array.IndexOf(w.pickKeys, key);
        // linkCatalogOnPlace는 건드리지 않는다 — 커밋 게이트의 overwriteSocket == null 조건이 링크 블록을 스킵
        w.repickMode = true;
        w.repickSocket = socket;
        w.repickPh = ph;
        w.repickRefPos = ph.transform.position;
        w.repickRefRot = ph.transform.rotation;
        w.repickRefDistWorld = ph.bakedRefDistLocal * EquipAuthoringUtil.LossyAvg(socket.transform);
        w.repickPrevRefDist = ph.bakedRefDistLocal;
        w.BuildRepickRefGhost(keyEntry);  // 실패 시 마커만으로 무음 폴백
        w.Repaint();
    }

    // repick 악세서리 key 해석 사다리: ① Record.accessoryKey(카탈로그 실존 시) ② key == slotId ③ targetSlotId == slotId
    private string ResolveRepickAccessoryKey(EquipSocket socket)
    {
        EquipPlacementRecord rec = socket.GetComponent<EquipPlacementRecord>();
        if (rec != null && string.IsNullOrEmpty(rec.accessoryKey) == false
            && pickCatalog != null && pickCatalog.Get(rec.accessoryKey) != null)
        {
            return rec.accessoryKey;
        }
        if (pickCatalog == null)
        {
            return null;
        }
        foreach (EquipEntry entry in pickCatalog.Entries)
        {
            if (entry != null && entry.key == socket.slotId)
            {
                return entry.key;
            }
        }
        foreach (EquipEntry entry in pickCatalog.Entries)
        {
            if (entry != null && entry.targetSlotId == socket.slotId)
            {
                return entry.key;
            }
        }
        return null;
    }

    // 기존 배치 참조 마커: 주황 디스크 + 로컬 축 3선 + 라벨 + refDist 와이어 원 (Repaint 전용)
    private void DrawRepickReference()
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }
        if (repickSocket == null || repickPh == null)
        {
            return;
        }
        float size = HandleUtility.GetHandleSize(repickRefPos) * 0.12f;
        Handles.color = new Color(1f, 0.6f, 0.15f, 0.9f);
        Handles.DrawWireDisc(repickRefPos, repickRefRot * Vector3.up, size);
        Handles.DrawLine(repickRefPos, repickRefPos + repickRefRot * Vector3.right * size * 1.5f);
        Handles.DrawLine(repickRefPos, repickRefPos + repickRefRot * Vector3.up * size * 1.5f);
        Handles.DrawLine(repickRefPos, repickRefPos + repickRefRot * Vector3.forward * size * 1.5f);
        Handles.Label(repickRefPos + repickRefRot * Vector3.up * size * 2f, "기존 배치 (승인 시 덮어씀)");
        if (repickRefDistWorld > 1e-12f)
        {
            Handles.color = new Color(1f, 0.8f, 0.3f, 0.5f);
            Handles.DrawWireDisc(repickSocket.transform.position, repickRefRot * Vector3.up, repickRefDistWorld);
        }
    }

    // 기존 배치 실물 참조: 진입 시점(아직 기존 배치)의 "지금 모습"을 실장착 함수로 박제.
    // 외관은 원본 그대로 (머티리얼 무변조 — sharedMaterial 오염/인스턴스 누수 회피), 구분은 Handles 라벨이 담당.
    private void BuildRepickRefGhost(EquipEntry entry)
    {
        DestroyRepickRefGhost();
        if (entry == null || entry.prefab == null || repickSocket == null || repickPh == null)
        {
            return;
        }
        repickRefGhost = (GameObject)Instantiate(entry.prefab);
        repickRefGhost.name = "__EquipPreview__RepickRef";  // 레이캐스터 제외 규약
        repickRefGhost.hideFlags = HideFlags.DontSave;
        bool fitted = EquipPlacement.FitToPlaceholder(repickRefGhost, repickSocket, repickPh, entry);
        if (fitted == false)
        {
            // 거부 경로에서는 인스턴스가 내부에서 파괴됨 — 마커만으로 무음 폴백
            repickRefGhost = null;
        }
    }

    private void DestroyRepickRefGhost()
    {
        if (repickRefGhost != null)
        {
            DestroyImmediate(repickRefGhost);
            repickRefGhost = null;
        }
    }

    // 소켓 GO 이름: "Socket_" + slotId — slotId가 이미 socket_로 시작하면 접두사 중복 제거 (Socket_socket_4 → Socket_4)
    private string SocketGoName(string slotId)
    {
        if (slotId.StartsWith("socket_"))
        {
            return "Socket_" + slotId.Substring("socket_".Length);
        }
        return "Socket_" + slotId;
    }

    // 히트 지점에 소켓 생성: 본 원점에 소켓(캡슐 없음) + 히트점에 placeholder(부착점, refDist 베이크)
    // fromPick: 픽 승인 경로 여부 (기존 픽 모드 판별자의 파라미터화 — 베이크는 false)
    // overwriteSocket: 재조정(repick) 덮어쓰기 대상 (null=신규/베이크). 반환: 커밋 성공 여부.
    private bool CreateSocketAtHit(string slotId, Transform bone, EquipMeshHit hit, Transform charRoot, bool fromPick, EquipSocket overwriteSocket)
    {
        // 덮어쓰기는 직접 참조 — Reviewing 중 소켓 리네임에도 대상 불변
        EquipSocket existing;
        if (overwriteSocket != null)
        {
            existing = overwriteSocket;
        }
        else
        {
            existing = EquipAuthoringUtil.FindSocketBySlotId(charRoot, slotId);
        }
        GameObject socketGo;
        bool created = false;

        if (existing != null)
        {
            socketGo = existing.gameObject;
            if (socketGo.transform.parent != bone)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(socketGo))
                {
                    Debug.LogWarning($"[SocketMaker] '{slotId}' 소켓은 프리팹에 구워져 있어 이동 불가 — 프리팹 모드에서 하세요.");
                    return false;
                }
                Undo.SetTransformParent(socketGo.transform, bone, "Move Socket");
            }
        }
        else
        {
            socketGo = new GameObject(SocketGoName(slotId));
            Undo.RegisterCreatedObjectUndo(socketGo, "Create Socket (Click)");
            socketGo.transform.SetParent(bone, false);
            created = true;
        }

        // 기존 소켓 재사용 시 TRS 재설정 Undo 갭 봉합 (신규 생성 GO에는 중복 기록이지만 무해)
        Undo.RecordObject(socketGo.transform, "Place Socket");
        socketGo.transform.position = bone.position;
        socketGo.transform.rotation = charRoot.rotation;
        socketGo.transform.localScale = Vector3.one;

        float hitDist = (hit.point - bone.position).magnitude;
        if (hitDist <= 1e-6f)
        {
            hitDist = EquipAuthoringUtil.MeasureCharHeight(charRoot.gameObject) * 0.05f;
        }

        EquipSocket socket = socketGo.GetComponent<EquipSocket>();
        if (socket == null)
        {
            socket = socketGo.AddComponent<EquipSocket>();
        }
        // slotId 대입 Undo 갭 봉합
        Undo.RecordObject(socket, "Place Socket");
        socket.slotId = slotId;

        // placeholder = 클릭점, 크기 기준 refDist 베이크 (구명 PH_spot/"spot" — 조회는 별칭으로 호환)
        EquipPlaceholder ph = socket.FindPlaceholder("placeholder");
        if (ph == null)
        {
            GameObject phGo = new GameObject("placeholder");
            Undo.RegisterCreatedObjectUndo(phGo, "Create Placeholder (Click)");
            phGo.transform.SetParent(socketGo.transform, false);
            ph = phGo.AddComponent<EquipPlaceholder>();
            ph.placeholderId = "placeholder";
        }
        else
        {
            Undo.RecordObject(ph.transform, "Place Placeholder");
            Undo.RecordObject(ph, "Place Placeholder");
        }

        Vector3 tangent = Vector3.Cross(hit.normal, Vector3.right);
        if (tangent.sqrMagnitude < 1e-6f)
        {
            tangent = Vector3.Cross(hit.normal, Vector3.forward);
        }

        // 부착점 = 히트점 + 거리 띄우기 (고스트로 보던 그대로 — 픽 승인 경로에서만, 베이크 경로는 소스 위치 그대로)
        // ghostLift/lastGhostRot은 라이브 필드 읽기 유지 — Reviewing 동안 휠/팝업이 잠겨 동결이므로 곧 후보값
        Vector3 phPoint = hit.point;
        if (fromPick && ghostLift > 0f)
        {
            phPoint = hit.point + hit.normal * (ghostLift * hitDist);
        }
        ph.transform.position = phPoint;

        // 회전: 고스트로 보던 회전 그대로 베이크 (없으면 표면 기준)
        if (lastGhostRotValid)
        {
            ph.transform.rotation = lastGhostRot;
        }
        else
        {
            ph.transform.rotation = Quaternion.LookRotation(tangent.normalized, hit.normal);
        }

        ph.contactAnchor = pickContactAnchor;  // 창에서 고른 접촉 기준 그대로 베이크 (고스트와 동일)
        ph.bakedRefDistLocal = hitDist / EquipAuthoringUtil.LossyAvg(socketGo.transform);

        // 장착 정보 저장: 후보에 박제한 key를 재해석해 이 소켓에 연결 (WYSIWYG 배치 → 바로 장착 가능)
        // repick(overwriteSocket != null)은 링크 블록 통째 스킵 = slotId·카탈로그 연결 유지, linkCatalogOnPlace 무변조
        EquipEntry linkEntry = null;
        if (fromPick)
        {
            linkEntry = ResolveCommitEntry();
        }
        if (fromPick && linkCatalogOnPlace && linkEntry != null && overwriteSocket == null)
        {
            // 카탈로그 쓰기는 "첫 등록"일 때만: 자리 이름이 비었거나 임시(socket_*)일 때 이 소켓으로 채운다.
            // 의미 있는 자리 이름(head1 등)은 절대 고치지 않는다 — 다른 캐릭터의 연결 자산(읽기 전용).
            // 그 경우의 처리(기존 소켓 덮어쓰기/키 이름/임시 이름)는 승인 다이얼로그(ApproveCandidate)가
            // "소켓 쪽을 맞추는" 선택으로 이미 끝냈고, 여기는 통과만 한다.
            bool firstRegistration = string.IsNullOrEmpty(linkEntry.targetSlotId)
                || linkEntry.targetSlotId.StartsWith("socket_");
            if (firstRegistration && linkEntry.targetSlotId != slotId)
            {
                Undo.RecordObject(pickCatalog, "Link Catalog Entry");
                linkEntry.targetSlotId = slotId;
                EditorUtility.SetDirty(pickCatalog);
                Debug.Log($"[SocketMaker] 카탈로그 첫 등록: '{linkEntry.key}' → '{slotId}'. (소켓 인스펙터에서 리네임하면 카탈로그도 자동 동기화됩니다)");
            }
        }

        // 배치 기록: 이 소켓을 만들 때의 고스트 결과(악세서리 key + 소켓-로컬 TRS)를 소켓에 남긴다
        // — 이후 재현·전파 검수·미세조정 시작값으로 활용 (카탈로그 연결 토글과 무관하게 기록).
        // 산출은 후보의 동결 TRS 기준 — 고스트 파괴/변형 내성 (repick 승인도 여기서 Record 갱신)
        if (fromPick && linkEntry != null && candidate.hasGhostPose)
        {
            EquipPlacementRecord record = socketGo.GetComponent<EquipPlacementRecord>();
            if (record == null)
            {
                record = Undo.AddComponent<EquipPlacementRecord>(socketGo);
            }
            else
            {
                Undo.RecordObject(record, "Update Placement Record");
            }
            record.accessoryKey = linkEntry.key;
            record.sizeRatioAtPlacement = linkEntry.sizeRatio;
            record.ghostLocalPosition = socketGo.transform.InverseTransformPoint(candidate.ghostWorldPos);
            record.ghostLocalEuler = (Quaternion.Inverse(socketGo.transform.rotation) * candidate.ghostWorldRot).eulerAngles;
            record.ghostLocalScale = candidate.ghostWorldScale / EquipAuthoringUtil.LossyAvg(socketGo.transform);
        }

        // 베이크 폴드아웃 자동 매핑: 방금 만든 부착점(placeholder)을 위치 소스로 —
        // 폴드아웃에서 본/이름만 바꿔 곧바로 "다른 본/이름으로 이사"가 가능해진다
        bakeSource = ph.gameObject;
        bakeBone = bone;
        if (slotId.StartsWith("socket_"))
        {
            // 임시 이름이면 추천명 프리필: 박제 엔트리의 2번째 추천명(targetSlotId, 의미 있는 이름일 때) → 1번째(key)
            bakeSlotId = "";
            if (fromPick && linkEntry != null)
            {
                if (string.IsNullOrEmpty(linkEntry.targetSlotId) == false && linkEntry.targetSlotId.StartsWith("socket_") == false)
                {
                    bakeSlotId = linkEntry.targetSlotId;
                }
                else
                {
                    bakeSlotId = linkEntry.key;
                }
            }
        }
        else
        {
            bakeSlotId = slotId;
        }

        MarkTargetDirty();

        // 소켓을 선택해 리네임 유도 (인스펙터에 미리네임 경고 배지가 뜸)
        Selection.activeGameObject = socketGo;
        // 신규/기존 재사용을 구분해 콘솔 추적 오도 방지 (덮어쓰기·재조정도 이 함수를 탄다)
        string commitVerb = "생성";
        if (created == false)
        {
            commitVerb = "갱신";
        }
        Debug.Log($"[SocketMaker] '{slotId}' {commitVerb} → 본 '{bone.name}' (refDist≈{hitDist:F2} 월드). 인스펙터에서 slotId에 의미 있는 이름을 지어주세요.");
        return true;
    }

    // 씬/프리팹 스테이지 dirty (플레이 모드 제외)
    private void MarkTargetDirty()
    {
        if (Application.isPlaying)
        {
            return;
        }

        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }
        else
        {
            EditorSceneManager.MarkSceneDirty(target.scene);
        }
    }

    // ── 악세서리 고스트 ──
    private EquipCatalog pickCatalog;
    private string[] pickKeys;
    private int pickKeyIndex;
    private GameObject pickGhost;
    private float ghostNatural;
    private Vector3 ghostNaturalCenter;   // identity 측정 바운드 중심 (결정적 배치 보정용)
    private Vector3 ghostNaturalExtents;  // identity 측정 반치수
    private bool ghostMeasured;
    // 회전 3축 — 표면 프레임(오른쪽=X, 법선=Y, 접선=Z) 기준. UI 표기는 회전 평면(ZX/YZ/XY)으로 통일.
    private float ghostYaw;               // ZX 회전 = 법선(Y) 축 (Ctrl+휠)
    private float ghostTilt;              // YZ 회전 = 기울임, 오른쪽(X) 축 (Shift+휠)
    private float ghostRoll;              // XY 회전 = 롤, 접선/전방(Z) 축 (Ctrl+Shift+휠) — 3축째, 이걸로 전 방향 커버
    private float ghostLift;              // 거리: 표면 노멀 방향 띄우기, hitDist 배수 (맨휠, 0=표면)
    private Quaternion lastGhostRot;      // 마지막 고스트 회전 (배치 시 베이크)
    private bool lastGhostRotValid;       // 위 값 유효 여부

    // 고스트 휠 조작 공용 처리 (Picking·Reviewing 공유). 반환 = 값이 바뀌었는지 (호출측이 e.Use/재핏 담당).
    // 회전 = 표면 프레임 평면 기준: Ctrl+휠=ZX(법선축), Shift+휠=YZ(기울임), Ctrl+Shift+휠=XY(롤).
    // 방향은 지배 축으로 읽는다 — Shift를 누르면 OS/에디터가 휠 델타를 가로축(delta.x)으로 보내
    // delta.y만 보면 한쪽으로만 도는 버그가 있다 (Shift+휠 증가 고정 버그의 원인).
    // allowBareLift=false(검수)면 맨휠은 건드리지 않는다(카메라 줌 통과) — 거리는 Alt+휠로 조정.
    private bool HandleGhostWheel(Event e, bool allowBareLift)
    {
        if (e.type != EventType.ScrollWheel)
        {
            return false;
        }
        if (e.control == false && e.shift == false && e.alt == false && allowBareLift == false)
        {
            return false;  // 검수 중 맨휠 = 카메라 줌 (미소비)
        }

        float raw = e.delta.y;
        if (Mathf.Abs(e.delta.x) > Mathf.Abs(raw))
        {
            raw = e.delta.x;
        }
        float sign = 1f;
        if (raw > 0f)
        {
            sign = -1f;
        }

        if (e.control && e.shift)
        {
            ghostRoll = ghostRoll + sign * 15f;
        }
        else
        {
            if (e.control)
            {
                ghostYaw = ghostYaw + sign * 15f;
            }
            else
            {
                if (e.shift)
                {
                    ghostTilt = ghostTilt + sign * 15f;
                }
                else
                {
                    // 맨휠 = 거리: hitDist의 5% 스텝, 표면 안쪽(음수)은 금지
                    // (픽 모드 중 카메라 줌은 휠 대신 Alt+우클릭 드래그)
                    ghostLift = Mathf.Max(0f, ghostLift + sign * 0.05f);
                }
            }
        }
        return true;
    }
    private bool linkCatalogOnPlace = true;  // 배치 시 카탈로그 targetSlotId를 이 소켓으로 연결
    private EquipContactAnchor pickContactAnchor = EquipContactAnchor.Pivot;  // 접촉 기준 (고스트=실장착 동일 적용). 기본 Pivot = 모델 원점을 클릭점에

    private void LoadPickCatalog()
    {
        if (pickCatalog == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:EquipCatalog");
            if (guids.Length > 0)
            {
                pickCatalog = AssetDatabase.LoadAssetAtPath<EquipCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            pickKeys = null;
        }

        // 키 목록은 매번 재구성 — 카탈로그에 엔트리를 추가/삭제하면 창에 즉시 반영
        List<string> keys = new List<string>();
        if (pickCatalog != null)
        {
            foreach (EquipEntry entry in pickCatalog.Entries)
            {
                if (entry != null && string.IsNullOrEmpty(entry.key) == false)
                {
                    keys.Add(entry.key);
                }
            }
        }
        pickKeys = keys.ToArray();
        if (pickKeyIndex >= pickKeys.Length)
        {
            pickKeyIndex = 0;
        }
    }

    private EquipEntry GetPickEntry()
    {
        if (pickCatalog == null || pickKeys == null || pickKeys.Length == 0)
        {
            return null;
        }
        if (pickKeyIndex < 0 || pickKeyIndex >= pickKeys.Length)
        {
            return null;
        }
        return pickCatalog.Get(pickKeys[pickKeyIndex]);
    }

    // 고스트를 히트점에 핏 — 실제 장착과 동일 규약 (크기=2×hitDist×sizeRatio, 바운드 중심 정렬 + BottomAlign)
    private void UpdateGhost(Transform charRoot, EquipMeshHit hit)
    {
        EquipEntry entry = GetPickEntry();
        if (entry == null || entry.prefab == null)
        {
            HideGhost();
            return;
        }

        if (pickGhost == null)
        {
            pickGhost = (GameObject)Instantiate(entry.prefab);
            pickGhost.name = "__EquipPreview__Ghost";  // 레이캐스터 제외 패턴
            pickGhost.hideFlags = HideFlags.DontSave;

            // 핵심: Instantiate는 활성 씬에 생성됨 — 프리팹 스테이지/멀티 씬에서는 대상과 다른 씬일 수 있다.
            // 대상 캐릭터와 같은 씬으로 이동시켜야 한다.
            if (target != null && target.scene.IsValid() && pickGhost.scene != target.scene)
            {
                try
                {
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pickGhost, target.scene);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[SocketMaker] 고스트 씬 이동 실패: " + ex.Message);
                }
            }

            // 진단: 고스트가 어느 씬에 있는지 (씬에서 안 보이는 문제 추적용)
            Debug.Log($"[SocketMaker] 고스트 생성: '{entry.key}' → 씬 '{pickGhost.scene.name}' (대상 씬 '{target.scene.name}', 활성 상태 {pickGhost.activeSelf})");

            // 측정 규약 통일: 런타임(FitToPlaceholder 1단계)과 동일하게 원점/identity/scale1에서 측정
            pickGhost.transform.position = Vector3.zero;
            pickGhost.transform.rotation = Quaternion.identity;
            pickGhost.transform.localScale = Vector3.one;

            ghostMeasured = EquipFitter.MeasureNaturalFull(pickGhost, out ghostNatural, out ghostNaturalCenter, out ghostNaturalExtents);
        }
        pickGhost.SetActive(true);

        Transform bone = EquipMeshRaycaster.Instance.QueryDominantBone(charRoot, hit);
        float hitDist = 0f;
        if (bone != null)
        {
            hitDist = (hit.point - bone.position).magnitude;
        }

        float scale = 1f;
        if (ghostMeasured && ghostNatural > 1e-9f && hitDist > 1e-9f)
        {
            scale = 2f * hitDist * entry.sizeRatio / ghostNatural * entry.fitBias;
        }

        Vector3 tangent = Vector3.Cross(hit.normal, Vector3.right);
        if (tangent.sqrMagnitude < 1e-6f)
        {
            tangent = Vector3.Cross(hit.normal, Vector3.forward);
        }

        // 회전 = 표면 기준(base) × 사용자 회전 (Ctrl+휠=ZX/법선축 yaw, Shift+휠=YZ/기울임 tilt, Ctrl+Shift+휠=XY/롤 roll).
        // placeholder에는 spunBase만 베이크 — 장착 시 entry.rotationOffset이 다시 곱해지므로(FitToPlaceholder),
        // 고스트 "표시"에는 entry.rotationOffset까지 합성해야 실장착과 동일하게 보인다 (WYSIWYG).
        Quaternion baseRot = Quaternion.LookRotation(tangent.normalized, hit.normal);
        // 3축 사용자 회전: yaw=법선(Y), tilt=오른쪽(X), roll=접선/전방(Z) — 세 축이면 임의 방향 도달 가능 (2축만으로는 불가)
        Quaternion spunBase = Quaternion.AngleAxis(ghostYaw, hit.normal)
            * Quaternion.AngleAxis(ghostTilt, baseRot * Vector3.right)
            * Quaternion.AngleAxis(ghostRoll, baseRot * Vector3.forward)
            * baseRot;
        Quaternion displayRot = spunBase * Quaternion.Euler(entry.rotationOffset);

        pickGhost.transform.localScale = Vector3.one * scale;
        pickGhost.transform.rotation = displayRot;

        // 부착점 = 히트점 + 거리 띄우기 (맨휠, hitDist 배수 — 클릭 시 placeholder 위치로 그대로 베이크됨)
        Vector3 anchorPoint = hit.point + hit.normal * (ghostLift * hitDist);

        // 접촉 규약 (실장착 FitToPlaceholder 4단계와 동일):
        // Pivot = 모델 원점(0,0,0)을 부착점에 그대로 / Center·BottomAlign = identity 측정 center/extents를
        // 현재 회전·스케일로 환산해 바운드 정렬 (Renderer.bounds stale 문제 회피)
        if (pickContactAnchor == EquipContactAnchor.Pivot)
        {
            pickGhost.transform.position = anchorPoint;
        }
        else
        {
            Vector3 worldCenterOffset = displayRot * (ghostNaturalCenter * scale);
            pickGhost.transform.position = anchorPoint - worldCenterOffset;
        }

        // BottomAlign: 회전된 AABB의 반치수만큼 올림.
        // up은 hit.normal이 아니라 spunBase*up을 쓴다 — placeholder에는 spunBase가 베이크되고
        // 실장착(FitToPlaceholder)은 placeholder.transform.up으로 밀어올리므로,
        // 기울임(Shift+휠, ghostTilt≠0) 상태에서 hit.normal을 쓰면 실장착과 방향이 어긋난다.
        // (tilt=0이면 spunBase*up == hit.normal — 기존 동작과 동일)
        if (pickContactAnchor == EquipContactAnchor.BottomAlign)
        {
            Vector3 up = spunBase * Vector3.up;
            float extentAlongUp =
                Mathf.Abs(Vector3.Dot(up, displayRot * Vector3.right)) * ghostNaturalExtents.x +
                Mathf.Abs(Vector3.Dot(up, displayRot * Vector3.up)) * ghostNaturalExtents.y +
                Mathf.Abs(Vector3.Dot(up, displayRot * Vector3.forward)) * ghostNaturalExtents.z;
            pickGhost.transform.position = pickGhost.transform.position + up * (extentAlongUp * scale);
        }

        // 아이템 고유 오프셋 — 실장착(FitToPlaceholder 5단계)과 동일 규약 (placeholder 프레임 = spunBase, rWorld = hitDist)
        if (hitDist > 1e-9f)
        {
            Vector3 offsetWorld =
                (spunBase * Vector3.right) * (entry.positionOffsetRadii.x * hitDist) +
                (spunBase * Vector3.up) * (entry.positionOffsetRadii.y * hitDist) +
                (spunBase * Vector3.forward) * (entry.positionOffsetRadii.z * hitDist);
            pickGhost.transform.position = pickGhost.transform.position + offsetWorld;
        }

        lastGhostRot = spunBase;
        lastGhostRotValid = true;

        // 진단: 픽 세션당 1회 고스트 배치 상태
        if (ghostDiagLogged == false)
        {
            ghostDiagLogged = true;
            Debug.Log($"[SocketMaker] 고스트 배치 진단: pos={pickGhost.transform.position}, 히트점={hit.point}, scale={scale:G3}, hitDist={hitDist:F2}, 씬='{pickGhost.scene.name}'");
        }

    }

    private void HideGhost()
    {
        if (pickGhost != null)
        {
            pickGhost.SetActive(false);
        }
    }

    private void DestroyGhost()
    {
        if (pickGhost != null)
        {
            DestroyImmediate(pickGhost);
            pickGhost = null;
        }
        ghostMeasured = false;
    }

    // ── 베이크 (접힘 기본 — 손 배치한 오브젝트를 소켓으로 굽기) ──
    private bool showBake = false;
    private bool showLinks = true;  // 카탈로그 연결 현황 폴드아웃
    private GameObject bakeSource;
    private Transform bakeBone;
    private string bakeSlotId = "";  // 비어 있으면 베이크 버튼 비활성 — 진짜 이름 입력 유도

    // 배치된 오브젝트의 현재 위치/회전을 소켓/placeholder로 굽는다 (소스 무변경).
    // 용도: (a) 손으로 배치한 오브젝트 → 소켓 (b) 이미 만든 소켓의 부착점을 다른 본/이름으로 다시 굽기
    private void BakeFromObject()
    {
        Transform charRoot = target.transform;
        string srcName = bakeSource.name;

        if (bakeBone.IsChildOf(charRoot) == false)
        {
            Debug.LogWarning($"[SocketMaker] 본 '{bakeBone.name}'이 대상 캐릭터의 하위가 아닙니다 — 베이크 중단.");
            return;
        }

        // 소스가 소켓(또는 그 하위)이면 실제 부착점(placeholder)으로 대체 —
        // 소켓 GO는 본 원점에 있어서 그대로 구우면 본 원점 소켓이라는 무의미한 결과가 된다
        Transform srcTr = bakeSource.transform;
        EquipSocket srcSocket = bakeSource.GetComponentInParent<EquipSocket>();
        if (srcSocket != null && (bakeSource.GetComponent<EquipPlaceholder>() == null))
        {
            EquipPlaceholder srcPh = srcSocket.FindPlaceholder("placeholder");
            if (srcPh != null)
            {
                srcTr = srcPh.transform;
                Debug.Log($"[SocketMaker] 위치 소스 '{srcName}'은 소켓이라 부착점(placeholder) 위치/회전으로 대체합니다.");
            }
        }

        // 자동 매핑(CreateSocketAtHit)이 bakeSource를 새 부착점으로 덮어쓰므로 위치/회전을 먼저 캡처
        Vector3 srcPos = srcTr.position;
        Quaternion srcRot = srcTr.rotation;

        // 가짜 히트를 만들어 클릭 생성과 동일 경로 사용
        EquipMeshHit fakeHit = new EquipMeshHit();
        fakeHit.point = srcPos;
        fakeHit.normal = srcTr.up;

        // fromPick=false = 기존 베이크 시점의 "픽 모드 아님"과 동치 (canBake가 세션 중 베이크를 원천 차단)
        CreateSocketAtHit(bakeSlotId, bakeBone, fakeHit, charRoot, false, null);

        // 회전은 소스 그대로 덮어쓰기
        EquipSocket socket = EquipAuthoringUtil.FindSocketBySlotId(charRoot, bakeSlotId);
        if (socket != null)
        {
            EquipPlaceholder ph = socket.FindPlaceholder("placeholder");
            if (ph != null)
            {
                ph.transform.rotation = srcRot;
            }
        }

        // 소스가 소켓이었다면 = "다시 굽기(이사/리네임)" — 장착 정보가 끊기지 않게 링크·기록을 새 소켓으로 이관
        if (srcSocket != null && socket != null && srcSocket.slotId != bakeSlotId)
        {
            // (a) 카탈로그: 원본 소켓을 가리키던 모든 엔트리의 targetSlotId를 새 slotId로
            if (pickCatalog != null)
            {
                int moved = 0;
                foreach (EquipEntry entry in pickCatalog.Entries)
                {
                    if (entry != null && entry.targetSlotId == srcSocket.slotId)
                    {
                        Undo.RecordObject(pickCatalog, "Relink Catalog Entry");
                        entry.targetSlotId = bakeSlotId;
                        moved = moved + 1;
                    }
                }
                if (moved > 0)
                {
                    EditorUtility.SetDirty(pickCatalog);
                    Debug.Log($"[SocketMaker] 카탈로그 링크 이관: '{srcSocket.slotId}' → '{bakeSlotId}' ({moved}개 엔트리). 원본 소켓 '{srcSocket.name}'은 남아 있으니 필요 없으면 삭제하세요.");
                }
            }

            // (b) 배치 기록: 소켓-로컬 값을 월드 경유로 새 소켓 기준으로 환산해 복사
            EquipPlacementRecord srcRec = srcSocket.GetComponent<EquipPlacementRecord>();
            if (srcRec != null)
            {
                EquipPlacementRecord dstRec = socket.GetComponent<EquipPlacementRecord>();
                if (dstRec == null)
                {
                    dstRec = Undo.AddComponent<EquipPlacementRecord>(socket.gameObject);
                }
                else
                {
                    Undo.RecordObject(dstRec, "Copy Placement Record");
                }

                Vector3 worldPos = srcSocket.transform.TransformPoint(srcRec.ghostLocalPosition);
                Quaternion worldRot = srcSocket.transform.rotation * Quaternion.Euler(srcRec.ghostLocalEuler);
                float worldScale = srcRec.ghostLocalScale * EquipAuthoringUtil.LossyAvg(srcSocket.transform);

                dstRec.accessoryKey = srcRec.accessoryKey;
                dstRec.sizeRatioAtPlacement = srcRec.sizeRatioAtPlacement;
                dstRec.ghostLocalPosition = socket.transform.InverseTransformPoint(worldPos);
                dstRec.ghostLocalEuler = (Quaternion.Inverse(socket.transform.rotation) * worldRot).eulerAngles;
                dstRec.ghostLocalScale = worldScale / EquipAuthoringUtil.LossyAvg(socket.transform);
            }

            // (c) 원본 소켓 정리 — 베이크는 "이사"이므로 소켓 2개가 남으면 안 된다.
            // 임시 이름(socket_*)은 자동 삭제, 의미 있는 이름은 확인 후 삭제.
            bool deleteSrc = srcSocket.slotId.StartsWith("socket_");
            if (deleteSrc == false)
            {
                deleteSrc = EditorUtility.DisplayDialog(
                    "원본 소켓 삭제",
                    $"'{srcSocket.slotId}' 소켓의 부착점·연결을 '{bakeSlotId}'로 옮겼습니다.\n원본 소켓을 삭제할까요?",
                    "삭제", "유지");
            }
            if (deleteSrc)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(srcSocket.gameObject))
                {
                    Debug.LogWarning($"[SocketMaker] 원본 소켓 '{srcSocket.name}'은 프리팹에 구워져 있어 여기서 삭제 불가 — 프리팹 모드에서 지우세요.");
                }
                else
                {
                    Debug.Log($"[SocketMaker] 원본 소켓 '{srcSocket.name}' 삭제 (이사 완료).");
                    Undo.DestroyObjectImmediate(srcSocket.gameObject);
                }
            }
        }

        Debug.Log($"[SocketMaker] 베이크 완료: '{bakeSlotId}' → 본 '{bakeBone.name}' 하단 소켓, 부착점 = '{srcName}'의 위치/회전 (소스 무변경).");
    }
}
