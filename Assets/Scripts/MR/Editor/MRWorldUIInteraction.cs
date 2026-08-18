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

        // rect 밖으로 나온 버튼용 판정 조각의 이름 접두사.
        private const string OutlyingPatchPrefix = "HandInteraction_Out_";

        // 그 조각을 패널 평면보다 사용자 쪽으로 얼마나 내밀지(m).
        // 잡기 바는 반대편(뒤)에 있으므로 이 값이면 겹치는 자리에서 버튼이 항상 이긴다.
        private const float PatchForwardOffset = 0.006f;

        // 상호작용 면의 두께(m). Poke가 뒤에서 뚫고 들어오는 것을 막는 용도라 얇아도 된다.
        private const float SurfaceDepth = 0.02f;

        // 패널 바깥으로 상호작용 면을 넓히는 여유(m). **0이어야 한다.**
        //
        // 이력: 자체 구현(MRPinchDraggable) 시절에는 잡기 띠를 겨눌 때 레이가 사라지는 것을
        // 막으려고 0.06을 넣었다. ISDK grab으로 전환(§4-22)한 뒤로는 띠마다 자기
        // RayInteractable(Bar_*)이 있어 이 여유가 필요 없어졌고, 오히려 해롭다:
        //   · 확장된 UI 면이 잡기 띠를 덮어 레이가 Bar가 아닌 UI 평면을 맞힌다
        //     → **원거리 grab이 아예 시작되지 않는다.**
        //   · 확장된 면이 PokeInteractable 면이라 ISDK 포크 제한이 손을 거기서 멈춘다
        //     → **테두리인데도 손이 정면에서 막혀** 잡기가 불편해진다.
        // (둘 다 2026-08-15 실기에서 그룹 A 패널들이 겪은 증상이다.)
        //
        // 상호작용 면은 패널 크기와 정확히 일치시킨다.
        private const float GraspBandPadding = 0f;

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

            // 메인 판정 면은 **패널 rect 그대로**다.
            //
            // rect 밖으로 삐져나온 닫기 버튼까지 덮으려고 면을 통째로 넓히면 안 된다 —
            // 넓어진 빈 공간이 UI 판정 면이 되어, 그 자리에 있어야 할 잡기 핸들(GrabFrame의 띠)이
            // 죽어버린다(§4-23에서 GraspBandPadding을 없앤 것과 같은 문제).
            // 대신 삐져나온 버튼마다 그 크기만큼의 작은 판정 조각을 따로 붙인다(아래 8번).
            Vector2 centerPx = rt.rect.center;
            childGo.transform.localPosition = new Vector3(centerPx.x, centerPx.y, 0f);

            float widthM = rt.rect.width * Mathf.Abs(canvasScale.x);
            float heightM = rt.rect.height * Mathf.Abs(canvasScale.y);
            log.AppendLine($"        패널 크기: {rt.rect.width:F0}×{rt.rect.height:F0} px → {widthM:F3}×{heightM:F3} m");

            if (widthM < 0.02f || heightM < 0.02f)
                log.AppendLine("      ⚠ 패널이 2cm 미만입니다. 손으로 누르기 어렵습니다.");

            // ---- 3. PlaneSurface ----
            var plane = GetOrAdd<PlaneSurface>(childGo, log, "PlaneSurface");
            // 실기 검증(Quest 3S, 2026-08-11): 캔버스 정면은 로컬 -Z다(Forward가 아니라 Backward).
            // 유니티 월드 스페이스 캔버스의 표준 관례가 -Z front이고, 이미 동작 중인
            // MRBalloonWorldFollow의 빌보드 공식(Quaternion.LookRotation(pos - camPos))도
            // 이 관례를 전제로 한다. 예전에 Forward로 넣었던 게 원인이 되어 Image_ChatBalloon에서
            // 포크 방향이 반대로 잡히고 캔버스 뒷면이 사용자 쪽을 향하는 문제가 있었다.
            plane.InjectNormalFacing(PlaneSurface.NormalFacing.Backward);
            plane.InjectDoubleSided(false);
            EditorUtility.SetDirty(plane);

            // ---- 4. BoundsClipper ----
            var clipper = GetOrAdd<BoundsClipper>(childGo, log, "BoundsClipper");
            clipper.Position = Vector3.zero;
            clipper.Size = new Vector3(
                widthM + GraspBandPadding * 2f,
                heightM + GraspBandPadding * 2f,
                SurfaceDepth);
            log.AppendLine($"        상호작용 면: {widthM + GraspBandPadding * 2f:F3}×{heightM + GraspBandPadding * 2f:F3} m");
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

            // ---- 8. rect 밖으로 나온 버튼용 판정 조각 ----
            SetupOutlyingPatches(canvas, rt, pointable, canvasScale, log);

            return true;
        }

        /// <summary>패널 rect 밖으로 삐져나온 그래픽(닫기 버튼, 헤더 아이콘 등)마다
        /// 그 크기만큼의 작은 판정 조각을 만든다.
        ///
        /// 왜 메인 면을 넓히지 않는가
        /// -------------------------
        /// 면을 통째로 넓히면 늘어난 빈 공간까지 UI 판정 면이 되어, 그 자리에 있어야 할
        /// 잡기 핸들(GrabFrame의 띠)이 죽는다. 버튼 자리만 콕 집어 덮으면 나머지 띠는
        /// 전부 잡기용으로 남는다.
        ///
        /// 깊이 우선순위
        /// -------------
        /// 조각을 패널 평면보다 살짝 **앞쪽**(사용자 쪽 = 로컬 -Z, §4-20)에 놓는다.
        /// 잡기 바는 반대로 뒤쪽에 있으므로, 겹치는 자리에서는 항상 버튼이 먼저 맞는다.</summary>
        private static void SetupOutlyingPatches(Canvas canvas, RectTransform panel,
                                                 PointableCanvas pointable, Vector3 canvasScale,
                                                 StringBuilder log)
        {
            Rect panelRect = panel.rect;
            var corners = new Vector3[4];
            int made = 0;

            // 기존 조각을 먼저 지운다 — 레이아웃이 바뀌면 위치·크기가 달라지므로
            // 남겨두면 엉뚱한 허공에 판정 면이 떠 있게 된다.
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = canvas.transform.GetChild(i);
                if (c.name.StartsWith(OutlyingPatchPrefix)) Undo.DestroyObjectImmediate(c.gameObject);
            }

            foreach (var graphic in panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
            {
                if (graphic == null || !graphic.raycastTarget) continue;

                var childRt = graphic.rectTransform;
                if (childRt == null || childRt == panel) continue;

                // 마스크(스크롤 뷰 등) 안의 내용물은 건너뛴다.
                //
                // 실기 확인 2026-08-15: Tab Window_Settings의 설정 항목들이 스크롤 뷰 안에 있는데,
                // 도구를 돌린 시점의 스크롤 위치에서 뷰포트 밖에 있던 항목들이 "패널 rect 밖"으로
                // 판정돼 조각 9개가 패널 위/아래 허공에 생겼다. 그 조각들은 UI 우선권을 갖도록
                // 앞으로 나와 있어서, 보이지도 않는데 레이 이동을 막는 유령 콜라이더가 됐다.
                //
                // 마스크 안의 그래픽은 정의상 뷰포트로 잘리므로 패널 밖으로 나갈 일이 없다.
                // 진짜 "삐져나온 닫기 버튼"은 마스크 밖에 있으므로 이 가드에 걸리지 않는다.
                if (IsUnderMask(childRt, panel)) continue;

                childRt.GetWorldCorners(corners);

                float xMin = float.MaxValue, xMax = float.MinValue;
                float yMin = float.MaxValue, yMax = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 local = panel.InverseTransformPoint(corners[i]);
                    xMin = Mathf.Min(xMin, local.x); xMax = Mathf.Max(xMax, local.x);
                    yMin = Mathf.Min(yMin, local.y); yMax = Mathf.Max(yMax, local.y);
                }

                // 패널 rect 안에 완전히 들어가면 메인 면이 이미 덮으므로 건너뛴다.
                bool inside = xMin >= panelRect.xMin - 0.5f && xMax <= panelRect.xMax + 0.5f &&
                              yMin >= panelRect.yMin - 0.5f && yMax <= panelRect.yMax + 0.5f;
                if (inside) continue;

                var patch = new GameObject(OutlyingPatchPrefix + graphic.name);
                Undo.RegisterCreatedObjectUndo(patch, "Create Outlying Patch");
                Undo.SetTransformParent(patch.transform, canvas.transform, "Parent Outlying Patch");

                patch.transform.localRotation = Quaternion.identity;
                patch.transform.localScale = new Vector3(
                    1f / canvasScale.x, 1f / canvasScale.y, 1f / canvasScale.z);

                // 캔버스 정면이 -Z이므로 사용자 쪽으로 나오려면 -Z로 민다.
                float forwardPx = PatchForwardOffset / Mathf.Abs(canvasScale.z);
                patch.transform.localPosition = new Vector3(
                    (xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f, -forwardPx);

                var pPlane = GetOrAdd<PlaneSurface>(patch, log, null);
                pPlane.InjectNormalFacing(PlaneSurface.NormalFacing.Backward);
                pPlane.InjectDoubleSided(false);

                var pClipper = GetOrAdd<BoundsClipper>(patch, log, null);
                pClipper.Position = Vector3.zero;
                pClipper.Size = new Vector3(
                    (xMax - xMin) * Mathf.Abs(canvasScale.x),
                    (yMax - yMin) * Mathf.Abs(canvasScale.y),
                    SurfaceDepth);

                var pClipped = GetOrAdd<ClippedPlaneSurface>(patch, log, null);
                pClipped.InjectAllClippedPlaneSurface(pPlane, new List<IBoundsClipper> { pClipper });

                var pRay = GetOrAdd<RayInteractable>(patch, log, null);
                pRay.InjectSurface(pClipped);
                pRay.InjectOptionalPointableElement(pointable);

                var pPoke = GetOrAdd<PokeInteractable>(patch, log, null);
                pPoke.InjectSurfacePatch(pClipped);
                pPoke.InjectOptionalPointableElement(pointable);

                made++;
                log.AppendLine($"        + 판정 조각 '{graphic.name}' " +
                               $"({(xMax - xMin):F0}×{(yMax - yMin):F0} px, rect 밖)");
            }

            if (made == 0) log.AppendLine("        · rect 밖으로 나온 버튼 없음");
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
        /// <summary>이 그래픽이 패널까지 올라가는 경로 어딘가에서 마스크로 잘리는지.</summary>
        private static bool IsUnderMask(Transform child, Transform panel)
        {
            Transform cur = child.parent;
            while (cur != null && cur != panel.parent)
            {
                if (cur.GetComponent<UnityEngine.UI.RectMask2D>() != null) return true;
                if (cur.GetComponent<UnityEngine.UI.Mask>() != null) return true;
                if (cur == panel) break;
                cur = cur.parent;
            }
            return false;
        }

        /// <summary>label이 null이면 로그를 남기지 않는다 (호출부가 자체 로그를 찍는 경우).</summary>
        private static T GetOrAdd<T>(GameObject go, StringBuilder log, string label) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null)
            {
                if (label != null) log.AppendLine($"        · {label} (있음)");
                return c;
            }
            c = Undo.AddComponent<T>(go);
            if (label != null) log.AppendLine($"        + {label}");
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
