// 월드 스페이스 캔버스에 손 상호작용(Poke + Ray)을 붙이는 도구
//
// 배경
// ----
// Phase 3-2에서 데스크톱 UI를 월드 스페이스로 옮겼지만, 버튼과 입력창은 여전히
// 마우스 이벤트를 기다린다. MR에서는 아무 반응이 없다.
// Meta Interaction SDK(ISDK)의 PointableCanvas가 손 포인터 이벤트를 Unity UI
// 이벤트로 번역해 주는데, 필요한 컴포넌트가 6종이고 배선 순서가 까다롭다.
//
// 설정·인벤토리·캐릭터 변경창까지 같은 작업을 반복해야 하므로 도구로 만든다.
//
// 조립 구조
// --------
//   ChatBalloonCanvas            [Canvas(WorldSpace) + PointableCanvas]
//     └ HandInteraction          [world scale 1로 보정]
//         · PlaneSurface         무한 평면 (법선 = +Z)
//         · BoundsClipper        평면을 패널 크기로 자름
//         · ClippedPlaneSurface  위 둘을 합친 실제 상호작용 면
//         · RayInteractable      원거리 손 레이
//         · PokeInteractable     근접 손가락 터치
//
// 왜 자식을 만들고 스케일을 보정하는가
// -----------------------------------
// 캔버스는 "1 canvas px = 1 mm" 규약 때문에 lossyScale이 0.001이다.
// 그런데 ISDK의 Poke 판정 거리(누름 깊이, 취소 거리 등)는 **월드 미터** 기준이라
// 0.001 스케일 밑에 두면 1000배로 왜곡된다. 자식의 localScale을 역수로 주어
// world scale을 1로 되돌린 뒤, 그 안에서는 미터 단위로 다룬다.
// (MRCharacterWorldRoot의 픽셀 공간 래퍼와 같은 발상이다 — 좌표계를 감싸서 변환한다.)
//
// 사용: 하이어라키에서 월드 스페이스 캔버스를 선택 → Tools → MR → 5. 월드 UI에 손 상호작용 추가

