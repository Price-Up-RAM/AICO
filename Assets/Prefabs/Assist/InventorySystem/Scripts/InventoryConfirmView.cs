using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 수량 선택 모달 (스토어 간 이동용). Store의 StoreConfirmView와 같은 UX 문법이지만
// 독립성 원칙(InventorySystem은 EquipSystem 외 무의존)에 따라 참조 없이 자체 구현.
// InventoryMenu 관례를 따른다: 코드 런타임 생성, 백드롭 클릭 = 취소, 동시 1개, 폰트는 뷰에서 상속.
// 키: Enter = 확정, Esc = 취소. 기본값 = 전량(max) — Enter 한 번이면 기존 "통째 이동"과 동일.
public class InventoryConfirmView : MonoBehaviour
{
    private static InventoryConfirmView current;  // 동시 1개

    private Action<int> onConfirm;
    private int quantity;
    private int maxQty;
    private TMP_Text countText;

    // 모달 열기: max 수량과 기본값(전량)으로 초기화, 확정 시 콜백으로 수량 전달
    public static void Show(Canvas canvas, string title, int max, TMP_FontAsset font, Action<int> onConfirm)
    {
        if (canvas == null || max <= 0)
        {
            return;
        }

        Close();

        GameObject root = new GameObject("InventoryConfirmView", typeof(RectTransform));
        root.layer = 5;  // UI 레이어 (InventoryMenu 관례 — ScreenSpace-Camera/World 캔버스에서도 렌더되게)
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;
        root.transform.SetAsLastSibling();

        InventoryConfirmView view = root.AddComponent<InventoryConfirmView>();
        current = view;
        view.onConfirm = onConfirm;
        view.maxQty = max;
        view.quantity = max;  // 기본값 = 전량
        view.Build(title, font);
    }

    // 열려 있는 모달 닫기 (확정 없이)
    public static void Close()
    {
        if (current != null)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    private void Update()
    {
        // 키보드: Enter = 확정, Esc = 취소
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Confirm();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }
    }

    private void Confirm()
    {
        Action<int> callback = onConfirm;
        onConfirm = null;  // 같은 프레임 이중 Confirm(Submit+폴링) 방어 — 콜백은 정확히 1회
        int qty = quantity;
        Close();
        callback?.Invoke(qty);
    }

    private void SetQuantity(int value)
    {
        quantity = Mathf.Clamp(value, 1, maxQty);
        if (countText != null)
        {
            countText.text = $"{quantity} / {maxQty}";
        }
        // 방금 클릭한 버튼이 EventSystem 선택 상태로 남으면 Enter/Space(Submit 축)가 그 버튼을
        // 재발화시켜 수량이 어긋난다 — 선택 해제로 Submit 경로 차단
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // ── 런타임 UI 구성 (다크 테마 — InventoryMenu와 동일 감각) ──
    private void Build(string title, TMP_FontAsset font)
    {
        // 백드롭: 화면 전체 반투명 + 클릭 = 취소
        Image backdrop = gameObject.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);
        Button backdropBtn = gameObject.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(() => Close());

        // 본체 박스 (중앙 고정)
        RectTransform box = MakeRect("Box", transform, new Vector2(380f, 200f));
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = new Color(0.13f, 0.13f, 0.15f, 0.98f);
        // 박스 클릭이 백드롭(취소)으로 새지 않게 막는다
        box.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

        // 타이틀
        TMP_Text titleText = MakeText("Title", box, title, 22f, font);
        Place(titleText.rectTransform, new Vector2(0f, 62f), new Vector2(340f, 34f));

        // 수량 행: [−] [n / max] [+]
        Button minusBtn = MakeButton("Minus", box, "−", 26f, font, new Vector2(60f, 44f));
        Place((RectTransform)minusBtn.transform, new Vector2(-110f, 8f), new Vector2(60f, 44f));
        minusBtn.onClick.AddListener(() => SetQuantity(quantity - 1));

        countText = MakeText("Count", box, "", 24f, font);
        Place(countText.rectTransform, new Vector2(0f, 8f), new Vector2(140f, 44f));

        Button plusBtn = MakeButton("Plus", box, "+", 26f, font, new Vector2(60f, 44f));
        Place((RectTransform)plusBtn.transform, new Vector2(110f, 8f), new Vector2(60f, 44f));
        plusBtn.onClick.AddListener(() => SetQuantity(quantity + 1));

        // 하단 버튼 행: [취소] [이동]
        Button cancelBtn = MakeButton("Cancel", box, TranslateUi("취소"), 20f, font, new Vector2(150f, 42f));
        Place((RectTransform)cancelBtn.transform, new Vector2(-82f, -62f), new Vector2(150f, 42f));
        cancelBtn.onClick.AddListener(() => Close());

        Button okBtn = MakeButton("Ok", box, TranslateUi("이동"), 20f, font, new Vector2(150f, 42f));
        Place((RectTransform)okBtn.transform, new Vector2(82f, -62f), new Vector2(150f, 42f));
        okBtn.onClick.AddListener(() => Confirm());

        SetQuantity(quantity);
    }

    // ── 소형 UI 헬퍼 (고정 앵커 — 레이아웃 그룹 미사용 관례) ──

    private static RectTransform MakeRect(string name, Transform parent, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;  // UI 레이어
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        return rt;
    }

    private static void Place(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private static TMP_Text MakeText(string name, Transform parent, string text, float size, TMP_FontAsset font)
    {
        RectTransform rt = MakeRect(name, parent, new Vector2(100f, 30f));
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null)
        {
            tmp.font = font;
        }
        return tmp;
    }

    private static Button MakeButton(string name, Transform parent, string label, float fontSize, TMP_FontAsset font, Vector2 size)
    {
        RectTransform rt = MakeRect(name, parent, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.24f, 0.24f, 0.28f, 1f);
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        TMP_Text txt = MakeText("Label", rt, label, fontSize, font);
        RectTransform txtRt = txt.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        return btn;
    }

    private static string TranslateUi(string text)
    {
        if (string.IsNullOrEmpty(text) || SettingManager.Instance == null ||
            SettingManager.Instance.settings == null ||
            string.IsNullOrEmpty(SettingManager.Instance.settings.ui_language))
        {
            return text;
        }

        return LanguageDataInventory.Translate(text, SettingManager.Instance.settings.ui_language);
    }
}
