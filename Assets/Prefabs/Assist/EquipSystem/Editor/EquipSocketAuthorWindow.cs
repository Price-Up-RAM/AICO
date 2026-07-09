using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Socket Author 창: 표준 5슬롯(chest/back/head/overhead/origin)에 "본을 드래그해 넣고" 소켓을 생성한다.
// 본 이름 하드코딩 없음 — 본은 사용자가 지정하는 것이 최종이고, [본 자동 제안]은 채워주는 참고일 뿐이다.
// 여기서 만든 소켓은 스탬프 마커가 없는 '수동 저작'이라 이후 어떤 전파도 덮어쓰지 않는다(KEEP_MANUAL).
public class EquipSocketAuthorWindow : EditorWindow
{
    private GameObject target;  // 대상 캐릭터 루트 (씬 인스턴스/프리팹 스테이지)
    private readonly Dictionary<string, Transform> boneFields = new Dictionary<string, Transform>();  // slotId → 지정 본
    private readonly Dictionary<string, string> rowNotes = new Dictionary<string, string>();          // slotId → 상태/제안 라벨
    private Vector2 scroll;  // 스크롤

    // 슬롯별 기본 캡슐 크기 비율 (캐릭터 키 대비, 템플릿에 def가 없을 때)
    private static readonly Dictionary<string, float> DefaultRatios = new Dictionary<string, float>
    {
        { "head", 0.03f }, { "overhead", 0.06f }, { "chest", 0.05f }, { "back", 0.09f }, { "origin", 0.35f },
    };

    [MenuItem("Tools/EquipSystem/Socket Author")]
    public static void Open()
    {
        GetWindow<EquipSocketAuthorWindow>(false, "Socket Author", true);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "슬롯 옆 필드에 대상 캐릭터의 '본(Transform)'을 드래그해 넣고 [소켓 생성]을 누르세요.\n" +
            "[본 자동 제안]은 빈 필드를 사다리 해석으로 채워줍니다 — 최종 결정은 넣은 본입니다.\n" +
            "생성 후: 소켓 선택 → 라이브 미리보기로 위치/캡슐 조정 → (씬 인스턴스면) Overrides → Apply All.",
            MessageType.Info);

