using System.Collections.Generic;

/// <summary>
/// Jukebox catalog. BGM comes from MRJukebox playlist + tags.
/// SFX entries can point either to StreamingAssets/Jukebox-relative paths or
/// to project-local Assets/... paths for editor-installed audio.
/// </summary>
public static class JukeboxCatalog
{
    public class TrackDef
    {
        public string id;
        public string display;
        public string file;
        public string category;        // 명시적 카테고리 id (비우면 id 이름에서 추론)
        public string categoryDisplay; // 카테고리 표시명 (비우면 category / display에서 추론)

        // 3-인자: 카테고리는 이름 규칙으로 추론(하위 호환).
        public TrackDef(string id, string display, string file)
            : this(id, display, file, null, null) { }

        // 5-인자: 카테고리를 명시적으로 지정(이름과 무관하게 원하는 대로 묶임).
        public TrackDef(string id, string display, string file, string category, string categoryDisplay)
        {
            this.id = id;
            this.display = display;
            this.file = file;
            this.category = category;
            this.categoryDisplay = categoryDisplay;
        }
    }

    /// <summary>
    /// SFX 카테고리. 같은 종류(thunder1~4 등)를 한 항목으로 묶는다.
    /// id 는 UI 행 이름(Row_&lt;id&gt;) 및 설정 키로 쓰이고, tracks 안에서 랜덤 1개를 재생한다.
    /// </summary>
    public class SfxCategory
    {
        public string id;       // "thunder"
        public string display;  // "Thunder"
        public readonly List<TrackDef> tracks = new List<TrackDef>();

        public SfxCategory(string id, string display)
        {
            this.id = id;
            this.display = display;
        }
    }

    public const string CustomFolder = "bgm";
    public const string CustomTag = "custom";

    // 묶음 규칙: category(5·6번째 인자)를 넣으면 그걸로 묶고, 비우면(3-인자) id 이름 끝 숫자를 떼서 추론.
    //  - 이름 규칙이 명확한 항목은 3-인자로 두면 자동으로 묶인다(thunder1~4 → "thunder"/"Thunder").
    //  - 이름만으론 애매하거나 다른 이름을 한 묶음으로 합치고 싶을 때만 category 를 명시한다(아래 Cafe 예시).
    // ※ 새 category 를 추가하면 SFX 프리팹에 Row_<category> 행도 있어야 UI에 나온다(아래 안내 참고).
    public static readonly List<TrackDef> Sfx = new List<TrackDef>
    {
        new TrackDef("chatter1", "Chatter 1", "Assets/Audio/Sfx/Chatter1.mp3"),
        new TrackDef("chatter2", "Chatter 2", "Assets/Audio/Sfx/Chatter2.mp3"),
        new TrackDef("chatter3", "Chatter 3", "Assets/Audio/Sfx/Chatter3.mp3"),
        new TrackDef("clock_ticking1", "Clock Ticking 1", "Assets/Audio/Sfx/Clock Ticking1.mp3"),
        new TrackDef("clock_ticking2", "Clock Ticking 2", "Assets/Audio/Sfx/Clock Ticking2.mp3"),
        new TrackDef("clock_ticking3", "Clock Ticking 3", "Assets/Audio/Sfx/Clock Ticking3.mp3"),
        new TrackDef("keyboard_typing1", "Keyboard Typing 1", "Assets/Audio/Sfx/Keyboard Typing1.mp3"),
        new TrackDef("keyboard_typing2", "Keyboard Typing 2", "Assets/Audio/Sfx/Keyboard Typing2.mp3"),
        new TrackDef("keyboard_typing3", "Keyboard Typing 3", "Assets/Audio/Sfx/Keyboard Typing3.mp3"),
        new TrackDef("keyboard_typing4", "Keyboard Typing 4", "Assets/Audio/Sfx/Keyboard Typing4.mp3"),
        new TrackDef("ocean_waves1", "Ocean Waves 1", "Assets/Audio/Sfx/Ocean Waves1.mp3"),
        new TrackDef("ocean_waves2", "Ocean Waves 2", "Assets/Audio/Sfx/Ocean Waves2.mp3"),
        new TrackDef("ocean_waves3", "Ocean Waves 3", "Assets/Audio/Sfx/Ocean Waves3.mp3"),
        new TrackDef("page_turning1", "Page Turning 1", "Assets/Audio/Sfx/Page Turning1.mp3"),
        new TrackDef("page_turning2", "Page Turning 2", "Assets/Audio/Sfx/Page Turning2.mp3"),
        new TrackDef("page_turning3", "Page Turning 3", "Assets/Audio/Sfx/Page Turning3.mp3"),
        new TrackDef("thunder1", "Thunder 1", "Assets/Audio/Sfx/thunder1.mp3"),
        new TrackDef("thunder2", "Thunder 2", "Assets/Audio/Sfx/thunder2.mp3"),
        new TrackDef("thunder3", "Thunder 3", "Assets/Audio/Sfx/thunder3.mp3"),
        new TrackDef("thunder4", "Thunder 4", "Assets/Audio/Sfx/thunder4.mp3"),
        new TrackDef("wind_blowing1", "Wind Blowing 1", "Assets/Audio/Sfx/Wind Blowing1.mp3"),
        new TrackDef("wind_blowing2", "Wind Blowing 2", "Assets/Audio/Sfx/Wind Blowing2.mp3"),
        new TrackDef("wind_blowing3", "Wind Blowing 3", "Assets/Audio/Sfx/Wind Blowing3.mp3"),

        // ── 이름만으로 안 되는 경우엔 category 명시 (파일 준비 후 주석 해제) ──────────────
        // "Cafe 1~3" 와 "Cafe Ambience" 는 이름이 달라 추론하면 두 묶음이 되지만,
        // category:"cafe" 로 통일하면 한 행("Cafe")으로 묶이고 재생 시 그 안에서 랜덤 1개.
        // new TrackDef("cafe1",         "Cafe 1",        "Assets/Audio/Sfx/Cafe1.mp3",         "cafe", "Cafe"),
        // new TrackDef("cafe2",         "Cafe 2",        "Assets/Audio/Sfx/Cafe2.mp3",         "cafe", "Cafe"),
        // new TrackDef("cafe_ambience", "Cafe Ambience", "Assets/Audio/Sfx/Cafe Ambience.mp3", "cafe", "Cafe"),
    };

