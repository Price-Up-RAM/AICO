using System;
using System.Collections.Generic;

// CharacterDetail 전용 고정 UI 문구 (친밀도 보상 모달 포함).
// 공용 문구(듣기/기본/생성/샘플 듣기/상태 등)는 LanguageData로 fallback하고,
// CharacterDetail 고유 문구는 이 파일에서 우선 관리한다.
// 주의: 특수 기능 태그(tagSpecials)의 ko 원문은 표시용이자 게이트 키(CharacterFeatureTags.SpecialCheekPull 등)라 임의 변경 금지.
public static class LanguageDataCharacterDetail
{
    public static readonly List<Dictionary<string, string>> Texts =
        new List<Dictionary<string, string>>
        {
            // 라벨
            Entry("사용가능 기능 태그", "使用可能な機能タグ", "Available Features"),
            Entry("프롬프트 영역", "プロンプト領域", "Prompt Area"),
            Entry("기본 알람 음성", "基本アラーム音声", "Default Alarm Voice"),
            Entry("커스텀 알람음성", "カスタムアラーム音声", "Custom Alarm Voice"),
            Entry("사용중", "使用中", "Enabled"),
            Entry("사용안함", "未使用", "Disabled"),
            Entry("알람 후보", "アラーム候補", "Alarm Choices"),
            Entry("후보", "候補", "Choice "),
            Entry("다시 듣기", "もう一度再生", "Replay"),
            Entry("사용하기", "使用する", "Use"),
            Entry("샘플을 듣고 사용할 대사를 선택하세요.", "サンプルを聞いて使用する台詞を選んでください。", "Preview and choose the message to use."),
            Entry("알람 음성 생성에 실패했습니다. 다시 시도해주세요.", "アラーム音声の生成に失敗しました。もう一度お試しください。", "Failed to generate the alarm voice. Please try again."),
            Entry("샘플듣기", "サンプル再生", "Play Sample"),
            Entry("생성된것 듣기", "生成済みを再生", "Play Generated"),
            Entry("캐릭터 이름", "キャラクター名", "Character Name"),
            Entry("출전", "出典", "Source"),
            Entry("대화횟수", "会話回数", "Conversations"),
            Entry("복장 수", "衣装数", "Costumes"),
            Entry("로딩 중...", "読み込み中...", "Loading..."),
            Entry("초기화 중...", "初期化中...", "Resetting..."),
            Entry("캐릭터 초상화", "キャラクターポートレート", "CHARACTER PORTRAIT"),
            Entry("+40 (테스트)", "+40 (テスト)", "+40 (Test)"),
            Entry("초기화 (테스트)", "リセット (テスト)", "Reset (Test)"),
            Entry("남자1", "男性1", "Male 1"),

            // 기능 태그 bool 4종 축약 라벨 — 극성(가능/불가)은 칩 배경색으로만 구분 (CharacterFeatureTags 참조)
            Entry("AI 대화", "AI会話", "AI Chat"),
            Entry("친밀도", "親密度", "Affinity"),
            Entry("감정표현", "感情表現", "Emotion"),
            Entry("악세서리", "アクセサリー", "Accessory"),

            // 특수 기능 태그 (tagSpecials 어휘) — 새 special 추가 시 여기에 번역도 함께 등록
            Entry("볼당기기", "ほっぺ引っ張り", "Cheek Pull"),
            Entry("머리쓰다듬기", "頭なでなで", "Head Pat"),
            Entry("음악재생", "音楽再生", "Music Player"),
            Entry("커피끓여주기", "コーヒー淹れ", "Coffee Brewing"),
            Entry("화초관리", "植物の世話", "Plant Care"),
            Entry("전원키기", "電源オン", "Power On"),

            // 출전
            Entry("블루아카이브", "ブルーアーカイブ", "Blue Archive"),
            Entry("원신", "原神", "Genshin Impact"),
            Entry("꿈씨패밀리", "クムシファミリー", "Kkumssi Family"),
            Entry("오리지널", "オリジナル", "Original"),

            // 친밀도
            Entry("낯선 사이", "見知らぬ仲", "Stranger"),
            Entry("아는 사이", "顔見知り", "Acquaintance"),
            Entry("친한 사이", "親しい仲", "Friend"),
            Entry("허물없는 사이", "気の置けない仲", "True Friend"),
            Entry("마음이 통하는 사이", "心が通じ合う仲", "Kindred Spirits"),
            Entry("둘도 없는 사이", "かけがえのない仲", "One and Only"),
            Entry("친밀도 보상", "親密度報酬", "Affinity Rewards"),
            Entry("전부 수령", "一括受取", "Claim All"),
            Entry("수령", "受取", "Claim"),
            Entry("수령 완료", "受取済み", "Claimed"),
            Entry("미도달", "未到達", "Locked"),
            Entry("100G + 전용 장신구 (후속)", "100G + 専用アクセサリー(後続)", "100G + Signature Accessory (TBD)"),
            Entry("100G + 카드 동테 (후속)", "100G + カード銅枠(後続)", "100G + Bronze Border (TBD)"),
            Entry("100G + 카드 은테 (후속)", "100G + カード銀枠(後続)", "100G + Silver Border (TBD)"),
            Entry("200G + 카드 금테 + 명칭 커스텀 (후속)", "200G + カード金枠 + 名称カスタム(後続)", "200G + Gold Border + Custom Title (TBD)")
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
