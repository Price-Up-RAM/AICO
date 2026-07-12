using System.Collections.Generic;
using UnityEngine;

// 아이템→소켓 이름 해석 사다리 (장착 폴백).
// 1순위: 아이템 key와 같은 이름의 소켓 (예: 아이템 cat_ears ↔ 소켓 cat_ears)
// 2순위: targetSlotId (예: hairpin — 아이템 부류의 특정 자리)
// 3순위: fallbackSlotIds 순서대로 (예: head, chest, origin — 웬만한 모델이 갖는 범용 자리)
// 4순위: 예약 소켓 origin (임시 안전망 — 카탈로그 후보에 없어도 마지막으로 시도)
// 5순위: 없음(null) = 장착 불가.
// 런타임 장착(EquipManager)과 에디터 현황판(Socket Maker)이 이 한 곳을 같이 쓴다.
public static class EquipSlotResolver
{
    // 사다리 순서로 소켓 탐색. priority: 1=key 일치, 2=targetSlotId, 3=폴백, 4=origin(임시 안전망). 못 찾으면 null/0.
    public static EquipSocket Resolve(GameObject character, EquipEntry entry, out string matchedSlotId, out int priority)
    {
        matchedSlotId = null;
        priority = 0;

        if (character == null || entry == null)
        {
            return null;
        }

        EquipSocket socket = FindAliased(character, entry.key);
        if (socket != null)
        {
            matchedSlotId = socket.slotId;
            priority = 1;
            return socket;
        }

        socket = FindAliased(character, entry.targetSlotId);
        if (socket != null)
        {
            matchedSlotId = socket.slotId;
            priority = 2;
            return socket;
        }

        if (entry.fallbackSlotIds != null)
        {
            foreach (string fallback in entry.fallbackSlotIds)
            {
                socket = FindAliased(character, fallback);
                if (socket != null)
                {
                    matchedSlotId = socket.slotId;
                    priority = 3;
                    return socket;
                }
            }
        }

        // 4단(임시 안전망): 예약 소켓 origin — 카탈로그 후보에 없어도 마지막으로 탐색.
        // 반드시 읽기 전용 — 생성 부작용 절대 금지 (에디터 현황판이 GUI 프레임마다 이 함수를 호출).
        EquipSocket origin = EquipSocket.Find(character, "origin");
        if (origin != null)
        {
            matchedSlotId = "origin";
            priority = 4;
            return origin;
        }

        return null;
    }

    // 후보 slotId 목록 (우선순위 순, 별칭 적용, 중복/빈 값 제거) — 경고 메시지·현황판 표시용
    public static List<string> Candidates(EquipEntry entry)
    {
        List<string> result = new List<string>();
        if (entry == null)
        {
            return result;
        }

        AddCandidate(result, entry.key);
        AddCandidate(result, entry.targetSlotId);
        if (entry.fallbackSlotIds != null)
        {
            foreach (string fallback in entry.fallbackSlotIds)
            {
                AddCandidate(result, fallback);
            }
        }
        return result;
    }

    private static void AddCandidate(List<string> list, string slotId)
    {
        string aliased = Alias(slotId);
        if (string.IsNullOrEmpty(aliased))
        {
            return;
        }
        if (list.Contains(aliased) == false)
        {
            list.Add(aliased);
        }
    }

    // 레거시 별칭: overhead 폐지 → head
    private static string Alias(string slotId)
    {
        if (slotId == "overhead")
        {
            return "head";
        }
        return slotId;
    }

    private static EquipSocket FindAliased(GameObject character, string slotId)
    {
        string aliased = Alias(slotId);
        if (string.IsNullOrEmpty(aliased))
        {
            return null;
        }
        return EquipSocket.Find(character, aliased);
    }
}
