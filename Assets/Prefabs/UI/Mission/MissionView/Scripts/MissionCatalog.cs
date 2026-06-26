using System.Collections.Generic;

// 미션 "정의" 목록(읽기 전용). 내용 원본: Assets/Prefabs/UI/Mission/MISSION_Catalog.md
// 출하 후 외부 수정 불가(코드에 박힘). 진행 상태는 MissionManager/Repository가 따로 관리.
public static class MissionCatalog
{
    private static List<MissionDef> _all;

    public static IReadOnlyList<MissionDef> All
    {
        get
        {
            if (_all == null)
            {
                _all = Build();
            }

            return _all;
        }
    }

    public static MissionDef GetById(string id)
    {
        IReadOnlyList<MissionDef> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].id == id)
            {
                return all[i];
            }
        }

        return null;
    }

    public static List<MissionDef> GetByCategory(MissionCategory category)
    {
        List<MissionDef> result = new List<MissionDef>();
        IReadOnlyList<MissionDef> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].category == category)
            {
                result.Add(all[i]);
            }
        }

        return result;
    }

    // 메타 미션 여부 (카테고리 전체 달성 / 누적 미션 달성). 진행은 내부에서 계산.
    public static bool IsMeta(string id)
    {
        return id == "CH0002" || id == "CH0003" || id == "CH0004" || id == "CH0005" || id == "CH0006";
    }

    // ── 팩토리 헬퍼 ───────────────────────────────────────────────────────────
    private static MissionReward R(int gold, int i1 = 0, int i2 = 0, int i3 = 0)
    {
        return new MissionReward(gold, i1, i2, i3);
    }

    private static MissionDef One(string id, string name, MissionCategory cat, int target, MissionReward reward,
        string ko, string en, string ja)
    {
        MissionDef def = new MissionDef
        {
            id = id,
            name = name,
            category = cat,
            type = MissionType.OneTime,
            title = new LocalizedText(ko, en, ja),
        };
        def.tiers.Add(new MissionTier(target, reward));
        return def;
    }

    private static MissionDef Tier(string id, string name, MissionCategory cat, int[] targets, MissionReward[] rewards,
        string ko, string en, string ja)
    {
        MissionDef def = new MissionDef
        {
            id = id,
            name = name,
            category = cat,
            type = MissionType.Tiered,
            title = new LocalizedText(ko, en, ja),
        };
        for (int i = 0; i < targets.Length; i++)
        {
            MissionReward reward = i < rewards.Length ? rewards[i] : new MissionReward();
            def.tiers.Add(new MissionTier(targets[i], reward));
        }

        return def;
    }

    private static MissionDef Inc(string id, string name, MissionCategory cat, int a, int b, MissionReward reward,
        string ko, string en, string ja)
    {
        return new MissionDef
        {
            id = id,
            name = name,
            category = cat,
            type = MissionType.Increment,
            title = new LocalizedText(ko, en, ja),
            incrementA = a,
            incrementB = b,
            incrementReward = reward,
        };
    }

    private static List<MissionDef> Build()
    {
        return new List<MissionDef>
        {
            // ── 첫걸음 (OB) ──────────────────────────────────────────────────
            One("OB0001", "ob_meet_aico", MissionCategory.Onboarding, 1, R(50),
                "아이코를 처음 만나기", "Meet Aiko for the first time", "アイコと初めて出会う"),
            One("OB0002", "ob_talk_first", MissionCategory.Onboarding, 1, R(50),
                "'아이코'와 처음 대화해보기", "Talk to Aiko for the first time", "アイコと初めて会話する"),
            One("OB0003", "ob_change_char", MissionCategory.Onboarding, 1, R(50),
                "캐릭터 변경해보기", "Change your character", "キャラクターを変更する"),
            One("OB0004", "ob_lang_change", MissionCategory.Onboarding, 1, R(50),
                "주언어 설정 변경해보기 (Preference 제외)", "Change main language (excl. Preference)", "主言語設定を変更する(Preference除く)"),
            One("OB0005", "ob_head_pat", MissionCategory.Onboarding, 1, R(0, 1),
                "머리 쓰다듬어 보기", "Pat Aiko's head", "頭をなでてみる"),
            One("OB0006", "ob_open_settings", MissionCategory.Onboarding, 1, R(30),
                "설정 화면 열어보기", "Open the settings screen", "設定画面を開く"),
            One("OB0007", "ob_accessory_first", MissionCategory.Onboarding, 1, R(50),
                "액세서리 처음 착용해보기", "Equip an accessory for the first time", "アクセサリーを初めて装着する"),
            One("OB0008", "ob_open_jukebox", MissionCategory.Onboarding, 1, R(30),
                "주크박스 열어보기", "Open the jukebox", "ジュークボックスを開く"),

            // ── 대화 (CV) ────────────────────────────────────────────────────
            One("CV0001", "talk_emotion", MissionCategory.Conversation, 1, R(80),
                "감정 표현이 포함된 대화해보기", "Have a conversation with emotion", "感情表現を含む会話をする"),
            Tier("CV0002", "talk_joy_5", MissionCategory.Conversation,
                new[] { 10, 30, 50 }, new[] { R(150), R(300, 0, 1), R(500, 0, 2) },
                "\"기쁨\" 감정이 담긴 답변 받기", "Get joyful replies", "「喜び」がこもった返答をもらう"),
            Tier("CV0003", "talk_sad_5", MissionCategory.Conversation,
                new[] { 10, 30, 50 }, new[] { R(150), R(300), R(500) },
                "\"슬픔\" 감정이 담긴 답변 받기", "Get sad replies", "「悲しみ」がこもった返答をもらう"),
            One("CV0004", "talk_choice", MissionCategory.Conversation, 1, R(80),
                "선택지로 답변해보기", "Answer with a choice option", "選択肢で答えてみる"),
            Tier("CV0005", "talk_choice_start", MissionCategory.Conversation,
                new[] { 1, 5, 15 }, new[] { R(80), R(150), R(300) },
                "선택지로 대화 시작하기", "Start a conversation with a choice", "選択肢で会話を始める"),
            One("CV0006", "talk_banana", MissionCategory.Conversation, 1, R(100, 0, 0, 1),
                "답변에 '바나나' 포함하기", "Get \"banana\" in a reply", "返答に「バナナ」を含める"),
            Tier("CV0007", "talk_count_10", MissionCategory.Conversation,
                new[] { 10, 50, 100 }, new[] { R(100), R(300, 0, 1), R(600, 0, 2) },
                "대화하기", "Talk with Aiko", "アイコと会話する"),
            One("CV0008", "talk_long", MissionCategory.Conversation, 1, R(80),
                "한 번에 긴 대화 나누기", "Have a long conversation at once", "一度に長い会話をする"),
            Tier("CV0009", "talk_choice_10", MissionCategory.Conversation,
                new[] { 10, 30 }, new[] { R(200), R(400) },
                "선택지로 대화하기", "Have choice-based conversations", "選択肢で会話する"),

            // ── 교감 (AF) ────────────────────────────────────────────────────
            Inc("AF0001", "aff_pat", MissionCategory.Affection, 10, 0, R(100),
                "머리 쓰다듬기", "Pat Aiko's head", "頭をなでる"),
            One("AF0002", "aff_see_all_emotion", MissionCategory.Affection, 6, R(200, 0, 1),
                "모든 감정 표현 보기", "See all emotional expressions", "全ての感情表現を見る"),
            One("AF0003", "aff_char_change", MissionCategory.Affection, 5, R(120),
                "캐릭터 변경", "Change your character", "キャラクターを変更する"),
            Inc("AF0004", "aff_affinity_up", MissionCategory.Affection, 2, 1, R(100),
                "인연도 레벨업", "Level up affinity", "親密度をレベルアップする"),
            Tier("AF0005", "aff_accessory_buy", MissionCategory.Affection,
                new[] { 5, 15, 30 }, new[] { R(150), R(300), R(600) },
                "액세서리 구매하기", "Buy accessories", "アクセサリーを購入する"),

            // ── 생활 (PR) ────────────────────────────────────────────────────
            Tier("PR0001", "pro_alarm_create", MissionCategory.Productivity,
                new[] { 1, 5, 10 }, new[] { R(50), R(120), R(250) },
                "알람 만들기", "Create alarms", "アラームを作成する"),
            One("PR0002", "pro_timer_use", MissionCategory.Productivity, 1, R(50),
                "타이머 사용해보기", "Use a timer", "タイマーを使ってみる"),
            Tier("PR0003", "pro_pomodoro_1", MissionCategory.Productivity,
                new[] { 1, 5, 20 }, new[] { R(80), R(200, 0, 1), R(500) },
                "포모도로 완료하기", "Complete pomodoro sessions", "ポモドーロを完了する"),
            One("PR0004", "pro_todo_add", MissionCategory.Productivity, 1, R(40),
                "할 일 추가하기", "Add a to-do", "やることを追加する"),
            Inc("PR0005", "pro_todo_done_10", MissionCategory.Productivity, 10, 0, R(120),
                "할 일 완료하기", "Complete to-dos", "やることを完了する"),
            One("PR0006", "pro_calendar_add", MissionCategory.Productivity, 1, R(40),
                "일정 추가하기", "Add a calendar event", "予定を追加する"),
            One("PR0007", "pro_calendar_open", MissionCategory.Productivity, 1, R(30),
                "캘린더 열어보기", "Open the calendar", "カレンダーを開く"),
            One("PR0008", "pro_jukebox_play", MissionCategory.Productivity, 1, R(50),
                "음악 재생하기", "Play music", "音楽を再生する"),

            // ── 도전 (CH) ────────────────────────────────────────────────────
            Tier("CH0001", "cha_gold", MissionCategory.Challenge,
                new[] { 100, 1000, 5000 }, new[] { R(0, 1), R(0, 0, 1, 1), R(0, 0, 0, 3) },
                "골드 모으기", "Accumulate gold", "ゴールドを貯める"),
            Tier("CH0002", "cha_mission_all", MissionCategory.Challenge,
                new[] { 10, 25, 50 }, new[] { R(300), R(600, 0, 0, 1), R(1500, 0, 0, 2) },
                "미션 달성하기", "Clear missions", "ミッションを達成する"),
            One("CH0003", "cha_clear_ob", MissionCategory.Challenge, 1, R(300, 1),
                "'첫걸음' 미션 모두 달성", "Complete all Onboarding missions", "「はじめの一歩」を全て達成する"),
            One("CH0004", "cha_clear_cv", MissionCategory.Challenge, 1, R(400, 0, 1),
                "'대화' 미션 모두 달성", "Complete all Conversation missions", "「会話」ミッションを全て達成する"),
            One("CH0005", "cha_clear_af", MissionCategory.Challenge, 1, R(400, 0, 1),
                "'교감' 미션 모두 달성", "Complete all Affection missions", "「ふれあい」ミッションを全て達成する"),
            One("CH0006", "cha_clear_pr", MissionCategory.Challenge, 1, R(400, 0, 1),
                "'생활' 미션 모두 달성", "Complete all Productivity missions", "「生活」ミッションを全て達成する"),
            Tier("CH0007", "cha_gold_spend", MissionCategory.Challenge,
                new[] { 100, 1000, 5000 }, new[] { R(0, 1), R(0, 0, 1), R(0, 0, 0, 1) },
                "골드 소비하기", "Spend gold", "ゴールドを使う"),
            Tier("CH0008", "cha_item_own", MissionCategory.Challenge,
                new[] { 5, 20, 50 }, new[] { R(300), R(800), R(2000) },
                "아이템 모으기", "Own items", "アイテムを集める"),
        };
    }
}
