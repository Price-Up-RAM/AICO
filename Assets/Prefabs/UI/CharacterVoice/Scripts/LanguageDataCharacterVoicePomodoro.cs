using System;
using System.Collections.Generic;

public static class LanguageDataCharacterVoicePomodoro
{
    public static readonly List<Dictionary<string, string>> Texts =
        new List<Dictionary<string, string>>
        {
            Entry("Pomodoro Voice", "ポモドーロ音声", "Pomodoro Voice"),
            Entry("컨셉을 적어주세요", "コンセプトを入力してください", "Enter a Pomodoro concept"),
            Entry("랜덤", "ランダム", "Random"),
            Entry("재생성", "再生成", "Regenerate"),
            Entry("아무때나", "いつでも", "Anytime"),
            Entry("준비", "準備", "Ready"),
            Entry("집중", "集中", "Focus"),
            Entry("휴식", "休憩", "Break"),
            Entry("직접 Pomodoro 대사 추가", "ポモドーロ台詞を直接追加", "Add a Pomodoro message"),
            Entry(
                "저장된 Pomodoro 대사가 없습니다.",
                "保存されたポモドーロ台詞がありません。",
                "No Pomodoro messages are saved."),

            Entry("포모도로 음성 추가 확인", "ポモドーロ音声の追加確認", "Confirm Pomodoro Voices"),
            Entry(
                "후보 음성을 듣고 추가할 포모도로 대사를 선택해주세요.",
                "候補音声を聞いて、追加するポモドーロ台詞を選択してください。",
                "Preview the candidates and select Pomodoro messages to add."),
            Entry("선택 항목 추가", "選択項目を追加", "Add Selected"),
            Entry(
                "사용할 수 있는 포모도로 대사가 없습니다.",
                "使用できるポモドーロ台詞がありません。",
                "No Pomodoro messages are available."),
            Entry(
                "포모도로 후보 음성을 준비합니다.",
                "ポモドーロ候補音声を準備します。",
                "Preparing Pomodoro candidate voices."),
            Entry(
                "포모도로 후보 음성을 준비 중입니다. ({0}/{1})",
                "ポモドーロ候補音声を準備中です。({0}/{1})",
                "Preparing Pomodoro candidate voices. ({0}/{1})"),

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
                "포모도로 사용 상태를 저장하지 못했습니다.",
                "ポモドーロの使用状態を保存できませんでした。",
                "Failed to save the Pomodoro enabled state."),
            Entry(
                "이 포모도로 음성을 사용합니다.",
                "このポモドーロ音声を使用します。",
                "This Pomodoro voice is enabled."),
            Entry(
                "이 포모도로 음성을 사용하지 않습니다.",
                "このポモドーロ音声は使用しません。",
                "This Pomodoro voice is disabled."),
            Entry(
                "포모도로 대사는 비워둘 수 없습니다.",
                "ポモドーロ台詞は空にできません。",
                "The Pomodoro message cannot be empty."),
            Entry(
                "수정한 포모도로 대사를 저장하지 못했습니다.",
                "編集したポモドーロ台詞を保存できませんでした。",
                "Failed to save the edited Pomodoro message."),
            Entry(
                "포모도로 대사 상황을 저장하지 못했습니다.",
                "ポモドーロ台詞の状況を保存できませんでした。",
                "Failed to save the Pomodoro message situation."),
            Entry(
                "포모도로 대사를 수정했습니다. 음성은 재생성으로 갱신할 수 있습니다.",
                "ポモドーロ台詞を編集しました。音声は再生成で更新できます。",
                "Pomodoro message updated. Regenerate to update its voice."),
            Entry(
                "포모도로 대사를 삭제했습니다.",
                "ポモドーロ台詞を削除しました。",
                "Pomodoro message deleted."),
            Entry(
                "포모도로 대사를 삭제하지 못했습니다.",
                "ポモドーロ台詞を削除できませんでした。",
                "Failed to delete the Pomodoro message."),
            Entry(
                "추가할 포모도로 대사를 입력해주세요.",
                "追加するポモドーロ台詞を入力してください。",
                "Enter a Pomodoro message to add."),
            Entry(
                "포모도로 대사를 생성 중입니다",
                "ポモドーロ台詞を生成中です",
                "Generating Pomodoro messages"),
            Entry(
                "포모도로 대사 생성 서버에 연결할 수 없습니다.",
                "ポモドーロ台詞生成サーバーに接続できません。",
                "Unable to connect to the Pomodoro message server."),
            Entry(
                "포모도로 대사 생성에 실패했습니다. 다시 시도해주세요.",
                "ポモドーロ台詞の生成に失敗しました。もう一度お試しください。",
                "Failed to generate Pomodoro messages. Please try again."),
            Entry(
                "포모도로 대사 생성 결과가 올바른 JSON 리스트가 아닙니다.",
                "ポモドーロ台詞の生成結果が有効なJSONリストではありません。",
                "The Pomodoro response is not a valid JSON list."),
            Entry(
                "{0}개의 포모도로 음성을 추가했습니다.",
                "{0}件のポモドーロ音声を追加しました。",
                "Added {0} Pomodoro voices."),
            Entry(
                "{0}개 추가, {1}개 저장 실패",
                "{0}件追加、{1}件の保存に失敗",
                "Added {0}; failed to save {1}."),
            Entry(
                "포모도로 음성을 생성 중입니다.",
                "ポモドーロ音声を生成しています。",
                "Generating Pomodoro voices."),
            Entry(
                "{0}개의 포모도로 음성을 저장했습니다.",
                "{0}件のポモドーロ音声を保存しました。",
                "Saved {0} Pomodoro voices."),
            Entry(
                "{0}개 저장, {1}개 생성 실패",
                "{0}件保存、{1}件の生成に失敗",
                "Saved {0}; failed to generate {1}."),
            Entry(
                "포모도로 음성을 다시 생성 중입니다.",
                "ポモドーロ音声を再生成しています。",
                "Regenerating the Pomodoro voice."),
            Entry(
                "포모도로 음성 재생성에 실패했습니다.",
                "ポモドーロ音声の再生成に失敗しました。",
                "Failed to regenerate the Pomodoro voice."),
            Entry(
                "포모도로 음성을 저장하지 못했습니다.",
                "ポモドーロ音声を保存できませんでした。",
                "Failed to save the Pomodoro voice."),
            Entry(
                "포모도로 음성을 다시 생성했습니다.",
                "ポモドーロ音声を再生成しました。",
                "Pomodoro voice regenerated."),
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
                "저장된 포모도로 음성을 재생하지 못했습니다.",
                "保存されたポモドーロ音声を再生できませんでした。",
                "Failed to play the saved Pomodoro voice."),
            Entry("집중할 시간이에요.", "集中する時間です。", "It's time to focus.")
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
