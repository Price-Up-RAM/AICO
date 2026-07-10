# Jukebox UI

사운드 패널. **BGM은 드롭다운에서 한 곡 선택**(음악+앰비언스 통합, crossfade 전환)하고,
**SFX(환경음)** 는 "환경음" 버튼으로 여는 별도 팝업에서 토글/볼륨/랜덤 간격으로 관리한다.
확정 스펙: `JUKEBOX_Design.md`.

## 프리팹은 2개뿐 (JukeboxTrackRow 없음)
- 메인 : `JukeboxView/Prefabs/JukeboxView.prefab` (`JukeboxView`) — BGM 드롭다운 + 볼륨 슬라이더 + 환경음 버튼
- 팝업 : `JukeboxView/Prefabs/JukeboxEnvironmentView.prefab` (`JukeboxEnvironmentView`) — SFX 행을 **내부에서 인라인 빌드**(별도 행 프리팹 없음)
- 공유 스크립트 : `JukeboxUi`(UI 팩토리), `JukeboxCatalog`(트랙 목록), `JukeboxSettings`(저장)
- 베이크 : `JukeboxView/Editor/JukeboxPrefabBuilder.cs`

---

## 현재 할 수 있는 일

- **BGM 드롭다운 선택** — "끄기" + 12곡. 선택 시 crossfade로 전환, loop, fade-in/out
- **볼륨 슬라이더**(마스터) — BGM/SFX에 곱연산
- **환경음(SFX) 팝업** — 헤더 "환경음" 버튼 → `JukeboxEnvironmentView` 인스턴스화. SFX별 on/off·볼륨·min/max 간격(초), 랜덤 시각 one-shot 재생
- **StreamingAssets 로드**(`Jukebox/BGM|SFX/<key>.ogg`), 없으면 회색 비활성
- **다운로드 곡 자동 등록** — 다운로더가 저장한 `persistentDataPath/Jukebox/download/*.mp3`를
  시작 시 스캔해 **download 카테고리**로 추가. 다운로드 직후에는 `AddDownloadedTrack()`,
  카테고리를 download/ALL로 고를 때도 폴더와 재동기화 (상세:
  `../JukeboxDownloader/JUKEBOX_DOWNLOADER_Design.md`)
- **ALL 카테고리** — 기본 트랙의 "bgm" 태그 카테고리를 대체하는 합성 카테고리(항상 맨 앞,
  기본 선택). 태그와 무관하게 전체 곡(기본+custom+download)을 보여준다.
  **download 카테고리는 곡이 0개여도 항상 드롭다운에 노출**된다(선택 시 폴더 재스캔 진입점).
- **설정 공유 저장**(`persistentDataPath/jukebox_settings.json`, 쓰기 직전 reload-머지)
- **정적 프리팹 + 이중 모드**(BindExisting). 드롭다운은 SkillView에서 검증된 TMP_Dropdown 구조 재사용
- 슬라이더는 Unity 표준 앵커 구조로 재작성(이전 Fill 음수 폭/빨간 X 해결)

## 데모
- 씬: `Demo/JukeboxDemo.unity` — 시작 시 `saxophone` BGM 자동 선택/재생
- 포함 음악(CC0): `StreamingAssets/Jukebox/BGM/saxophone.ogg`, `jazz.ogg`, `lofi.ogg`

---

## 하고 있는 일 / 남은 것

- [ ] **프리팹 재베이크 필수** — 구조 전면 변경(드롭다운화 + SFX 팝업 분리 + JukeboxTrackRow 삭제). `Tools → Jukebox → Build Jukebox Prefabs` 1회.
- [ ] 재베이크 전까지 옛 `JukeboxView.prefab`/데모 씬은 삭제된 JukeboxTrackRow를 참조해 **missing-script 에러**가 보일 수 있음(베이크하면 해소).
- [ ] Play 모드 실재생 검증(사용자): 드롭다운 곡 전환, 환경음 팝업 토글/슬라이더/간격.
- [ ] 나머지 트랙 `<key>.ogg`를 StreamingAssets에 추가.

---

## How to use

### 1. 프리팹 굽기 (코드 변경 후 반드시 1회)
- Unity 메뉴 **`Tools → Jukebox → Build Jukebox Prefabs`** (환경음 → 메인 순)

### 2. 오디오 파일
`Assets/StreamingAssets/Jukebox/BGM|SFX/`에 `<key>.ogg`. 키는 `JukeboxCatalog`의 Bgm/Sfx 참조.

### 3. 씬
- `JukeboxView.prefab`을 Canvas 하위에 배치. "환경음" 버튼이 `JukeboxEnvironmentView`를 런타임 인스턴스화(메인이 환경음 프리팹 참조).

### 4. 트랙 추가/변경
`JukeboxCatalog`의 `Bgm`/`Sfx`에 `TrackDef(id, 표시명, "BGM|SFX/파일")` 추가 후 재베이크.
