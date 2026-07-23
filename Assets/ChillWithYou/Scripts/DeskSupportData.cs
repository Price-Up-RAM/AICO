using System.Collections.Generic;
using UnityEngine;

// DeskSupport(책상 꾸미기)의 영속 데이터.
// - skinMaterials: 스킨 버튼으로 교체할 머티리얼 후보 (책상 소품이 현재 쓰는 머티리얼과 같은 계열이어야 교체가 성립)
// - decorations: 장식 슬롯(DecoSlot_XX 마커)에 배치할 수 있는 프리팹 목록
// - desks: deskId별 선택 상태(스킨 인덱스 + 슬롯별 장식 id). 지금은 desk_default 1종이지만
//   책상 종류가 늘면 deskId 키로 구별한다 (ChillSitData의 charcode별 구조와 동일한 발상).
[CreateAssetMenu(fileName = "DeskSupportData", menuName = "AICO/Chill With You/Desk Support Data")]
public class DeskSupportData : ScriptableObject
{
    public const string DefaultDeskId = "desk_default";

    [System.Serializable]
    public class DecoDef
    {
        public string id;         // 저장용 식별자 (예: test_cube)
        public string label;      // 버튼 표기
        public GameObject prefab; // 슬롯 마커 아래 그대로 Instantiate된다 (localPosition 0)
    }

    [System.Serializable]
    public class DeskState
    {
        public string deskId = DefaultDeskId;
        public int skinIndex;                                   // skinMaterials 인덱스
        public List<string> slotDecoIds = new List<string>();   // 슬롯 순서(DecoSlot_01..)별 장식 id ("" = 없음)
    }

    [Header("머티리얼 스킨 후보")]
    public List<Material> skinMaterials = new List<Material>();

    [Header("배치 가능한 장식")]
    public List<DecoDef> decorations = new List<DecoDef>();

    [Header("책상별 저장 상태")]
    public List<DeskState> desks = new List<DeskState>();

    public DeskState GetOrCreateDesk(string deskId)
    {
        foreach (DeskState desk in desks)
        {
            if (desk.deskId == deskId)
            {
                return desk;
            }
        }
        DeskState created = new DeskState { deskId = deskId };
        desks.Add(created);
        return created;
    }

    public DecoDef FindDecoration(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (DecoDef deco in decorations)
        {
            if (deco.id == id)
            {
                return deco;
            }
        }
        return null;
    }
}
