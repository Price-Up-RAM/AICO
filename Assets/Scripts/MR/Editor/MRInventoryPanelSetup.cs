// 인벤토리 패널을 MR 씬에 배치하고 UIManager에 재배선하는 원스톱 도구.
//
// 왜 필요한가
// ----------
// UIManager.inventoryPanel이 **프리팹 에셋**으로 배선돼 있으면 ResolveManagedUI가
// CanvasManager.canvasUI(메인 Canvas, 월드 1920 m) 아래로 인스턴스화한다 —
// 월드 캔버스를 캔버스 안에 넣는 §4-18이라 버튼도 텍스트도 깨진다.
// 기존 패널 9개가 전부 씬에 미리 배치돼 있는 이유가 이것이다.
//
// 왜 Tools → MR → 9를 그대로 쓰지 않는가 (§4-43의 반대 방향)
// --------------------------------------------------------
// MRPanelFullConvert는 "6번이 바꾼 스케일을 6번 실행 전 값으로 되돌린다".
// 이미 손으로 크기를 맞춰둔 기존 패널에는 맞는 동작이지만, **갓 인스턴스화한 프리팹은
// 되돌릴 원본이 1**이라 600×512 px 패널이 600 m가 된다.
// 그래서 6 → 스케일 **명시 지정** → 5 → 8 순으로 직접 조립한다.
// 순서가 중요하다 — 5번과 8번이 상호작용 면과 잡기 띠를 현재 스케일 기준으로 계산한다.
//
// 창은 1개다 (2026-08-26 결정)
// --------------------------
// 원래 구조는 상점 / 메인(공용) 인벤토리 / 캐릭터 인벤토리 3단이었다. 공용 창고는
// 캐릭터가 여럿일 때 아이템을 옮겨 담으려던 것인데 이 프로젝트는 캐릭터 1명으로 확정됐고,
// 카탈로그 32엔트리 중 isMainOnly가 하나도 없어 공용에만 있어야 할 아이템도 없다.
// → 캐릭터 인벤토리 한 장만 배치한다. UIManager가 InventorySection.Char로 고정한다.
// 이전 버전이 만든 InventoryPanelChar가 남아 있으면 이 도구가 지운다(Ctrl+Z로 복원 가능).
//
// 크기: 0.0007 → 600×512 px가 0.42 × 0.36 m.
// 기존 패널 실측 범위(0.0005~0.0015)의 중간값이고 JukeboxView(0.42×0.50)와 같은 급이다.
//
// 몇 번 실행해도 안전하다 — 이미 있으면 새로 만들지 않고 배선만 다시 맞춘다 (§7-1 A).
//
// 사용: Tools → MR → 인벤토리 패널 배치

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AICO.MR.EditorTools
{
    public static class MRInventoryPanelSetup
    {
        private const string MenuRoot = "Tools/MR/";
        private const string PanelsParentName = "Panels";
        private const string PanelName = "InventoryPanel";
        private const string LegacyCharName = "InventoryPanelChar";   // 이전 버전이 만든 두 번째 창
        private const string PrefabPath = "Assets/Prefabs/Assist/InventorySystem/InventoryPanel.prefab";

        private const float PanelScale = 0.0007f;        // 600×512 px → 0.42 × 0.36 m
        private const float LateralOffset = -0.24f;      // 소환 시 왼쪽으로. 상점은 +0.24

        [MenuItem(MenuRoot + "인벤토리 패널 배치 (씬 배치 → 변환 → UIManager 재배선)", false, 120)]
        public static void SetupInventoryPanels()
        {
            var log = new StringBuilder("[MRInventoryPanelSetup] 인벤토리 패널 배치\n");

            Transform parent = FindPanelsParent();
            if (parent == null)
            {
                EditorUtility.DisplayDialog("Panels 없음",
                    "씬에서 MR/WorldUI/Panels 를 찾지 못했습니다.\n" +
                    "MR > WorldUI > Panels 계층이 있는지 확인하세요.", "확인");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("프리팹 없음",
                    $"{PrefabPath} 를 찾지 못했습니다.", "확인");
                return;
            }

            log.AppendLine($"  부모: {GetPath(parent)} (localScale {parent.localScale.x})");

            // ---- 1. 이전 버전이 만든 두 번째 창 정리 ----
            RemoveLegacyCharPanel(parent, log);

            // ---- 2. 인스턴스 확보 (있으면 재사용) ----
            GameObject panel = EnsureInstance(parent, prefab, PanelName, log);
            if (panel == null)
            {
                Debug.LogError(log.ToString());
                return;
            }

            // ---- 3. 변환: 6 → 스케일 지정 → 5 → 8 ----
            Object[] previousSelection = Selection.objects;
            var one = new GameObject[] { panel };

            // 5번이 자식 world scale을 읽어 역수를 계산하므로 켜둔 상태로 돌린다. 마지막에 다시 끈다.
            bool wasActive = panel.activeSelf;
            panel.SetActive(true);

            Selection.objects = one;

            log.AppendLine("\n  ── Tools 6 (플로팅 패널 변환) ──");
            MRFloatingPanelSetup.ConvertSelectionToFloatingPanel();

            // 6번은 스케일을 레시피 0.001로 덮어쓴다. 9번처럼 "원래 값 복원"을 하면
            // 새 인스턴스는 1로 돌아가 600 m가 되므로, 여기서 확정값을 명시 지정한다.
            ApplyScale(panel, log);

            log.AppendLine("  ── Tools 5 (손 상호작용) ──");
            Selection.objects = one;
            MRWorldUIInteraction.AddInteractionToSelectedCanvas();

            log.AppendLine("  ── Tools 8 (잡기) ──");
            Selection.objects = one;
            MRPanelGrabSetup.AddGrabToSelectedPanels();

            Selection.objects = previousSelection;

            // ---- 4. 배치: 열 때마다 사용자 눈앞 ----
            ConfigurePanel(panel, log);

            // ---- 5. 시작 시에는 닫혀 있어야 한다 (기존 패널 9개와 동일) ----
            panel.SetActive(false);
            log.AppendLine($"\n  · 시작 상태 비활성으로 저장 (이전: {wasActive})");

            // ---- 6. UIManager 재배선 ----
            RewireUIManager(panel, log);

            // ---- 7. 실측 리포트 — 파싱한 값이 아니라 실제 rect로 계산한다 ----
            ReportSize(panel, log);

            EditorUtility.SetDirty(panel);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            log.AppendLine("\n완료. 씬을 저장한 뒤 Play → 시스템 메뉴 → 열기 → 인벤토리 로 확인하세요.");
            Debug.Log(log.ToString());
        }

        // 같은 이름의 씬 오브젝트가 있으면 재사용, 없으면 프리팹 인스턴스 생성
        private static GameObject EnsureInstance(Transform parent, GameObject prefab, string name, StringBuilder log)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                log.AppendLine($"\n  ● {name}: 이미 있음 — 새로 만들지 않고 배선만 갱신한다");
                return existing.gameObject;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                log.AppendLine($"\n  ❌ {name}: 프리팹 인스턴스화 실패");
                return null;
            }

            instance.name = name;
            Undo.RegisterCreatedObjectUndo(instance, "Create Inventory Panel");

            RectTransform rt = instance.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition3D = Vector3.zero;
                rt.localRotation = Quaternion.identity;
            }

            log.AppendLine($"\n  ● {name}: 새로 생성 (프리팹 인스턴스)");
            return instance;
        }

        // 확정 스케일 적용
        private static void ApplyScale(GameObject panel, StringBuilder log)
        {
            Transform t = panel.transform;
            Undo.RecordObject(t, "Set inventory panel scale");

            float before = t.localScale.x;
            t.localScale = Vector3.one * PanelScale;

            log.AppendLine($"      · {panel.name} 스케일 {before} → {PanelScale} (9번의 '원본 복원' 대신 확정값 지정)");
        }

        // 열릴 때마다 사용자 눈앞에 놓는다
        private static void ConfigurePanel(GameObject panel, StringBuilder log)
        {
            MRFloatingPanel floating = panel.GetComponent<MRFloatingPanel>();
            if (floating == null)
            {
                log.AppendLine("      ⚠ MRFloatingPanel이 없다 — 6번이 실패했을 수 있다");
                return;
            }

            var so = new SerializedObject(floating);
            SerializedProperty behavior = so.FindProperty("spawnBehavior");
            if (behavior != null)
            {
                behavior.enumValueIndex = (int)MRFloatingPanel.SpawnBehavior.InFrontOfUser;
            }

            // 상점과 나란히 뜨도록 왼쪽으로 민다. 소환 시점에만 적용되고,
            // 사용자가 잡아 옮기면 그 자리에 남는다 (§4-27). 런타임 결합은 없다.
            SerializedProperty lateral = so.FindProperty("spawnLateralOffset");
            if (lateral != null)
            {
                lateral.floatValue = LateralOffset;
            }
            so.ApplyModifiedProperties();
            log.AppendLine($"      · spawnBehavior = InFrontOfUser, 좌우 오프셋 = {LateralOffset} m (왼쪽)");
        }

        // 이전 버전이 만든 캐릭터 전용 창을 지운다.
        // 남겨두면 UIManager가 안 여는 유령 패널이 씬에 떠 있고, 잡기 판정만 살아 있어
        // "안 보이는 것이 손에 걸리는" 상태가 된다. Ctrl+Z 1회로 복원된다.
        private static void RemoveLegacyCharPanel(Transform parent, StringBuilder log)
        {
            Transform legacy = parent.Find(LegacyCharName);
            if (legacy == null)
            {
                return;
            }

            log.AppendLine($"  ● {LegacyCharName}: 창을 1개로 줄였으므로 제거한다 (Ctrl+Z로 복원 가능)");
            Undo.DestroyObjectImmediate(legacy.gameObject);
        }

        // UIManager.inventoryPanel을 씬 오브젝트로 바꾼다
        private static void RewireUIManager(GameObject panel, StringBuilder log)
        {
            UIManager manager = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                log.AppendLine("\n  ❌ 씬에서 UIManager를 찾지 못했다 — 배선하지 못했다");
                return;
            }

            var so = new SerializedObject(manager);
            SerializedProperty prop = so.FindProperty("inventoryPanel");

            log.AppendLine("\n  ── UIManager 재배선 ──");
            log.AppendLine($"      · inventoryPanel  {Describe(prop)} → {panel.name} (씬)");

            if (prop != null)
            {
                prop.objectReferenceValue = panel;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
        }

        // 최종 물리 크기 — 정적 파싱값이 아니라 실제 RectTransform.rect × lossyScale (§7-1 B)
        private static void ReportSize(GameObject panel, StringBuilder log)
        {
            RectTransform rt = panel.transform as RectTransform;
            if (rt == null)
            {
                return;
            }

            Vector3 lossy = rt.lossyScale;
            float w = rt.rect.width * lossy.x;
            float h = rt.rect.height * lossy.y;

            log.AppendLine($"  · {panel.name} 실측 {rt.rect.width:F0}×{rt.rect.height:F0} px " +
                           $"× lossyScale {lossy.x:F5} → {w:F2} × {h:F2} m");
        }

        private static string Describe(SerializedProperty prop)
        {
            if (prop == null || prop.objectReferenceValue == null)
            {
                return "(비어 있음)";
            }

            GameObject go = prop.objectReferenceValue as GameObject;
            if (go != null && go.scene.IsValid() == false)
            {
                return $"{go.name} (프리팹 에셋)";
            }

            return prop.objectReferenceValue.name;
        }

        // MR/WorldUI/Panels 를 찾는다 (비활성 포함)
        private static Transform FindPanelsParent()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != PanelsParentName)
                {
                    continue;
                }

                if (t.parent != null && t.parent.name == "WorldUI")
                {
                    return t;
                }
            }
            return null;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            Transform cur = t.parent;
            while (cur != null)
            {
                path = cur.name + "/" + path;
                cur = cur.parent;
            }
            return path;
        }
    }
}
