// 선택한 월드 스페이스 UI 패널에 ISDK 정식 Grab 상호작용을 조립한다.
//
// 왜 ISDK 정식 방식인가 (2026-08-15 전환)
// -------------------------------------
// 그 전에는 손 관절 거리로 주먹 쥠을 직접 판정하는 자체 구현(MRPinchDraggable)을 썼는데,
// "버튼 누르려고 검지만 편 손"을 주먹으로 오인하는 등 휴리스틱의 한계가 계속 드러났다.
// ISDK의 HandGrabInteractable을 쓰면 grab 판정·손 붙임·놓기를 전부 SDK가 처리한다.
//
// 처음에 ISDK grab을 피했던 이유(§4-15 UIResizeHandler 충돌 재발 우려)는 과했다 —
// §4-15는 uGUI의 IDragHandler가 PointableCanvasModule의 **UI 포인터 이벤트**를 가로챈 것이고,
// HandGrabInteractable은 HandGrabInteractor라는 별개 인터랙터가 처리해 경로가 겹치지 않는다.
//
// 조립 내용 (씬의 [BuildingBlock] Cube 구성을 그대로 따름)
// ----------------------------------------------------
//   패널 루트: Rigidbody(kinematic, no gravity) + Grabbable + MRPanelGrabTransformer
//   자식 "GrabFrame": 테두리 모양 BoxCollider 4개(trigger) + GrabInteractable + HandGrabInteractable
//
// 왜 콜라이더가 "테두리 4개"인가
// ----------------------------
// 메타 홈처럼 패널 **가장자리**만 잡히게 하기 위함이다. 패널 전체를 덮는 콜라이더 하나로 하면
// 버튼을 누르려는 손이 잡기로 오인된다. BoxCollider로는 프레임 모양을 만들 수 없으므로
// 상/하/좌/우 4개를 띠처럼 배치한다. 안쪽(내용물·버튼)에는 콜라이더가 없어 잡히지 않는다.
//
// 인터랙터(손 쪽)는 [BuildingBlock] OVRInteractionComprehensive 프리팹이 이미 제공하므로
// 건드릴 필요가 없다 — 새 Interactable은 ISDK 레지스트리를 통해 자동으로 인식된다.
//
// 사용: 하이어라키에서 패널(월드 스페이스 캔버스) 선택 → Tools → MR → 8

using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;  // HandGrabInteractable만 별도 네임스페이스에 있다
using Oculus.Interaction.Surfaces;  // ColliderSurface
using UnityEditor;
using UnityEngine;

namespace AICO.MR.EditorTools
{
    public static class MRPanelGrabSetup
    {
        private const string MenuRoot = "Tools/MR/";
        private const string FrameChildName = "GrabFrame";

        // 잡기 띠의 두께(m). 패널 가장자리 바깥으로 이만큼이 잡기 영역이 된다.
        // 조준 편의를 위해 넉넉하게 둔다 — UI와 겹치는 문제는 두께가 아니라
        // 깊이(BandDepth)와 Z 오프셋(BarZOffset)으로 해결한다.
        private const float BandThickness = 0.03f;

        // 콜라이더의 앞뒤 두께(m).
        //
        // 얇게 유지하는 이유 (2026-08-15 실기): 두꺼우면 콜라이더가 패널 면보다 앞으로
        // 튀어나와서, 패널을 비스듬히 볼 때 레이가 UI보다 이 콜라이더를 먼저 맞힌다.
        // 그러면 뒤에 있는 버튼을 조준할 수 없다. 아래 BarZOffset과 함께 쓴다.
        private const float BandDepth = 0.008f;

        // 바를 패널 면보다 **뒤쪽**으로 더 밀어내는 여유(m).
        // 캔버스 정면은 -Z 관례(§4-20)이므로 +Z가 사용자에게서 멀어지는 방향이다.
        // 실제 오프셋은 BandDepth/2 + 이 값 — 바의 앞면이 패널 평면보다 확실히 뒤에 와야
        // 콜라이더가 UI를 가리지 않고 UI가 항상 조준 우선권을 갖는다.
        private const float ExtraZOffset = 0.004f;

