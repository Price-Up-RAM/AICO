using System.Collections.Generic;

/// <summary>
/// Jukebox 카탈로그. BGM은 더 이상 여기 정적 목록이 아니라 MRJukebox(playlist + tags)에서 온다.
/// 이 클래스는 SFX(환경음) 목록과 custom 폴더 상수만 보유한다.
/// </summary>
public static class JukeboxCatalog
{
    public class TrackDef
    {
        public string id;
        public string display;
        public string file; // StreamingAssets/Jukebox/ 기준 상대 경로

        public TrackDef(string id, string display, string file)
        {
            this.id = id;
            this.display = display;
            this.file = file;
        }
    }

    // custom BGM 카테고리가 읽는 폴더: StreamingAssets/bgm
    public const string CustomFolder = "bgm";
    public const string CustomTag = "custom";

    // SFX는 JukeboxEnvironmentView가 사용(플랫 목록).
    public static readonly List<TrackDef> Sfx = new List<TrackDef>
    {
        new TrackDef("thunder", "Thunder", "SFX/thunder.ogg"),
        new TrackDef("page_turning", "Page Turning", "SFX/page_turning.ogg"),
        new TrackDef("keyboard_typing", "Keyboard Typing", "SFX/keyboard_typing.ogg"),
        new TrackDef("clock_ticking", "Clock Ticking", "SFX/clock_ticking.ogg"),
        new TrackDef("chatter", "Chatter", "SFX/chatter.ogg"),
        new TrackDef("wind_blowing", "Wind Blowing", "SFX/wind_blowing.ogg"),
        new TrackDef("ocean_waves", "Ocean Waves", "SFX/ocean_waves.ogg"),
    };
}
