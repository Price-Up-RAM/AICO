// 상점 패널을 MR 씬에 배치하는 도구.
//
// 왜 씬에 놓아야 하는가
// -------------------
// `KAIManager.ToggleStoreInternal()`은 원래 `storePanelPrefab`을
// `CanvasManager.canvasUI`(메인 Canvas, 월드 1920 m) 아래로 인스턴스화했다.
// 월드 스페이스 패널을 캔버스 안에 넣는 §4-18이라 버튼도 텍스트도 깨진다(§4-36도 같이).
// 2026-08-26에 `ToggleStore`가 **씬의 StorePanel을 먼저 찾도록** 고쳤고,
// 이 도구가 그 씬 오브젝트를 만든다. 인스펙터 배선은 필요 없다 —
// `Resources.FindObjectsOfTypeAll<StoreView>()` + `scene.IsValid()`로 찾는다.
//
// 왜 Tools → MR → 9를 쓰지 않는가
// -----------------------------
// `MRPanelFullConvert`는 "6번이 바꾼 스케일을 6번 실행 **전** 값으로 되돌린다".
// 갓 인스턴스화한 프리팹은 그 값이 1이라 520×560 px 패널이 520 m가 된다(§4-43의 반대 방향).
// 그래서 6 → 스케일 **명시 지정** → 5 → 8 순으로 직접 조립한다.
// 순서가 중요하다 — 5번과 8번이 상호작용 면과 잡기 띠를 현재 스케일 기준으로 계산한다.
//
// 크기: 0.0007 → 520×560 px가 0.36 × 0.39 m. 인벤토리 패널(0.42 × 0.36)과 나란한 급이다.
//
// 몇 번 실행해도 안전하다 — 이미 있으면 새로 만들지 않고 설정만 다시 맞춘다 (§7-1 A).
//
// 사용: Tools → MR → 상점 패널 배치

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AICO.MR.EditorTools
{
    public static class MRStorePanelSetup
    {
        private const string MenuRoot = "Tools/MR/";
        private const string PanelsParentName = "Panels";
        private const string PanelName = "StorePanel";
        private const string PrefabPath = "Assets/Prefabs/UI/Store/Prefabs/StorePanel.prefab";

        private const float PanelScale = 0.0007f;      // 520×560 px → 0.36 × 0.39 m
        private const float LateralOffset = 0.24f;     // 소환 시 오른쪽으로. 인벤토리는 -0.24
        private const string InventoryPanelName = "InventoryPanel";

        [MenuItem(MenuRoot + "상점 패널 배치 (씬 배치 → 변환)", false, 121)]
        public static void SetupStorePanel()
        {
            var log = new StringBuilder("[MRStorePanelSetup] 상점 패널 배치\n");

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
                EditorUtility.DisplayDialog("프리팹 없음", $"{PrefabPath} 를 찾지 못했습니다.", "확인");
                return;
            }

            log.AppendLine($"  부모: {GetPath(parent)} (localScale {parent.localScale.x})");

            GameObject panel = EnsureInstance(parent, prefab, PanelName, log);
            if (panel == null)
            {
                Debug.LogError(log.ToString());
                return;
            }

            Object[] previousSelection = Selection.objects;
            var one = new GameObject[] { panel };

            // 5번이 자식 world scale을 읽어 역수를 계산하므로 켜둔 상태로 돌린다. 마지막에 다시 끈다.
            bool wasActive = panel.activeSelf;
            panel.SetActive(true);

            Selection.objects = one;
            log.AppendLine("\n  ── Tools 6 (플로팅 패널 변환) ──");
            MRFloatingPanelSetup.ConvertSelectionToFloatingPanel();

            ApplyScale(panel, log);

            log.AppendLine("  ── Tools 5 (손 상호작용) ──");
            Selection.objects = one;
            MRWorldUIInteraction.AddInteractionToSelectedCanvas();

            log.AppendLine("  ── Tools 8 (잡기) ──");
            Selection.objects = one;
            MRPanelGrabSetup.AddGrabToSelectedPanels();

            Selection.objects = previousSelection;

            ConfigurePanel(panel, log);
            SeparateFromInventory(panel, log);

            // 시작 시에는 닫혀 있어야 한다 (기존 패널들과 동일)
            panel.SetActive(false);
            log.AppendLine($"\n  · 시작 상태 비활성으로 저장 (이전: {wasActive})");

            ReportSize(panel, log);
            WarnIfStoreViewMissing(panel, log);

            EditorUtility.SetDirty(panel);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            log.AppendLine("\n완료. 씬을 저장한 뒤 Play → 시스템 메뉴 → 열기 → 인벤토리/상점 으로 확인하세요.");
            log.AppendLine("  상점과 인벤토리는 독립입니다 — 각각 잡아 옮기고 각각 닫습니다. 소환 시에만 좌우로 갈라 뜹니다.");
            Debug.Log(log.ToString());
        }

        // 같은 이름의 씬 오브젝트가 있으면 재사용, 없으면 프리팹 인스턴스 생성
        private static GameObject EnsureInstance(Transform parent, GameObject prefab, string name, StringBuilder log)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                log.AppendLine($"\n  ● {name}: 이미 있음 — 새로 만들지 않고 설정만 갱신한다");
                return existing.gameObject;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                log.AppendLine($"\n  ❌ {name}: 프리팹 인스턴스화 실패");
                return null;
            }

            instance.name = name;
            Undo.RegisterCreatedObjectUndo(instance, "Create Store Panel");

            RectTransform rt = instance.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition3D = Vector3.zero;
                rt.localRotation = Quaternion.identity;
            }

            log.AppendLine($"\n  ● {name}: 새로 생성 (프리팹 인스턴스)");
            return instance;
        }

        // 확정 스케일 적용 — 9번의 "원본 복원" 대신 값을 못박는다
        private static void ApplyScale(GameObject panel, StringBuilder log)
        {
            Transform t = panel.transform;
            Undo.RecordObject(t, "Set store panel scale");

            float before = t.localScale.x;
            t.localScale = Vector3.one * PanelScale;

            log.AppendLine($"      · 스케일 {before} → {PanelScale} (9번의 '원본 복원' 대신 확정값 지정)");
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
                log.AppendLine("      · spawnBehavior = InFrontOfUser");
            }
            so.ApplyModifiedProperties();
        }

        // 인벤토리와 **독립**으로 둔다. 소환 시점에만 오른쪽으로 갈라 세운다 (2026-08-26 재결정).
        //
        // 처음에는 Context Menu / Context Menu Sub처럼 한 쌍으로 묶었다(팔로워 + 동반 열기).
        // 실기에서 써 보니 두 창을 따로 잡아 옮기고 따로 닫고 싶다는 게 확인돼 되돌린다.
        // 남는 요구는 "처음 뜰 때 나란히" 하나뿐이고, 그건 MRFloatingPanel의
        // spawnLateralOffset으로 충분하다 — 런타임 결합이 아니라 소환 좌표의 문제였다.
        //
        // 이전 버전이 붙인 MRSubMenuFollower / MRStorePanelCompanion이 남아 있으면 제거한다.
        // 남겨두면 팔로워가 매 프레임 위치를 덮어써서 잡아 옮겨도 제자리로 끌려간다 (§4-59).
        //
        // GrabFrame은 **건드리지 않는다** — 독립이므로 상점도 스스로 잡혀야 한다.
        private static void SeparateFromInventory(GameObject panel, StringBuilder log)
        {
            log.AppendLine("\n  ── 독립 배치 ──");

            MRSubMenuFollower follower = panel.GetComponent<MRSubMenuFollower>();
            if (follower != null)
            {
                Undo.DestroyObjectImmediate(follower);
                log.AppendLine("      - MRSubMenuFollower 제거 (위치를 매 프레임 덮어써서 잡아 옮길 수 없다)");
            }

            MRStorePanelCompanion companion = panel.GetComponent<MRStorePanelCompanion>();
            if (companion != null)
            {
                Undo.DestroyObjectImmediate(companion);
                log.AppendLine("      - MRStorePanelCompanion 제거 (인벤토리와 함께 열고 닫지 않는다)");
            }

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

            SerializedProperty lateral = so.FindProperty("spawnLateralOffset");
            if (lateral != null)
            {
                lateral.floatValue = LateralOffset;
            }
            so.ApplyModifiedProperties();

            log.AppendLine($"      · spawnBehavior = InFrontOfUser, 좌우 오프셋 = +{LateralOffset} m (오른쪽)");

            // 잡기 판이 살아 있는지 확인만 한다 — 끄지 않는다
            Transform frame = panel.transform.Find("GrabFrame");
            if (frame == null)
            {
                log.AppendLine("      ⚠ GrabFrame이 없다 — 8번이 실패했을 수 있다. 잡아서 옮길 수 없다");
            }
            else if (frame.gameObject.activeSelf == false)
            {
                Undo.RecordObject(frame.gameObject, "Enable store grab");
                frame.gameObject.SetActive(true);
                log.AppendLine("      · GrabFrame 활성화 (이전 버전이 꺼둔 것을 되살림)");
            }
            else
            {
                log.AppendLine("      · GrabFrame 활성 확인");
            }
        }

        private static void SetRef(SerializedObject so, string path, Object value)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
            }
        }

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            SerializedProperty prop = so.FindProperty(path);
            if (prop != null)
            {
                prop.boolValue = value;
            }
        }

        // ToggleStore가 이 패널을 찾으려면 StoreView가 계층 안에 있어야 한다.
        // 없으면 메뉴 항목도 안 생기고 열어도 아무 일이 없다 — 부재를 무증상으로 두지 않는다(§4-51).
        private static void WarnIfStoreViewMissing(GameObject panel, StringBuilder log)
        {
            StoreView view = panel.GetComponentInChildren<StoreView>(true);
            if (view == null)
            {
                log.AppendLine("  ⚠ 이 패널 계층에 StoreView가 없다 — KAIManager.ToggleStore가 찾지 못한다");
                return;
            }

            log.AppendLine($"  · StoreView 확인: {view.gameObject.name}");
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
            log.AppendLine($"  · {panel.name} 실측 {rt.rect.width:F0}×{rt.rect.height:F0} px " +
                           $"× lossyScale {lossy.x:F5} → {rt.rect.width * lossy.x:F2} × {rt.rect.height * lossy.y:F2} m");
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
