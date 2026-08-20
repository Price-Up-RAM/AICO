// 시스템 메뉴 패널(MR/WorldUI/Panels/SystemMenu)을 통째로 생성하고 배선하는 도구.
// MR_Phase4A_Input_Plan.md §5-3의 유일한 잔여 작업이다.
//
// 왜 도구로 만드는가
// ----------------
// MRSystemMenuController는 인스펙터 참조가 16개다(슬라이더 5 + 값 텍스트 4 + 버튼 5 + 참조 2).
// 손으로 만들면 오브젝트 30여 개를 배치하고 16줄을 드래그로 꽂아야 하는데, 하나만 빠져도
// 증상이 "그 항목만 조용히 안 먹는다"로 나타나 실기에서 찾기가 매우 어렵다.
// 슬라이더 범위·기본값도 코드(PlayerPrefs 기본값)와 손으로 맞춰야 해서 어긋나기 쉽다.
// 여기서 한 번에 만들면 재실행으로 언제든 같은 결과를 얻는다.
//
// 이 도구가 하는 일
//   ① MR/WorldUI/Panels 아래에 SystemMenu 계층을 생성 (배경·제목·구역·슬라이더·버튼)
//   ② MRFloatingPanel + MRSystemMenuController 부착
//   ③ 16개 참조를 SerializedObject로 전부 연결 (private 필드라 세터가 없다)
//   ④ 씬의 MRIntentRouter.systemMenu 까지 연결
//   ⑤ 기존 도구 6 → 5 → 8 을 순서대로 호출 (Tools 9와 같은 순서)
//
// 이 도구가 하지 않는 일
//   - 계층 이동. Tools 9는 패널을 WorldUI **직속**으로 끌어내지만(§4-18 회피 목적),
//     이 패널은 처음부터 Canvas 밖인 Panels 아래에 생기므로 옮길 이유가 없다.
//     그래서 9를 부르지 않고 9가 부르는 6/5/8을 같은 순서로 직접 호출한다.
//   - 스케일 변경. 아래 PanelScale(0.001)을 6번 실행 **전에** 넣어두고, 6번이 덮어쓴 값을
//     같은 값으로 되돌린다. §4-43("9는 6이 설정한 스케일을 되돌린다")과 같은 처리다.
//
// 알아둘 것
//   - GameObject는 **활성 상태로 둔다.** MRIntentRouter가 FindFirstObjectByType으로 찾는데
//     이 API는 비활성 오브젝트를 반환하지 않는다(§5-2의 7개 패널과 다른 이유). 닫힌 상태는
//     MRFloatingPanel.Awake()가 panelCanvas.enabled = false로 만든다 — 활성 12개와 같은 방식이다.
//   - 폰트는 씬에 이미 있는 TMP_Text에서 그대로 물려받는다. 한글 폰트를 GUID로 박아두면
//     프로젝트가 폰트를 바꿀 때 이 도구만 옛 폰트를 계속 쓴다.
//
// 사용: Tools → MR → 10. 시스템 메뉴 패널 생성

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AICO.MR.EditorTools
{
    public static class MRSystemMenuBuilder
    {
        private const string MenuRoot = "Tools/MR/";
        private const string PanelName = "SystemMenu";
        private const string WorldUIName = "WorldUI";
        private const string PanelsGroupName = "Panels";

        // 확정 캔버스 레시피 — 1 canvas px = 1 mm (Kickoff Guide §5 "확정된 월드 스페이스 레시피")
        private const float PanelScale = 0.001f;

        // 패널 가로 640 px = 0.64 m. 기존 활성 패널대와 같은 결이다(§1-2).
        // 세로는 상수가 아니다 — 아래 "조용히 죽는 컨트롤" 처리 때문에 구역이 빠질 수 있어서,
        // 내용을 다 쌓은 뒤 실제 높이로 rect를 줄인다. 안 그러면 빈 패널 아래쪽이 그대로
        // 잡기 판(Tools 8)이 되어 아무것도 없는 공간이 레이를 먹는다.
        private const float PanelWidth = 640f;
        private const float Pad = 32f;
        private const float ContentWidth = PanelWidth - Pad * 2f;

        // 행 치수
        private const float RowHeight = 56f;
        private const float SectionHeight = 40f;
        private const float LabelWidth = 150f;
        private const float ValueWidth = 72f;
        private const float Gap = 12f;

        // 색
        private static readonly Color PanelBg = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        private static readonly Color TitleColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color SectionColor = new Color(0.55f, 0.75f, 1f, 1f);
        private static readonly Color LabelColor = new Color(0.9f, 0.9f, 0.92f, 1f);
        private static readonly Color SliderBg = new Color(0.2f, 0.22f, 0.26f, 1f);
        private static readonly Color SliderFill = new Color(0.35f, 0.62f, 1f, 1f);
        private static readonly Color HandleColor = new Color(0.95f, 0.96f, 1f, 1f);
        private static readonly Color ButtonBg = new Color(0.18f, 0.21f, 0.28f, 1f);
        private static readonly Color ExitBg = new Color(0.55f, 0.2f, 0.22f, 1f);

        // 도구가 만든 오브젝트를 배선 단계에서 찾기 위한 임시 보관소
        private static readonly Dictionary<string, GameObject> _made = new Dictionary<string, GameObject>();
        private static TMP_FontAsset _font;

        // 대상 컴포넌트가 씬에 있을 때만 그 구역을 만든다 — 아래 CreateHierarchy 주석 참고.
        private static bool _hasJukebox;
        private static bool _hasAnchorEditor;

        [MenuItem(MenuRoot + "10. 시스템 메뉴 패널 생성", false, 109)]
        public static void BuildSystemMenu()
        {
            Transform parent = FindPanelsParent();
            if (parent == null)
            {
                EditorUtility.DisplayDialog("WorldUI 없음",
                    $"씬에서 '{WorldUIName}' 오브젝트를 찾지 못했습니다.\n" +
                    "MR > WorldUI 계층이 있는 씬(SampleSceneKAI-MR)인지 확인하세요.", "확인");
                return;
            }

            Transform existing = parent.Find(PanelName);
            if (existing != null)
            {
                bool rebuild = EditorUtility.DisplayDialog("이미 있음",
                    $"'{PanelName}'이 이미 있습니다. 지우고 다시 만들까요?\n" +
                    "(손으로 조정한 위치·크기·색은 전부 사라집니다)", "다시 만들기", "취소");
                if (!rebuild)
                {
                    return;
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            _made.Clear();
            _font = FindSceneFont();
            _hasJukebox = Object.FindFirstObjectByType<MRJukebox>() != null;
            _hasAnchorEditor = Object.FindFirstObjectByType<MRSpatialAnchorEditor>() != null;

            var log = new StringBuilder("[MRSystemMenuBuilder] 시스템 메뉴 패널 생성\n");
            if (_font != null)
            {
                log.AppendLine($"  · 폰트: {_font.name} (씬에서 물려받음)");
            }
            else
            {
                log.AppendLine("  ⚠ 씬에서 TMP 폰트를 찾지 못해 TMP 기본 폰트를 씁니다 — 한글이 깨지면 폰트를 바꾸세요.");
            }

            GameObject panel = CreateHierarchy(parent, log);

            // 6번이 스케일을 덮어쓰므로 원하는 최종값을 먼저 넣어두고, 6번 뒤에 되돌린다 (§4-43).
            panel.transform.localScale = Vector3.one * PanelScale;

            WireController(panel, log);

            // Tools 9와 같은 순서: 6(플로팅 패널) → 5(손 상호작용) → 8(잡기).
            // 계층 이동만 빠졌다 — 이 패널은 이미 Canvas 밖에 있다(§4-18).
            Object[] previousSelection = Selection.objects;
            Selection.objects = new Object[] { panel };

            log.AppendLine("\n  ── Tools 6 (플로팅 패널 변환) ──");
            MRFloatingPanelSetup.ConvertSelectionToFloatingPanel();

            var rt = panel.transform as RectTransform;
            if (rt != null && rt.localScale != Vector3.one * PanelScale)
            {
                Undo.RecordObject(rt, "Restore SystemMenu Scale");
                rt.localScale = Vector3.one * PanelScale;
                log.AppendLine($"      · 스케일 복원 → {PanelScale} (6번이 바꾼 것을 되돌림)");
            }

            log.AppendLine("  ── Tools 5 (손 상호작용) ──");
            MRWorldUIInteraction.AddInteractionToSelectedCanvas();

            log.AppendLine("  ── Tools 8 (잡기) ──");
            MRPanelGrabSetup.AddGrabToSelectedPanels();

            Selection.objects = previousSelection;

            bool routerWired = WireIntentRouter(panel, log);
            ParkPanelInactive(panel, routerWired, log);

            log.AppendLine("\n완료. 남은 확인:");
            log.AppendLine("  1) Play 후 콘솔에 [MRSystemMenuController] 관련 경고가 없는지");
            log.AppendLine("  2) 빈 공간 + 왼손 palm-up + 탭 → 패널이 눈앞에 뜨는지");
            log.AppendLine("  3) 슬라이더 값이 PlayerPrefs에 남고 재시작 후 복원되는지");
            log.AppendLine("  4) 패널을 닫은 자리에 레이를 쏘면 통과하는지 (§8-7)");
            Debug.Log(log.ToString());

            Selection.activeGameObject = panel;
            EditorGUIUtility.PingObject(panel);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // 패널 계층을 위에서 아래로 쌓는다. y는 패널 위쪽부터 내려가는 커서다.
        private static GameObject CreateHierarchy(Transform parent, StringBuilder log)
        {
            GameObject panel = new GameObject(PanelName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panel, "Create SystemMenu");
            panel.transform.SetParent(parent, false);

            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PanelWidth, 100f); // 마지막에 실제 내용 높이로 줄인다
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.localRotation = Quaternion.identity;

            // 배경 — 패널 전체를 채운다. 잡기 띠(Tools 8)가 이 rect를 기준으로 계산된다.
            GameObject bg = CreateChild("Background", panel.transform, 0f, 0f, PanelWidth, 100f);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.sprite = Builtin("UI/Skin/Background.psd");
            bgImage.type = Image.Type.Sliced;
            bgImage.color = PanelBg;

            float y = Pad;

            // ---- 제목 ----
            MakeText("Title", panel.transform, "시스템 메뉴", 34f, TitleColor,
                     TextAlignmentOptions.Left, Pad, y, ContentWidth, 48f);
            y += 48f + 12f;

            // ---- 주크박스 ----
            //
            // MRJukebox가 씬에 없으면 이 줄을 아예 만들지 않는다.
            // MRSystemMenuController.OnVolumeChanged는 `if (MRJukebox.Instance != null)`이라
            // 슬라이더를 만들어두면 **끌리기는 하는데 아무 일도 안 일어난다** — 실기에서
            // "고장"과 구별되지 않는 가장 나쁜 형태다. 없는 기능은 아예 그리지 않는다.
            if (_hasJukebox)
            {
                y = MakeSection(panel.transform, "주크박스", y);
                MakeSliderRow(panel.transform, "Row_Volume", "볼륨", 0f, 1f, 1f, false, y);
                y += RowHeight + 8f;
            }
            else
            {
                log.AppendLine("  ⚠ 씬에 MRJukebox가 없어 **볼륨 슬라이더를 만들지 않았습니다.**");
                log.AppendLine("      MRSampleScene의 'JukeBox' 오브젝트를 이 씬으로 가져온 뒤 다시 실행하세요.");
            }

            // ---- 공간 앵커 (MRSpatialAnchorEditor) ----
            //
            // 같은 이유로 조건부다. 버튼 4개는 전부 `spatialAnchorEditor?.Method()`라
            // 참조가 비면 눌러도 조용히 아무 일이 없다.
            // 2026-08-19 결정: 앵커는 Phase 2로 보류한다(인벤토리에 "버그 있음", §2-3에서
            // 앵커 생성 경로를 일부러 이식하지 않았다). 지우지 않고 조건부로 두는 이유는
            // Phase 2에서 에디터가 씬에 들어오면 **이 도구 재실행만으로 되살아나게** 하기 위해서다.
            if (_hasAnchorEditor)
            {
                y = MakeSection(panel.transform, "공간 앵커", y);
                float halfWidth = (ContentWidth - Gap) * 0.5f;
                MakeButton("Btn_Rescan", panel.transform, "방 재스캔", ButtonBg,
                           Pad, y, halfWidth, RowHeight - 8f);
                MakeButton("Btn_RebuildEffectMesh", panel.transform, "이펙트 메시 재생성", ButtonBg,
                           Pad + halfWidth + Gap, y, halfWidth, RowHeight - 8f);
                y += RowHeight - 8f + Gap;
                MakeButton("Btn_ToggleEditMode", panel.transform, "앵커 편집 모드", ButtonBg,
                           Pad, y, halfWidth, RowHeight - 8f);
                MakeButton("Btn_ResetAnchors", panel.transform, "앵커 초기화", ButtonBg,
                           Pad + halfWidth + Gap, y, halfWidth, RowHeight - 8f);
                y += RowHeight - 8f + 8f;
            }
            else
            {
                log.AppendLine("  · 씬에 MRSpatialAnchorEditor가 없어 '공간 앵커' 구역을 건너뜁니다 " +
                               "(Phase 2로 보류, 2026-08-19 결정). 에디터가 씬에 들어오면 이 도구를 " +
                               "다시 실행하면 구역이 생깁니다.");
            }

            // ---- 캐릭터 크기 ----
            // 범위 0.5~2.0. SetSizeMultiplier의 하한이 0.01이라 아래로 더 내려도 안전하지만,
            // 실사용에서 캐릭터가 점이 되는 값은 쓸 일이 없다.
            y = MakeSection(panel.transform, "캐릭터", y);
            MakeSliderRow(panel.transform, "Row_Size", "크기", 0.5f, 2f, 1f, true, y);
            y += RowHeight + 8f;

            // ---- Idle 확률/주기 ----
            // 기본값은 MRSpineCharacterController의 직렬화 기본값과 같다 (1 / 1 / 5).
            y = MakeSection(panel.transform, "Idle 재추첨", y);
            MakeSliderRow(panel.transform, "Row_IdleDelay", "음성 후 지연", 0f, 5f, 1f, true, y);
            y += RowHeight;
            MakeSliderRow(panel.transform, "Row_IdleChance", "재추첨 확률", 0f, 1f, 1f, true, y);
            y += RowHeight;
            MakeSliderRow(panel.transform, "Row_IdleInterval", "재추첨 주기", 1f, 30f, 5f, true, y);
            y += RowHeight + 16f;

            // ---- 종료 ----
            MakeButton("Btn_Exit", panel.transform, "종료", ExitBg, Pad, y, ContentWidth, 64f);
            y += 64f + Pad;

            // 실제 내용 높이로 패널을 줄인다. 구역이 빠졌을 때 남는 빈 공간이
            // 그대로 잡기 판·판정 면이 되는 것을 막는다 (Tools 5/8이 이 rect를 읽는다).
            panelRt.sizeDelta = new Vector2(PanelWidth, y);
            log.AppendLine($"  · 패널 {PanelWidth}x{y}px (= {PanelWidth * PanelScale:0.000} x {y * PanelScale:0.000} m)");

            return panel;
        }

        // 구역 제목을 찍고 다음 커서 위치를 돌려준다.
        private static float MakeSection(Transform parent, string title, float y)
        {
            MakeText("Section_" + title, parent, title, 24f, SectionColor,
                     TextAlignmentOptions.Left, Pad, y, ContentWidth, SectionHeight);
            return y + SectionHeight;
        }

        // 라벨 + 슬라이더 (+ 값 텍스트) 한 줄. withValue가 false면 값 텍스트를 만들지 않는다 —
        // 볼륨에는 대응하는 TMP_Text 필드가 컨트롤러에 없어서 만들면 배선되지 않은 채 남는다.
        private static void MakeSliderRow(Transform parent, string rowName, string label,
                                          float min, float max, float value, bool withValue, float y)
        {
            GameObject row = CreateChild(rowName, parent, Pad, y, ContentWidth, RowHeight);
            Transform t = row.transform;

            MakeText("Label", t, label, 22f, LabelColor,
                     TextAlignmentOptions.Left, 0f, (RowHeight - 32f) * 0.5f, LabelWidth, 32f);

            float sliderX = LabelWidth + Gap;
            float sliderWidth = ContentWidth - LabelWidth - Gap;
            if (withValue)
            {
                sliderWidth -= ValueWidth + Gap;
            }

            GameObject slider = MakeSlider("Slider", t, min, max, value,
                                           sliderX, (RowHeight - 28f) * 0.5f, sliderWidth, 28f);
            _made[rowName + "/Slider"] = slider;

            if (withValue)
            {
                GameObject valueText = MakeText("Value", t, "-", 22f, LabelColor,
                                                TextAlignmentOptions.Right,
                                                ContentWidth - ValueWidth, (RowHeight - 32f) * 0.5f,
                                                ValueWidth, 32f);
                _made[rowName + "/Value"] = valueText;
            }
        }

        private static GameObject MakeSlider(string name, Transform parent, float min, float max,
                                             float value, float x, float y, float w, float h)
        {
            GameObject root = CreateChild(name, parent, x, y, w, h);
            var slider = root.AddComponent<Slider>();

            GameObject bg = CreateChild("Background", root.transform, 0f, 0f, w, h);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.25f);
            bgRt.anchorMax = new Vector2(1f, 0.75f);
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var bgImage = bg.AddComponent<Image>();
            bgImage.sprite = Builtin("UI/Skin/Background.psd");
            bgImage.type = Image.Type.Sliced;
            bgImage.color = SliderBg;

            GameObject fillArea = CreateChild("Fill Area", root.transform, 0f, 0f, w, h);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.offsetMin = new Vector2(10f, 0f);
            fillAreaRt.offsetMax = new Vector2(-10f, 0f);

            GameObject fill = CreateChild("Fill", fillArea.transform, 0f, 0f, 10f, h);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImage = fill.AddComponent<Image>();
            fillImage.sprite = Builtin("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Sliced;
            fillImage.color = SliderFill;

            GameObject handleArea = CreateChild("Handle Slide Area", root.transform, 0f, 0f, w, h);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(10f, 0f);
            handleAreaRt.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = CreateChild("Handle", handleArea.transform, 0f, 0f, 24f, h);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(0f, 1f);
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;
            handleRt.sizeDelta = new Vector2(24f, 0f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.sprite = Builtin("UI/Skin/Knob.psd");
            handleImage.color = HandleColor;

            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.value = value;

            return root;
        }

        private static GameObject MakeButton(string name, Transform parent, string label, Color bg,
                                             float x, float y, float w, float h)
        {
            GameObject root = CreateChild(name, parent, x, y, w, h);
            var image = root.AddComponent<Image>();
            image.sprite = Builtin("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = bg;

            var button = root.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject text = MakeText("Text", root.transform, label, 22f, Color.white,
                                       TextAlignmentOptions.Center, 0f, 0f, w, h);
            var textRt = text.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 0f);
            textRt.offsetMax = new Vector2(-8f, 0f);

            _made[name] = root;
            return root;
        }

        private static GameObject MakeText(string name, Transform parent, string content, float size,
                                           Color color, TextAlignmentOptions align,
                                           float x, float y, float w, float h)
        {
            GameObject go = CreateChild(name, parent, x, y, w, h);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null)
            {
                text.font = _font;
            }
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            // 텍스트는 절대 레이를 먹지 않는다. 먹으면 뒤의 슬라이더/버튼이 안 눌린다.
            text.raycastTarget = false;
            return go;
        }

        // 좌상단 원점(x는 오른쪽, y는 아래쪽이 +)으로 배치한다. 계산이 쌓기 순서와 같아진다.
        private static GameObject CreateChild(string name, Transform parent, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            return go;
        }

        // 16개 인스펙터 참조를 채운다. 전부 private이라 SerializedObject로 간다.
        private static void WireController(GameObject panel, StringBuilder log)
        {
            var controller = Undo.AddComponent<MRSystemMenuController>(panel);
            var so = new SerializedObject(controller);

            int wired = 0;
            if (_hasJukebox)
            {
                wired += Set(so, "volumeSlider", Comp<Slider>("Row_Volume/Slider"));
            }

            if (_hasAnchorEditor)
            {
                wired += Set(so, "spatialAnchorEditor", Object.FindFirstObjectByType<MRSpatialAnchorEditor>());
                wired += Set(so, "rescanButton", Comp<Button>("Btn_Rescan"));
                wired += Set(so, "rebuildEffectMeshButton", Comp<Button>("Btn_RebuildEffectMesh"));
                wired += Set(so, "toggleEditModeButton", Comp<Button>("Btn_ToggleEditMode"));
                wired += Set(so, "resetAnchorsButton", Comp<Button>("Btn_ResetAnchors"));
            }

            wired += Set(so, "characterWorldRoot", Object.FindFirstObjectByType<MRCharacterWorldRoot>());
            wired += Set(so, "sizeSlider", Comp<Slider>("Row_Size/Slider"));
            wired += Set(so, "sizeValueText", Comp<TMP_Text>("Row_Size/Value"));

            wired += Set(so, "idleDelaySlider", Comp<Slider>("Row_IdleDelay/Slider"));
            wired += Set(so, "idleDelayValueText", Comp<TMP_Text>("Row_IdleDelay/Value"));
            wired += Set(so, "idleChanceSlider", Comp<Slider>("Row_IdleChance/Slider"));
            wired += Set(so, "idleChanceValueText", Comp<TMP_Text>("Row_IdleChance/Value"));
            wired += Set(so, "idleIntervalSlider", Comp<Slider>("Row_IdleInterval/Slider"));
            wired += Set(so, "idleIntervalValueText", Comp<TMP_Text>("Row_IdleInterval/Value"));

            wired += Set(so, "exitButton", Comp<Button>("Btn_Exit"));

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // 기대치는 16 고정이 아니다. 씬에 대상이 없어 만들지 않은 구역만큼 빠진다 —
            // 볼륨 1개, 공간 앵커 5개(에디터 참조 + 버튼 4).
            int expected = 16;
            if (!_hasJukebox)
            {
                expected -= 1;
            }
            if (!_hasAnchorEditor)
            {
                expected -= 5;
            }

            log.AppendLine($"\n  · MRSystemMenuController 참조 {wired}/{expected} 연결 " +
                           $"(구역 제외분을 뺀 기대치. 전부 갖춰지면 16)");
            if (wired < expected)
            {
                log.AppendLine("      ⚠ 기대치에 못 미칩니다. 씬에 MRCharacterWorldRoot가 없으면 그 하나는 " +
                               "비는 것이 정상입니다 (컨트롤러 Awake가 런타임에 찾습니다). " +
                               "그 외에는 필드 이름이 바뀌었는지 확인하세요.");
            }
        }

        // MRIntentRouter가 시스템 메뉴를 여는 쪽.
        //
        // ResolveRefs()가 FindFirstObjectByType으로 스스로 찾긴 하지만, 그 API는 **비활성
        // 오브젝트를 반환하지 않는다.** 아래 ParkPanelInactive가 패널을 꺼진 채로 저장하므로,
        // 이 배선이 성공해야 런타임에 시스템 메뉴를 열 수 있다. 성공 여부를 돌려주는 이유다.
        private static bool WireIntentRouter(GameObject panel, StringBuilder log)
        {
            var router = Object.FindFirstObjectByType<MRIntentRouter>();
            if (router == null)
            {
                log.AppendLine("  ⚠ 씬에 MRIntentRouter가 없어 systemMenu 배선을 건너뜁니다.");
                return false;
            }

            var controller = panel.GetComponent<MRSystemMenuController>();
            var so = new SerializedObject(router);
            if (Set(so, "systemMenu", controller) > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(router);
                log.AppendLine("  · MRIntentRouter.systemMenu 연결");
                return true;
            }

            log.AppendLine("  ⚠ MRIntentRouter에 systemMenu 필드를 찾지 못했습니다.");
            return false;
        }

        // 패널을 GameObject 비활성으로 저장한다.
        //
        // 왜 — MRFloatingPanel.Awake()는 캔버스만 끄고 GameObject는 켠 채로 둔다. 그런데 Tools 8이
        // 붙인 GrabFrame의 BoxCollider와 Tools 5의 ISDK 판정 면은 **캔버스와 무관하게 살아 있다.**
        // 그대로 두면 앱 시작 직후부터 보이지 않는 판이 방 한가운데(Panels 원점)에서 레이를 먹는다 —
        // 설계서 §8-7의 "닫힌 위젯의 유령 히트박스"가 시작 시점 형태로 재현되는 것이다.
        // MRFloatingPanel.hideInteractionWhenTransparent는 CanvasGroup.alpha를 보는데 이 패널은
        // 가시성을 canvas.enabled로 관리하므로 그 보호가 걸리지 않는다.
        //
        // 비활성으로 두면 콜라이더째 사라진다. 여는 데도 지장이 없다 — MRFloatingPanel.Show()가
        // 제일 먼저 SetActive(true)를 하고, 그때 Awake/OnEnable이 정상적으로 발화한다.
        // 닫을 때도 Close()가 마지막에 SetActive(false)를 하므로 열기 전후 상태가 대칭이 된다.
        //
        // 단, 라우터 배선이 실패했다면 켜둔 채로 남긴다. 그 경우 런타임 FindFirstObjectByType이
        // 유일한 연결 수단인데 그 API는 비활성을 못 찾기 때문이다.
        private static void ParkPanelInactive(GameObject panel, bool routerWired, StringBuilder log)
        {
            if (!routerWired)
            {
                log.AppendLine("  ⚠ 라우터 배선이 안 돼 패널을 **활성**으로 남깁니다 " +
                               "(런타임 탐색이 비활성 오브젝트를 못 찾습니다). " +
                               "대신 시작 직후부터 잡기 판이 레이를 먹으니 배선을 먼저 고치세요.");
                return;
            }

            Undo.RecordObject(panel, "Deactivate SystemMenu");
            panel.SetActive(false);
            log.AppendLine("  · 패널을 GameObject 비활성으로 저장 (시작 시 유령 히트박스 방지, §8-7)");
        }

        private static int Set(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[MRSystemMenuBuilder] 필드 '{propertyName}'을 찾지 못했습니다 — 이름이 바뀌었는지 확인하세요.");
                return 0;
            }
            if (value == null)
            {
                return 0;
            }
            prop.objectReferenceValue = value;
            return 1;
        }

        private static T Comp<T>(string key) where T : Component
        {
            GameObject go;
            if (!_made.TryGetValue(key, out go))
            {
                Debug.LogWarning($"[MRSystemMenuBuilder] '{key}'을 만들지 못했습니다.");
                return null;
            }
            return go.GetComponent<T>();
        }

        // Panels 그룹을 우선하고, 없으면 WorldUI 직속에 만든다.
        private static Transform FindPanelsParent()
        {
            Transform worldUI = null;
            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == WorldUIName)
                {
                    worldUI = t;
                    break;
                }
            }

            if (worldUI == null)
            {
                return null;
            }

            Transform panels = worldUI.Find(PanelsGroupName);
            if (panels != null)
            {
                return panels;
            }
            return worldUI;
        }

        // 씬에 이미 쓰이고 있는 TMP 폰트를 그대로 쓴다 — 한글 폰트를 GUID로 박지 않기 위해서다.
        private static TMP_FontAsset FindSceneFont()
        {
            foreach (var text in Object.FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.font != null)
                {
                    return text.font;
                }
            }
            return TMP_Settings.defaultFontAsset;
        }

        private static Sprite Builtin(string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }
    }
}
