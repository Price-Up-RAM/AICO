using System;
using System.Collections.Generic;

// InventorySystem 전용 고정 UI 문구.
// 공용 문구는 LanguageData로 fallback하고, 인벤토리 문구는 이 파일에서 우선 관리한다.
public static class LanguageDataInventory
{
    public static readonly List<Dictionary<string, string>> Texts =
        new List<Dictionary<string, string>>
        {
            Entry("인벤토리", "インベントリ", "INVENTORY"),
            Entry("인벤토리({0})", "インベントリ({0})", "INVENTORY ({0})"),
            Entry("인벤토리 - 메인", "インベントリ - メイン", "INVENTORY - MAIN"),
            Entry("인벤토리 - 캐릭터", "インベントリ - キャラクター", "INVENTORY - CHAR"),
            Entry("정렬", "並べ替え", "Sort"),
            Entry("골드 +100", "ゴールド +100", "Gold +100"),

            Entry("마우스클릭 : +2G", "マウスクリック：+2G", "Mouse click: +2G"),
            Entry("대화(Local) : +10G", "会話（Local）：+10G", "Conversation (Local): +10G"),
            Entry("기타 : 미션 달성", "その他：ミッション達成", "Other: Mission completion"),

            Entry("장착", "装備", "Equip"),
            Entry("해제", "解除", "Unequip"),
            Entry("CHAR로 이동", "キャラクターへ移動", "Move to CHAR"),
            Entry("MAIN으로 이동", "MAINへ移動", "Move to MAIN"),
            Entry("이동", "移動", "Move"),
            Entry("수량 {0}", "数量 {0}", "Quantity {0}"),
            Entry("수량: {0}", "数量: {0}", "Quantity: {0}"),
            Entry("분류: {0}", "分類: {0}", "Category: {0}"),
            Entry("키: {0}", "キー: {0}", "Key: {0}"),
            Entry("'{0}' 이동 수량", "「{0}」の移動数量", "Move quantity for '{0}'"),
            Entry("액세서리", "アクセサリー", "accessory")
        };

    private static readonly Dictionary<string, Dictionary<string, string>>
        TranslationIndex = BuildTranslationIndex();

    public static string Translate(string word)
    {
        return Translate(word, GetCurrentUiLanguage());
    }

    public static string Translate(string word, string targetLang)
    {
        if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(targetLang))
        {
            return word;
        }

        string language = NormalizeLanguage(targetLang);
        if (TranslationIndex.TryGetValue(word, out Dictionary<string, string> entry) &&
            entry.TryGetValue(language, out string translated))
        {
            return translated;
        }

        return LanguageData.Translate(word, language);
    }

    private static Dictionary<string, Dictionary<string, string>> BuildTranslationIndex()
    {
        var index = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (Dictionary<string, string> entry in Texts)
        {
            foreach (string value in entry.Values)
            {
                if (!index.ContainsKey(value))
                {
                    index.Add(value, entry);
                }
            }
        }

        return index;
    }

    private static Dictionary<string, string> Entry(string ko, string ja, string en)
    {
        return new Dictionary<string, string>
        {
            { "ko", ko },
            { "ja", ja },
            { "en", en }
        };
    }

    private static string GetCurrentUiLanguage()
    {
        try
        {
            if (SettingManager.Instance != null && SettingManager.Instance.settings != null)
            {
                return NormalizeLanguage(SettingManager.Instance.settings.ui_language);
            }
        }
        catch
        {
        }

        return "ko";
    }

    private static string NormalizeLanguage(string value)
    {
        string language = string.IsNullOrWhiteSpace(value)
            ? "ko"
            : value.Trim().ToLowerInvariant();
        return language == "jp" ? "ja" : language;
    }
}
