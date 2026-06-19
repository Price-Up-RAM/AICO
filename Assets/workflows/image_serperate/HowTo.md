# HowTo — 캐릭터 요소 분해 (Qwen-Image-Edit 2509)

캐릭터 1장(이미 **T 포즈**)을 입력하면 머리·재킷·장갑·고글 등 **요소별로 "흰 배경에 그것만"** 추출한다.
civitai "Outfit Extractor - Qwen Edit" 와 같은 **생성형 편집(방식 A)** 방식이며,
ComfyUI 내장 공식 템플릿 `image_qwen_image_edit_2509.json` 의 파이프라인을 요소 수만큼 복제한 것이다.

> **이전 SCHP(human-parser) 방식은 폐기했다.** SCHP/ATR 는 실사 인물 사진으로 학습된 분할 모델이라
> 일러스트/스타일 캐릭터에서 마스크가 깨져 "검은 뭉개짐" 이 나왔다. 이 한계가 없는 Qwen 편집으로 전면 교체.

---

## 0. 구성

- 워크플로우 1개: **`decompose.json`** (커스텀 노드 **0개**, 전부 ComfyUI 코어 노드).
- 요소 목록은 **`elements.txt`** 로 가변 지정 → `python build_workflow.py` 로 `decompose.json` 재생성.
- 엔진: **Qwen-Image-Edit-2509** + Qwen2.5-VL 인코더 + qwen_image_vae + Lightning 4-step LoRA.
- 출력: 요소별 **흰 배경 PNG** (`output/decompose/<요소>_*.png`). 알파 PNG가 필요하면 §6 참고.

파이프라인(요소 레인 1개):
```
LoadImage ─ FluxKontextImageScale ┬─ TextEncodeQwenImageEditPlus(+프롬프트) ┐
                                  ├─ TextEncodeQwenImageEditPlus(neg, 빈)  ├─ KSampler ─ VAEDecode ─ SaveImage
                                  └─ VAEEncode ───────────────────(latent)─┘
공유: UNETLoader → Lightning LoRA → ModelSamplingAuraFlow(shift 3) → CFGNorm(1) / CLIPLoader / VAELoader
KSampler: euler · simple · steps 4 · cfg 1 · denoise 1   (Lightning 4-step 카논 값)
```

---

## 1. 요구 사양 / 환경

| 항목 | 값 |
|---|---|
| 실행 위치 | **GPU PC** (현재 개발 머신은 GPU 없음 → 설계/생성만, 런타임 미검증) |
| 모델 디스크 | 약 **20GB** (fp8 diffusion ~14GB + 인코더 ~8GB + VAE + LoRA) |
| VRAM | **fp8: 16GB+ 권장**(오프로딩 시 그 이하도 가능) / **GGUF Q4: ~12GB** |
| 커스텀 노드 | **없음.** `TextEncodeQwenImageEditPlus` 등은 최신 ComfyUI 코어 노드 |
| ComfyUI | 최신으로 업데이트 (2509 템플릿/노드 포함 버전) |

---

## 2. 설치 (GPU PC, 최초 1회)

1. **ComfyUI 최신 업데이트** (코어에 Qwen-Image-Edit 2509 노드가 있어야 함).
2. **모델 다운로드** — `download_models.ps1` 실행 (스크립트 안 `$COMFY` 가 설치 경로와 맞는지 확인):
   ```powershell
   powershell -ExecutionPolicy Bypass -File download_models.ps1
   ```
   받는 파일과 위치:
   | 폴더 | 파일 |
   |---|---|
   | `models/diffusion_models/` | `qwen_image_edit_2509_fp8_e4m3fn.safetensors` |
   | `models/text_encoders/` | `qwen_2.5_vl_7b_fp8_scaled.safetensors` |
   | `models/vae/` | `qwen_image_vae.safetensors` |
   | `models/loras/` | `Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors` |
3. ComfyUI 재시작.

> **저VRAM(≤12GB)**: fp8 대신 `QuantStack/Qwen-Image-Edit-2509-GGUF`(예: Q4_K_M)를 받아
> `models/diffusion_models/` 에 두고, `ComfyUI-GGUF` 커스텀 노드 설치 후
> `decompose.json` 의 **UNETLoader → "Unet Loader (GGUF)"** 로 교체.

