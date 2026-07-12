using System;
using System.Collections.Generic;
using UnityEngine;

// 카드 테두리 등급 — 친밀도 레벨 구간에 대응 (Lv4~6 동 / Lv7~9 은 / Lv10 금).
public enum AffinityBorderTier { None = 0, Bronze = 1, Silver = 2, Gold = 3 }

// 친밀도 보상 성분 타입 — Mission의 gold 단일화(MissionReward.gold)와 달리 친밀도 보상은
// 재화/아이템/테두리/호칭으로 확장된다. gem·crystal 등 신규 재화는 Currency + 재화 키 추가만으로 수용.
public enum AffinityRewardType
{
    Currency = 0, // id = 재화 키 (ItemCurrencyCatalog/CurrencyManager 공간, 예: currency_gold)
    Item = 1,     // id = 아이템 키 (ItemCatalog 공간 — 전용 장신구 등 수량형)
    Border = 2,   // id = 카드 테두리 해금 id (캐릭터 단위 해금물)
    Title = 3,    // id = 호칭/명칭 해금 id (캐릭터 단위 해금물)
}

// 보상 성분 1개 — 타입 + 대상 id + 수량 (해금물은 amount 미사용)
[Serializable]
public class AffinityRewardDef
{
    public AffinityRewardType type;
    public string id;
    public int amount;

    public AffinityRewardDef(AffinityRewardType type, string id, int amount = 1)
    {
        this.type = type;
        this.id = id;
        this.amount = amount;
    }
}

// 친밀도(affinity) 도메인 상수/계산의 단일 출처.
// 계획: Assets/Prefabs/UI/CharacterDetail/Affinity_Plan.md — 100포인트/레벨, 최대 Lv.10(1000pt), 6단계 명칭.
// 주의: "relationship"은 다른 시스템의 예약어 — 이 시스템의 어떤 식별자에도 쓰지 않는다.
public static class AffinityData
{
    public const int PointsPerLevel = 100;
    public const int MaxLevel = 10;
    public const int MaxPoints = PointsPerLevel * MaxLevel; // 1000

    // 누적 포인트 → 레벨 (0~10)
    public static int LevelFor(int points)
    {
        return Mathf.Min(Mathf.Max(points, 0) / PointsPerLevel, MaxLevel);
    }

    // 현재 레벨 안에서의 진행 포인트 (0~100). Lv.MAX면 100 고정.
    public static int PointsInLevel(int points)
    {
        int level = LevelFor(points);
        if (level >= MaxLevel) return PointsPerLevel;
        return Mathf.Max(points, 0) - level * PointsPerLevel;
    }

    // 현재 레벨 안에서의 진행률 (0~1). 게이지 fillAmount용.
    public static float ProgressInLevel(int points)
    {
        return PointsInLevel(points) / (float)PointsPerLevel;
    }

    // 6단계 명칭 (레벨 구간: 0~1 / 2~3 / 4~5 / 6~7 / 8~9 / 10)
    public static string StageNameFor(int level)
    {
        if (level >= 10) return "둘도 없는 사이";
        if (level >= 8) return "마음이 통하는 사이";
        if (level >= 6) return "허물없는 사이";
        if (level >= 4) return "친한 사이";
        if (level >= 2) return "아는 사이";
        return "낯선 사이";
    }

    // ── 레벨 보상 정의 (단일 출처) ──
    // 표시 문자열(RewardDescFor)은 이 목록과 수동 동기 — 목록을 바꾸면 표기·LanguageData 등록도 함께 갱신할 것.

    public const string CurrencyGoldKey = "currency_gold"; // CurrencyManager.GoldKey와 동일 값 (ItemSystem 결합 최소화용 자체 상수)
    public const string BorderBronzeId = "border_affinity_bronze";
    public const string BorderSilverId = "border_affinity_silver";
    public const string BorderGoldId = "border_affinity_gold";
    public const string TitleCustomLabelId = "title_affinity_custom"; // Lv.10 명칭 커스텀 해금
    public const string PendingSignatureItemId = "pending_signature_item"; // Lv.3 장신구 키 확정 전 수령 이력 마커 (백필용)

    public static List<AffinityRewardDef> RewardsFor(int level)
    {
        List<AffinityRewardDef> rewards = new List<AffinityRewardDef>();
        rewards.Add(new AffinityRewardDef(AffinityRewardType.Currency, CurrencyGoldKey, level >= MaxLevel ? 200 : 100));

        switch (level)
        {
            case 3:
                // 전용 장신구 — 캐릭터별 시그니처 키가 정해지기 전까지 id 빈값 (지급 라우터가 보류 처리)
                rewards.Add(new AffinityRewardDef(AffinityRewardType.Item, "", 1));
                break;
            case 4:
                rewards.Add(new AffinityRewardDef(AffinityRewardType.Border, BorderBronzeId));
                break;
            case 7:
                rewards.Add(new AffinityRewardDef(AffinityRewardType.Border, BorderSilverId));
                break;
            case 10:
                rewards.Add(new AffinityRewardDef(AffinityRewardType.Border, BorderGoldId));
                rewards.Add(new AffinityRewardDef(AffinityRewardType.Title, TitleCustomLabelId));
                break;
        }

        return rewards;
    }

    // 레벨 보상 골드 합계 — RewardsFor에서 파생 (Lv.10만 200G, 나머지 100G)
    public static int RewardGoldFor(int level)
    {
        int gold = 0;
        foreach (AffinityRewardDef def in RewardsFor(level))
        {
            if (def.type == AffinityRewardType.Currency && def.id == CurrencyGoldKey)
            {
                gold += def.amount;
            }
        }
        return gold;
    }

    // 레벨 보상 표기 (모달용). 골드 외 항목은 아직 표기만(후속 구현).
    public static string RewardDescFor(int level)
    {
        switch (level)
        {
            case 3: return "100G + 전용 장신구 (후속)";
            case 4: return "100G + 카드 동테 (후속)";
            case 7: return "100G + 카드 은테 (후속)";
            case 10: return "200G + 카드 금테 + 명칭 커스텀 (후속)";
            default: return "100G";
        }
    }

    // 카드 테두리 등급 판정 — Lv4~6 동테 / Lv7~9 은테 / Lv10 금테 / 그 미만은 없음
    public static AffinityBorderTier BorderTierFor(int level)
    {
        if (level >= 10) return AffinityBorderTier.Gold;
        if (level >= 7) return AffinityBorderTier.Silver;
        if (level >= 4) return AffinityBorderTier.Bronze;
        return AffinityBorderTier.None;
    }

    // 카드 테두리 틴트 — 보조 테두리(White 프레임)에 곱해질 색. None은 흰색(원본 그대로).
    public static Color BorderTintFor(AffinityBorderTier tier)
    {
        switch (tier)
        {
            case AffinityBorderTier.Bronze: return new Color(0.722f, 0.451f, 0.20f);  // 동 #B87333
            case AffinityBorderTier.Silver: return new Color(0.753f, 0.753f, 0.753f); // 은 #C0C0C0
            case AffinityBorderTier.Gold: return new Color(1f, 0.843f, 0f);           // 금 #FFD700
            default: return Color.white;
        }
    }
}
