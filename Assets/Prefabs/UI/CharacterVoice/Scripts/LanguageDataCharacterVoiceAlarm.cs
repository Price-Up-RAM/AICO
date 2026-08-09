using System;
using System.Collections.Generic;

public static class LanguageDataCharacterVoiceAlarm
{
    public static readonly List<Dictionary<string, string>> Texts =
        new List<Dictionary<string, string>>
        {
            Entry("Alarm Voice", "アラーム音声", "Alarm Voice"),
            Entry("컨셉을 적어주세요", "コンセプトを入力してください", "Enter an alarm concept"),
            Entry("랜덤", "ランダム", "Random"),
            Entry("재생성", "再生成", "Regenerate"),
            Entry("직접 알람 대사 추가", "アラーム台詞を直接追加", "Add an alarm message"),
            Entry(
                "저장되거나 매핑된 알람 대사가 없습니다.",
                "保存または割り当てられたアラーム台詞がありません。",
                "No alarm messages are saved or mapped."),
            Entry("캐릭터를 선택해주세요.", "キャラクターを選択してください。", "Select a character."),

            Entry("알람 음성 추가 확인", "アラーム音声の追加確認", "Confirm Alarm Voices"),
            Entry(
                "후보 음성을 듣고 추가할 알람 대사를 선택해주세요.",
                "候補音声を聞いて、追加するアラーム台詞を選択してください。",
                "Preview the candidates and select alarm messages to add."),
            Entry("선택 항목 추가", "選択項目を追加", "Add Selected"),
            Entry(
                "사용할 수 있는 알람 대사가 없습니다.",
                "使用できるアラーム台詞がありません。",
                "No alarm messages are available."),
            Entry(
                "알람 후보 음성을 준비합니다.",
                "アラーム候補音声を準備します。",
                "Preparing alarm candidate voices."),
            Entry(
                "알람 후보 음성을 준비 중입니다. ({0}/{1})",
                "アラーム候補音声を準備中です。({0}/{1})",
                "Preparing alarm candidate voices. ({0}/{1})"),

            Entry(
                "선택한 대사의 음성을 먼저 준비해주세요.",
                "選択した台詞の音声を先に準備してください。",
                "Prepare the selected message voices first."),
            Entry(
                "하나 이상의 대사를 선택해주세요.",
                "1つ以上の台詞を選択してください。",
                "Select at least one message."),
            Entry(
                "{0}개의 음성을 준비하지 못했습니다. 재생성을 눌러주세요.",
                "{0}件の音声を準備できませんでした。再生成してください。",
                "{0} voices could not be prepared. Select Regenerate."),
            Entry(
                "후보 음성을 다시 생성 중입니다.",
                "候補音声を再生成しています。",
                "Regenerating the candidate voice."),
            Entry(
                "후보 음성을 다시 생성했습니다.",
                "候補音声を再生成しました。",
                "Candidate voice regenerated."),
            Entry(
                "후보 음성 재생성에 실패했습니다.",
                "候補音声の再生成に失敗しました。",
                "Failed to regenerate the candidate voice."),

            Entry(
                "알람 사용 상태를 저장하지 못했습니다.",
                "アラームの使用状態を保存できませんでした。",
                "Failed to save the alarm enabled state."),
            Entry("이 알람 음성을 사용합니다.", "このアラーム音声を使用します。", "This alarm voice is enabled."),
            Entry(
                "이 알람 음성을 사용하지 않습니다.",
                "このアラーム音声は使用しません。",
                "This alarm voice is disabled."),
            Entry("알람 대사는 비워둘 수 없습니다.", "アラーム台詞は空にできません。", "The alarm message cannot be empty."),
            Entry(
                "수정한 알람 대사를 저장하지 못했습니다.",
                "編集したアラーム台詞を保存できませんでした。",
                "Failed to save the edited alarm message."),
            Entry(
                "알람 대사를 수정했습니다. 음성은 재생성으로 갱신할 수 있습니다.",
                "アラーム台詞を編集しました。音声は再生成で更新できます。",
                "Alarm message updated. Regenerate to update its voice."),
            Entry("알람을 삭제했습니다.", "アラームを削除しました。", "Alarm deleted."),
            Entry("알람을 삭제하지 못했습니다.", "アラームを削除できませんでした。", "Failed to delete the alarm."),
            Entry(
                "추가할 알람 대사를 입력해주세요.",
                "追加するアラーム台詞を入力してください。",
                "Enter an alarm message to add."),
            Entry("알람 대사를 생성 중입니다", "アラーム台詞を生成中です", "Generating alarm messages"),
            Entry(
                "알람 대사 생성 서버에 연결할 수 없습니다.",
                "アラーム台詞生成サーバーに接続できません。",
                "Unable to connect to the alarm message server."),
            Entry(
                "알람 대사 생성에 실패했습니다. 다시 시도해주세요.",
                "アラーム台詞の生成に失敗しました。もう一度お試しください。",
                "Failed to generate alarm messages. Please try again."),
            Entry(
                "알람 대사 생성 결과가 올바른 JSON 리스트가 아닙니다.",
                "アラーム台詞の生成結果が有効なJSONリストではありません。",
                "The alarm response is not a valid JSON list."),
            Entry(
                "{0}개의 알람 음성을 추가했습니다.",
                "{0}件のアラーム音声を追加しました。",
                "Added {0} alarm voices."),
            Entry(
                "{0}개 추가, {1}개 저장 실패",
                "{0}件追加、{1}件の保存に失敗",
                "Added {0}; failed to save {1}."),
            Entry(
                "선택한 알람 음성을 생성 중입니다.",
                "選択したアラーム音声を生成しています。",
                "Generating the selected alarm voices."),
            Entry(
                "{0}개의 알람 음성을 저장했습니다.",
                "{0}件のアラーム音声を保存しました。",
                "Saved {0} alarm voices."),
            Entry(
                "{0}개 저장, {1}개 생성 실패",
                "{0}件保存、{1}件の生成に失敗",
                "Saved {0}; failed to generate {1}."),
            Entry(
                "알람 음성을 다시 생성 중입니다.",
                "アラーム音声を再生成しています。",
                "Regenerating the alarm voice."),
            Entry(
                "알람 음성 재생성에 실패했습니다.",
                "アラーム音声の再生成に失敗しました。",
                "Failed to regenerate the alarm voice."),
            Entry(
                "알람 음성을 저장하지 못했습니다.",
                "アラーム音声を保存できませんでした。",
                "Failed to save the alarm voice."),
            Entry("알람 음성을 다시 생성했습니다.", "アラーム音声を再生成しました。", "Alarm voice regenerated."),
            Entry(
                "샘플 음성을 불러오는 중입니다.",
                "サンプル音声を読み込んでいます。",
                "Loading the sample voice."),
            Entry(
                "샘플 음성 서버에 연결할 수 없습니다.",
                "サンプル音声サーバーに接続できません。",
                "Unable to connect to the sample voice server."),
            Entry(
                "샘플 음성을 불러오지 못했습니다.",
                "サンプル音声を読み込めませんでした。",
                "Failed to load the sample voice."),
            Entry(
                "저장된 알람 음성을 재생하지 못했습니다.",
                "保存されたアラーム音声を再生できませんでした。",
                "Failed to play the saved alarm voice."),
            Entry("시간이 되었습니다.", "時間になりました。", "It's time.")
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
        if (TranslationIndex.TryGetValue(word, out var entry) &&
            entry.TryGetValue(language, out string translated))
        {
            return translated;
        }

        return LanguageData.Translate(word, language);
    }

    private static Dictionary<string, Dictionary<string, string>>
        BuildTranslationIndex()
    {
        var index =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
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

    private static Dictionary<string, string> Entry(
        string ko,
        string ja,
        string en)
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
            if (SettingManager.Instance != null &&
                SettingManager.Instance.settings != null)
            {
                return NormalizeLanguage(
                    SettingManager.Instance.settings.ui_language);
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