---

## 3. 사용법

1. `decompose.json` 을 ComfyUI 캔버스에 **드래그**. (4개 로더가 위 파일을 가리키는지 확인 — 빨간 노드면 파일명 불일치)
2. 좌측 `Load Input (T-pose)` 에 캐릭터 이미지 업로드.
3. **Queue Prompt** → 요소마다 흰 배경 추출본이 `output/decompose/` 에 저장.

### 특정 부위만 재생성
- 다시 만들 **요소 그룹만 남기고** 나머지 그룹을 우클릭 → **Mute Group** (또는 노드 선택 후 `Ctrl+M`).
- **Queue** → 켜진 레인의 KSampler seed 가 randomize 되어 **그 부위만 새 변형**. (꺼진 레인은 실행 안 됨)

### 같은 부위 N장 (가변 다중 생성)
- 상단 Queue 버튼 옆 **batch count** 를 N 으로 설정 후 Queue → seed 가 매번 바뀌어 N장 생성.

---

## 4. 요소 바꾸기 (`elements.txt`)

Qwen 편집이라 **ATR 18클래스 같은 제약이 없다.** 자유 텍스트로 적으면 된다.

```
hair
pants
jacket | the jacket / upper-body outerwear     # 'name | 프롬프트 문구' 로 파일명과 문구 분리 지정
goggles | the goggles / eyewear
```
- 한 줄 = 한 요소. `#` 뒤 주석, 빈 줄 무시.
- `|` 없으면 줄 전체가 이름이자 문구(`hair` → `extract only the hair …`).
- 편집 후 **`python build_workflow.py`** → `decompose.json` 재생성(요소 수만큼 레인 자동 생성).

프롬프트 템플릿(고정):
```
Extract only {PHRASE} from this character.
Place it centered on a plain pure-white background.
Remove the character and all other items.
Preserve the original art style, colors, and lighting.
Single object, product-shot style, no text, no shadow.
```

---

## 5. 자주 묻는 동작

- **없는 요소를 요청하면?** (예: 고글 없는 캐릭터) 생성형이라 에러는 안 나지만 결과가 부정확/엉뚱할 수 있음 → 그 요소 줄을 빼라.
- **요소 간 겹침이 생길 수 있다.** Qwen 추출은 "그 요소만" 을 *재생성*하는 것이라 픽셀 단위 무겹침을 보장하지 않는다(원본 civitai 도 동일).
- **매번 같은 그림** → 해당 KSampler seed 가 `fixed` 면 동일. `randomize` 로 두거나 seed 를 바꿔야 변형됨.
- **품질이 약하면** steps 를 6~8 로(LoRA 빼고 cfg 2~3), 또는 프롬프트의 PHRASE 를 더 구체적으로.

---

## 6. 흰 배경 → 투명 알파 PNG (선택)

흰 배경 결과를 투명 PNG로 바꾸려면 SaveImage 앞에 **BiRefNet(MIT)** 류 배경제거 노드를 끼우거나,
별도 후처리(흰색 → 알파)를 둔다. 현재 워크플로우는 원본 civitai 와 동일하게 **흰 배경 product-shot** 까지만 한다.

---

## 7. 파일 목록

| 파일 | 용도 |
|---|---|
| `decompose.json` | **메인 워크플로우** (요소별 흰 배경 추출, 코어 노드만) |
| `elements.txt` | 분해할 요소 리스트 (여기 편집) |
| `build_workflow.py` | `elements.txt` → `decompose.json` 생성기 |
| `download_models.ps1` | Qwen 모델 4종 다운로드 |
| `HowTo.md` | 이 문서 |

> 노드/링크 구조는 공식 2509 템플릿 값과 대조해 검증했으나, **이 머신엔 GPU·모델이 없어 런타임 실행은 미검증.**
> GPU PC 에서 첫 로드 시 4개 로더가 빨갛지 않은지(파일명 일치)만 확인하면 된다.
