using UnityEngine;
using Track = JukeboxDownloaderView.Track;

/// <summary>
/// JukeboxDownloader 데모. 서버 없이 UI를 확인할 수 있도록, 키보드 1/2/3 으로
/// mock 검색 결과를 주입한다. (0 을 누르면 비운다)
///
/// mock 데이터는 실제 youtube 검색기(/youtube/search)가 반환한 값을 그대로 옮긴 것이다.
///   1: "lofi"   2: "따뜻한 음악"   3: "카마도 탄지로의 노래"
///
/// 썸네일은 실제 i.ytimg.com URL이므로 인터넷이 되면 이미지도 로드된다.
/// (실제 다운로드는 씬의 ServerManager가 파이썬 서버에 연결돼 있어야 동작)
/// </summary>
public class JukeboxDownloaderDemo : MonoBehaviour
{
    [SerializeField] private JukeboxDownloaderView view;

    private void Start()
    {
        if (view == null)
        {
            view = FindObjectOfType<JukeboxDownloaderView>();
        }
        if (view == null)
        {
            Debug.LogWarning("[JukeboxDownloaderDemo] JukeboxDownloaderView를 찾지 못했습니다.");
            return;
        }
        view.Show();
        Debug.Log("[JukeboxDownloaderDemo] 키 1/2/3 = mock 결과 주입, 0 = 비우기");
    }

