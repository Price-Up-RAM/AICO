# sprite_maker — 사용법 & 현황

캐릭터 PNG 1장에서 Unity 2D 애니메이션 자산까지 만드는 파이프라인.
생성·투명화는 **ComfyUI 워크플로**가, Unity 자산화는 **파이썬 스크립트 2개**가 담당.

```
PNG 1장
  │  [ComfyUI]  comfyui/sprite_wan22_5B_to_transparent_frames.json
  ▼            (Wan2.2 5B I2V → 배경제거 → 투명 PNG 프레임)
투명 프레임 시퀀스
  │  [py] media_to_frames.py   (프레임 솎기 + NN_<ms>.png 명명)
  ▼
  │  [py] make_unity_assets.py (.anim + .png.meta 생성)
  ▼
Unity 폴더방식 자산  →  D:\unity\AICO\Assets\Char2D\<char>\<action>\
```

---

## 현황 (할 수 있는 것 / 없는 것)

| 단계 | 도구 | 상태 |
|---|---|---|
| 생성: PNG 1장 → 액션 영상 | ComfyUI Wan2.2 | ✅ 워크플로 제공 |
| 투명화: 배경 제거(애니/치비) | ComfyUI rembg(isnet-anime) | ✅ 워크플로에 포함 |
| 모델 자동 다운로드 | ComfyUI 내장(로더 메타) | ✅ 누락 시 자동 |
| 프레임 솎기 + 명명 | `media_to_frames.py` | ✅ |
| Unity `.anim`/`.png.meta` 생성 | `make_unity_assets.py` | ✅ AICO 에셋과 바이트 단위 일치 |
| 애니메이터 컨트롤러 상태 연결 | — | ❌ 수동 (Unity 에디터에서) |
| 스프라이트 시트 슬라이싱 | — | ❌ 미구현 (폴더방식만) |

**검증 범위**: 파이썬 2개는 end-to-end 동작 검증됨. ComfyUI 워크플로는 JSON 구조·노드·링크
무결성만 검증(이 환경에 GPU 없음) → 실제 생성은 GPU 머신에서 확인 필요.

**공통 한계**: AI 생성이라 프레임 간 캐릭터 스타일이 미세하게 흔들릴 수 있음. 클립을 짧게(2~5s)
가는 것이 안전. 복잡한 디자인은 약간의 수동 보정이 필요할 수 있음.

---

## 사전 준비 (GPU 테스트 머신, Windows)

1. **ComfyUI** 설치.
2. **커스텀 노드 1개** — 배경제거용 `batchImg-rembg-ComfyUI-nodes`
   (ComfyUI Manager → Install via Git URL: `https://github.com/Mamaaaamooooo/batchImg-rembg-ComfyUI-nodes`,
   또는 `custom_nodes/` 에 clone 후 `pip install rembg onnxruntime`).
3. **모델**: 워크플로 로드 시 ComfyUI 가 누락 모델을 표준 폴더로 **자동 다운로드**(로더에 URL·폴더 메타 내장).
   자동 프롬프트가 안 뜨면 ComfyUI Manager의 "Install Models" 사용. 수동 배치 경로/URL:
   - `models/diffusion_models/wan2.2_ti2v_5B_fp16.safetensors` —
     `https://huggingface.co/Comfy-Org/Wan_2.2_ComfyUI_Repackaged/resolve/main/split_files/diffusion_models/wan2.2_ti2v_5B_fp16.safetensors`
   - `models/vae/wan2.2_vae.safetensors` —
     `https://huggingface.co/Comfy-Org/Wan_2.2_ComfyUI_Repackaged/resolve/main/split_files/vae/wan2.2_vae.safetensors`
   - `models/text_encoders/umt5_xxl_fp8_e4m3fn_scaled.safetensors` —
     `https://huggingface.co/Comfy-Org/Wan_2.1_ComfyUI_repackaged/resolve/main/split_files/text_encoders/umt5_xxl_fp8_e4m3fn_scaled.safetensors`
4. **파이썬**(워크스테이션): 3.x + `pip install Pillow`. (영상 입력을 쓸 때만 `pip install "imageio[ffmpeg]"`)

---

## 1단계 — ComfyUI: PNG → 투명 프레임

워크플로: **`comfyui/sprite_wan22_5B_to_transparent_frames.json`**
흐름: `LoadImage → Wan2.2 5B I2V → VAEDecode → Image Remove Background(rembg, isnet-anime) → SaveImage`

1. ComfyUI 에 워크플로 드래그.
2. `LoadImage` 에 기준 캐릭터 PNG 지정.
3. 프롬프트(positive)를 동작에 맞게 수정. 예: `walking in place, side view, seamless loop, consistent character design, simple flat background, no camera movement`.
4. 실행 → `ComfyUI/output/sprite_transparent/frame_*.png` 에 **투명 배경 프레임** 생성.

