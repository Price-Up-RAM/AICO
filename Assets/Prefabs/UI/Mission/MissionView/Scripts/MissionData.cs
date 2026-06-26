using System;
using System.Collections.Generic;

// 미션(업적) 데이터 모델. 설계: Assets/Prefabs/UI/Mission/MISSION_Design.md §3
// 정의(MissionDef)는 빌드에 박혀 출하되는 읽기 전용. 진행 상태(MissionProgress)만 JSON 저장.

// 탭 = 카테고리 (수집 폐지, 5개).
public enum MissionCategory
{
    Onboarding,    // 첫걸음
    Conversation,  // 대화
    Affection,     // 교감
    Productivity,  // 생활
    Challenge,     // 도전(누적·마일스톤·메타)
}

// 진행 구조(열거형). 목표는 전부 int.
public enum MissionType
{
    OneTime,    // 일회용: 단일 단계. 목표=정수, 1번 수령
    Increment,  // 증가형(무한): 레벨 N(1-based) 목표 = incrementA*N + incrementB
    Tiered,     // 열거형: 정해진 단계 배열. 단계마다 목표·보상
}

[Serializable]
public class MissionReward
{
    public int gold;
    public int item1;
    public int item2;
    public int item3;

    public bool IsEmpty => gold == 0 && item1 == 0 && item2 == 0 && item3 == 0;

    public int RewardKinds =>
        (gold != 0 ? 1 : 0) + (item1 != 0 ? 1 : 0) + (item2 != 0 ? 1 : 0) + (item3 != 0 ? 1 : 0);

    public MissionReward() { }

    public MissionReward(int gold, int item1 = 0, int item2 = 0, int item3 = 0)
    {
        this.gold = gold;
        this.item1 = item1;
        this.item2 = item2;
        this.item3 = item3;
    }
}

// 다국어 제목 (ko/en/ja). 앱 주언어 설정에 따라 노출.
[Serializable]
public class LocalizedText
{
    public string ko;
    public string en;
    public string ja;

    public LocalizedText() { }

    public LocalizedText(string ko, string en, string ja)
    {
        this.ko = ko;
        this.en = en;
        this.ja = ja;
    }

    public string Get(string lang)
    {
        if (lang == "en")
        {
            return string.IsNullOrEmpty(en) ? ko : en;
        }

        if (lang == "ja")
        {
            return string.IsNullOrEmpty(ja) ? ko : ja;
        }

        return ko;
    }
}

// 단계(Tiered/OneTime 공용): 목표 누적치 + 그 단계 보상.
[Serializable]
public class MissionTier
{
    public int target;
    public MissionReward reward;

    public MissionTier() { }

    public MissionTier(int target, MissionReward reward)
    {
        this.target = target;
        this.reward = reward;
    }
}

// "정의": 읽기 전용 스펙 (MissionCatalog가 보유)
[Serializable]
public class MissionDef
{
    public string id;             // 6글자 (2영문+4숫자). 저장 매칭 키
    public string name;           // 옛 식별자(메타데이터)
    public MissionCategory category;
    public LocalizedText title;
    public MissionType type;

    // OneTime/Tiered: tiers 사용 (OneTime은 1개). Increment: incrementA/B + incrementReward.
    public List<MissionTier> tiers = new List<MissionTier>();
    public int incrementA;        // 레벨당 증가량 a
    public int incrementB;        // 상수항 b → target(N) = a*N + b
    public MissionReward incrementReward;

    public bool IsIncrement => type == MissionType.Increment;

    // 단계 수. Increment는 무한이라 int.MaxValue.
    public int TierCount => IsIncrement ? int.MaxValue : (tiers != null ? tiers.Count : 0);

    // level: 0-based (= 그 단계를 달성하기 위한 인덱스 = claimedTiers 값)
    public int TargetForLevel(int level)
    {
        if (IsIncrement)
        {
            return incrementA * (level + 1) + incrementB;
        }

        if (tiers != null && level >= 0 && level < tiers.Count)
        {
            return tiers[level].target;
        }

        // 모든 단계 완료 이후: 마지막 목표 유지
        if (tiers != null && tiers.Count > 0)
        {
            return tiers[tiers.Count - 1].target;
        }

        return 1;
    }

    public MissionReward RewardForLevel(int level)
    {
        if (IsIncrement)
        {
            return incrementReward ?? new MissionReward();
        }

        if (tiers != null && level >= 0 && level < tiers.Count && tiers[level].reward != null)
        {
            return tiers[level].reward;
        }

        return new MissionReward();
    }

    // 전부 완료? (Increment는 영원히 false)
    public bool IsAllDone(int claimedTiers)
    {
        if (IsIncrement)
        {
            return false;
        }

        return claimedTiers >= TierCount;
    }
}

// "진행 상태": JSON 저장 대상
[Serializable]
public class MissionProgress
{
    public string id;
    public int currentCount;
    public int claimedTiers;   // 수령 완료 단계 수. OneTime:0/1, Tiered:0..N, Increment:무한(=레벨)

    public MissionProgress() { }

    public MissionProgress(string id)
    {
        this.id = id;
    }
}

[Serializable]
public class MissionSaveData
{
    public List<MissionProgress> progresses = new List<MissionProgress>();
    // public string sig;   // (develop) HMAC 서명. 1차 평문 JSON에선 미사용 (MISSION_Design.md §6.1)
}

// 인벤토리(gold/item1~3) — 미션 보상 적립 대상. inventory.json. (MISSION_Design.md §6.2)
[Serializable]
public class InventoryData
{
    public int gold;
    public int item1;
    public int item2;
    public int item3;

    // 누적 통계(도전 미션용. 보상 순환 방지를 위해 monotonic 누적치를 별도 보관)
    public int goldEarnedTotal;
    public int goldSpentTotal;
}
