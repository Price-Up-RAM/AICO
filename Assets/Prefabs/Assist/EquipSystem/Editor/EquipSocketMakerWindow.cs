using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Socket Maker: "클릭 = 소켓". 커서 레이가 메시에 처음 맞는 곳이 소켓 자리다.
// 악세서리 고스트로 결과를 미리 보며 클릭 → socket_N 생성(지배 본 자동, 크기 기준 refDist 베이크).
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
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("[+ 소켓] → 씬에서 캐릭터 표면 클릭 = 그 자리에 소켓.\n악세서리를 골라두면 고스트가 커서를 따라다니며 결과를 미리 보여줍니다.", MessageType.Info);

        // 대상 (자동 인식: 프리팹 스테이지 루트 → 씬의 유일 스킨 캐릭터)
        EditorGUILayout.BeginHorizontal();
        target = (GameObject)EditorGUILayout.ObjectField("대상 캐릭터", target, typeof(GameObject), true);
        if (GUILayout.Button("선택에서", GUILayout.Width(64)))
        {
            if (Selection.activeGameObject != null)
            {
                target = Selection.activeGameObject.transform.root.gameObject;
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
        EditorGUILayout.LabelField("악세서리 (고스트 미리보기)", EditorStyles.boldLabel);
        LoadPickCatalog();
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

        EditorGUILayout.Space();

        // 핵심 버튼: 클릭 = 소켓
        Color prevBg = GUI.backgroundColor;
        if (pickActive)
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.4f);
            if (GUILayout.Button($"클릭 대기 중… '{pickSlotId}' (Esc 또는 여기 클릭으로 취소)", GUILayout.Height(34)))
            {
                StopPick();
            }
        }
        else
        {
            GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            if (GUILayout.Button("[+ 소켓] — 클릭한 자리에 socket_N 생성 (이름은 나중에)", GUILayout.Height(34)))
            {
                StartPick();
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

            // 픽 모드 중 베이크 금지: pickActive 상태로 CreateSocketAtHit가 돌면
            // ghostLift 위치·현재 선택 악세서리 링크·고스트 Record가 베이크 결과를 3중 오염시킨다
            if (pickActive)
            {
                EditorGUILayout.HelpBox("픽 모드 중에는 베이크할 수 없습니다 — 클릭 배치를 끝내거나 Esc로 취소한 뒤 실행하세요.", MessageType.Info);
            }
            bool canBake = pickActive == false && bakeSource != null && bakeBone != null && string.IsNullOrEmpty(bakeSlotId) == false;
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

                Color prevRow = GUI.color;
                GUI.color = rowColor;
                EditorGUILayout.LabelField(entry.key, state);
                GUI.color = prevRow;
            }
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

    // ── 픽 모드 (클릭 = 소켓) ──
    private bool pickActive;         // 픽 모드 활성
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

        pickActive = true;
        pickSlotId = NextAutoSlotId();
        hoverDiagCount = 0;
        pickMouseMoved = false;
        lastHoverSuccess = false;
        ghostDiagLogged = false;
        ghostLift = 0f;  // 새 픽 세션은 표면에서 시작 (회전은 세션 간 유지)
        lastGhostRotValid = false;  // 이전 픽 세션의 회전이 새 소켓에 베이크되는 것 방지

        SceneView.duringSceneGui += OnPickSceneGUI;
        EditorApplication.update += OnPickUpdate;

        SceneView sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.Focus();
            // 초기값 = 뷰포트 중앙 (sv.position은 탭+툴바 포함 rect라 높이에서 툴바만큼 보정)
            pickMousePos = new Vector2(sv.position.width * 0.5f, (sv.position.height - 21f) * 0.5f);
        }
        SceneView.RepaintAll();
    }

    private void StopPick()
    {
        if (pickActive)
        {
            SceneView.duringSceneGui -= OnPickSceneGUI;
            EditorApplication.update -= OnPickUpdate;
            pickActive = false;
            SceneView.RepaintAll();
        }
        DestroyGhost();
        Repaint();
    }

    // 픽 모드 중 씬 뷰 상시 리페인트 (~30fps)
    private void OnPickUpdate()
    {
        if (EditorApplication.timeSinceStartup < nextPickRepaint)
        {
            return;
        }
        nextPickRepaint = EditorApplication.timeSinceStartup + 0.033;
        SceneView.RepaintAll();
    }

    // 씬 처리: 고스트 호버 + 클릭 배치
    private void OnPickSceneGUI(SceneView sceneView)
    {
        if (pickActive == false || target == null)
        {
            StopPick();
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

        // 고스트 조작: 휠 = 거리(표면에서 띄우기), Ctrl+휠 = 법선 축 회전, Shift+휠 = 기울임, R = 리셋
        if (e.type == EventType.ScrollWheel)
        {
            float sign = 1f;
            if (e.delta.y > 0f)
            {
                sign = -1f;
            }

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
            e.Use();
            sceneView.Repaint();
        }
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            ghostYaw = 0f;
            ghostTilt = 0f;
            ghostLift = 0f;
            e.Use();
            sceneView.Repaint();
        }

        // 안내 배지
        string rotInfo = "";
        if (ghostYaw != 0f || ghostTilt != 0f)
        {
            rotInfo = $"  회전 {ghostYaw:F0}°/{ghostTilt:F0}°";
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
        GUILayout.BeginArea(new Rect(10, 10, 480, 24));
        GUILayout.Label($"클릭 = 소켓 [{pickSlotId}] — 클릭=배치, 휠=거리, Ctrl/Shift+휠=회전, Esc=취소{rotInfo}", EditorStyles.helpBox);
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
                    CreateSocketAtHit(pickSlotId, bone, hit, charRoot);
                    e.Use();
                    StopPick();
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
    private void CreateSocketAtHit(string slotId, Transform bone, EquipMeshHit hit, Transform charRoot)
    {
        EquipSocket existing = EquipAuthoringUtil.FindSocketBySlotId(charRoot, slotId);
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
                    return;
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
        socket.slotId = slotId;
        // 신모델 소켓에는 레거시 필드(fit/pivot)를 더 이상 명시 기록하지 않는다
        // (클래스 기본값 그대로 — P4 캡슐 철거 때 마이그레이션 대상이 늘지 않게)

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

        // 부착점 = 히트점 + 거리 띄우기 (고스트로 보던 그대로 — 픽 모드에서만, 베이크 경로는 소스 위치 그대로)
        Vector3 phPoint = hit.point;
        if (pickActive && ghostLift > 0f)
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
        ph.CaptureFromTransform();

        // 장착 정보 저장: 선택한 악세서리의 카탈로그 엔트리를 이 소켓에 연결 (WYSIWYG 배치 → 바로 장착 가능)
        EquipEntry linkEntry = GetPickEntry();
        if (pickActive && linkCatalogOnPlace && linkEntry != null)
        {
            // 의미 있는 이름의 기존 연결을 덮기 전 확인 — 다른 캐릭터를 향한 링크를 조용히 끊는 사고 방지
            // (socket_N 임시 이름이면 확인 없이 덮음)
            bool doLink = true;
            if (string.IsNullOrEmpty(linkEntry.targetSlotId) == false
                && linkEntry.targetSlotId != slotId
                && linkEntry.targetSlotId.StartsWith("socket_") == false)
            {
                doLink = EditorUtility.DisplayDialog(
                    "카탈로그 연결 덮어쓰기",
                    $"'{linkEntry.key}'는 이미 '{linkEntry.targetSlotId}'에 연결되어 있습니다.\n'{slotId}'로 바꾸면 기존 연결(다른 캐릭터 포함)이 끊깁니다.",
                    "덮어쓰기", "기존 연결 유지");
            }

            if (doLink)
            {
                Undo.RecordObject(pickCatalog, "Link Catalog Entry");
                linkEntry.targetSlotId = slotId;
                linkEntry.fitMode = EquipEntryFit.RadiusRelative;
                EditorUtility.SetDirty(pickCatalog);
                Debug.Log($"[SocketMaker] 카탈로그 연결: '{linkEntry.key}' → '{slotId}'. (소켓 slotId를 리네임하면 카탈로그의 targetSlotId도 같이 바꿔야 합니다!)");
            }
        }

        // 배치 기록: 이 소켓을 만들 때의 고스트 결과(악세서리 key + 소켓-로컬 TRS)를 소켓에 남긴다
        // — 이후 재현·전파 검수·미세조정 시작값으로 활용 (카탈로그 연결 토글과 무관하게 기록)
        if (pickActive && linkEntry != null && pickGhost != null && lastGhostRotValid)
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
            record.ghostLocalPosition = socketGo.transform.InverseTransformPoint(pickGhost.transform.position);
            record.ghostLocalEuler = (Quaternion.Inverse(socketGo.transform.rotation) * pickGhost.transform.rotation).eulerAngles;
            record.ghostLocalScale = pickGhost.transform.localScale.x / EquipAuthoringUtil.LossyAvg(socketGo.transform);
        }

        // 베이크 폴드아웃 자동 매핑: 방금 만든 부착점(placeholder)을 위치 소스로 —
        // 폴드아웃에서 본/이름만 바꿔 곧바로 "다른 본으로 다시 굽기"가 가능해진다
        bakeSource = ph.gameObject;
        bakeBone = bone;
        bakeSlotId = slotId;

        MarkTargetDirty();

        // 소켓을 선택해 리네임 유도 (인스펙터에 미리네임 경고 배지가 뜸)
        Selection.activeGameObject = socketGo;
        Debug.Log($"[SocketMaker] '{slotId}' 생성 → 본 '{bone.name}' (refDist≈{hitDist:F2} 월드). 인스펙터에서 slotId에 의미 있는 이름을 지어주세요.");
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
    private float ghostYaw;               // 호버 중 회전: 법선 축 (Ctrl+휠)
    private float ghostTilt;              // 호버 중 회전: 기울임 (Shift+휠)
    private float ghostLift;              // 호버 중 거리: 표면 노멀 방향 띄우기, hitDist 배수 (맨휠, 0=표면)
    private Quaternion lastGhostRot;      // 마지막 고스트 회전 (배치 시 베이크)
    private bool lastGhostRotValid;       // 위 값 유효 여부
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

        // 회전 = 표면 기준(base) × 사용자 회전 (Ctrl+휠=법선 축, Shift+휠=기울임).
        // placeholder에는 spunBase만 베이크 — 장착 시 entry.rotationOffset이 다시 곱해지므로(FitToPlaceholder),
        // 고스트 "표시"에는 entry.rotationOffset까지 합성해야 실장착과 동일하게 보인다 (WYSIWYG).
        Quaternion baseRot = Quaternion.LookRotation(tangent.normalized, hit.normal);
        Quaternion spunBase = Quaternion.AngleAxis(ghostYaw, hit.normal) * Quaternion.AngleAxis(ghostTilt, baseRot * Vector3.right) * baseRot;
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
    private string bakeSlotId = "socket_bake";

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

        CreateSocketAtHit(bakeSlotId, bakeBone, fakeHit, charRoot);

        // 회전은 소스 그대로 덮어쓰기
        EquipSocket socket = EquipAuthoringUtil.FindSocketBySlotId(charRoot, bakeSlotId);
        if (socket != null)
        {
            EquipPlaceholder ph = socket.FindPlaceholder("placeholder");
            if (ph != null)
            {
                ph.transform.rotation = srcRot;
                ph.CaptureFromTransform();
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
        }

        Debug.Log($"[SocketMaker] 베이크 완료: '{bakeSlotId}' → 본 '{bakeBone.name}' 하단 소켓, 부착점 = '{srcName}'의 위치/회전 (소스 무변경).");
    }
}
