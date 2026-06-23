# Jukebox UI 설계 (확정 스펙)

> 작성: 2026-06-20 · GRILL ME 라운드로 확정한 요구사항. 구현은 이 문서를 기준으로 자가검수한다.
> 레퍼런스: "Chill with You: Lo-Fi Story" (음악 트랙 선택 + 앰비언스 믹스 + 포모도로).

## 1. 재생 모델 — 하이브리드

- **음악(Music)**: 한 번에 1개만. 다른 음악 선택 시 crossfade로 전환. (EDM, JAZZ, Lo-fi, New Age, Sleep, Saxophone)
- **앰비언스(Ambience)**: 여러 개 동시 on/off, 각자 loop + 볼륨. (Beach, Cafe, Coffee Shop Ambience, Crowd, Fire Crackle, Rain)
- **SFX**: 짧은 효과음. 정해진 간격이 아니라 **트랙별 min/max 범위 안에서 랜덤 시각**에 1회씩 재생. (Thunder, Page Turning, Keyboard Typing, Clock Ticking, Chatter, Wind Blowing, Ocean Waves)

## 2. 오디오

- 소스: `StreamingAssets/Jukebox/BGM/<key>.ogg`, `StreamingAssets/Jukebox/SFX/<key>.ogg`
  (`UnityWebRequestMultimedia.GetAudioClip(file://..., AudioType)`로 런타임 로드, 확장자로 타입 판별)
- 파일이 없으면 해당 트랙은 **비활성(회색)** 표시, 재생 시도 안 함.
- BGM은 loop 전제. 시작 fade-in / 정지 fade-out. 음악 전환은 crossfade.
  (※ 한 트랙 내부의 끊김 없는 crossfade-loop는 1차 구현에서 단순 loop로 두고 추후 개선)
- SFX는 공용 one-shot 소스로 `PlayOneShot`, 재생 후 `now + Random(min,max)`로 다음 시각 예약.

## 3. 컨트롤

- 트랙별: on/off 토글, 볼륨 슬라이더
- SFX별: min/max 간격(초) 입력 (기본 30~60)
- 전역: 마스터 볼륨 슬라이더 1개 (모든 소리에 곱연산)
- 최종 음량 = master × trackVolume

## 4. 저장 (persistentDataPath/jukebox_settings.json)

- masterVolume, 그리고 트랙별 { id, enabled, volume, minInterval, maxInterval }
- 음악은 "마지막으로 켠 1개"만 enabled로 복원.

## 5. 프리팹 구조 (SkillView와 동일 방법론)

- 메인: `JukeboxView/Prefabs/JukeboxView.prefab` (정적 베이크, 컨트롤러 `JukeboxView`)
- 보조: `JukeboxView/Prefabs/JukeboxTrackRow.prefab` (행 템플릿, 컨트롤러 `JukeboxTrackRow`)
- 메인은 섹션(Music/Ambience/SFX)마다 보조 프리팹을 Instantiate해 행을 만든다.
- 베이크: `Tools → Jukebox → Build Jukebox Prefabs` (보조 → 메인 순, 메인이 보조를 참조)
- 런타임은 `BindExisting`(이중 모드): 베이크된 계층이면 다시 만들지 않고 참조만 연결.

## 6. 레이아웃 (개략)

```
┌─────────────────────────────────────────┐
│ Jukebox                  [마스터 ▭▭▭]  × │ Header
├─────────────────────────────────────────┤
│ MUSIC                                     │
│  ○ EDM        ▭▭▭▭                        │ (라디오 1개 선택)
│  ● Lo-fi      ▭▭▭▭▭                       │
│  ...                                      │
│ AMBIENCE                                  │
│  ☑ Rain       ▭▭▭▭▭▭                      │ (다중 토글)
│  ☑ Cafe       ▭▭▭                         │
│  ...                                      │
│ SFX                                       │
│  ☑ Thunder    ▭▭▭   [30]~[60]s            │ (토글+볼륨+간격)
│  ...                                      │
└─────────────────────────────────────────┘
```

## 7. 범위 밖 (이번 작업 제외)

- UIManager / UIPositionManager 연계
- 포모도로 타이머 연동
- 실제 오디오 에셋 제작/배치 (파일은 사용자가 StreamingAssets에 넣음)
