// 패널 하나를 MR용으로 끝까지 전환하는 원스톱 도구.
//
// 왜 필요한가
// ----------
// 지금까지 패널 하나를 옮기려면 손으로 5단계를 반복해야 했다:
//   ① WorldUI 밑으로 드래그 → ② 스케일 복구 → ③ Tools 6 → ④ Tools 5 → ⑤ Tools 8
// 남은 패널이 10개가 넘어가면 실수(특히 ②를 빠뜨려 스케일이 1000배로 튀는 §4-3)가
// 반드시 생긴다. 이 도구가 순서와 스케일까지 한 번에 처리한다.
//
// 왜 부모를 WorldUI로 옮기는가
// --------------------------
// 월드 스페이스 캔버스를 다른 캔버스 **안**에 두면 버튼도 텍스트도 깨진다 (§4-18).
// 반드시 기존 Canvas 계층에서 꺼내야 한다.
//
// 사용: 하이어라키에서 패널(들)을 선택 → Tools → MR → 9

using System.Text;
using UnityEditor;
using UnityEngine;

namespace AICO.MR.EditorTools
{
    public static class MRPanelFullConvert
    {
        private const string MenuRoot = "Tools/MR/";
        private const string WorldUIName = "WorldUI";

        // 참고: 예전에는 여기서 스케일을 0.0005로 강제 정규화했다.
        // 2026-08-15부터 패널 크기는 사용자가 직접 맞추기로 해서, 이 도구는 스케일을
        // 바꾸지 않는다 — Tools 6이 바꾼 것만 원래 값으로 되돌린다.

        [MenuItem(MenuRoot + "9. 선택 패널 MR 전환 (전체: 이동→스케일→6→5→8)", false, 108)]
        public static void ConvertFully()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "전환할 패널을 하이어라키에서 선택한 뒤 다시 실행하세요.", "확인");
                return;
            }

            Transform worldUI = FindWorldUI();
            if (worldUI == null)
            {
                EditorUtility.DisplayDialog("WorldUI 없음",
                    $"씬에서 '{WorldUIName}' 오브젝트를 찾지 못했습니다.\n" +
                    "MR > WorldUI 계층이 있는지 확인하세요.", "확인");
                return;
            }

            var log = new StringBuilder("[MRPanelFullConvert] 패널 전체 전환\n");
            var converted = new System.Collections.Generic.List<GameObject>();
            var originalScales = new System.Collections.Generic.List<Vector3>();

            // ---- 1단계: 부모 이동 (스케일은 건드리지 않고 원래 값만 기억) ----
            foreach (GameObject go in selection)
            {
                if (go == null) continue;

                var rt = go.transform as RectTransform;
                if (rt == null)
                {
                    log.AppendLine($"  ❌ {go.name}: RectTransform이 아닙니다. 건너뜁니다.");
                    continue;
                }

                log.AppendLine($"\n  ● {go.name}");

                if (go.transform.parent != worldUI)
                {
                    Undo.SetTransformParent(go.transform, worldUI, "Move Panel to WorldUI");
                    log.AppendLine($"      · 부모 이동 → {WorldUIName}");

                    // 계층 이동 시 Unity가 월드 스케일을 보존하려고 로컬 스케일을 바꿔놓는다(§4-3).
                    // 그룹 오브젝트의 스케일이 1이면 값이 그대로라 대개 문제없지만,
                    // 그렇지 않으면 여기서 값이 튄다 — 로그로 알 수 있게 남겨둔다.
                    log.AppendLine($"      · 현재 스케일 {rt.localScale.x}");
                }
                else log.AppendLine($"      · 이미 {WorldUIName} 아래에 있음");

                converted.Add(go);
                originalScales.Add(rt.localScale);
            }

            if (converted.Count == 0)
            {
                Debug.Log(log.ToString());
                return;
            }

            // ---- 2단계: 기존 도구들을 순서대로 실행 ----
            // 각 도구가 Selection을 읽으므로 선택을 갈아끼우며 호출한다.
            Object[] previousSelection = Selection.objects;
            Selection.objects = converted.ToArray();

            log.AppendLine("\n  ── Tools 6 (플로팅 패널 변환) ──");
            MRFloatingPanelSetup.ConvertSelectionToFloatingPanel();

            // ⚠ Tools 6은 스케일을 자기 레시피 값(0.001)으로 덮어쓴다.
            // 패널 크기는 사용자가 수동으로 맞추기로 했으므로(2026-08-15 결정),
            // 6번 실행 전 값을 기억해뒀다가 여기서 그대로 되돌린다.
            // 순서가 중요하다 — 6번 **뒤**에 복원해야 이어지는 5번·8번이 올바른 스케일 기준으로
            // 상호작용 면과 잡기 띠를 계산한다.
            for (int i = 0; i < converted.Count; i++)
            {
                var rt = converted[i].transform as RectTransform;
                if (rt == null) continue;

                Vector3 original = originalScales[i];
                if (rt.localScale == original) continue;

                Undo.RecordObject(rt, "Restore Panel Scale");
                Vector3 changed = rt.localScale;
                rt.localScale = original;
                log.AppendLine($"      · {converted[i].name} 스케일 복원 {changed.x} → {original.x} " +
                               "(6번이 바꾼 것을 되돌림)");
            }

            log.AppendLine("  ── Tools 5 (손 상호작용) ──");
            MRWorldUIInteraction.AddInteractionToSelectedCanvas();

            log.AppendLine("  ── Tools 8 (잡기) ──");
            MRPanelGrabSetup.AddGrabToSelectedPanels();

            Selection.objects = previousSelection;

            log.AppendLine($"\n완료: {converted.Count}개 (각 도구의 상세 로그는 위 항목들 참고)");
            Debug.Log(log.ToString());
        }

        private static Transform FindWorldUI()
        {
            GameObject found = GameObject.Find(WorldUIName);
            if (found != null) return found.transform;

            // 비활성 상태일 수도 있으므로 전체 탐색으로 한 번 더 찾는다.
            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == WorldUIName) return t;
            }
            return null;
        }
    }
}
