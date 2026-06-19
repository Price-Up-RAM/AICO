# HowTo — 캐릭터 요소 분해 워크플로우

캐릭터 1장(이미 **T 포즈**)을 입력하면 머리·몸체·상의·하의·벨트·신발 등 **개별 요소로 분해**한다.
실행은 **GPU PC**에서 한다(현재 개발 머신은 GPU 없음 → 설계/생성만 수행).

---

## 0. 워크플로우 3종 — 한눈에

| 워크플로우 | 한 줄 설명 | 엔진 | 요소 겹침 | 출력 | VRAM |
|---|---|---|---|---|---|
| **`decompose_all.json`** | 고정 10요소 분리 | SCHP 파싱 | ❌ 없음 | 투명 PNG | 4GB / CPU |
| **`decompose_list.json`** | 요소를 리스트로 가변 지정 | SCHP 파싱 | ❌ 없음 | 투명 PNG | 4GB / CPU |
| **`decompose_regen.json`** | 요소별로 다른 버전 재생성 | Qwen 편집 | ⚠️ 가능 | 흰 배경 | 12~24GB |

> **all = 고정 무겹침 · list = 가변 무겹침 · regen = 가변 생성형 재생성**

**두 엔진의 차이**
- **SCHP(ATR) 의미 분할** — 픽셀마다 라벨을 정확히 하나 배정. 그래서 **요소 간 겹침이 구조적으로 불가능**(벨트가 상의·하의에 중복되지 않음). 결정적(다시 돌려도 동일), 가벼움.
- **Qwen-Image-Edit** — 생성형 편집. 매번 다른 **변형(재생성)** 가능, 흰 배경 product-shot. 무거움.

---

## 1. 환경 / 요구 사양

### SCHP 계열 (`decompose_all`, `decompose_list`)
| 항목 | 값 |
|---|---|
| 모델 | SCHP ATR 체크포인트 `exp-schp-201908301523-atr.pth` **약 267MB** (유일) |
| 디스크 | 신규 0.5GB 미만 (여유 포함 2GB) |
| VRAM | **4GB+** 권장 (실사용 ~2GB). **GPU 없으면 CPU도 동작**(느림) |
| 커스텀 노드 | `cozymantis/human-parser-comfyui-node`, `jnxmx/ComfyUI_HuggingFace_Downloader` |

### Qwen 계열 (`decompose_regen`) — 위에 더해서
| 항목 | 값 |
|---|---|
| 모델 | Qwen-Image-Edit-2509 + Qwen2.5-VL 인코더 + VAE + Lightning LoRA **약 15~20GB** |
| VRAM | **12GB (GGUF Q4) ~ 24GB (fp8)** |
| 커스텀 노드 | 위 2종 + `rgthree-comfy` (요소별 토글용) |

---

## 2. 설치 (GPU PC, 최초 1회)

1. **ComfyUI** 최신 업데이트, **ComfyUI-Manager** 권장.
2. **커스텀 노드** 설치 (Manager의 *Install via Git URL*, 또는 clone 후 각 `requirements.txt` 설치):
   ```bash
   cd ComfyUI/custom_nodes
   git clone https://github.com/cozymantis/human-parser-comfyui-node   # SCHP 분할 (all/list/regue 공통 입력)
   git clone https://github.com/jnxmx/ComfyUI_HuggingFace_Downloader    # 모델 다운로드 노드
   git clone https://github.com/rgthree/rgthree-comfy                   # 요소별 토글 (regen 전용)
   ```
   설치 후 ComfyUI 재시작.
3. **모델 다운로드** — `download_models.ps1` 의 `$env:COMFY` 를 자기 ComfyUI 경로로 고친 뒤:
   ```powershell
   powershell -ExecutionPolicy Bypass -File download_models.ps1                      # [A] SCHP (필수)
   $env:WITH_QWEN=1; powershell -ExecutionPolicy Bypass -File download_models.ps1    # [B] + Qwen (regen용)
   ```
   - SCHP 결과 경로(정확히 일치해야 함): `ComfyUI/models/schp/exp-schp-201908301523-atr.pth`
   - SCHP는 `decompose_all`/`list` 안의 `⬇ SCHP 모델 다운로드` 노드를 큐에 1회 넣어도 받아진다.

---

## 3. 워크플로우별 설명 & 사용법

### 3-A. `decompose_all.json` — 고정 10요소, 무겹침
머리·몸체·상의·하의·벨트·신발·모자·가방·선글라스·스카프 **10개**를 한 번에 투명 PNG로 분리.

- **구조**: `LoadImage → [요소마다] Cozy Human Parser ATR(해당 클래스만 ON) → JoinImageWithAlpha → SaveImage`
  (+ SCHP 다운로드 노드, 레인별 주석·그룹)
- **무겹침 원리**: 같은 결정적 파싱이라 레인끼리 마스크가 상호배타. 벨트는 독립 클래스(8).
- **사용**:
  1. drag-drop → (최초 1회) `⬇ SCHP 모델 다운로드` 노드만 큐.
  2. `Load Input Image` 에 T 포즈 캐릭터 업로드 → **Queue Prompt**.
  3. `output/elements/` 에 `hair_*.png … scarf_*.png` 생성.

### 3-B. `decompose_list.json` — 가변 요소, 무겹침
3-A와 같은 SCHP 엔진이지만, 요소가 고정이 아니라 **`elements.txt` 리스트로 가변**.