기본 설정: 480×832(세로), 49프레임. 16GB VRAM 여유. (해상도/프레임은 노드에서 조정 가능)

**더 높은 품질이 필요하면 (14B):** `comfyui/video_wan2_2_14B_i2v.official.json` 사용.
16GB 에서는 GGUF 로 교체 — `ComfyUI-GGUF` 설치 → QuantStack/bullerwins `Wan2.2-I2V-A14B-GGUF`
의 high/low `Q4_K_M.gguf` 를 `models/unet/` 에 두고, 두 `UNETLoader` 를 `Unet Loader (GGUF)` 로 교체.

**걷기/idle 루프를 깔끔하게:** `comfyui/video_wan2_2_14B_flf2v.official.json`(First-Last-Frame) 사용 →
첫·끝 이미지 로더에 **같은 PNG** 지정(첫=끝) → 순환. 이후 2단계에서 `--drop-last`.

---

## 2단계 — 프레임 솎기 & 명명 (media_to_frames.py)

ComfyUI 가 만든 프레임(보통 49장)을 스프라이트용(6~16장)으로 솎고 `NN_<ms>.png` 로 명명.

```bash
# output/sprite_transparent/ 의 frame_*.png 들을 walk_src/ 로 복사한 뒤:
python scripts/media_to_frames.py walk_src --out walk --fps 16 --max-frames 8 --drop-last
#   → walk/01_<ms>.png ... 생성 (투명 유지)
```
- `--max-frames K` : 최종 프레임 수(걷기 6~12, 점프 6~10, idle 4~8). 버린 프레임 시간은 직전에 합산(속도 유지).
- `--drop-last` : 루프(첫=끝)일 때 중복 끝프레임 제거.
- 입력으로 **GIF/mp4** 도 가능(`media_to_frames.py walk.gif ...`, `walk.mp4 --fps 16`; mp4는 imageio 필요).

---

## 3단계 — Unity 자산 생성 (make_unity_assets.py)

```bash
python scripts/make_unity_assets.py build walk --from-name --pivot bottom
#   → walk/walk.anim, walk/walk.anim.meta, walk/NN_<ms>.png.meta
```
- `--from-name` : 파일명 `NN_<ms>.png` 의 ms 를 프레임 타이밍으로 사용. (또는 `--fps 12` 균일)
- `--pivot bottom` : 서 있는 캐릭터(발 기준). UI 중앙정렬이면 `center`.
- `--name <이름>` : 애니메이션/클립 이름 지정(기본=폴더명).
- `--target-path Size/Image`(기본): 2D_General 프리팹 계층. 다른 프리팹이면 조정.
- `--dry-run` 미리보기, `--force` 덮어쓰기. 기본은 기존 파일 비파괴.

생성물은 AICO 폴더방식 레이아웃과 동일(단일 스프라이트 메타 + PPtr 커브 `.anim` + 루프).
내부 규칙(스프라이트 fileID, 경로 해시 crc32 등)은 [CLAUDE.md](CLAUDE.md) 참고.

---

## 4단계 — AICO 반영

```
walk/  →  D:\unity\AICO\Assets\Char2D\<character>\walk\
```
1. 폴더를 대상 캐릭터 아래로 복사.
2. Unity 에디터에서 해당 폴더 재임포트.
3. 애니메이터 컨트롤러에서 상태/전이로 연결(현재 수동).

> ⚠️ 기존 동작 폴더를 덮어쓰기 전 백업/확인.

---

## 한 동작 전체 예시 (걷기)

```bash
# 1) ComfyUI 에서 sprite_wan22_5B_to_transparent_frames.json 실행 → output/sprite_transparent/*.png
# 2) 그 PNG 들을 walk_src/ 로 복사한 뒤:
python scripts/media_to_frames.py walk_src --out walk --fps 16 --max-frames 8 --drop-last
python scripts/make_unity_assets.py build walk --from-name --pivot bottom
# 3) walk/ → D:\unity\AICO\Assets\Char2D\<char>\walk\ 복사 후 Unity 재임포트
```

---

## 파일 맵

```
comfyui/
  sprite_wan22_5B_to_transparent_frames.json   ← 1단계 메인 워크플로
  video_wan2_2_14B_i2v.official.json           ← 고품질(14B, GGUF 교체)
  video_wan2_2_14B_flf2v.official.json         ← 루프(First-Last-Frame)
scripts/
  media_to_frames.py    ← 2단계 (솎기·명명; GIF/mp4/PNG폴더 입력)
  make_unity_assets.py  ← 3단계 (.anim/.png.meta 생성)
CLAUDE.md               ← Unity 자산 내부 규칙(개발 참고)
HowTo.md                ← 이 문서
```
