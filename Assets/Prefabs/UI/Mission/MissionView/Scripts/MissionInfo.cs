using System;
using System.Collections.Generic;
using UnityEngine;

// 미션 1개의 정의 + 런타임 상태를 한 곳에 담는다(ChangeCharInfo 방식).
// JSON 없음 — 정의는 MissionList가 코드로 보유하고, 진행 상태(current/claimedTiers)는 메모리에만.

public enum MissionTab
{
    Onboarding,    // 첫걸음
    Conversation,  // 대화
    Affection,     // 교감
    Productivity,  // 생활
    Challenge,     // 도전(누적·마일스톤·메타)
}

// 진행 구조. 목표는 전부 int.
public enum MissionType
{
    OneTime,    // 일회용: 단일 단계
    Increment,  // 증가형(무한): 레벨 N(1-based) 목표 = incrementA*N + incrementB
    Tiered,     // 열거형: 정해진 단계 배열(단계마다 목표·보상)
}

[Serializable]
public class MissionReward
{
    public int gold;
    public int item1;
    public int item2;
    public int item3;

    public MissionReward() { }

    public MissionReward(int gold, int item1 = 0, int item2 = 0, int item3 = 0)
    {
        this.gold = gold;
        this.item1 = item1;
        this.item2 = item2;
        this.item3 = item3;
    }

    public bool IsEmpty => gold == 0 && item1 == 0 && item2 == 0 && item3 == 0;

    public int RewardKinds =>
        (gold != 0 ? 1 : 0) + (item1 != 0 ? 1 : 0) + (item2 != 0 ? 1 : 0) + (item3 != 0 ? 1 : 0);

    // kind: 0=gold, 1=item1, 2=item2, 3=item3 — 보상 종류 처리를 한 곳(데이터)에 모음.
    public int ValueOf(int kind)
    {
        switch (kind)
        {
            case 0: return gold;
            case 1: return item1;
            case 2: return item2;
            case 3: return item3;
            default: return 0;
        }
    }

    public List<int> NonZeroKinds()
    {
        List<int> list = new List<int>();
        if (gold != 0) list.Add(0);
        if (item1 != 0) list.Add(1);
        if (item2 != 0) list.Add(2);
        if (item3 != 0) list.Add(3);
        return list;
    }

    public static string KindLabel(int kind)
    {
        switch (kind)
        {
            case 0: return "G";
            case 1: return "i1";
            case 2: return "i2";
            case 3: return "i3";
            default: return string.Empty;
        }
    }
}

// 다국어 제목 (ko/en/ja).
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

[Serializable]
public class MissionInfo
{
    // ── 정의 ──────────────────────────────────────────────────────────────
    public string id;             // 6글자 식별자 (2영문+4숫자, 예 "CV0002")
    public bool isMeta;           // 메타(다른 미션 달성을 집계) — 달성 카운트/탭완료 계산에서 제외
    public MissionTab tab;        // 탭(카테고리)
    public LocalizedText title;   // ko/en/ja
    public MissionType type;

    // OneTime/Tiered: tiers 사용 (OneTime은 1개). Increment: incrementA/B + incrementReward.
    public List<MissionTier> tiers = new List<MissionTier>();
    public int incrementA;        // 레벨당 증가량 a
    public int incrementB;        // 상수항 b → target(N) = a*N + b
    public MissionReward incrementReward;

    // ── 런타임 상태 (메모리 전용, 저장 없음) ───────────────────────────────
    public int current;           // 누적 진행치
    public int claimedTiers;      // 수령 완료 단계 수(=레벨). OneTime 0/1, Tiered 0..N, Increment 무한

    // ── 파생 (계산) ───────────────────────────────────────────────────────
    public bool IsIncrement => type == MissionType.Increment;

    public int TierCount => IsIncrement ? int.MaxValue : (tiers != null ? tiers.Count : 0);

    // level: 0-based(= 그 단계 인덱스 = claimedTiers 값)
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

    public int NextTarget => TargetForLevel(claimedTiers);
    public MissionReward NextReward => RewardForLevel(claimedTiers);
    public bool AllDone => !IsIncrement && claimedTiers >= TierCount;          // Increment은 영원히 false
    public bool Claimable => !AllDone && current >= NextTarget;
    public float Progress01 => AllDone ? 1f : Mathf.Clamp01((float)current / Mathf.Max(1, NextTarget));
}

// 진행도 저장 DTO (missions.json). 정의는 코드에 있으니 상태만 저장한다.
[Serializable]
public class MissionProgressDto
{
    public string id;
    public int current;
    public int claimedTiers;
}

[Serializable]
public class MissionSaveData
{
    public List<MissionProgressDto> items = new List<MissionProgressDto>();
}