- **사용**:
  1. `elements.txt` 편집 — 분리할 요소를 쉼표/줄바꿈으로 나열 (예: `hair, body, top, pants, belt, shoes`).
  2. `python build_decompose_list.py` 실행 → `decompose_list.json` 재생성(요소마다 레인 자동 생성).
  3. drag-drop → 이미지 업로드 → Queue. (출력은 3-A와 동일, 투명 PNG)
- 친화적 이름이 ATR 클래스로 자동 매핑되고, 두 요소가 **같은 클래스를 공유하면 겹침 경고**가 뜬다(4장 참고).

### 3-C. `decompose_regen.json` — 요소별 재생성 (생성형)
요소마다 Qwen으로 "흰 배경에 그 요소만" 추출/변형하고, **부위별로 따로 재생성**한다(원본 Accept/Regenerate 등가).

- **구조**: 공유 모델 로더(MODEL/CLIP/VAE) + `[요소마다] FluxKontextImageScale → 인코더(pos/neg) → VAEEncode → KSampler(레인별 seed) → VAEDecode → SaveImage` + `🔇 Fast Groups Muter`.
- **요소 목록**: `decompose_list` 와 같은 `elements.txt` 를 읽음 → `python build_decompose_regen.py` 로 재생성.
- **해당 부위만 재생성**:
  1. 우측 `🔇 Fast Groups Muter` 패널에서 **재생성할 요소만 ON**, 나머지 OFF.
  2. **Queue** → 켜진 레인의 KSampler seed가 randomize → **그 요소만 새 변형**.
  3. 꺼진 레인은 실행 안 됨(전체 재생성 아님). 입력이 안 바뀐 레인은 ComfyUI 캐시로 재계산 생략.
  4. `output/regen/` 에 요소별 변형(흰 배경) PNG. 마음에 드는 것만 남기면 = Accept.
- **주의**: 생성형이라 **요소 간 겹침이 가능**(무겹침이 필요하면 `decompose_all`/`list` 사용).

---

## 4. 요소 이름 ↔ ATR 클래스 (`list`/`regen` 공용)

`elements.txt` 에 쓸 수 있는 친화적 이름:

| 이름 | 매핑 |
|---|---|
| hair | hair |
| head | hair + face (※ body와 함께 쓰면 face 겹침) |
| face | face |
| body, skin | face + 양팔 + 양다리 (피부) |
| arms / legs | 양팔 / 양다리 |
| top, upper, shirt, jacket | upper-clothes |
| bottom, lower | skirt + pants + dress |
| pants / skirt / dress | 각각 |
| **belt** | belt (독립 — 상/하의와 안 겹침) |
| shoes, footwear | 양쪽 신발 |
| hat / bag / sunglasses(glasses) / scarf | 각각 |

전체 ATR 라벨: `0 bg, 1 hat, 2 hair, 3 sunglasses, 4 upper-clothes, 5 skirt, 6 pants,
7 dress, 8 belt, 9 left-shoe, 10 right-shoe, 11 face, 12 left-leg, 13 right-leg,
14 left-arm, 15 right-arm, 16 bag, 17 scarf`

---

## 5. 자주 묻는 동작

- **스카프/모자 없는 캐릭터?** 에러 없음. SCHP는 해당 픽셀 0개 → **빈 투명 PNG** 생성(거슬리면 그 SaveImage를 Mute/삭제). regen은 없는 요소를 요청하면 결과가 부정확할 수 있음.
- **요소 추가는 번거로운가?** `elements.txt` 에 단어 추가 → 생성기 재실행. **18클래스 안이면 끝.** 18클래스 밖(고글·망토 등)은 SCHP로 불가 → 6장 참고.
- **분할 경계가 거칠다** → 마스크에 GrowMask/Blur, 또는 BiRefNet 후처리.
- **regen인데 매번 같은 그림** → 해당 KSampler seed가 fixed면 동일. randomize로 두거나 seed를 바꿔야 변형됨.

---

## 6. 한계 & 다음 단계

- **요소 개수가 ATR 18클래스에 고정.** 임의 이름을 프롬프트로 가변 분리하려면
  **오픈보캐(GroundingDINO/Florence-2 + SAM2)** 경로 필요 — 텍스트에 라벨 나열 → 라벨 수만큼 출력.
  단 마스크 **겹침 가능**(우선순위/차집합 정리 + 검출0 가드 필요).
- **무겹침 + 변형 둘 다**: SCHP 컷아웃 → 흰 배경 합성 → Qwen 변형 하이브리드(추가 합성 노드 필요).

---

## 7. 파일 목록 (최종)

| 파일 | 용도 |
|---|---|
| `decompose_all.json` | 고정 10요소 무겹침 분리 (투명 PNG) |
| `decompose_list.json` | 가변 요소 무겹침 분리 — `elements.txt` 구동 |
| `decompose_regen.json` | 요소별 생성형 재생성 (Qwen + rgthree 토글) |
| `elements.txt` | `list`/`regen` 이 읽는 **요소 리스트** (여기 편집) |
| `build_decompose.py` | `decompose_all` 생성기 |
| `build_decompose_list.py` | `decompose_list` 생성기 (별칭 매핑 + 겹침 경고) |
| `build_decompose_regen.py` | `decompose_regen` 생성기 |
| `download_models.ps1` | SCHP(필수) + `WITH_QWEN=1` 시 Qwen 모델 다운로드 |

> 모든 워크플로우는 **노드/링크 구조를 검증**했으나, 이 머신에 GPU가 없어 **런타임 실행 테스트는 미완**.
> GPU PC 첫 로드 시 커스텀 노드 설치와 모델 경로(`models/schp/...atr.pth` 등)만 확인하면 된다.