    // Sfx 를 "이름 뒤 숫자"를 떼어 카테고리로 묶는다. (thunder1~4 -> thunder / "Thunder")
    // 행 이름 Row_<id> 와 매칭되고, 재생 시 카테고리 안에서 랜덤 파일 1개를 고른다.
    public static IReadOnlyList<SfxCategory> SfxCategories => _sfxCategories;

    private static readonly List<SfxCategory> _sfxCategories = BuildCategories();

    private static List<SfxCategory> BuildCategories()
    {
        List<SfxCategory> cats = new List<SfxCategory>();
        Dictionary<string, SfxCategory> byId = new Dictionary<string, SfxCategory>();
        foreach (TrackDef d in Sfx)
        {
            // 명시적 category 우선, 없으면 id 이름에서 추론.
            string catId = !string.IsNullOrEmpty(d.category) ? d.category : TrimTrailingDigits(d.id);
            if (!byId.TryGetValue(catId, out SfxCategory cat))
            {
                string disp = !string.IsNullOrEmpty(d.categoryDisplay) ? d.categoryDisplay
                            : !string.IsNullOrEmpty(d.category) ? d.category
                            : TrimTrailingIndex(d.display);
                cat = new SfxCategory(catId, disp);
                byId[catId] = cat;
                cats.Add(cat);
            }
            cat.tracks.Add(d);
        }
        return cats;
    }

    private static string TrimTrailingDigits(string s)
    {
        int i = s.Length;
        while (i > 0 && char.IsDigit(s[i - 1])) i--;
        return s.Substring(0, i);
    }

    private static string TrimTrailingIndex(string s)
    {
        string t = s.TrimEnd();
        int i = t.Length;
        while (i > 0 && char.IsDigit(t[i - 1])) i--;
        return t.Substring(0, i).TrimEnd();
    }
}
