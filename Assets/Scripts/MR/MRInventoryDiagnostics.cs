using System.Collections.Generic;
using UnityEngine;

// A0 진단 — 인벤토리 장착이 실제로 성립하는지를 Play 1회로 확정한다.
//
// 왜 필요한가: CharManager.charList는 씬에 비어 있고(런타임에 PrefabDataLocal이 채운다),
// 어떤 프리팹이 스폰되는지도 소켓이 저작돼 있는지도 정적 파싱으로 확정할 수 없다(§7-1 B).
//
// 판정은 반드시 **런타임과 같은 코드**(EquipSlotResolver.Resolve)로 한다.
// slotId 합집합을 세면 폴백까지 "없는 자리"로 세어 그럴듯한 오진이 나온다 —
// 사다리는 ①key 일치 ②targetSlotId ③폴백 ④origin 순이라 상위가 맞으면 하위는 무의미하다.
// (2026-08-26에 실제로 이 오진을 냈다: 실제로는 다 되는데 "4개 저작 필요"라고 찍었다.)
//
// 로그는 "지금 값 + 제안 값"을 한 줄에 찍는다(§7-1 C). 태그에 채널을 넣는다(§7-1 D).
// SetActiveOwner가 origin 임시 소켓을 주입하기 **전에** 불러야 정식 저작분만 보인다.
public static class MRInventoryDiagnostics
{
    private static bool reported = false;  // 캐릭터 전환마다 도배되지 않도록 1회만

    // 장착 성립 여부 리포트. CharManager.setInventoryVar에서 SetActiveOwner 직전에 호출한다.
    public static void ReportSocketState(GameObject target)
    {
        if (target == null)
        {
            Debug.LogWarning("[MRInv/소켓] target이 null이라 진단을 건너뜁니다.");
            return;
        }

        if (reported)
        {
            return;
        }
        reported = true;

        // 1) 캐릭터에 실재하는 정식 소켓 (origin 주입 전)
        EquipSocket[] sockets = target.GetComponentsInChildren<EquipSocket>(true);
        List<string> haveSlots = new List<string>();
        foreach (EquipSocket socket in sockets)
        {
            if (socket != null && string.IsNullOrEmpty(socket.slotId) == false)
            {
                haveSlots.Add(socket.slotId);
            }
        }

        // 2) 게이트 — character_database.json의 tagEquip. JSON 미등재 캐릭터는 **불가**가 기본값이라
        //    소켓이 다 있어도 여기서 통째로 잠긴다 (§4-69 계열).
        bool equipAllowed = CharacterFeatureTags.IsEquipAllowed(target);

        // 3) 카탈로그 엔트리별로 런타임과 같은 사다리를 돌린다
        EquipCatalog catalog = Resources.Load<EquipCatalog>("EquipCatalog");
        if (catalog == null)
        {
            Debug.LogWarning("[MRInv/소켓] Resources/EquipCatalog를 찾지 못했습니다.");
            return;
        }

        int total = 0;
        int ok = 0;
        List<string> unresolved = new List<string>();   // 사다리 전멸
        List<string> originOnly = new List<string>();   // origin(4순위)으로만 붙는다 = 발밑 장착

        foreach (EquipEntry entry in catalog.Entries)
        {
            if (entry == null)
            {
                continue;
            }

            total = total + 1;

            string matchedSlotId;
            int priority;
            EquipSocket resolved = EquipSlotResolver.Resolve(target, entry, out matchedSlotId, out priority);

            if (resolved == null || priority == 0)
            {
                unresolved.Add(entry.key);
                continue;
            }

            if (priority >= 4)
            {
                originOnly.Add(entry.key);
                continue;
            }

            ok = ok + 1;
        }

        // 4) 제안 값 — 이 줄만 보고 다음 행동이 정해져야 한다
        string suggestion;
        if (equipAllowed == false)
        {
            suggestion = "장착 게이트 차단 — character_database.json의 이 의상 엔트리 tagEquip을 true로. " +
                         "소켓 상태와 무관하게 지금은 전면 불가";
        }
        else if (unresolved.Count == 0 && originOnly.Count == 0)
        {
            suggestion = "소켓 저작 완료 — A 단계 불필요, C-2(inventory_equip/unequip) 진행 가능";
        }
        else
        {
            suggestion = "Tools → EquipSystem → Socket Maker 필요 | 미해결=" + Join(unresolved) +
                         " | origin(발밑)=" + Join(originOnly);
        }

        Debug.Log($"[MRInv/소켓] 스폰={target.name} | 정식소켓 {sockets.Length}개 [{Join(haveSlots)}] " +
                  $"| tagEquip={equipAllowed} | 카탈로그 {total}종 중 정상해결 {ok} / 미해결 {unresolved.Count} / origin {originOnly.Count} " +
                  $"→ {suggestion}");
    }

    // 리스트를 콤마 구분 한 줄로 (빈 리스트는 '없음')
    private static string Join(List<string> items)
    {
        if (items == null || items.Count == 0)
        {
            return "없음";
        }

        return string.Join(",", items);
    }
}
