using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 소켓 전파 창: Donor 모드(같은 캐릭터 의상 전파) / Template 모드(크로스 캐릭터 스탬프).
// 드라이런이 기본값 — 리포트 확인 후 실제 적용. 각 행 [열기]로 대상 프리팹을 바로 열어 검수.
public class EquipPropagationWindow : EditorWindow
{
    private enum Mode
    {
        Donor,     // 같은 스켈레톤 의상 전파 (본 이름 일치 + 로컬 값 무손실 복사)
        Template,  // 크로스 캐릭터 스탬프 (본 해석 사다리)
    }

    private Mode mode = Mode.Donor;                     // 현재 모드
    private GameObject donorPrefab;                     // Donor 모드: 소스 프리팹
    private EquipSlotTemplate template;                 // Template 모드: 원본 템플릿
    private readonly List<GameObject> targets = new List<GameObject>();  // 대상 프리팹들
    private bool dryRun = true;                         // 드라이런 (기본 ON)
    private List<EquipStampEntry> report;               // 마지막 실행 리포트
    private Vector2 targetScroll;                       // 대상 목록 스크롤
    private Vector2 reportScroll;                       // 리포트 스크롤

    [MenuItem("Tools/EquipSystem/Propagation Window")]
    public static void Open()
    {
        GetWindow<EquipPropagationWindow>(false, "Equip Propagation", true);
    }

    // 열릴 때 기본 템플릿 자동 연결
    private void OnEnable()
    {
        if (template == null)
        {
            template = EquipAuthoringUtil.GetOrCreateDefaultTemplate();
        }
    }

    private void OnGUI()
    {
        // 모드 선택
        mode = (Mode)GUILayout.Toolbar((int)mode, new string[] { "Donor (의상 전파)", "Template (새 캐릭터)" });
        EditorGUILayout.Space();

        if (mode == Mode.Donor)
        {
            EditorGUILayout.HelpBox("같은 스켈레톤(의상 프리팹들)에 소켓을 무손실 복사합니다. 본 이름이 일치해야 합니다.", MessageType.Info);
            donorPrefab = (GameObject)EditorGUILayout.ObjectField("Donor Prefab", donorPrefab, typeof(GameObject), false);
        }
        else
        {
            EditorGUILayout.HelpBox("다른 캐릭터에 템플릿을 스탬프합니다. 본 해석: 정확이름 → Humanoid → 별칭 → 최근접(물리 본 제외).", MessageType.Info);
            template = (EquipSlotTemplate)EditorGUILayout.ObjectField("Slot Template", template, typeof(EquipSlotTemplate), false);
        }

        EditorGUILayout.Space();

        // 대상 목록
        EditorGUILayout.LabelField($"Targets ({targets.Count})", EditorStyles.boldLabel);
        targetScroll = EditorGUILayout.BeginScrollView(targetScroll, GUILayout.MaxHeight(140));
        int removeAt = -1;
        for (int i = 0; i < targets.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            targets[i] = (GameObject)EditorGUILayout.ObjectField(targets[i], typeof(GameObject), false);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                removeAt = i;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (removeAt >= 0)
        {
            targets.RemoveAt(removeAt);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add"))
        {
            targets.Add(null);
        }
        if (GUILayout.Button("Add Selected (Project)"))
        {
            AddSelectedPrefabs();
        }
        if (GUILayout.Button("Clear"))
        {
            targets.Clear();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 실행
        dryRun = EditorGUILayout.ToggleLeft("드라이런 (수정 없이 리포트만 — 확인 후 해제하고 실제 적용)", dryRun);

        bool canRun = targets.Count > 0;
        if (mode == Mode.Donor && donorPrefab == null)
        {
            canRun = false;
        }
        if (mode == Mode.Template && template == null)
        {
            canRun = false;
        }

        using (new EditorGUI.DisabledScope(canRun == false))
        {
            string label;
            if (dryRun)
            {
                label = "Run (Dry-Run)";
            }
            else
            {
                label = "Stamp (실제 적용)";
            }

            if (GUILayout.Button(label, GUILayout.Height(30)))
            {
                Run();
            }
        }

        // 리포트
        if (report != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Report ({report.Count})", EditorStyles.boldLabel);
            reportScroll = EditorGUILayout.BeginScrollView(reportScroll);

            foreach (EquipStampEntry e in report)
            {
                EditorGUILayout.BeginHorizontal();

                // 상태 색상 (경고=노랑, 실패=빨강, 정상=기본)
                Color prev = GUI.color;
                if (e.status != "OK" && e.status != "SELF" && e.status != "KEEP_TUNED" && e.status != "KEEP_MANUAL")
                {
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                }
                else
                {
                    if (e.isWarning)
                    {
                        GUI.color = new Color(1f, 0.9f, 0.4f);
                    }
                }

                string fileName = System.IO.Path.GetFileNameWithoutExtension(e.prefabPath);
                string line = $"{fileName}  [{e.slotId}]  {e.status}";
                if (string.IsNullOrEmpty(e.method) == false)
                {
                    line = line + $"  ({e.method}→{e.boneName})";
                }
                if (string.IsNullOrEmpty(e.note) == false)
                {
                    line = line + $"  · {e.note}";
                }
                EditorGUILayout.LabelField(line);

                GUI.color = prev;

                if (GUILayout.Button("열기", GUILayout.Width(44)))
                {
                    OpenPrefab(e.prefabPath);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }

    // Project 선택에서 프리팹만 대상 목록에 추가 (중복 제외)
    private void AddSelectedPrefabs()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null)
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (targets.Contains(go) == false)
            {
                targets.Add(go);
            }
        }
    }

    // 실행 (드라이런이면 apply=false)
    private void Run()
    {
        List<GameObject> valid = new List<GameObject>();
        foreach (GameObject t in targets)
        {
            if (t != null)
            {
                valid.Add(t);
            }
        }

        if (mode == Mode.Donor)
        {
            report = EquipSlotStamper.RunDonorBatch(donorPrefab, valid, dryRun == false);
        }
        else
        {
            report = EquipSlotStamper.RunTemplateBatch(template, valid, dryRun == false);
        }

        // 콘솔 요약
        int ok = 0;
        int warn = 0;
        int fail = 0;
        foreach (EquipStampEntry e in report)
        {
            if (e.status == "OK")
            {
                ok = ok + 1;
                if (e.isWarning)
                {
                    warn = warn + 1;
                }
            }
            else
            {
                if (e.status != "SELF" && e.status != "KEEP_TUNED" && e.status != "KEEP_MANUAL")
                {
                    fail = fail + 1;
                }
            }
        }

        string runMode;
        if (dryRun)
        {
            runMode = "드라이런";
        }
        else
        {
            runMode = "적용";
        }
        Debug.Log($"[EquipPropagation] {runMode} 완료 — OK {ok} (경고 {warn}), 실패/스킵 {fail}. 창의 리포트에서 [열기]로 검수하세요.");
    }

    // 프리팹 열기 (프리팹 모드)
    private void OpenPrefab(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            AssetDatabase.OpenAsset(prefab);
        }
    }
}
