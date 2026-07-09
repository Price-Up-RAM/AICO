# JukeboxDownloader UI 설계

> 유튜브를 키워드로 검색해 결과(썸네일+제목)를 목록으로 보여주고, 각 행에서 mp3 다운로드를 요청한다.
> SkillView / Jukebox와 동일 방법론(EditorBuild 베이크 + 런타임 BindExisting, ServerManager 연동).

## 구성 (사용자 요청 레이아웃)

패널 420×520 (Jukebox 플레이어와 같은 폭). **레이아웃 그룹을 쓰지 않고 SkillView.prefab처럼
전부 고정 앵커로 배치**한다 (동적 리스트 행 내부만 레이아웃 그룹).

- `Handler` — JukeboxView.prefab 방식. 첫 자식으로 패널 전체를 덮는 투명 Image + DragUIHandler.
  버튼/입력이 위에 있어 클릭은 그대로 통하고, 빈 곳을 잡으면 창이 끌린다.
- `Header` — 패딩 없이 패널 폭을 꽉 채우는 고정 높이 36 바(HeaderBg). 제목/개수/×는 명시 앵커.

```
┌──────────────────────────────┐
│ Jukebox Downloader          ×│ Header (full-width bar, 패딩 없음)
╞══════════════════════════════╡
│ [v][ 검색어 입력 .. ] [ 검색 ]│ SearchRow (왼쪽 정사각형 = 필터 토글, Enter 로도 검색)
│ [정렬▾][기간▾][길이▾][개수▾] │ FilterRow (기본 닫힘 — 토글로 펼침)
│ ┌thumb┐ 제목           [받기]│ Results (우측 세로 스크롤, 행 hover = 상세 툴팁)
│ └─────┘ 채널·5:27·128만회    │
│  (결과 없으면 안내 라벨)     │
└──────────────────────────────┘
```

모든 정적 요소의 RectTransform이 프리팹에 그대로 구워져 있어 **런타임 위치 재조정이 없다**
(BindExisting은 참조/리스너 연결만 한다). 필터 토글 클릭 시에만 Results top이 코드로 움직인다.

## 파일

| 파일 | 역할 |
|------|------|
| `Scripts/JukeboxDownloaderView.cs` | UI 컨트롤러 (헤더/검색/필터/결과 스크롤/빈상태). EditorBuild+BindExisting 이중모드 |
| `Scripts/JukeboxDownloaderTooltip.cs` | hover 상세 툴팁 + 행 hover 컴포넌트 (InventoryTooltip 방법론의 독립 구현) |
| `Scripts/JukeboxDownloaderClient.cs` | 서버 연동. `ServerManager.GetBaseUrl` → `/youtube/*` 호출 |
| `Editor/JukeboxDownloaderPrefabBuilder.cs` | 프리팹 베이커. 메뉴 `Tools/JukeboxDownloader/Build Prefab` (SUIT-Bold로 굽는다) |
| `Editor/JukeboxDownloaderFontApply.cs` | 폰트 마무리. 메뉴 `Tools/JukeboxDownloader/Apply SUIT-Bold Font` |
| `Prefabs/JukeboxDownloader.prefab` | 베이크 산출물 (메뉴 실행 시 생성) |

## 서버 API 매핑 (server_impl_youtube.py)

- 검색   : `GET /youtube/search?q=&limit=&sort=views&recent=1&max_duration=`
- 다운로드 : `POST /youtube/download` `{ "url": "..." }` → `{ "job_id": ... }`
- 진행률  : `GET /youtube/progress/<job_id>` 폴링 → status(downloading/converting/completed/error)

## 필터 → 쿼리 매핑

검색 행 왼쪽 정사각형 버튼(^/v)으로 필터 행 전체를 접고 펼 수 있다 (접으면 결과 영역 확장).
**기본값은 닫힘**(프리팹에 FilterRow 비활성 + Results 확장 상태로 베이크됨).

- 정렬: 관련성순 → (없음) / 조회수순 → `sort=views` / 최신순 → `sort=date`
- 기간: 전체 → (없음) / 오늘·이번주·이번달·올해 → `period=today|week|month|year`
- 길이: 전체 → (없음) / 짧음·중간·김 → `duration=short|medium|long`
  (서버가 카테고리만 지원 — 정확한 시간 필터 불가)
- 개수: 5/10/20 → `limit=` (서버 1~30 지원, 기본 10)

## 결과 행 UX

- 제목/메타는 Truncate로 자른다 (SUIT-Bold에 '…' 글리프가 없어 Ellipsis는 □로 깨짐).
- 행에 마우스를 올리면 `JukeboxDownloaderTooltip`이 커서 옆에 전체 제목 + 채널/길이/조회수를 띄운다.
- 다운로드 진행률/완료/실패는 별도 칸 없이 **받기 버튼 라벨**에 표시. 실패/서버없음이면 재시도 가능.

## 사용 방법

1. Unity 메뉴 **Tools → JukeboxDownloader → Build Prefab** 실행 → `Prefabs/JukeboxDownloader.prefab` 생성/갱신.
   (레이아웃/헤더를 바꾸면 이 메뉴로 **다시 구워야** 반영됨)
2. 씬(Canvas 하위)에 프리팹 배치. `ServerManager`가 씬에 있어야 검색/다운로드 서버 연동 동작.
3. 파이썬 서버가 `/youtube/*` 엔드포인트를 제공해야 함(server_impl_youtube.py).

## 데모 씬 (`Demo/JukeboxDownloaderDemo.unity`)

- 서버 없이 UI를 확인하는 용도. `JukeboxDownloaderDemo`가 붙어 있고, **키보드 1/2/3** 으로
  실제 `/youtube/search` 반환값 기반 mock 결과를 주입한다(0 = 비우기).
  - 1: `lofi`  ·  2: `따뜻한 음악`  ·  3: `카마도 탄지로의 노래`
- 썸네일은 실제 i.ytimg.com URL이라 인터넷이 되면 이미지도 로드된다.
- 데모 씬 자체는 이미 생성돼 있으며, 씬 생성용 에디터 빌더는 제거했다(일회성 스캐폴딩).

## Jukebox 플레이어 연동

- JukeboxView 헤더에 **[유튜브 아이콘 + Download] 버튼**이 있다 (SFX 버튼 왼쪽).
  클릭 시 `JukeboxView.ToggleDownloader()`가 이 프리팹을 별도 창으로 토글한다
  (SFX/ToggleEnvironment와 동일 패턴, SFX가 오른쪽에 열리므로 다운로더는 **왼쪽**에 열림).
- 버튼 주입은 `Tools → Jukebox → Inject Download Button`
  (`Jukebox/JukeboxView/Editor/JukeboxDownloadButtonInject.cs`, 재실행 안전).
  JukeboxView.prefab은 손질된 구조라 재베이크 금지 — 이 메뉴로만 갱신한다.
- 아이콘: `Sprites/YoutubeIcon.png` (주입 스크립트가 코드로 생성).

## 범위 밖 (필요 시 추가)

- UIManager / UIPositionManager 등록 (다른 UI처럼 `ShowJukeboxDownloader()` 등). `Skills/CLAUDE.md` 3~4절 참고.
- 썸네일은 Unity가 URL로 직접 로드(프록시 불필요). WebGL이면 `/youtube/thumbnail/<id>` 프록시로 전환 필요.

## 검증 상태

- 2026-07-09 batchmode 베이크: 컴파일 에러 0, 프리팹 420×520 재생성, SUIT-Bold 적용(TMP_Text 13개) 확인.
- Play 모드 실기(검색/다운로드/썸네일 로드)는 미검증 — 사용자 확인 필요.