using System.Collections.Generic;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AICO.MR.EditorTools
{
    public static class MRWorldUIInteraction
    {
        private const string MenuRoot = "Tools/MR/";
        private const string InteractionChildName = "HandInteraction";

        // 상호작용 면의 두께(m). Poke가 뒤에서 뚫고 들어오는 것을 막는 용도라 얇아도 된다.
        private const float SurfaceDepth = 0.02f;

        [MenuItem(MenuRoot + "5. 월드 UI에 손 상호작용 추가 (캔버스 선택)", false, 104)]
        public static void AddInteractionToSelectedCanvas()
        {
            var log = new StringBuilder("[MRWorldUIInteraction] 손 상호작용 추가\n");

            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "하이어라키에서 월드 스페이스 캔버스를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            // 씬에 PointableCanvasModule이 하나 있어야 한다 (전역 싱글턴)
            EnsurePointableCanvasModule(log);
            DisableConflictingInputModules(log);   // 이미 있던 경우에도 매번 확인한다

            int done = 0;
            foreach (GameObject go in selection)
            {
                var canvas = go.GetComponent<Canvas>();
                if (canvas == null)
                {
                    log.AppendLine($"  ⚠ 건너뜀 (Canvas 없음): {go.name}");
                    continue;
                }
                if (canvas.renderMode != RenderMode.WorldSpace)
                {
                    log.AppendLine($"  ⚠ 건너뜀 (World Space 아님, {canvas.renderMode}): {go.name}");
                    continue;
                }

                if (Setup(canvas, log)) done++;
            }

            log.AppendLine($"\n{done}개 캔버스 처리 완료");
            Debug.Log(log.ToString());

            if (done > 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[MRWorldUIInteraction] 씬이 변경됨으로 표시되었습니다. Ctrl+S로 저장하세요.");
            }
        }

        // =========================================================
        // 캔버스 하나 처리
        // =========================================================
        private static bool Setup(Canvas canvas, StringBuilder log)
        {
            var rt = canvas.transform as RectTransform;
            if (rt == null)
            {
                log.AppendLine($"  ❌ RectTransform 없음: {canvas.name}");
                return false;
            }

            log.AppendLine($"\n  ● {GetPath(canvas.gameObject)}");

            // ---- 1. PointableCanvas ----
            var pointable = canvas.GetComponent<PointableCanvas>();
            if (pointable == null)
            {
                pointable = Undo.AddComponent<PointableCanvas>(canvas.gameObject);
                log.AppendLine("      + PointableCanvas");
            }
            else log.AppendLine("      · PointableCanvas (있음)");
            pointable.InjectCanvas(canvas);
            EditorUtility.SetDirty(pointable);

            // ---- 2. 상호작용 전용 자식 (world scale 1) ----
            Transform child = canvas.transform.Find(InteractionChildName);
            GameObject childGo;
            if (child == null)
            {
                childGo = new GameObject(InteractionChildName);
                Undo.RegisterCreatedObjectUndo(childGo, "Create HandInteraction");
                Undo.SetTransformParent(childGo.transform, canvas.transform, "Parent HandInteraction");
                log.AppendLine($"      + {InteractionChildName}");
            }
            else
            {
                childGo = child.gameObject;
                log.AppendLine($"      · {InteractionChildName} (있음)");
            }

            // 캔버스의 월드 스케일을 상쇄해 world scale = 1 로 만든다.
            Vector3 canvasScale = canvas.transform.lossyScale;
            if (Mathf.Approximately(canvasScale.x, 0f) ||
                Mathf.Approximately(canvasScale.y, 0f) ||
                Mathf.Approximately(canvasScale.z, 0f))
            {
                log.AppendLine("      ❌ 캔버스 스케일에 0이 있습니다. 중단합니다.");
                return false;
            }

            Undo.RecordObject(childGo.transform, "Configure HandInteraction");
            childGo.transform.localRotation = Quaternion.identity;
            childGo.transform.localScale = new Vector3(
                1f / canvasScale.x, 1f / canvasScale.y, 1f / canvasScale.z);

            // 패널의 피벗이 중앙이 아닐 수 있으므로 rect 중심으로 맞춘다.
            Vector2 centerPx = rt.rect.center;
            childGo.transform.localPosition = new Vector3(centerPx.x, centerPx.y, 0f);

            // 패널의 실제 월드 크기(m)
            float widthM = rt.rect.width * Mathf.Abs(canvasScale.x);
            float heightM = rt.rect.height * Mathf.Abs(canvasScale.y);
            log.AppendLine($"        패널 크기: {rt.rect.width}×{rt.rect.height} px → {widthM:F3}×{heightM:F3} m");

            if (widthM < 0.02f || heightM < 0.02f)
                log.AppendLine("      ⚠ 패널이 2cm 미만입니다. 손으로 누르기 어렵습니다.");

            // ---- 3. PlaneSurface ----
            var plane = GetOrAdd<PlaneSurface>(childGo, log, "PlaneSurface");
            // 캔버스는 +Z를 향한다. 법선도 +Z(Forward)로 맞춘다.
            plane.InjectNormalFacing(PlaneSurface.NormalFacing.Forward);
            plane.InjectDoubleSided(false);
            EditorUtility.SetDirty(plane);

            // ---- 4. BoundsClipper ----
            var clipper = GetOrAdd<BoundsClipper>(childGo, log, "BoundsClipper");
            clipper.Position = Vector3.zero;
            clipper.Size = new Vector3(widthM, heightM, SurfaceDepth);
            EditorUtility.SetDirty(clipper);

            // ---- 5. ClippedPlaneSurface ----
            var clipped = GetOrAdd<ClippedPlaneSurface>(childGo, log, "ClippedPlaneSurface");
            clipped.InjectAllClippedPlaneSurface(plane, new List<IBoundsClipper> { clipper });
            EditorUtility.SetDirty(clipped);

            // ---- 6. RayInteractable (원거리) ----
            var ray = GetOrAdd<RayInteractable>(childGo, log, "RayInteractable");
            ray.InjectSurface(clipped);
            ray.InjectOptionalPointableElement(pointable);
            EditorUtility.SetDirty(ray);

            // ---- 7. PokeInteractable (근접) ----
            var poke = GetOrAdd<PokeInteractable>(childGo, log, "PokeInteractable");
            poke.InjectSurfacePatch(clipped);
            poke.InjectOptionalPointableElement(pointable);
            EditorUtility.SetDirty(poke);

            return true;
        }

        // =========================================================
        // PointableCanvasModule — 씬에 하나만 있으면 된다
        // =========================================================
        private static void EnsurePointableCanvasModule(StringBuilder log)
        {
            var existing = Object.FindFirstObjectByType<PointableCanvasModule>(FindObjectsInactive.Include);
            if (existing != null)
            {
                log.AppendLine($"  · PointableCanvasModule (있음): {GetPath(existing.gameObject)}");
                return;
            }

            var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            GameObject host;
            if (eventSystem != null)
            {
                host = eventSystem.gameObject;
            }
            else
            {
                host = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(host, "Create EventSystem");
                Undo.AddComponent<EventSystem>(host);
                log.AppendLine("  + EventSystem (새로 생성)");
            }

            Undo.AddComponent<PointableCanvasModule>(host);
            log.AppendLine($"  + PointableCanvasModule → {GetPath(host)}");
        }

        // EventSystem은 활성화된 입력 모듈 하나만 사용한다.
        // 데스크톱용 StandaloneInputModule이 남아 있으면 PointableCanvasModule이
        // 선택되지 않아 손 입력이 통째로 무시될 수 있다.
        private static void DisableConflictingInputModules(StringBuilder log)
        {
            var modules = Object.FindObjectsByType<BaseInputModule>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var m in modules)
            {
                if (m is PointableCanvasModule) continue;
                if (!m.enabled) continue;

                Undo.RecordObject(m, "Disable desktop input module");
                m.enabled = false;
                EditorUtility.SetDirty(m);
                log.AppendLine($"  − {m.GetType().Name} 비활성화 ({GetPath(m.gameObject)}) — PointableCanvasModule과 충돌");
            }
        }

        // =========================================================
        // 유틸
        // =========================================================
        private static T GetOrAdd<T>(GameObject go, StringBuilder log, string label) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null)
            {
                log.AppendLine($"        · {label} (있음)");
                return c;
            }
            c = Undo.AddComponent<T>(go);
            log.AppendLine($"        + {label}");
            return c;
        }

        private static string GetPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            Transform t = go.transform.parent;
            while (t != null) { sb.Insert(0, t.name + "/"); t = t.parent; }
            return sb.ToString();
        }
    }
}
