// 캐릭터 부착형 말풍선(8종)을 확정된 월드 스페이스 레시피로 변환하고 MRBalloonWorldFollow를 붙인다.
//
// 대상: Image_ChatBalloon(전환 완료·참조용), Image_AnswerBalloon, Image_AnswerBalloonSimple,
//      Image_AskBalloon, Image_NoticeBalloon, Image_SubAnswerBalloon, Image_SubChatBalloon,
//      Image_PortraitBalloonSimple, EmotionBalloon 프리팹
//
// 확정 레시피는 MRFloatingPanelSetup과 동일하다 (MR_Phase3-2_Canvas_Plan.md §7).
// 차이는 딱 하나 — MRFloatingPanel(드래그 가능한 고정 패널) 대신
// MRBalloonWorldFollow(캐릭터를 따라다니는 빌보드)를 붙인다는 것뿐이다.
//
// 사용: 하이어라키(또는 프로젝트 뷰의 프리팹)에서 말풍선 루트를 선택 →
//       Tools → MR → 7. 선택 오브젝트를 캐릭터 부착 말풍선으로 변환
// 이어서: Tools → MR → 5. 월드 UI에 손 상호작용 추가 (말풍선은 보통 손 상호작용이 필요 없지만
//         입력창이 있는 ChatBalloon류는 필요하다)

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Text;

namespace AICO.MR.EditorTools
{
    public static class MRBalloonWorldFollowSetup
    {
        private const string MenuRoot = "Tools/MR/";
        private const float RecipeCanvasScale = 0.001f;
        private const float RecipeDynamicPPU = 3f;

        [MenuItem(MenuRoot + "7. 선택 오브젝트를 캐릭터 부착 말풍선으로 변환", false, 106)]
        public static void ConvertSelectionToBalloon()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("선택 없음",
                    "하이어라키(또는 프로젝트 뷰)에서 말풍선 루트를 선택한 뒤 실행하세요.\n" +
                    "예: Image_AnswerBalloon, Image_AskBalloon, Image_SubChatBalloon", "확인");
                return;
            }

            var log = new StringBuilder("[MRBalloonWorldFollowSetup] 말풍선 → 월드 빌보드 변환\n");
            int done = 0;

            foreach (GameObject go in selection)
            {
                if (Convert(go, log)) done++;
            }

            log.AppendLine($"\n{done}개 처리 완료.");
            log.AppendLine("확인할 것: 캐릭터 매니저 스크립트(예: AnswerBalloonManager)의 characterTransform " +
                            "필드는 그대로 둬도 된다 — MRBalloonWorldFollow가 LateUpdate에서 최종 위치를 덮어쓴다.");
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

            var canvas = go.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = Undo.AddComponent<Canvas>(go);
                log.AppendLine("      + Canvas");
            }
            else log.AppendLine("      · Canvas (있음)");

            Undo.RecordObject(canvas, "Configure balloon canvas");
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            EditorUtility.SetDirty(canvas);

            var scaler = go.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = Undo.AddComponent<CanvasScaler>(go);
                log.AppendLine("      + CanvasScaler");
            }
            else log.AppendLine("      · CanvasScaler (있음)");
            Undo.RecordObject(scaler, "Configure balloon scaler");
            scaler.dynamicPixelsPerUnit = RecipeDynamicPPU;
            EditorUtility.SetDirty(scaler);

            var raycaster = go.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                Undo.AddComponent<UnityEngine.UI.GraphicRaycaster>(go);
                log.AppendLine("      + GraphicRaycaster");
            }
            else log.AppendLine("      · GraphicRaycaster (있음)");

            Undo.RecordObject(go.transform, "Set balloon canvas scale");
            if (!IsApprox(go.transform.localScale, RecipeCanvasScale))
            {
                log.AppendLine($"      · Scale {go.transform.localScale} → {RecipeCanvasScale}");
                go.transform.localScale = Vector3.one * RecipeCanvasScale;
            }
            else
            {
                log.AppendLine("      · Scale 이미 0.001 (변경 없음)");
            }

            var follow = go.GetComponent<MRBalloonWorldFollow>();
            if (follow == null)
            {
                Undo.AddComponent<MRBalloonWorldFollow>(go);
                log.AppendLine("      + MRBalloonWorldFollow (기본 오프셋 — Inspector에서 말풍선별로 조정 권장)");
            }
            else log.AppendLine("      · MRBalloonWorldFollow (있음)");

            return true;
        }

        private static bool IsApprox(Vector3 v, float target)
        {
            return Mathf.Abs(v.x - target) < 0.0001f &&
                   Mathf.Abs(v.y - target) < 0.0001f &&
                   Mathf.Abs(v.z - target) < 0.0001f;
        }
    }
}
