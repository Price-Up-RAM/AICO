using System.Collections.Generic;

// 미션 "정의" 데이터베이스. 미션을 1줄씩 코드로 담는다(JSON 없음). MissionList가 Build()로 가져다 쓴다.
// 표 원본/편집 가이드: Assets/Prefabs/UI/Mission/MISSION_Catalog.md
// meta:true = 다른 미션 달성을 집계하는 메타 미션(달성 카운트/탭완료 계산에서 스스로 제외).
public static class MissionDatabase
{
    public static List<MissionInfo> Build()
    {
        return new List<MissionInfo>
        {
            // 첫걸음 (OB)
            One("OB0001", MissionTab.Onboarding, 1, R(50), "아이코를 처음 만나기", "Meet Aiko for the first time", "アイコと初めて出会う"),
            One("OB0002", MissionTab.Onboarding, 1, R(50), "'아이코'와 처음 대화해보기", "Talk to Aiko for the first time", "アイコと初めて会話する"),
            One("OB0003", MissionTab.Onboarding, 1, R(50), "캐릭터 변경해보기", "Change your character", "キャラクターを変更する"),
            One("OB0004", MissionTab.Onboarding, 1, R(50), "주언어 설정 변경해보기 (Preference 제외)", "Change main language (excl. Preference)", "主言語設定を変更する(Preference除く)"),
            One("OB0005", MissionTab.Onboarding, 1, R(0, 1), "머리 쓰다듬어 보기", "Pat Aiko's head", "頭をなでてみる"),
            One("OB0006", MissionTab.Onboarding, 1, R(30), "설정 화면 열어보기", "Open the settings screen", "設定画面を開く"),
            One("OB0007", MissionTab.Onboarding, 1, R(50), "액세서리 처음 착용해보기", "Equip an accessory for the first time", "アクセサリーを初めて装着する"),
            One("OB0008", MissionTab.Onboarding, 1, R(30), "주크박스 열어보기", "Open the jukebox", "ジュークボックスを開く"),

            // 대화 (CV)
            One("CV0001", MissionTab.Conversation, 1, R(80), "감정 표현이 포함된 대화해보기", "Have a conversation with emotion", "感情表現を含む会話をする"),
            Tier("CV0002", MissionTab.Conversation, T(10, 30, 50), Rs(R(150), R(300, 0, 1), R(500, 0, 2)), "\"기쁨\" 감정이 담긴 답변 받기", "Get joyful replies", "「喜び」がこもった返答をもらう"),
            Tier("CV0003", MissionTab.Conversation, T(10, 30, 50), Rs(R(150), R(300), R(500)), "\"슬픔\" 감정이 담긴 답변 받기", "Get sad replies", "「悲しみ」がこもった返答をもらう"),
            One("CV0004", MissionTab.Conversation, 1, R(80), "선택지로 답변해보기", "Answer with a choice option", "選択肢で答えてみる"),
            Tier("CV0005", MissionTab.Conversation, T(1, 5, 15), Rs(R(80), R(150), R(300)), "선택지로 대화 시작하기", "Start a conversation with a choice", "選択肢で会話を始める"),
            One("CV0006", MissionTab.Conversation, 1, R(100, 0, 0, 1), "답변에 '바나나' 포함하기", "Get \"banana\" in a reply", "返答に「バナナ」を含める"),
            Tier("CV0007", MissionTab.Conversation, T(10, 50, 100), Rs(R(100), R(300, 0, 1), R(600, 0, 2)), "대화하기", "Talk with Aiko", "アイコと会話する"),
            One("CV0008", MissionTab.Conversation, 1, R(80), "한 번에 긴 대화 나누기", "Have a long conversation at once", "一度に長い会話をする"),
            Tier("CV0009", MissionTab.Conversation, T(10, 30), Rs(R(200), R(400)), "선택지로 대화하기", "Have choice-based conversations", "選択肢で会話する"),
            // 최대값(best) 집계형 — 한 번의 대화에서의 최고치로 진행. 보고는 MissionList.ReportBest("CV0010", 그 대화의 바나나 수).
            Tier("CV0010", MissionTab.Conversation, T(5, 10), Rs(R(120), R(300, 0, 0, 1)), "한 번의 대화에 '바나나' 포함하기", "Get \"banana\" N times in one conversation", "一度の会話で「バナナ」をN回出す"),

            // 교감 (AF)
            Inc("AF0001", MissionTab.Affection, 10, 0, R(100), "머리 쓰다듬기", "Pat Aiko's head", "頭をなでる"),
            One("AF0002", MissionTab.Affection, 6, R(200, 0, 1), "모든 감정 표현 보기", "See all emotional expressions", "全ての感情表現を見る"),
            One("AF0003", MissionTab.Affection, 5, R(120), "캐릭터 변경", "Change your character", "キャラクターを変更する"),
            Inc("AF0004", MissionTab.Affection, 2, 1, R(100), "인연도 레벨업", "Level up affinity", "親密度をレベルアップする"),
            Tier("AF0005", MissionTab.Affection, T(5, 15, 30), Rs(R(150), R(300), R(600)), "액세서리 구매하기", "Buy accessories", "アクセサリーを購入する"),

            // 생활 (PR)
            Tier("PR0001", MissionTab.Productivity, T(1, 5, 10), Rs(R(50), R(120), R(250)), "알람 만들기", "Create alarms", "アラームを作成する"),
            One("PR0002", MissionTab.Productivity, 1, R(50), "타이머 사용해보기", "Use a timer", "タイマーを使ってみる"),
            Tier("PR0003", MissionTab.Productivity, T(1, 5, 20), Rs(R(80), R(200, 0, 1), R(500)), "포모도로 완료하기", "Complete pomodoro sessions", "ポモドーロを完了する"),
            One("PR0004", MissionTab.Productivity, 1, R(40), "할 일 추가하기", "Add a to-do", "やることを追加する"),
            Inc("PR0005", MissionTab.Productivity, 10, 0, R(120), "할 일 완료하기", "Complete to-dos", "やることを完了する"),
            One("PR0006", MissionTab.Productivity, 1, R(40), "일정 추가하기", "Add a calendar event", "予定を追加する"),
            One("PR0007", MissionTab.Productivity, 1, R(30), "캘린더 열어보기", "Open the calendar", "カレンダーを開く"),
            One("PR0008", MissionTab.Productivity, 1, R(50), "음악 재생하기", "Play music", "音楽を再生する"),

            // 도전 (CH) — 누적·마일스톤·메타
            Tier("CH0001", MissionTab.Challenge, T(100, 1000, 5000), Rs(R(0, 1), R(0, 0, 1, 1), R(0, 0, 0, 3)), "골드 모으기", "Accumulate gold", "ゴールドを貯める"),
            Tier("CH0002", MissionTab.Challenge, T(10, 25, 50), Rs(R(300), R(600, 0, 0, 1), R(1500, 0, 0, 2)), "미션 달성하기", "Clear missions", "ミッションを達成する", meta: true),
            One("CH0003", MissionTab.Challenge, 1, R(300, 1), "'첫걸음' 미션 모두 달성", "Complete all Onboarding missions", "「はじめの一歩」を全て達成する", meta: true),
            One("CH0004", MissionTab.Challenge, 1, R(400, 0, 1), "'대화' 미션 모두 달성", "Complete all Conversation missions", "「会話」ミッションを全て達成する", meta: true),
            One("CH0005", MissionTab.Challenge, 1, R(400, 0, 1), "'교감' 미션 모두 달성", "Complete all Affection missions", "「ふれあい」ミッションを全て達成する", meta: true),
            One("CH0006", MissionTab.Challenge, 1, R(400, 0, 1), "'생활' 미션 모두 달성", "Complete all Productivity missions", "「生活」ミッションを全て達成する", meta: true),
            Tier("CH0007", MissionTab.Challenge, T(100, 1000, 5000), Rs(R(0, 1), R(0, 0, 1), R(0, 0, 0, 1)), "골드 소비하기", "Spend gold", "ゴールドを使う"),
            Tier("CH0008", MissionTab.Challenge, T(5, 20, 50), Rs(R(300), R(800), R(2000)), "아이템 모으기", "Own items", "アイテムを集める"),
        };
    }