    private void Update()
    {
        if (view == null)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            view.SetResults(MockLofi());
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            view.SetResults(MockWarm());
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            view.SetResults(MockTanjiro());
        }
        else if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0))
        {
            view.ClearResults();
        }
    }

    // ── 실제 /youtube/search 반환값 기반 mock ─────────────────────────────────
    // 'lofi' 실제 검색 결과 5개
    private static Track[] MockLofi() => new[]
    {
        new Track { videoId="4xDzrJKXOOY", title="synthwave radio 🌌 beats to chill/game to", url="https://www.youtube.com/watch?v=4xDzrJKXOOY", channel="Lofi Girl", durationStr="??:??", viewsStr="조회수 미상", thumbnailHq="https://i.ytimg.com/vi/4xDzrJKXOOY/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/4xDzrJKXOOY/hq720.jpg?v=698f87d4&sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLD9nmtPNUcnouDajpqg0K_8ZoYpuQ" },
        new Track { videoId="X4VbdwhkE10", title="lofi hip hop radio 📚 beats to relax/study to", url="https://www.youtube.com/watch?v=X4VbdwhkE10", channel="Lofi Girl", durationStr="??:??", viewsStr="조회수 미상", thumbnailHq="https://i.ytimg.com/vi/X4VbdwhkE10/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/X4VbdwhkE10/hq720.jpg?v=6a212016&sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLAgn4FKbiWr9gXiEhIf0t5Y6y9TuQ" },
        new Track { videoId="sF80I-TQiW0", title="90's Chill Lofi ☕️ Study Music Lofi Rain Chillhop Beats ☔️ Lofi Rain Playlist", url="https://www.youtube.com/watch?v=sF80I-TQiW0", channel="The Japanese Town", durationStr="11:53:45", viewsStr="2564.7만회", thumbnailHq="https://i.ytimg.com/vi/sF80I-TQiW0/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/sF80I-TQiW0/hq720.jpg?sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLDvC6F5Dp3k6NUG05KMqn8XpBFQuQ" },
        new Track { videoId="IxPANmjPaek", title="medieval lofi radio 🏰 - beats to scribe manuscripts to", url="https://www.youtube.com/watch?v=IxPANmjPaek", channel="Lofi Girl", durationStr="??:??", viewsStr="조회수 미상", thumbnailHq="https://i.ytimg.com/vi/IxPANmjPaek/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/IxPANmjPaek/hq720.jpg?v=67ff8cac&sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLCu_JlwgTXaSEPEOfXb7ICEACZXHQ" },
        new Track { videoId="QwYKO-SCRaI", title="Japanese Beach 🌤️ Summer Lofi at Balcony Ocean 🌊 Morning Vibe & Lofi Hip Hop to Calm, Stress Relief", url="https://www.youtube.com/watch?v=QwYKO-SCRaI", channel="LOFI KEEP YOU SAFE", durationStr="3:49:53", viewsStr="7.8만회", thumbnailHq="https://i.ytimg.com/vi/QwYKO-SCRaI/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/QwYKO-SCRaI/hq720.jpg?sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLD1-JcpUgSkkcDFO4yN1uFnLYWueg" },
    };

    // '따뜻한 음악' 실제 검색 결과 5개
    private static Track[] MockWarm() => new[]
    {
        new Track { videoId="gVqYuE5IKYg", title="너의 마음을 들어줄 따뜻한 음악들 ⎮ 중간광고없음 ⎮ 집중 공부 힐링 휴식 감성 명상 태교음악 카페음악", url="https://www.youtube.com/watch?v=gVqYuE5IKYg", channel="Cold Water", durationStr="10:00:00", viewsStr="195.4만회", thumbnailHq="https://i.ytimg.com/vi/gVqYuE5IKYg/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/gVqYuE5IKYg/hq720.jpg?sqp=-oaymwEgCNAFEJQDSFXyq4qpAxIIARUAAIhCGAFwAcABBrgC8xg=&rs=AOn4CLCiCMUOhSKI5BMDQCrMttTMm0sZmg" },
        new Track { videoId="GQ3eehbCAyo", title="A warm piano suite that soothes the tired and weary mind", url="https://www.youtube.com/watch?v=GQ3eehbCAyo", channel="BeiGe Mellow 베이지멜로우", durationStr="5:34:38", viewsStr="412.4만회", thumbnailHq="https://i.ytimg.com/vi/GQ3eehbCAyo/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/GQ3eehbCAyo/hq720.jpg?sqp=-oaymwEgCNAFEJQDSFXyq4qpAxIIARUAAIhCGAFwAcABBrgC8xg=&rs=AOn4CLBpEDnOfioH0HB_5vEUFDD3iwxvKQ" },
        new Track { videoId="VAoR8bL04qA", title="부드럽게 듣기 좋은 행복음악 모음 🌸 입춘을 반기는 온화한 멜로디", url="https://www.youtube.com/watch?v=VAoR8bL04qA", channel="케어멜로디 caremelody", durationStr="3:03:19", viewsStr="247.4만회", thumbnailHq="https://i.ytimg.com/vi/VAoR8bL04qA/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/VAoR8bL04qA/hq720.jpg?sqp=-oaymwEgCNAFEJQDSFXyq4qpAxIIARUAAIhCGAFwAcABBrgC8xg=&rs=AOn4CLC5J4n-tNQ4PPfbpLEc9mMZANks5Q" },
        new Track { videoId="XlI5p2EsepY", title="Cafe Playlist ☕ 도입부부터 너무 좋은 겨울 카페 플리 🎶❄️ | A Perfect Winter Cafe Playlist from the Start", url="https://www.youtube.com/watch?v=XlI5p2EsepY", channel="cherry music", durationStr="3:03:30", viewsStr="124.5만회", thumbnailHq="https://i.ytimg.com/vi/XlI5p2EsepY/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/XlI5p2EsepY/hq720.jpg?sqp=-oaymwEgCNAFEJQDSFXyq4qpAxIIARUAAIhCGAFwAcABBrgC8xg=&rs=AOn4CLACOiEDpshFqMkIPT5LrdSjIfl02g" },
        new Track { videoId="sS1R-P2JV-c", title="따뜻한 음악이 흐르는 카페 l GRASS COTTON+", url="https://www.youtube.com/watch?v=sS1R-P2JV-c", channel="GRASS COTTON 그래스코튼", durationStr="3:01:17", viewsStr="74.0만회", thumbnailHq="https://i.ytimg.com/vi/sS1R-P2JV-c/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/sS1R-P2JV-c/hqdefault.jpg?sqp=-oaymwEgCOADEI4CSFXyq4qpAxIIARUAAIhCGAFwAcABBrgC8xg=&rs=AOn4CLAKYzFY4AkHjxV5Bd3vBpb6bxS_MA" },
    };

    // '카마도 탄지로의 노래' 실제 검색 결과 5개
    private static Track[] MockTanjiro() => new[]
    {
        new Track { videoId="vcB209kumLY", title="소중한 것을 지키기 위해🔥 : 시이나 고 - 카마도 탄지로의 노래 (Feat. 나카가와 나미)　[가사/자막/발음/해석]ㅣ귀멸의 칼날 1기 ED (EP19)", url="https://www.youtube.com/watch?v=vcB209kumLY", channel="화복화 [BokHwa / 花復花]", durationStr="6:27", viewsStr="128.5만회", thumbnailHq="https://i.ytimg.com/vi/vcB209kumLY/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/vcB209kumLY/hq720.jpg?sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLByI-yPNZ7T-Ij6oWTwJo2MSb-LMw" },
        new Track { videoId="Jo1dql9OqSs", title="나카가와 나미 - 카마도 탄지로의 노래(竈門炭治郎のうた) [가사/발음/해석]", url="https://www.youtube.com/watch?v=Jo1dql9OqSs", channel="지구의 가사집 [J-POP]", durationStr="5:26", viewsStr="3.0만회", thumbnailHq="https://i.ytimg.com/vi/Jo1dql9OqSs/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/Jo1dql9OqSs/hq720.jpg?sqp=-oaymwE2CNAFEJQDSFXyq4qpAygIARUAAIhCGAFwAcABBvABAfgB_gmAAtAFigIMCAAQARgUIGUoUTAP&rs=AOn4CLAsFaxm_jaM_GWCrLA4cpIvgh0Kig" },
        new Track { videoId="QJJjLKeiuuU", title="Tanjiro Kamado's Song -OST version-", url="https://www.youtube.com/watch?v=QJJjLKeiuuU", channel="Go Shiina - Topic", durationStr="5:31", viewsStr="57.2만회", thumbnailHq="https://i.ytimg.com/vi/QJJjLKeiuuU/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/QJJjLKeiuuU/hq720.jpg?sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLAy_5AEuUhoQX94wV7yyWwIL_Y_7Q" },
        new Track { videoId="KwX212taIlY", title="귀멸의칼날 OST | 카마도 탄지로의 독음, 가사 | 귀멸의칼날 19.ED | 귀멸의칼날", url="https://www.youtube.com/watch?v=KwX212taIlY", channel="はち하치", durationStr="5:25", viewsStr="680.5만회", thumbnailHq="https://i.ytimg.com/vi/KwX212taIlY/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/KwX212taIlY/hq720.jpg?sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLDbecdTBk9KNI5Gp473gBAXsY0ftA" },
        new Track { videoId="crQW9bIxMys", title="[윰탁스튜디오] 귀멸의 칼날 - 탄지로의 노래 | 잠잘때 듣기 좋은 음악 8시간 재생(30분후 화면꺼짐) | Relaxing sleep music | 수면음악 | 피아노 | 꿀잠", url="https://www.youtube.com/watch?v=crQW9bIxMys", channel="YumTak Studio 윰탁스튜디오", durationStr="8:06:16", viewsStr="277.4만회", thumbnailHq="https://i.ytimg.com/vi/crQW9bIxMys/hqdefault.jpg", thumbnail="https://i.ytimg.com/vi/crQW9bIxMys/hq720.jpg?sqp=-oaymwEcCNAFEJQDSFXyq4qpAw4IARUAAIhCGAFwAcABBg==&rs=AOn4CLBuNEeb6QEXT-pP819Ov-ND830RyQ" },
    };
}
