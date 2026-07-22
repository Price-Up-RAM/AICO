using System.Collections.Generic;
using UnityEngine;

// 캐릭터(charcode)별 착석용 휴머노이드 Avatar 매핑.
// ChillModeManager가 포모도로(칠윗유) 진입 시에만 animator.avatar를 이 매핑으로 교체하고 종료 시 원복한다.
// 프리팹에 아바타를 영구 할당하면 평상시 generic 경로 커브 애니메이션이 휴머노이드 매핑 본에
// 적용되지 않아 idle이 죽기 때문에, 반드시 착석 구간 한정 런타임 스왑으로만 사용한다.
[CreateAssetMenu(fileName = "CharAvatarSO", menuName = "AICO/Chill With You/Char Avatar SO")]
public class CharAvatarSO : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string charcode;  // ch0066 같은 캐릭터 코드 (CharAttributes.charcode)
        public Avatar avatar;    // 해당 캐릭터 스켈레톤으로 생성된 휴머노이드 Avatar
    }

    // 미등록 캐릭터용 공용 아바타 (SimpleBAAvatar — BA 표준 bone_root/Bip001 스켈레톤 전용.
    // 래퍼 노드가 있는 캐릭터(Momoi_Original류/CH0293/CH0334)는 경로가 달라 폴백으로는 바인딩되지 않음)
    public Avatar fallbackAvatar;
    public List<Entry> entries = new List<Entry>();

    // charcode 전용 아바타 반환. 등록이 없으면 null — 폴백 적용 여부는 호출부가
    // 대상 Animator의 아바타 유무를 보고 결정한다 (자체 아바타 보유 캐릭터를 건드리지 않기 위함).
    public Avatar GetMappedAvatar(string charcode)
    {
        foreach (Entry entry in entries)
        {
            if (entry.charcode == charcode)
            {
                return entry.avatar;
            }
        }
        return null;
    }

#if UNITY_EDITOR
    // 에디터 툴(CharAvatarGenerator)용 upsert
    public void SetEntry(string charcode, Avatar avatar)
    {
        foreach (Entry entry in entries)
        {
            if (entry.charcode == charcode)
            {
                entry.avatar = avatar;
                return;
            }
        }
        entries.Add(new Entry { charcode = charcode, avatar = avatar });
    }
#endif
}
