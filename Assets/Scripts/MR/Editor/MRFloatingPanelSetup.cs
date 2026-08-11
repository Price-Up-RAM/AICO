// 공간 고정형 UI 패널(설정·캐릭터 목록·기록 등)을 확정된 월드 스페이스 레시피로 변환하는 도구.
//
// 확정 레시피 (MR_Phase3-2_Canvas_Plan.md §7, 실기 Quest 3S 검증 완료)
//   Canvas        renderMode = World Space
//                 Scale      = 0.001        (1 canvas px = 1 mm)
//   CanvasScaler  Dynamic Pixels Per Unit = 3
//   자식 UI       Scale = 1                 (계층 드래그로 튀지 않았는지 확인)
//
// 이 도구가 하는 일: 선택한 패널 오브젝트에 위 레시피를 적용하고 MRFloatingPanel을 붙인다.
// 이 도구가 하지 않는 일:
//   - 손 상호작용 부착 (Tools → MR → 5. 월드 UI에 손 상호작용 추가를 이어서 실행할 것)
//   - 계층 재배치 (기존 부모 그대로 둔다 — 필요하면 손으로 옮길 것)
//   - 패널 내용물 배선 (버튼 이벤트 등은 기존 매니저가 그대로 담당)
//
// 사용: 하이어라키에서 패널 루트(예: "Tab Window_Settings")를 선택 → Tools → MR →
//       6. 선택 오브젝트를 플로팅 패널로 변환

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Text;

namespace AICO.MR.EditorTools
{
    public static class MRFloatingPanelSetup
    {
        private const string MenuRoot = "Tools/MR/";
        private const float RecipeCanvasScale = 0.001f;
        private const float RecipeDynamicPPU = 3f;

        [MenuItem(MenuRoot + "6. 선택 오브젝트를 플로팅 패널로 변환", false, 105)]
        public static void ConvertSelectionToFloatingPanel()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "하이어라키에서 패널 루트 오브젝트를 선택한 뒤 실행하세요.\n" +
                    "예: Tab Window_Settings, CharChangeListSample, CharChange, CharSummon", "확인");
                return;
            }

            var log = new StringBuilder("[MRFloatingPanelSetup] 플로팅 패널 변환\n");
            int done = 0;

            foreach (GameObject go in selection)
            {
                if (Convert(go, log)) done++;
            }

            log.AppendLine($"\n{done}개 처리 완료.");
            log.AppendLine("다음: 하이어라키에서 같은 오브젝트를 선택한 채 " +
                            "Tools → MR → 5. 월드 UI에 손 상호작용 추가 를 실행하세요.");
            Debug.Log(log.ToString());

            if (done > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static bool Convert(GameObject go, StringBuilder log)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null)
            {
                log.AppendLine($"  ❌ 건너뜀 (RectTransform 없음): {go.name}");
                return false;
            }

            log.AppendLine($"\n  ● {go.name}");

            // ---- 1. Canvas ----
            var canvas = go.GetComponent<Canvas>();
            bool canvasIsNew = canvas == null;
            if (canvasIsNew)
            {
                canvas = Undo.AddComponent<Canvas>(go);
                log.AppendLine("      + Canvas");
            }
            else log.AppendLine("      · Canvas (있음)");

            Undo.RecordObject(canvas, "Configure panel canvas");
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true; // 부모 캔버스의 정렬에 휘둘리지 않고 독립적으로 렌더링 순서를 갖는다
            EditorUtility.SetDirty(canvas);

            // ---- 2. CanvasScaler ----
            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = Undo.AddComponent<CanvasScaler>(go);
                log.AppendLine("      + CanvasScaler");
            }
            else log.AppendLine("      · CanvasScaler (있음)");

            Undo.RecordObject(scaler, "Configure panel scaler");
            scaler.dynamicPixelsPerUnit = RecipeDynamicPPU;
            EditorUtility.SetDirty(scaler);

            // ---- 3. GraphicRaycaster ----
            // 독립 rootCanvas가 되므로(리빌드 격리) 자체 레이캐스터가 있어야 PointableCanvasModule이
            // 이 캔버스를 정확히 지목한다 (PointableCanvasModule.FindFirstRaycastWithinCanvas 참고).
            var raycaster = go.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                Undo.AddComponent<UnityEngine.UI.GraphicRaycaster>(go);
                log.AppendLine("      + GraphicRaycaster");
            }
            else log.AppendLine("      · GraphicRaycaster (있음)");

            // ---- 4. 캔버스 스케일 ----
            Undo.RecordObject(go.transform, "Set panel canvas scale");
            Vector3 curScale = go.transform.localScale;
            if (!IsApprox(curScale, RecipeCanvasScale))
            {
                go.transform.localScale = Vector3.one * RecipeCanvasScale;
                log.AppendLine($"      · Scale {curScale} → {RecipeCanvasScale} (1 canvas px = 1 mm)");
            }
            else
            {
                log.AppendLine("      · Scale 이미 0.001 (변경 없음)");
            }

            // ---- 5. 자식 UI 스케일 점검 (함정 §4-3: 계층 드래그 시 자식 스케일이 튐) ----
            int suspicious = 0;
            foreach (RectTransform child in go.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == rt) continue;
                if (child.localScale.x > 10f || child.localScale.x < 0.01f)
                {
                    suspicious++;
                }
            }
            if (suspicious > 0)
            {
                log.AppendLine($"      ⚠ 자식 {suspicious}개의 localScale이 1에서 크게 벗어나 있습니다. " +
                                "계층 이동 중 스케일이 보존되어 튄 것일 수 있습니다 — 수동으로 1로 리셋하세요.");
            }

            // ---- 6. MRFloatingPanel ----
            var panel = go.GetComponent<MRFloatingPanel>();
            if (panel == null)
            {
                panel = Undo.AddComponent<MRFloatingPanel>(go);
                log.AppendLine("      + MRFloatingPanel");
            }
            else log.AppendLine("      · MRFloatingPanel (있음)");

            AutoWirePanel(panel, go, canvas, log);
            EditorUtility.SetDirty(panel);

            return true;
        }

        // MRFloatingPanel의 private 필드는 SerializedObject로 채운다 (public 세터가 없으므로).
        private static void AutoWirePanel(MRFloatingPanel panel, GameObject go, Canvas canvas, StringBuilder log)
        {
            var so = new SerializedObject(panel);
            var rootProp = so.FindProperty("panelRoot");
            var canvasProp = so.FindProperty("panelCanvas");

            if (rootProp != null && rootProp.objectReferenceValue == null)
                rootProp.objectReferenceValue = go.transform;
            if (canvasProp != null && canvasProp.objectReferenceValue == null)
                canvasProp.objectReferenceValue = canvas;

            so.ApplyModifiedProperties();
        }

        private static bool IsApprox(Vector3 v, float target)
        {
            return Mathf.Abs(v.x - target) < 0.0001f &&
                   Mathf.Abs(v.y - target) < 0.0001f &&
                   Mathf.Abs(v.z - target) < 0.0001f;
        }
    }
}
