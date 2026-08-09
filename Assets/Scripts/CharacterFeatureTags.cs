using System.Collections.Generic;
using UnityEngine;

// 캐릭터 기능 태그 판정/표시 유틸 — 정본은 character_database.json 의상 엔트리의 bool 4종 + tagSpecials.
// - 판정 단위는 의상(clothes) — 의상마다 프리팹(모델)이 달라 표정 mesh/장착 소켓 보유 여부가 갈린다.
// - bool 태그는 극성(가능/불가)을 값으로 표현하고, 표시 라벨은 축약형 하나만 사용한다 (불가 = 붉은 배경으로만 구분).
// - tagSpecials는 자유 문자열 리스트(예: "볼당기기") — 게이트 키이자 표시 문구이며, 번역은 표시 시점에만.
// - JSON 미등재 캐릭터 폴백: 대화 허용, 장착 불가, 특수 태그 없음 (로컬/외부 캐릭터 보호).
public static class CharacterFeatureTags
{
    // 축약 표시 라벨 (표시 전용 — 번역은 LanguageDataCharacterDetail)
    public const string LabelAiChat = "AI 대화";
    public const string LabelAffinity = "친밀도";
    public const string LabelEmotionExpression = "감정표현";
    public const string LabelEquip = "악세서리";

    // 특수 기능 태그 키 (tagSpecials 내 문자열)
    public const string SpecialCheekPull = "볼당기기";

    // 표시용 태그 칩 한 건 — text는 ko 원문, isNegative면 붉은 배경
    public struct DisplayTag
    {
        public string text;
        public bool isNegative;
    }

    // 캐릭터 GameObject → character_database.json 의상 엔트리 해석 (charcode 우선, nickname 폴백)
    public static ChangeCharClothesInfo FindClothes(GameObject character)
    {
        CharAttributes attrs = character != null ? character.GetComponent<CharAttributes>() : null;
        if (attrs == null || CharManager.Instance == null)
        {
            return null;
        }

        ChangeCharClothesInfo clothes = null;
        if (string.IsNullOrEmpty(attrs.charcode) == false)
        {
            clothes = CharManager.Instance.FindClothesInfoByCharacterId(attrs.charcode.ToLower());
        }
        if (clothes == null && string.IsNullOrEmpty(attrs.nickname) == false)
        {
            clothes = CharManager.Instance.FindClothesInfoByCharacterId(attrs.nickname.ToLower());
        }
        return clothes;
    }

    // 대화 가능 여부 — JSON 미등재 캐릭터는 허용 (기본 = 가능, 기존 캐릭터 무수정 호환)
    public static bool IsChatAllowed(GameObject character)
    {
        ChangeCharClothesInfo clothes = FindClothes(character);
        return clothes == null || clothes.tagAiChat;
    }

    // 장착 가능 여부 — JSON 미등재 캐릭터는 불허 (기본 = 불가)
    public static bool IsEquipAllowed(GameObject character)
    {
        ChangeCharClothesInfo clothes = FindClothes(character);
        return clothes != null && clothes.tagEquip;
    }

    // 특수 기능 태그 보유 여부 (예: 볼당기기) — JSON 미등재 캐릭터는 불허
    public static bool HasSpecial(GameObject character, string special)
    {
        ChangeCharClothesInfo clothes = FindClothes(character);
        return clothes != null && clothes.tagSpecials != null && clothes.tagSpecials.Contains(special);
    }

    // 표시용 태그 칩 목록 — bool 4종은 고정 순서로 항상 표시(불가 = 네거티브), specials는 뒤에 일반 칩으로 추가
    public static List<DisplayTag> BuildDisplayTags(ChangeCharClothesInfo clothes)
    {
        List<DisplayTag> result = new List<DisplayTag>();
        if (clothes == null)
        {
            return result;
        }

        result.Add(new DisplayTag { text = LabelAiChat, isNegative = clothes.tagAiChat == false });
        result.Add(new DisplayTag { text = LabelAffinity, isNegative = clothes.tagAffinity == false });
        result.Add(new DisplayTag { text = LabelEmotionExpression, isNegative = clothes.tagEmotionExpression == false });
        result.Add(new DisplayTag { text = LabelEquip, isNegative = clothes.tagEquip == false });

        if (clothes.tagSpecials != null)
        {
            foreach (string special in clothes.tagSpecials)
            {
                if (string.IsNullOrEmpty(special) == false)
                {
                    result.Add(new DisplayTag { text = special, isNegative = false });
                }
            }
        }

        return result;
    }
}
