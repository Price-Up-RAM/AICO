using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 테스트 전용: 미션 카드를 "꾸욱 누르면(롱프레스)" 그 카드의 미션만 진행도 +1.
/// 최대치는 MissionManager.TestIncrement가 막는다.
///
/// 결합도 최소화: 같은 GameObject의 MissionCardRow에서 id만 읽고, MissionManager.TestIncrement만 호출.
/// 프리팹에 굽지 않고 MissionView가 런타임에만 AddComponent한다(enableTestPoke).
/// 제거하려면 이 파일 + MissionView의 AddComponent 한 줄만 지우면 된다.
/// </summary>
[RequireComponent(typeof(MissionCardRow))]
public class MissionTestPoke : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float holdSeconds = 0.4f;

    private MissionCardRow row;
    private bool pressing;
    private bool fired;
    private float pressStart;

    private void Awake()
    {
        row = GetComponent<MissionCardRow>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
        fired = false;
        pressStart = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
    }

    private void Update()
    {
        if (!pressing || fired)
        {
            return;
        }

        if (Time.unscaledTime - pressStart < holdSeconds)
        {
            return;
        }

        fired = true; // 한 번 누름당 +1 (증가 시 카드가 갱신되며 다시 눌러야 함)
        if (row != null && !string.IsNullOrEmpty(row.MissionId) && MissionManager.Instance != null)
        {
            MissionManager.Instance.TestIncrement(row.MissionId);
        }
    }
}
