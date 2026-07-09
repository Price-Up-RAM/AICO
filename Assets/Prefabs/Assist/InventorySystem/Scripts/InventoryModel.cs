using System;
using System.Collections.Generic;

// 인벤토리 순수 데이터 모델 (JsonUtility 직렬화 호환 POCO).
// 스토어는 "소유"만 추적한다 — 장착 상태의 진실은 EquipSystem에 있음.
// 각 스택은 그리드 칸 위치(slot)를 가진다 (드래그 앤 드롭 배치/페이지의 기준).

// 아이템 스택: 키 + 개수 + 칸 위치
[Serializable]
public class InvItemStack
{
    public string key;      // 아이템 식별 키 (InventoryCatalog/EquipCatalog와 동일 문자열 공간)
    public int count = 1;   // 보유 개수
    public int slot = -1;   // 그리드 칸 인덱스 (0부터. -1 = 미배정 → NormalizeSlots가 자동 배정)
}

// 스토어: 한 소유자("MAIN" 또는 charcode)의 아이템 스택 목록
[Serializable]
public class InvStore
{
    public string ownerId;                                          // "MAIN" 또는 charcode
    public List<InvItemStack> stacks = new List<InvItemStack>();    // 보유 스택 목록

    // 키로 스택 조회 (없으면 null)
    public InvItemStack Find(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        foreach (InvItemStack stack in stacks)
        {
            if (stack != null && stack.key == key)
            {
                return stack;
            }
        }

        return null;
    }

    // 칸 인덱스로 스택 조회 (없으면 null)
    public InvItemStack FindBySlot(int slot)
    {
        if (slot < 0)
        {
            return null;
        }

        foreach (InvItemStack stack in stacks)
        {
            if (stack != null && stack.slot == slot)
            {
                return stack;
            }
        }

        return null;
    }

    // 키의 보유 개수 (없으면 0)
    public int CountOf(string key)
    {
        InvItemStack stack = Find(key);
        if (stack == null)
        {
            return 0;
        }

        return stack.count;
    }

    // 비어 있는 가장 앞 칸 인덱스
    public int FirstFreeSlot()
    {
        int slot = 0;
        while (FindBySlot(slot) != null)
        {
            slot = slot + 1;
        }

        return slot;
    }

    // 아이템 추가: 기존 스택 증가, 없으면 신규 스택을 빈 칸에 생성 (amount<=0은 무시)
    public void Add(string key, int amount)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (amount <= 0)
        {
            return;
        }

        InvItemStack stack = Find(key);
        if (stack == null)
        {
            stack = new InvItemStack();
            stack.key = key;
            stack.count = amount;
            stack.slot = FirstFreeSlot();
            stacks.Add(stack);
            return;
        }

        stack.count += amount;
    }

    // 아이템 제거: 부족하면 false(무변경), 0이 되면 스택 엔트리 자체를 제거 (칸이 비워짐)
    public bool Remove(string key, int amount)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (amount <= 0)
        {
            return false;
        }

        InvItemStack stack = Find(key);
        if (stack == null)
        {
            return false;
        }

        if (stack.count < amount)
        {
            // 보유량 부족 — 아무것도 바꾸지 않음
            return false;
        }

        stack.count -= amount;

        if (stack.count == 0)
        {
            stacks.Remove(stack);
        }

        return true;
    }

    // 칸 위치 정규화: 미배정(-1)/중복 칸을 빈 칸으로 재배정 (구버전 세이브 로드 대응)
    public void NormalizeSlots()
    {
        HashSet<int> used = new HashSet<int>();

        foreach (InvItemStack stack in stacks)
        {
            if (stack == null)
            {
                continue;
            }

            if (stack.slot < 0 || used.Contains(stack.slot))
            {
                // 빈 칸 탐색 (used 기준)
                int slot = 0;
                while (used.Contains(slot) || (FindBySlot(slot) != null && FindBySlot(slot) != stack))
                {
                    slot = slot + 1;
                }

                stack.slot = slot;
            }

            used.Add(stack.slot);
        }
    }
}
