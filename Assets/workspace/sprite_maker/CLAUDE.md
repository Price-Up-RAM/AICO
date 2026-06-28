# sprite_maker

AICO Unity 프로젝트의 2D 캐릭터 애니메이션 제작을 돕는 도구 모음.
캐릭터 PNG 1장 → (ComfyUI 생성·투명화) → (파이썬 솎기·Unity 자산화) → AICO 폴더방식 자산.

> 사용법/현황은 **[HowTo.md](HowTo.md)**. 이 문서는 Unity 자산 생성의 **내부 규칙(개발 참고)**.

---

## 대상 프로젝트

- Unity 루트: `D:\unity\AICO`
- 캐릭터 2D 에셋: `D:\unity\AICO\Assets\Char2D\<character>`
- 프리팹: `D:\unity\AICO\Assets\Prefabs\Char2D\` (예: `2D_General.prefab`)

> ⚠️ AICO 파일을 덮어쓰기 전 백업/확인. 스크립트는 에셋(.png/.anim/.meta)을 생성하고
> Unity가 재임포트하도록 두는 방식. 기존 파일은 기본 비파괴(`--force` 필요).

---

## Unity 자산 규칙 (리버스 엔지니어링, make_unity_assets.py 의 근거)

AICO에는 두 레이아웃이 있으며, 현재 자동화는 **레이아웃 A(프레임 폴더)** 만 생성한다.

### 레이아웃 A — 프레임 폴더 방식 (`arona`, `shiroko`) ← 생성 대상
```
Char2D/shiroko/walk/
  01_200.png  01_200.png.meta   ...   walk.anim   walk.anim.meta
```
- 파일명 `NN_<ms>.png`: `NN`=프레임 순서(2자리), `<ms>`=프레임 노출 시간.
- 프레임 `.png.meta`: `spriteMode: 1`(Single), sprite sub-asset **fileID = `21300000`** 고정,
  단일모드 spriteID 고정값 `5e97eb03825dee720800000000000000`.
- `.anim` 은 각 프레임을 `{fileID: 21300000, guid: <PNG 텍스처 guid>}` 로 참조.

### 레이아웃 B — 스프라이트 시트 방식 (`arona_ball`) ← 미구현
- 애니메이션당 PNG 1장을 `spriteMode: 2` 로 슬라이싱, 서브 스프라이트가 `internalID` 보유.
- `.anim` 은 `{fileID: <internalID>, guid: <시트 guid>}` 로 참조.

### `.anim` (AnimationClip) 구조
- 스프라이트 교체 = `m_PPtrCurves` 의 `m_Sprite` 어트리뷰트, 타깃 경로 `Size/Image`(UI `Image`).
  - script `{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc}` = `UnityEngine.UI.Image`(classID 114).
  - `m_ClipBindingConstant` 에도 동일 바인딩. **바인딩 path 해시 = `crc32(path)`** (`Size/Image`→2438195069, `Size`→1475513172).
- `time`(초) = duration_ms 누적. 클립 길이는 `Size` 스케일 커브(값 1 고정) 끝키로 확정 → 마지막 프레임이
  자기 duration 만큼 표시된 뒤 LoopTime 으로 0번 복귀.
- `.anim.meta`: `NativeFormatImporter`, `mainObjectFileID: 7400000`.

### 프리팹 계층 (2D_General)
```
2D_General (Animator[m_Controller] + Collider)
  └ Size (스케일 커브 타깃)
      └ Image (스프라이트 교체 타깃)  ← path "Size/Image"
```

### 텍스처 임포트 (`.png.meta` TextureImporter)
- `textureType: 8`(Sprite), `alphaIsTransparency: 1`, `spritePixelsToUnits: 100`, `spriteMode: 1`.
- pivot/alignment: 서 있는 캐릭터는 alignment 7(BottomCenter)/pivot {0.5,0}, UI 중앙은 alignment 0/pivot{0.5,0.5}.

> `make_unity_assets.py` 생성물은 실제 AICO 에셋(shiroko/walk)과 **바이트 단위로 일치**(guid/시간 데이터 제외).
> 결정적 GUID = Assets 상대경로 기반 md5 → 재실행 안정.

---

## 파이프라인 / 파일

| 단계 | 도구 | 비고 |
|---|---|---|
| 생성+투명화 | ComfyUI `comfyui/sprite_wan22_5B_to_transparent_frames.json` | Wan2.2 5B I2V → rembg(isnet-anime) → 투명 프레임. 모델 누락 시 ComfyUI 내장 자동 다운로드 |
| 솎기+명명 | `scripts/media_to_frames.py` | PNG폴더/GIF/mp4 → `NN_<ms>.png` (`--max-frames`, `--drop-last`) |
| Unity 자산 | `scripts/make_unity_assets.py` | `.anim` + `.png.meta` 생성 |
| 컨트롤러 연결 | — | 미구현(Unity 에디터에서 수동) |

생성 백엔드는 **WAN 확정**. 고품질은 14B GGUF, 루프는 FLF2V — [HowTo.md](HowTo.md) 참고.

---

## 작업 규칙

- 사용자는 한국어로 소통. 기술 용어/코드는 영어.
- 스크립트는 Windows/Unix 경로를 안전하게 처리.
- Unity YAML 생성 시 위 규칙을 정확히 따를 것. 의심되면 기존 AICO 에셋을 먼저 읽어 대조.
- GPU 없는 워크스테이션에선 ComfyUI 실행 검증 불가 — JSON 구조/링크만 검증, 실측은 GPU 머신.
