using UnityEngine;

// ⚠ 폐기 예정 (2026-08-26). **새로 붙이지 말 것.**
//
// 상점 창을 인벤토리 창에 붙여 함께 열고 닫으려고 만들었으나, 실기에서 써 보니
// 두 창을 따로 잡아 옮기고 따로 닫고 싶다는 것이 확인돼 결합을 되돌렸다.
// 남은 요구는 "처음 뜰 때 나란히" 하나뿐이고, 그건 런타임 결합이 아니라 소환 좌표의 문제라
// MRFloatingPanel.spawnLateralOffset(인벤토리 -0.24 / 상점 +0.24)이 처리한다.
//
// 이 파일이 아직 남아 있는 이유는 하나다 — 씬에 이미 붙은 인스턴스를
// `Tools → MR → 상점 패널 배치`가 **타입으로 찾아 제거**해야 하기 때문이다.
// 클래스를 먼저 지우면 씬에 Missing Script가 남는다.
// 툴을 한 번 돌려 씬에서 사라진 것을 확인한 뒤 이 파일을 삭제할 것.
//
// 동작은 그대로 두되(제거 전까지 붙어 있으면 예전대로 움직인다) 새 배선은 하지 않는다.
[DisallowMultipleComponent]
public class MRStorePanelCompanion : MonoBehaviour
{
    [Tooltip("따라갈 리더 패널. 인벤토리 패널의 InventoryView.")]
    [SerializeField] private InventoryView leader;

    [Tooltip("이 오브젝트가 제어할 상점 뷰. 비우면 자기 계층에서 찾는다.")]
    [SerializeField] private StoreView storeView;

    [Tooltip("진단 로그 (열림/닫힘 전이만 찍는다 — 매 프레임이 아니다).")]
    [SerializeField] private bool verboseLog = true;

    private bool lastLeaderVisible;
    private bool initialized;

    private void Awake()
    {
        if (storeView == null)
        {
            storeView = GetComponentInChildren<StoreView>(true);
        }
    }

    // 리더의 표시 상태를 따라간다.
    // MRFloatingPanel의 알파 감시도 LateUpdate라 순서가 갈릴 수 있어, 전이가 생긴 프레임에만 움직인다.
    private void LateUpdate()
    {
        if (leader == null || storeView == null)
        {
            return;
        }

        bool leaderVisible = leader.IsVisible;

        if (initialized == false)
        {
            initialized = true;
            lastLeaderVisible = leaderVisible;
            return;
        }

        if (leaderVisible == lastLeaderVisible)
        {
            return;
        }

        lastLeaderVisible = leaderVisible;

        if (leaderVisible)
        {
            // 상점은 SetActive로도 꺼져 있을 수 있다 — 씬 저장 상태가 비활성이다.
            if (gameObject.activeSelf == false)
            {
                gameObject.SetActive(true);
            }

            storeView.Show();

            if (verboseLog)
            {
                Debug.Log("[MRInv/한쌍] 인벤토리 열림 → 상점도 연다");
            }
            return;
        }

        storeView.Hide();

        if (verboseLog)
        {
            Debug.Log("[MRInv/한쌍] 인벤토리 닫힘 → 상점도 닫는다");
        }
    }

    // 에디터 툴이 배선할 때 쓴다.
    public void Bind(InventoryView leaderView, StoreView view)
    {
        leader = leaderView;
        storeView = view;
    }
}