    // ── 작성 헬퍼 (1줄 표기용) ────────────────────────────────────────────────
    private static MissionReward R(int gold, int i1 = 0, int i2 = 0, int i3 = 0) => new MissionReward(gold, i1, i2, i3);
    private static int[] T(params int[] targets) => targets;
    private static MissionReward[] Rs(params MissionReward[] rewards) => rewards;

    private static MissionInfo One(string id, MissionTab tab, int target, MissionReward reward,
        string ko, string en, string ja, bool meta = false)
    {
        MissionInfo info = new MissionInfo
        {
            id = id, tab = tab, type = MissionType.OneTime, title = new LocalizedText(ko, en, ja), isMeta = meta,
        };
        info.tiers.Add(new MissionTier(target, reward));
        return info;
    }

    private static MissionInfo Tier(string id, MissionTab tab, int[] targets, MissionReward[] rewards,
        string ko, string en, string ja, bool meta = false)
    {
        MissionInfo info = new MissionInfo
        {
            id = id, tab = tab, type = MissionType.Tiered, title = new LocalizedText(ko, en, ja), isMeta = meta,
        };
        for (int i = 0; i < targets.Length; i++)
        {
            info.tiers.Add(new MissionTier(targets[i], i < rewards.Length ? rewards[i] : new MissionReward()));
        }

        return info;
    }

    private static MissionInfo Inc(string id, MissionTab tab, int a, int b, MissionReward reward,
        string ko, string en, string ja, bool meta = false)
    {
        return new MissionInfo
        {
            id = id, tab = tab, type = MissionType.Increment, title = new LocalizedText(ko, en, ja),
            incrementA = a, incrementB = b, incrementReward = reward, isMeta = meta,
        };
    }
}