        // 원거리(레이) 잡기를 붙일지 여부.
        //
        // 기본 false — 실기 확인(2026-08-15)에서 DistanceHandGrabInteractable을 붙이면
        // 두 가지 문제가 생겼다:
        //   1) DistanceHandGrab 전용 레이는 잡을 후보를 찾아 **휘어지는 곡선**이라,
        //      UI 조준용 직선 레이를 밀어내고 버튼을 겨눌 수 없게 만든다.
        //   2) 손 정렬(HandAlignType.AlignOnGrab) 때문에 패널이 오는 게 아니라
        //      **손 모델이 패널 쪽으로 딸려간다.**
        // 근접 잡기(HandGrabInteractable)만으로도 패널 이동은 충분하고, 레이는 UI 조준
        // 전용으로 두는 편이 훨씬 깔끔하다. 원거리 이동이 꼭 필요해지면 이 값을 켜되
        // 위 두 문제를 함께 해결해야 한다.
        private const bool EnableDistanceGrab = false;

        [MenuItem(MenuRoot + "8. 선택 패널에 잡기(Grab) 추가", false, 107)]
        public static void AddGrabToSelectedPanels()
        {
            var log = new StringBuilder("[MRPanelGrabSetup] 잡기 조립\n");

            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "하이어라키에서 잡기를 추가할 패널을 선택한 뒤 다시 실행하세요.", "확인");
                return;
            }

            int done = 0;
            foreach (GameObject go in selection)
            {
                if (Setup(go, log)) done++;
            }