        // 대상
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
            EditorGUILayout.HelpBox("씬 인스턴스(또는 프리팹 스테이지의 루트)를 지정하세요.", MessageType.Warning);
            return;
        }

        if (target.transform.rotation != Quaternion.identity)
        {
            EditorGUILayout.HelpBox("루트 회전이 identity가 아닙니다 — 템플릿 캡처는 프리팹(Apply 후)에서 하는 것을 권장합니다.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("표준 슬롯 (본을 넣으세요)", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string slotId in EquipSlotTemplate.StandardSlotIds)
        {
            DrawSlotRow(slotId);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        // 실행 버튼들
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("본 자동 제안", GUILayout.Height(26)))
        {
            SuggestBones();
        }
        if (GUILayout.Button("소켓 생성/이동", GUILayout.Height(26)))
        {
            CreateSockets();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("현재 소켓 → 템플릿 캡처 (전파용 원본 갱신)"))
        {
            EquipSlotTemplate template = EquipAuthoringUtil.GetOrCreateDefaultTemplate();
            EquipSlotStamper.CaptureTemplate(target, template);
        }
    }

    // 슬롯 1행: 라벨 + 현재 소켓 상태 + 본 필드 (origin은 루트 자동)
    private void DrawSlotRow(string slotId)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(slotId, GUILayout.Width(70));

        // 현재 상태 (소켓 존재 여부 + 부모 본)
        EquipSocket existing = EquipAuthoringUtil.FindSocketBySlotId(target.transform, slotId);
        string state = "―";
        if (existing != null)
        {
            if (existing.transform.parent != null)
            {
                state = "✓ " + existing.transform.parent.name;
            }
            else
            {
                state = "✓";
            }
        }
        EditorGUILayout.LabelField(state, GUILayout.Width(150));

        if (slotId == "origin")
        {
            // origin은 항상 루트 부착
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(target.transform, typeof(Transform), true);
            }
        }
        else
        {
            Transform current = null;
            if (boneFields.ContainsKey(slotId))
            {
                current = boneFields[slotId];
            }
            boneFields[slotId] = (Transform)EditorGUILayout.ObjectField(current, typeof(Transform), true);
        }

        // 제안/결과 라벨
        string label = "";
        if (rowNotes.ContainsKey(slotId))
        {
            label = rowNotes[slotId];
        }
        EditorGUILayout.LabelField(label, GUILayout.Width(140));

        EditorGUILayout.EndHorizontal();
    }

    // 빈 본 필드를 사다리 해석(NAME→HUMANOID→ALIAS→NEAREST)으로 채움 — 참고용 제안
    private void SuggestBones()
    {
        Transform rootT = target.transform;
        float height = EquipAuthoringUtil.MeasureCharHeight(target);
        Bounds bounds;
        EquipAuthoringUtil.MeasureBounds(target, out bounds);
        HashSet<Transform> skinBones = EquipAuthoringUtil.CollectSkinBones(rootT);
        HashSet<Transform> physicsBones = EquipPhysicsBoneFilter.CollectPhysicsBones(rootT);

        EquipSlotTemplate template = EquipAuthoringUtil.GetOrCreateDefaultTemplate();

        foreach (string slotId in EquipSlotTemplate.StandardSlotIds)
        {
            if (slotId == "origin")
            {
                rowNotes[slotId] = "루트 부착";
                continue;
            }

            // 이미 본을 넣어놨으면 건드리지 않음
            if (boneFields.ContainsKey(slotId) && boneFields[slotId] != null)
            {
                continue;
            }

            // def: 템플릿에 있으면 그것, 없으면 기본 별칭/휴머노이드로 합성
            EquipSlotDef def = template.Find(slotId);
            if (def == null)
            {
                def = new EquipSlotDef();
                def.slotId = slotId;
                def.boneAliases = EquipSlotTemplate.DefaultAliases(slotId);
                def.humanoidBone = EquipSlotTemplate.DefaultHumanoidBone(slotId);
            }

            string method;
            string note;
            bool warning;
            Transform bone = EquipSlotStamper.ResolveBone(def, target, skinBones, physicsBones, bounds, height, out method, out note, out warning);

            if (bone != null)
            {
                boneFields[slotId] = bone;
                rowNotes[slotId] = "제안: " + method;
                if (warning)
                {
                    rowNotes[slotId] = rowNotes[slotId] + " ⚠";
                }
            }
            else
            {
                rowNotes[slotId] = "제안 실패 — 직접 드래그";
            }
        }

        Repaint();
    }

    // 본이 지정된 슬롯마다 소켓 생성/이동. 위치/캡슐은 템플릿 def(있으면) 또는 본 원점+기본 비율.
    private void CreateSockets()
    {
        Transform rootT = target.transform;
        Quaternion rootRot = rootT.rotation;
        float height = EquipAuthoringUtil.MeasureCharHeight(target);
        if (height <= 1e-6f)
        {
            Debug.LogError("[SocketAuthor] 캐릭터 키 측정 실패 — 중단.");
            return;
        }

        EquipSlotTemplate template = EquipAuthoringUtil.GetOrCreateDefaultTemplate();
        int made = 0;

        foreach (string slotId in EquipSlotTemplate.StandardSlotIds)
        {
            // 부착 본 결정 (origin=루트, 그 외=필드값. 비어 있으면 스킵)
            Transform bone = null;
            if (slotId == "origin")
            {
                bone = rootT;
            }
            else
            {
                if (boneFields.ContainsKey(slotId))
                {
                    bone = boneFields[slotId];
                }
            }

            if (bone == null)
            {
                continue;
            }

            // 지정 본이 대상 캐릭터 소속인지 검증
            if (bone.IsChildOf(rootT) == false)
            {
                rowNotes[slotId] = "본이 대상 밖 ✗";
                Debug.LogWarning($"[SocketAuthor] '{slotId}'에 넣은 본 '{bone.name}'이 대상 캐릭터의 하위가 아닙니다 — 스킵.");
                continue;
            }

            // 기존 소켓 재사용(이동) 또는 생성
            EquipSocket existing = EquipAuthoringUtil.FindSocketBySlotId(rootT, slotId);
            GameObject socketGo;
            bool created = false;

            if (existing != null)
            {
                socketGo = existing.gameObject;
                if (socketGo.transform.parent != bone)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(socketGo))
                    {
                        rowNotes[slotId] = "프리팹 소속 — 이동 불가";
                        Debug.LogWarning($"[SocketAuthor] '{slotId}' 소켓은 프리팹에 구워져 있어 씬에서 이동할 수 없습니다. 프리팹 모드에서 하세요.");
                        continue;
                    }
                    socketGo.transform.SetParent(bone, false);
                }
            }
            else
            {
                socketGo = new GameObject("Socket_" + slotId);
                Undo.RegisterCreatedObjectUndo(socketGo, "Create EquipSocket");
                socketGo.transform.SetParent(bone, false);
                created = true;
            }

            // 배치: 템플릿 def가 있으면 그 오프셋/비율(루트 프레임), 없으면 본 원점 + 기본 비율
            EquipSlotDef def = template.Find(slotId);
            float ratio = DefaultRatios[slotId];
            if (def != null && def.capsuleHeightRatio > 1e-9f)
            {
                ratio = def.capsuleHeightRatio;
            }

            if (created || socketGo.transform.parent == bone)
            {
                if (def != null)
                {
                    socketGo.transform.position = bone.position + rootRot * (def.rootDirFromBone * height);
                    socketGo.transform.rotation = rootRot * Quaternion.Euler(def.rootFrameEuler);
                }
                else
                {
                    socketGo.transform.localPosition = Vector3.zero;
                    socketGo.transform.rotation = rootRot;
                }
                socketGo.transform.localScale = Vector3.one;
            }

            int direction = 1;
            if (def != null)
            {
                direction = def.capsuleDirection;
            }
            EquipAuthoringUtil.SetCapsuleByWorldLength(socketGo, ratio * height, direction);

            EquipSocket socket = socketGo.GetComponent<EquipSocket>();
            if (socket == null)
            {
                socket = socketGo.AddComponent<EquipSocket>();
            }
            socket.slotId = slotId;
            if (created)
            {
                socket.fit = EquipFitMode.ContainUniform;
                socket.pivot = EquipAnchorPivot.VolumeCenter;
            }

            rowNotes[slotId] = "생성됨 → " + bone.name;
            made = made + 1;
        }

        // 씬/프리팹 스테이지 dirty 처리
        UnityEditor.SceneManagement.PrefabStage stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            EditorSceneManager.MarkSceneDirty(stage.scene);
        }
        else
        {
            EditorSceneManager.MarkSceneDirty(target.scene);
        }

        Debug.Log($"[SocketAuthor] 소켓 생성/갱신 {made}건. 라이브 미리보기로 조정 후 Apply(씬 인스턴스) 또는 저장(프리팹 모드) 하세요.");
        Repaint();
    }
}
