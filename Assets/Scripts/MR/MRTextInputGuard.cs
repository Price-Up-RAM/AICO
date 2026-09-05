// 말풍선 입력창이 손 포크만 닿아도 포커스를 먹어 Quest 시스템 키보드/IME 동기화를
// 트리거하는 것을 막는다.
//
// 실측(Quest 3S, 2026-08-10): Image_ChatBalloon을 포크하면 TMP_InputField.LateUpdate()
// 하나가 프레임당 최대 223ms(self)를 먹었다 — 안드로이드 IME 인셋/키보드 상태 동기화 비용으로
// 보인다. requiresSystemKeyboard 매니페스트 플래그(§4-16)를 켜도 이 프레임 비용 자체는
// 줄지 않았다 — 포커스가 걸리는 한 계속 발생한다.
//
// MR에서는 음성이 1급 입력이고(Feature Inventory §2), 텍스트 키보드 UX는 아직 설계되지
// 않았다(Phase 5). 그때까지는 InputField가 "표시는 되지만 손으로 눌러도 포커스가 안 걸리는"
// 상태가 맞다 — STT 결과 등 프로그램적으로 text를 채우는 것(ChatBalloonManager.
// AppendSTTTextToInputField)은 interactable과 무관하게 계속 동작한다.
//
// 사용: Image_ChatBalloon(및 이후 변환하는 다른 말풍선/패널)에 이 컴포넌트를 추가하면
// 자식의 TMP_InputField를 자동으로 찾아 막는다. 별도 배선 불필요.
// Phase 5에서 실제 텍스트 입력 UX가 나오면 AllowInteraction(true)로 되돌리면 된다.
//
// 실기 검증(Quest 3S, 2026-08-11): interactable=false만으로는 막히지 않았다 —
// ISDK PokeInteractable의 select 경로가 Selectable.interactable 체크를 거치지 않고
// TMP_InputField.OnSelect() -> ActivateInputField()를 바로 호출하는 것으로 보인다
// (로그상 interactable=False 설정 후에도 8초 뒤 시스템 키보드가 열림 확인).
// 그래서 raycastTarget 자체를 꺼서 포인터/셀렉트 이벤트가 아예 도달하지 않게 막는다.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MRTextInputGuard : MonoBehaviour
{
    // 2026-08-19 결정 — 기본값을 **끔**으로 바꿨다 (MR_Phase4A_SystemMenu_Design.md §4).
    //
    // 알림·설정 등 다른 패널에도 TMP_InputField가 있어 어차피 키보드가 뜨고,
    // Phase 5에서 키보드 UX를 통째로 교체할 예정이라 지금 막을 실익이 없다는 판단이다.
    //
    // ⚠ 감수하는 비용: 실측(Quest 3S, 2026-08-10)에서 TMP_InputField.LateUpdate() 하나가
    // 프레임당 최대 223 ms(self)를 먹었다. PlayerLoop 예산 13.89 ms의 16배다.
    // **입력창에 포커스가 걸려 있는 동안은 성능 측정이 무의미하다** — 성능을 잴 때는
    // 입력창을 건드리지 않은 상태에서 잰다.
    //
    // ⚠ 인스펙터에서 이 컴포넌트의 체크를 끄는 것으로는 차단이 안 꺼진다 —
    // Awake()는 컴포넌트를 비활성화해도 실행되기 때문이다(Kickoff Guide §4-2).
    // 그래서 필드 가드로 만들었다. 되살리려면 이 값을 켜면 된다.
    [Tooltip("켜면 TMP_InputField의 raycastTarget을 꺼서 시스템 키보드 호출을 차단한다. " +
             "2026-08-19부터 기본 꺼짐 — Phase 5의 텍스트 입력 UX까지 그대로 둔다.")]
    [SerializeField] private bool blockTextInput = false;

    [Tooltip("비워두면 자식에서 자동으로 찾는다.")]
    [SerializeField] private TMP_InputField[] targetFields;

    private readonly Dictionary<Graphic, bool> _originalRaycastTarget = new Dictionary<Graphic, bool>();

    // 계측(Phase 5): 키보드 입력이 막히는 지점을 가른다.
    // blockTextInput이 꺼져 있어도 다른 이유로 포커스가 안 잡힐 수 있다 —
    // '차단이 꺼져 있다'와 '입력이 된다'는 다른 사실이다 (Kickoff Guide 4-58).
    private TMP_InputField _diagLastFocused;

    private void Start()
    {
        TMP_InputField[] fields = targetFields;
        if (fields == null || fields.Length == 0)
        {
            fields = GetComponentsInChildren<TMP_InputField>(true);
        }

        Debug.Log($"[MRInput/진단] blockTextInput={blockTextInput} | 대상 InputField {fields.Length}개 | 오브젝트='{gameObject.name}'");

        for (int i = 0; i < fields.Length; i++)
        {
            TMP_InputField f = fields[i];
            if (f == null)
            {
                continue;
            }

            Graphic g = f.GetComponent<Graphic>();
            string raycast = "(Graphic없음)";
            if (g != null)
            {
                raycast = g.raycastTarget.ToString();
            }

            Debug.Log($"[MRInput/진단] '{f.gameObject.name}' interactable={f.interactable} readOnly={f.readOnly} raycastTarget={raycast} 활성={f.gameObject.activeInHierarchy} shouldHideSoftKeyboard={f.shouldHideSoftKeyboard} 부모활성={f.transform.parent != null && f.transform.parent.gameObject.activeInHierarchy}");
        }
    }

    private void Update()
    {
        // 포커스가 잡히는 순간만 찍는다 (매 프레임 찍으면 로그가 묻힌다).
        TMP_InputField focused = null;
        TMP_InputField[] fields = targetFields;
        if (fields == null || fields.Length == 0)
        {
            return;
        }

        for (int i = 0; i < fields.Length; i++)
        {
            if (fields[i] != null && fields[i].isFocused)
            {
                focused = fields[i];
                break;
            }
        }

        if (focused == _diagLastFocused)
        {
            return;
        }

        _diagLastFocused = focused;
        if (focused != null)
        {
            Debug.Log($"[MRInput/진단] 포커스 획득: '{focused.gameObject.name}' text='{focused.text}'");
        }
        else
        {
            Debug.Log("[MRInput/진단] 포커스 해제");
        }
    }

    private void Awake()
    {
        if (!blockTextInput) return;

        if (targetFields == null || targetFields.Length == 0)
        {
            targetFields = GetComponentsInChildren<TMP_InputField>(true);
        }

        AllowInteraction(false);
    }

    // 이 오브젝트는 시작 시 비활성화되어 있다가 나중에 SetActive(true)로 켜지는 구조라,
    // 다른 초기화 코드가 Awake 이후에 다시 켜버릴 가능성을 대비해
    // 활성화될 때마다 한 번 더 강제로 막는다.
    private void OnEnable()
    {
        if (!blockTextInput) return;

        AllowInteraction(false);
    }

    /// <summary>Phase 5에서 실제 MR 텍스트 입력 UX가 준비되면 true로 호출해 되돌린다.</summary>
    public void AllowInteraction(bool allow)
    {
        int count = 0;
        foreach (var f in targetFields)
        {
            if (f == null) continue;

            // interactable도 같이 맞춰둔다 (프로그램적 텍스트 조작 안전장치 겸,
            // 표준 마우스/에디터 클릭 경로에 대한 대비).
            f.interactable = allow;

            // 실제로 막는 건 이쪽: 배경/텍스트/플레이스홀더 등 모든 Graphic의
            // raycastTarget을 꺼서 포크/레이가 아예 히트하지 못하게 한다.
            var graphics = f.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                if (!_originalRaycastTarget.ContainsKey(g))
                {
                    _originalRaycastTarget[g] = g.raycastTarget;
                }

                g.raycastTarget = allow ? _originalRaycastTarget[g] : false;
            }

            count++;
        }
        Debug.Log($"[MRTextInputGuard] {name}: {count}개 필드 raycast 차단={!allow}");
    }
}