            log.AppendLine($"\n완료: {done}/{selection.Length}");
            Debug.Log(log.ToString());
        }

        private static bool Setup(GameObject panel, StringBuilder log)
        {
            var rt = panel.transform as RectTransform;
            if (rt == null)
            {
                log.AppendLine($"  ❌ RectTransform 없음: {panel.name}");
                return false;
            }

            log.AppendLine($"\n  ● {panel.name}");

            Vector3 lossy = panel.transform.lossyScale;
            if (Mathf.Approximately(lossy.x, 0f) || Mathf.Approximately(lossy.y, 0f))
            {
                log.AppendLine("      ❌ 스케일에 0이 있습니다. 중단합니다.");
                return false;
            }

            float widthM = rt.rect.width * Mathf.Abs(lossy.x);
            float heightM = rt.rect.height * Mathf.Abs(lossy.y);
            log.AppendLine($"      패널 크기: {widthM:F3} × {heightM:F3} m");

            // ---- 1. Rigidbody (kinematic) ----
            var rb = panel.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody>(panel);
                log.AppendLine("      + Rigidbody");
            }
            else log.AppendLine("      · Rigidbody (있음)");

            Undo.RecordObject(rb, "Configure Rigidbody");
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            EditorUtility.SetDirty(rb);

            // ---- 2. Grabbable ----
            var grabbable = panel.GetComponent<Grabbable>();
            if (grabbable == null)
            {
                grabbable = Undo.AddComponent<Grabbable>(panel);
                log.AppendLine("      + Grabbable");
            }
            else log.AppendLine("      · Grabbable (있음)");

            // ---- 3. 커스텀 트랜스포머 (빌보드 + 스무딩) ----
            var transformer = panel.GetComponent<MRPanelGrabTransformer>();
            if (transformer == null)
            {
                transformer = Undo.AddComponent<MRPanelGrabTransformer>(panel);
                log.AppendLine("      + MRPanelGrabTransformer");
            }
            else log.AppendLine("      · MRPanelGrabTransformer (있음)");

            // Grabbable의 private 필드를 SerializedObject로 배선한다
            // (InjectOptionalOneGrabTransformer 등이 공개돼 있지만, 에디터에서는
            //  SerializedObject 쪽이 Undo/프리팹 오버라이드와 더 잘 맞는다.)
            var so = new SerializedObject(grabbable);
            SetIfExists(so, "_targetTransform", panel.transform);
            SetIfExists(so, "_rigidbody", rb);
            SetIfExists(so, "_oneGrabTransformer", transformer);
            SetIfExists(so, "_kinematicWhileSelected", true);
            SetIfExists(so, "_throwWhenUnselected", false); // UI 패널은 던져지면 안 된다
            so.ApplyModifiedProperties();

            // ---- 4. GrabFrame 자식 ----
            Transform existing = panel.transform.Find(FrameChildName);
            GameObject frame;
            if (existing == null)
            {
                frame = new GameObject(FrameChildName);
                Undo.RegisterCreatedObjectUndo(frame, "Create GrabFrame");
                Undo.SetTransformParent(frame.transform, panel.transform, "Parent GrabFrame");
                log.AppendLine($"      + {FrameChildName}");
            }
            else
            {
                frame = existing.gameObject;
                log.AppendLine($"      · {FrameChildName} (있음)");
            }

            // 스케일 보정·바 크기 계산은 MRGrabFrameFitter가 런타임에도 다시 하므로
            // 여기서는 컴포넌트만 붙이고 즉시 한 번 맞춘다.
            // (예전에는 이 시점 스케일을 구워 넣어서, 나중에 패널 크기를 바꾸면
            //  잡기 띠만 옛 크기로 남아 어긋났다.)
            var fitter = GetOrAdd<MRGrabFrameFitter>(frame, log, "MRGrabFrameFitter");

            // 띠 수치는 이 툴의 상수를 정본으로 삼아 컴포넌트에 밀어넣는다
            // (두 곳에 따로 적어두면 값이 갈라진다).
            var fSo = new SerializedObject(fitter);
            SetIfExists(fSo, "panelRect", rt);
            SetIfExists(fSo, "bandThickness", BandThickness);
            SetIfExists(fSo, "bandDepth", BandDepth);
            SetIfExists(fSo, "extraZOffset", ExtraZOffset);
            fSo.ApplyModifiedProperties();

            // ---- 5. 테두리 바 4개 ----
            // 각 바를 **독립 자식 오브젝트**로 만든다. ColliderSurface가 콜라이더를 하나만
            // 참조할 수 있어서, 직선 레이로 잡으려면 바마다 자기 ColliderSurface +
            // RayInteractable이 필요하기 때문이다. (근접 grab은 콜라이더가 어디 있든
            // Rigidbody 기준으로 자식 전체를 훑으므로 이 분리에 영향받지 않는다.)
            foreach (var old in frame.GetComponents<BoxCollider>())
            {
                Undo.DestroyObjectImmediate(old); // 이전 버전이 프레임에 직접 붙였던 것 정리
            }

            // 원거리 이동 방식: 각도는 레이를 1:1로 따라가고, 앞뒤 거리는 잡은 순간의
            // (카메라~패널)/(카메라~손) 비율만큼 증폭한다 — 멀리 있는 패널도 손을 조금만
            // 당기면 앞으로 오고, 가까운 패널은 세밀하게 조절된다.
            // (MoveFromTargetProvider는 손 이동량과 1:1이라 멀리 있는 패널이 레이를
            //  못 따라와 "절반만 움직이는" 느낌을 준다 — 실기 확인 2026-08-15.)
            var mover = GetOrAdd<MRRayDistanceMovementProvider>(frame, log, "MRRayDistanceMovementProvider");
            RemoveIfPresent<MoveFromTargetProvider>(frame, log, "MoveFromTargetProvider");

            // 바 4개를 만들고 컴포넌트를 배선한다. 위치·크기는 아래 fitter.Fit()이 채운다.
            SetupBar(frame, "Bar_Top", grabbable, mover, log);
            SetupBar(frame, "Bar_Bottom", grabbable, mover, log);
            SetupBar(frame, "Bar_Left", grabbable, mover, log);
            SetupBar(frame, "Bar_Right", grabbable, mover, log);

            fitter.Fit();
            log.AppendLine($"      + 테두리 바 4개 (띠 두께 {BandThickness:F2}m, 직선 레이 잡기 포함)");
            log.AppendLine($"        GrabFrame 보정 스케일: {frame.transform.localScale.x:F0} " +
                           $"(패널 lossyScale {lossy.x:F5} 기준, 이후 크기 변경 시 런타임에 자동 재계산)");

            // ---- 6. GrabInteractable / HandGrabInteractable ----
            var grabInteractable = GetOrAdd<GrabInteractable>(frame, log, "GrabInteractable");
            var goSo = new SerializedObject(grabInteractable);
            SetIfExists(goSo, "_pointableElement", grabbable);
            SetIfExists(goSo, "_rigidbody", rb);
            goSo.ApplyModifiedProperties();

            var handGrab = GetOrAdd<HandGrabInteractable>(frame, log, "HandGrabInteractable");
            var hgSo = new SerializedObject(handGrab);
            SetIfExists(hgSo, "_pointableElement", grabbable);
            SetIfExists(hgSo, "_rigidbody", rb);
            // Pinch(1) | Palm(2) = 3 — 손가락 집기와 손바닥 쥐기 둘 다 허용.
            SetIfExists(hgSo, "_supportedGrabTypes", 3);
            hgSo.ApplyModifiedProperties();

            // ---- 6b. 원거리(레이) 잡기 ----
            // DistanceHandGrabInteractor(리그에 이미 4개 존재)는 HandGrabInteractable이 아니라
            // **DistanceHandGrabInteractable**하고만 짝을 이룬다.
            // 다만 기본적으로는 붙이지 않는다 — EnableDistanceGrab 주석 참고.
            if (EnableDistanceGrab)
            {
                var distGrab = GetOrAdd<DistanceHandGrabInteractable>(frame, log, "DistanceHandGrabInteractable");
                var distMover = GetOrAdd<MoveTowardsTargetProvider>(frame, log, "MoveTowardsTargetProvider");

                var dgSo = new SerializedObject(distGrab);
                SetIfExists(dgSo, "_pointableElement", grabbable);
                SetIfExists(dgSo, "_rigidbody", rb);
                SetIfExists(dgSo, "_supportedGrabTypes", 3);
                SetIfExists(dgSo, "_movementProvider", distMover);
                dgSo.ApplyModifiedProperties();
            }
            else
            {
                // 이전 실행에서 붙었을 수 있으므로 제거한다 — 남아 있으면 곡선 레이가
                // UI 조준을 계속 뺏는다.
                RemoveIfPresent<DistanceHandGrabInteractable>(frame, log, "DistanceHandGrabInteractable");
                RemoveIfPresent<MoveTowardsTargetProvider>(frame, log, "MoveTowardsTargetProvider");
            }

            // ---- 7. 자체 구현 드래그가 남아 있으면 경고 ----
            var legacy = panel.GetComponent<MRPinchDraggable>();
            if (legacy != null)
            {
                log.AppendLine("      ⚠ MRPinchDraggable이 아직 붙어 있습니다. " +
                               "ISDK 잡기와 동시에 동작하면 서로 싸우므로 제거하세요.");
            }

            EditorUtility.SetDirty(panel);
            return true;
        }

        private static void RemoveIfPresent<T>(GameObject go, StringBuilder log, string label) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) return;
            Undo.DestroyObjectImmediate(c);
            log.AppendLine($"      − {label} 제거 (원거리 잡기 비활성)");
        }

        /// <summary>테두리 바 하나를 만들고 컴포넌트를 배선한다 — 근접 grab용 콜라이더 +
        /// 직선 레이 grab용 ColliderSurface/RayInteractable. 실제 위치·크기는
        /// MRGrabFrameFitter가 패널의 현재 스케일을 보고 채운다.</summary>
        private static void SetupBar(GameObject frame, string barName,
                                     Grabbable grabbable, MRRayDistanceMovementProvider mover, StringBuilder log)
        {
            Transform existing = frame.transform.Find(barName);
            GameObject bar;
            if (existing == null)
            {
                bar = new GameObject(barName);
                Undo.RegisterCreatedObjectUndo(bar, "Create Grab Bar");
                Undo.SetTransformParent(bar.transform, frame.transform, "Parent Grab Bar");
            }
            else bar = existing.gameObject;

            var box = bar.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(bar);
            box.isTrigger = true;

            // 직선 레이로 잡기 — RayInteractor(리그의 기본 직선 레이)가 이 RayInteractable을
            // 선택하면, _pointableElement로 연결된 Grabbable이 움직인다.
            // DistanceHandGrab의 곡선 레이와 달리 UI 조준용 직선 레이를 그대로 쓴다.
            var surface = bar.GetComponent<ColliderSurface>();
            if (surface == null) surface = Undo.AddComponent<ColliderSurface>(bar);
            var sSo = new SerializedObject(surface);
            SetIfExists(sSo, "_collider", box);
            sSo.ApplyModifiedProperties();

            var rayInteractable = bar.GetComponent<RayInteractable>();
            if (rayInteractable == null) rayInteractable = Undo.AddComponent<RayInteractable>(bar);
            var rSo = new SerializedObject(rayInteractable);
            SetIfExists(rSo, "_surface", surface);
            SetIfExists(rSo, "_pointableElement", grabbable);
            SetIfExists(rSo, "_movementProvider", mover);
            rSo.ApplyModifiedProperties();
        }

        private static T GetOrAdd<T>(GameObject go, StringBuilder log, string label) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null)
            {
                c = Undo.AddComponent<T>(go);
                log.AppendLine($"      + {label}");
            }
            else log.AppendLine($"      · {label} (있음)");
            return c;
        }

        private static void SetIfExists(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetIfExists(SerializedObject so, string prop, bool value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = value;
        }

        private static void SetIfExists(SerializedObject so, string prop, int value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.intValue = value;
        }

        private static void SetIfExists(SerializedObject so, string prop, float value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.floatValue = value;
        }
    }
}
