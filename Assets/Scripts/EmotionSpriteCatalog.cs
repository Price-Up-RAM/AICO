using System;
using System.Collections.Generic;
using UnityEngine;

// 감정/알림 이모션 벌룬 스프라이트 카탈로그 (ScriptableObject)
// - EmotionBalloonManager의 하드코딩 스프라이트 공급을 데이터 등록으로 대체
// - Resources/EmotionSpriteCatalog.asset 으로 배치하고 이름으로 조회
[CreateAssetMenu(fileName = "EmotionSpriteCatalog", menuName = "Jarvis/Emotion Sprite Catalog")]
public class EmotionSpriteCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string name;    // 스프라이트 이름 (예: Time, No, Search)
        public Sprite sprite;  // 표시할 스프라이트
    }

    public List<Entry> entries = new List<Entry>();

    private static EmotionSpriteCatalog cached;  // Resources 로드 1회 캐시
    private static bool loadTried = false;       // 에셋 부재 시 반복 로드 방지

    // 이름으로 스프라이트 조회 (대소문자 무시, 미등록/에셋 부재 시 null)
    public static Sprite GetSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;

        if (cached == null && !loadTried)
        {
            cached = Resources.Load<EmotionSpriteCatalog>("EmotionSpriteCatalog");
            loadTried = true;
        }
        if (cached == null) return null;

        foreach (Entry entry in cached.entries)
        {
            if (entry != null && string.Equals(entry.name, spriteName, StringComparison.OrdinalIgnoreCase))
            {
                return entry.sprite;
            }
        }
        return null;
    }
}
