using System;
using System.Collections.Generic;
using UnityEngine;

// 캐릭터 아이콘 카탈로그 (ScriptableObject)
// - 다음 발화자 힌트 벌룬 등에서 캐릭터 아이콘을 닉네임으로 조회 (하드코딩 sensei/arona/plana 대체)
// - 캐릭터 추가 = 이 에셋에 엔트리 등록 (코드 수정 불필요)
// - Resources/CharacterIconCatalog.asset 으로 배치. 키는 CharAttributes.nickname(서버 speaker와 동일)
[CreateAssetMenu(fileName = "CharacterIconCatalog", menuName = "Jarvis/Character Icon Catalog")]
public class CharacterIconCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string nickname;  // 캐릭터 닉네임 (API char_name과 동일)
        public Sprite icon;      // 캐릭터 아이콘 스프라이트
    }

    public List<Entry> entries = new List<Entry>();

    private static CharacterIconCatalog cached;  // Resources 로드 1회 캐시
    private static bool loadTried = false;       // 에셋 부재 시 반복 로드 방지

    // 닉네임으로 아이콘 조회 (대소문자 무시, 미등록/에셋 부재 시 null)
    public static Sprite GetIcon(string nickname)
    {
        if (string.IsNullOrEmpty(nickname)) return null;

        if (cached == null && !loadTried)
        {
            cached = Resources.Load<CharacterIconCatalog>("CharacterIconCatalog");
            loadTried = true;
        }
        if (cached == null) return null;

        foreach (Entry entry in cached.entries)
        {
            if (entry != null && string.Equals(entry.nickname, nickname, StringComparison.OrdinalIgnoreCase))
            {
                return entry.icon;
            }
        }
        return null;
    }
}
