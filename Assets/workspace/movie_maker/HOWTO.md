# movie_maker 사용법 (HOWTO)

대본(검수된 음성 기준)에서 **좌/우 스타일 자막 + 다국어 SRT + 표정 PNG 큐 + 내레이션**을
자동 생성하고, 이를 **Premiere Pro로 가져오는** 방법.

> 이 도구가 맡는 단계 = 전체 흐름의 **S4(자산 생성)**. 대본 작성/번역·TTS·검수·합성·업로드는
> 사람/Premiere 몫. (전체 단계는 [CLAUDE.md](CLAUDE.md) "실제 제작 흐름" 참고)

---

## 0. 한 줄 답: Premiere로 부를 수 있나?

| 산출물 | Premiere | 방법 |
|---|---|---|
| `captions_greenscreen.mp4` | ✅ **권장** | 임포트 → 상단 트랙 → **Ultra Key(초록 제거)** → 스타일 자막이 영상 위로 |
| `proto_*.srt` (ko/en/ja) | ✅ | 캡션으로 임포트. 단 **타이밍·텍스트만**, 좌/우 위치·색은 안 살아남(전역 스타일 1종) |
| `narration.wav` | ✅ | 오디오 트랙으로 임포트 |
| `char_cue.csv` | ⚠️ 참고용 | 줄별 (화자·표정·구간·PNG). 캐릭터 표정 배치 가이드 |
| `timing.csv` | ⚠️ 참고용 | 줄별 시작/끝/길이. 영상 멈춤·배속 지점 가이드 |
| `proto.ass` | ❌ 직접 임포트 X | ASS는 Premiere가 못 읽음 → 위 greenscreen mp4가 이를 대체 |

**핵심**: 스타일 자막(위치·색·외곽선)을 Premiere에서 쓰려면 **greenscreen mp4를 Ultra Key**로 빼는 게
정답. (ASS를 투명 알파로 빼는 방식은 libass 한계로 글자까지 투명해져서 안 됨 — 검증함.)

---

## 1. 준비물 (이미 설치됨)

- Python venv: `venv\` (yt-dlp, edge-tts, pydub, pykakasi, Pillow, audioop-lts)
- ffmpeg 8.1.1 (winget 전역 설치)
- 폰트: Yu Gothic (이 PC에 meiryo 없어서 사용)

---

## 2. 대본 작성 (입력)

`data/ep1_master.txt` — **빈 줄로 구분된 블록**, 각 블록은 4줄:

```
<화자> [표정] : <KO 원문>
<화자> : <JA 한자>      ← 화면 자막용
<화자> : <JA 요미가나>  ← TTS 입력용(한자를 읽기 가나로)
<화자> : <EN>
```

- 화자: `arona`(좌측) / `plana`(우측)
- `[표정]` 태그: 선택. 예 `arona [laugh] : ...` → 그 줄 PNG = `arona_laugh.png`.
  없으면 `neutral` → `arona.png`(기본 png). **1대사 1표정** 기준.
- KO/EN/JA·요미가나 변환은 ChatGPT 등으로 만들어 채워 넣으면 됨(자동화 가치 낮은 부분).
  요미가나만 `scripts/yomigana_test.py`처럼 pykakasi로 보조 가능.

---

## 3. 음성 (TTS) — 검수 단계

현재 PoC는 **프로토 음성(edge-tts)** 을 자동 생성한다. 실제 운영은:

1. 요미가나(3번째 줄)를 본인 **GUI TTS**로 줄별 생성
2. 파일을 `scripts/poc/audio/` 에 **`001_arona.mp3`, `002_plana.mp3` …**(대본 순서) 로 저장
3. 이미 있는 파일은 재사용, **다시 뽑고 싶은 줄은 그 mp3만 삭제** 후 재실행

> ⚠️ 음성을 듣고 OK 한 **뒤에** 영상 배치/분량/배속을 정하는 게 맞음(원샷 아님).
> 자막·타이밍은 "확정된 음성"을 입력으로 계산된다.

---

## 4. 실행

```powershell
# 앞 N줄만(기본 8). 전체면 숫자 생략 또는 큰 수
venv\Scripts\python.exe scripts\pipeline_poc.py 8
```

산출물 → `scripts/poc/`:

| 파일 | 용도 |
|---|---|
| `captions_greenscreen.mp4` | **Premiere 자막 오버레이**(Ultra Key용) |
| `proto_ko.srt` / `_en.srt` / `_ja.srt` | 유튜브 CC 업로드 또는 Premiere 캡션 |
| `narration.wav` | 내레이션 오디오 트랙 |
| `char_cue.csv` | 줄별 표정 PNG 큐(캐릭터 배치 가이드) |
| `timing.csv` | 줄별 타이밍(멈춤·배속 가이드) |
| `proto.ass` | (내부용) 스타일 자막 원본 |
| `preview.mp4` | 확인용 데모(어두운 배경, 캐릭터 자리=박스) |

---

## 5. Premiere Pro 합성 순서

1. 화면 녹화 footage 배치 (멈춤/배속은 `timing.csv` 참고해 편집)
2. **캐릭터 PNG** 좌/우 배치 — 발화 구간은 `char_cue.csv`의 표정 PNG, 비발화자는 기본 png
3. `narration.wav` 오디오 트랙에 배치 (자막과 동일 타임코드)
4. `captions_greenscreen.mp4` 를 최상단 트랙에 → **이펙트 > Ultra Key** 적용,
   Key Color = 초록(0x00FF00) → 초록 제거, 스타일 자막만 남음
5. BGM/효과음 추가
6. 내보내기 → 유튜브 업로드. SRT는 유튜브 자막으로 직접 업로드

---

## 6. 튜닝 (레이아웃 설정값)

`scripts/pipeline_poc.py` 상단 config:

| 변수 | 의미 | 현재값 |
|---|---|---|
| `W, H` | 캔버스 | 1920×1080 |
| `FONT_SIZE` | 자막 크기 | 58 |
| `SAFE_W` | 한 줄 최대 폭(px). 넘으면 줄바꿈 | 1200 |
| `MARGIN_V` | 자막 하단 여백(px) | 130 |
| `SIDE_MARGIN` | 화자쪽 가장자리 여백 | 270 |
| `CHARBOX` | 캐릭터 자리(미리보기 박스용) | 측정값 |
| `VOICE` | 화자별 프로토 음성 | Nanami(피치변형) |

자막 색: `ASS_HEADER`의 Style 줄(아로나=파란 외곽선, 프라나=빨간 외곽선) 수정.

---

## 7. 한계 / 알아둘 것

- **표정 PNG 자동 합성은 아직 안 함** — char_cue.csv는 "어떤 png를 언제" 알려주는 표.
  실제 합성은 Premiere에서(또는 추후 자동 오버레이 기능 추가 가능).
- **Ultra Key 초록 경계 미세 프린지** 가능 — Ultra Key 설정으로 대부분 제거됨.
  완전 무손실 알파가 필요하면 2-pass(흑/백 렌더 차이) 알파 추출 추가 가능.
- 자동 줄바꿈이 **영문/숫자 연속을 중간에 끊을 수 있음**(예 `RTA`). 보완 예정.
- edge-tts는 **프로토**. 실제 캐릭터 음성 아님 → 본인 TTS로 교체 전제.
