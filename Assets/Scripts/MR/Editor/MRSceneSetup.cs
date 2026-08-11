// MR 씬 셋업 도구 — SampleSceneKAI-MR 전용
//
// 씬 YAML을 손으로 고치는 것은 fileID 참조와 자식 목록이 얽혀 있어 위험하다.
// Unity API를 통해 처리하면 참조 무결성을 엔진이 보장한다.
//
// 모든 메뉴 항목은 **멱등(idempotent)** 이다. 여러 번 실행해도 안전하다.
// Undo를 지원하므로 Ctrl+Z로 되돌릴 수 있다.
//
// 사용: Tools → MR → ...
// 참고: KAI/Editor/KAISceneBuilder.cs 의 선례를 따른다.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AICO.MR.EditorTools
{
    public static class MRSceneSetup
    {
        private const string MenuRoot = "Tools/MR/";
        private const string ExpectedSceneName = "SampleSceneKAI-MR";

        // =========================================================
        // 삭제 대상 — 데스크톱 전용 오브젝트
        // =========================================================
        // 이름으로 찾는다. 경로가 아니라 이름 기준이라 계층이 바뀌어도 동작한다.
        // 대신 이름 충돌 위험이 있으므로 부모 이름을 함께 지정할 수 있게 했다.
        private static readonly (string name, string parentHint, string reason)[] DeleteTargets =
        {
            // 아래 5개는 2026-08-02에 삭제 완료. 씬을 다시 만들 경우를 위해 목록에 남긴다.
            ("Cameras",                    "Root260616", "데스크톱 카메라 3종. CenterEyeAnchor가 대체"),
            ("PIP",                        "Legacy",     "PIP 카메라·캔버스. MR에 대상 화면 없음"),
            ("Tester",                     "Root260616", "개발용 (깨진 스크립트 참조 포함)"),
            ("DevManager",                 "Manager",    "개발용"),
            ("SitSupport",                 null,         "포모도로 착석 지원. 포모도로 제외에 따라 함께 제외"),

            // --- 화면 캡처 / OCR ---
            ("OCRAutoMap",                 "Canvas",     "화면 OCR 자동 매핑"),
            ("ScreenshotAreaImage",        "Canvas",     "스크린샷 영역 표시"),
            ("ScreenshotOCRAreaImage",     "Canvas",     "OCR 영역 표시"),
            ("ScreenshotBackgroundImage",  "Canvas",     "스크린샷 배경"),
            ("OCR(Special)",               "Tabs",       "OCR 설정 탭 버튼"),
            ("OCR",                        "Content",    "OCR 설정 탭 내용"),
            ("HotKey",                     "Tabs",       "핫키 설정 탭 버튼"),
            ("HotKey",                     "Content",    "핫키 설정 탭 내용"),

            // ⚠ 초상화(Portrait)는 삭제 대상이 아니다 (2026-08-02 판정 번복).
            //    Operator 모드 = "3D 캐릭터 대신 얼굴 창으로 대화하는 UI 모드"이며
            //    화면 인식·조작(VL 에이전트)과 무관하다. MR에서는 손목 패널로 활용한다.
            //    ("PortraitMask", "Canvas", ...)              ← 삭제 금지
            //    ("Image_PortraitBalloonSimple", "Canvas", ...) ← 삭제 금지

            // --- 기타 ---
            ("2DCharSample",               "Canvas",     "Spine 2D 캐릭터 샘플. MR은 VRM만"),
            ("OverlayFilter",              "Canvas",     "데스크톱 오버레이 필터"),
        };

        // =========================================================
        // 반드시 활성이어야 하는 오브젝트
        // =========================================================
        private static readonly string[] RequiredActive =
        {
            "CharManager",   // 꺼지면 캐릭터가 스폰되지 않는다 (실제 사고 사례)
            "GameManager",
            "UIManager",
            "Canvases",
            "Canvas",
            "Canvas_Char",
        };

        // =========================================================
        // 0. Root260616 프리팹 완전 언팩 (Phase 3-2 선행 조건)
        // =========================================================
        // 프리팹 인스턴스는 자식을 재배치할 수 없다. UI 2029개를 서브 캔버스로
        // 재구성하려면 언팩이 필수다 (SampleSceneKAI_MR_Port_Plan.md §0).
        // 이 씬(SampleSceneKAI-MR)에만 적용된다 — 원본 Root260616.prefab 에셋과
        // 데스크톱 씬(SampleScene/SampleSceneKAI)의 인스턴스는 영향받지 않는다.
        [MenuItem(MenuRoot + "0. Root260616 프리팹 완전 언팩", false, 99)]
        public static void UnpackRootPrefab()
        {
            if (!ConfirmScene()) return;

            GameObject root = FindByName("Root260616", null);
            if (root == null)
            {
                Debug.LogError("[MRSceneSetup] Root260616을 찾지 못했습니다. 씬 구조가 바뀌었는지 확인하세요.");
                return;
            }

            var status = PrefabUtility.GetPrefabInstanceStatus(root);
            if (status != PrefabInstanceStatus.Connected)
            {
                Debug.Log($"[MRSceneSetup] Root260616은 이미 프리팹 인스턴스가 아닙니다 (상태: {status}). 언팩을 건너뜁니다.");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "프리팹 언팩",
                "Root260616을 완전히 언팩합니다 (Unpack Completely).\n\n" +
                "· 이 씬(SampleSceneKAI-MR)에만 적용됩니다.\n" +
                "· 원본 Root260616.prefab 에셋과 데스크톱 씬은 영향받지 않습니다.\n" +
                "· Ctrl+Z로 되돌릴 수 있습니다(저장 전까지).\n\n" +
                "진행하시겠습니까?",
                "언팩", "취소");
            if (!proceed) return;

            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            Debug.Log("[MRSceneSetup] Root260616 언팩 완료. Ctrl+S로 저장하세요.");
            MarkSceneDirty();
        }

        // =========================================================
        // 1. 데스크톱 전용 오브젝트 삭제
        // =========================================================
        [MenuItem(MenuRoot + "1. 데스크톱 전용 오브젝트 삭제", false, 100)]
        public static void StripDesktopObjects()
        {
            if (!ConfirmScene()) return;

            var log = new StringBuilder("[MRSceneSetup] 데스크톱 전용 오브젝트 삭제\n");
            int deleted = 0, notFound = 0;

            foreach (var (name, parentHint, reason) in DeleteTargets)
            {
                GameObject target = FindByName(name, parentHint);
                if (target == null)
                {
                    log.AppendLine($"  · 없음(이미 삭제됨): {parentHint}/{name}");
                    notFound++;
                    continue;
                }

                log.AppendLine($"  ✔ 삭제: {GetPath(target)}  — {reason}");
                Undo.DestroyObjectImmediate(target);
                deleted++;
            }

            log.AppendLine($"\n삭제 {deleted}개 / 이미 없음 {notFound}개");
            Debug.Log(log.ToString());

            MarkSceneDirty();
        }

        // =========================================================
        // 2. 필수 오브젝트 활성화
        // =========================================================
        [MenuItem(MenuRoot + "2. 필수 오브젝트 활성화", false, 101)]
        public static void EnsureRequiredActive()
        {
            if (!ConfirmScene()) return;

            var log = new StringBuilder("[MRSceneSetup] 필수 오브젝트 활성 확인\n");
            int fixedCount = 0;

            foreach (string name in RequiredActive)
            {
                GameObject go = FindByName(name, null);
                if (go == null)
                {
                    log.AppendLine($"  ⚠ 찾지 못함: {name}");
                    continue;
                }

                if (go.activeSelf)
                {
                    log.AppendLine($"  · 이미 활성: {GetPath(go)}");
                    continue;
                }

                Undo.RecordObject(go, "Enable required object");
                go.SetActive(true);
                log.AppendLine($"  ✔ 활성화: {GetPath(go)}");
                fixedCount++;
            }

            log.AppendLine($"\n{fixedCount}개 활성화");
            Debug.Log(log.ToString());

            if (fixedCount > 0) MarkSceneDirty();
        }

        // =========================================================
        // 3. 씬 상태 리포트
        // =========================================================
        [MenuItem(MenuRoot + "3. 씬 상태 리포트", false, 102)]
        public static void ReportSceneState()
        {
            Scene scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            var log = new StringBuilder($"[MRSceneSetup] 씬 상태 — {scene.name}\n\n");

            // 루트 오브젝트
            log.AppendLine($"루트 오브젝트 {roots.Length}개:");
            foreach (var r in roots)
                log.AppendLine($"  {(r.activeSelf ? "●" : "○")} {r.name}");

            // 전체 통계
            var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var allCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var allGraphics = Object.FindObjectsByType<CanvasRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            log.AppendLine($"\nGameObject: {allTransforms.Length}");
            log.AppendLine($"CanvasRenderer (UI 요소): {allGraphics.Length}");

            // 캔버스
            log.AppendLine($"\n캔버스 {allCanvases.Length}개:");
            foreach (var c in allCanvases)
                log.AppendLine($"  {(c.gameObject.activeInHierarchy ? "●" : "○")} {GetPath(c.gameObject)}  [{c.renderMode}]");

            // 카메라
            log.AppendLine($"\n카메라 {allCameras.Length}개:");
            foreach (var c in allCameras)
                log.AppendLine($"  {(c.isActiveAndEnabled ? "●" : "○")} {GetPath(c.gameObject)}  clear={c.clearFlags} depth={c.depth}");

            // 남아있는 삭제 대상
            log.AppendLine("\n남아있는 삭제 대상:");
            bool anyLeft = false;
            foreach (var (name, parentHint, _) in DeleteTargets)
            {
                var go = FindByName(name, parentHint);
                if (go == null) continue;
                log.AppendLine($"  ⚠ {GetPath(go)}");
                anyLeft = true;
            }
            if (!anyLeft) log.AppendLine("  없음 ✔");

            // 깨진 스크립트 참조
            int missing = CountMissingScripts();
            log.AppendLine($"\n깨진 스크립트 참조: {missing}개");

            Debug.Log(log.ToString());
        }

        // =========================================================
        // 4. 깨진 스크립트 참조 정리
        // =========================================================
        [MenuItem(MenuRoot + "4. 깨진 스크립트 참조 제거", false, 103)]
        public static void RemoveMissingScripts()
        {
            if (!ConfirmScene()) return;

            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int total = 0;
            var log = new StringBuilder("[MRSceneSetup] 깨진 스크립트 참조 제거\n");

            foreach (var t in all)
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n == 0) continue;

                log.AppendLine($"  ✔ {GetPath(t.gameObject)} — {n}개");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                total += n;
            }

            log.AppendLine($"\n총 {total}개 제거");
            Debug.Log(log.ToString());

            if (total > 0) MarkSceneDirty();
        }

        // =========================================================
        // 전체 실행
        // =========================================================
        [MenuItem(MenuRoot + "전체 실행 (0→1→2→4→3)", false, 1)]
        public static void RunAll()
        {
            if (!ConfirmScene()) return;
            UnpackRootPrefab();
            StripDesktopObjects();
            EnsureRequiredActive();
            RemoveMissingScripts();
            ReportSceneState();
        }

        // =========================================================
        // 유틸
        // =========================================================
        private static bool ConfirmScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name == ExpectedSceneName) return true;

            return EditorUtility.DisplayDialog(
                "씬 확인",
                $"현재 씬은 '{scene.name}'입니다.\n" +
                $"이 도구는 '{ExpectedSceneName}' 전용입니다.\n\n" +
                "데스크톱 씬에서 실행하면 되돌리기 어려운 손상이 발생할 수 있습니다.\n\n" +
                "그래도 진행하시겠습니까?",
                "진행", "취소");
        }

        /// 이름으로 오브젝트를 찾는다. parentHint가 있으면 조상 중에 그 이름이 있는 것만 반환한다.
        private static GameObject FindByName(string name, string parentHint)
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var t in all)
            {
                if (t.name != name) continue;
                if (string.IsNullOrEmpty(parentHint)) return t.gameObject;

                // 조상 중에 parentHint 이름이 있는지
                Transform p = t.parent;
                while (p != null)
                {
                    if (p.name == parentHint) return t.gameObject;
                    p = p.parent;
                }
            }
            return null;
        }

        private static string GetPath(GameObject go)
        {
            var parts = new List<string>();
            Transform t = go.transform;
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static int CountMissingScripts()
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[MRSceneSetup] 씬이 변경됨으로 표시되었습니다. Ctrl+S로 저장하세요.");
        }
    }
}
