# AIStatus UI — 설계

로컬 llama.cpp 기반 파이썬 서버의 현황을 Unity에서 보여주는 패널. 서버의 `/status`(lite)와
`/status/full`을 호출해 서버/GPU/시스템 현황과, full 모드의 속도 벤치·모델 fit 판정을 다크 테마로 표시한다.

## 목적
- 어느 PC에서 실행 중인지에 따라 **모델 적재 가능/권장 여부**, **예상 GPU 레이어**, **토큰 속도**를 한눈에.
- 서버가 없어도 UI는 동작(graceful fallback / 샘플).

## 화면 구성
```
Header : [AI 상태]           [lite|full] [↻] [×]
Body(세로 스크롤)
 ├ ServerCard : 모델 / 헬스 / 슬롯(busy) / 컨텍스트(n_ctx)
 ├ GPU 섹션   : 디바이스별 카드(이름 · 온도 배지 · 사용률 게이지 · VRAM/사용량)
 ├ SystemCard : RAM · CPU · VRAM 게이지(+수치)
 ├ BenchCard  : (full) 생성속도 / 프롬프트속도 / 소요·토큰
 └ Fit 섹션   : (full) 모델별 카드(모델명 · verdict 배지 · 레이어 게이지 · 필요VRAM/레이어/플래그)
```

## 서버 API 계약 (server_impl_status.py)
- `GET /status` (lite, 부작용 없음): `ok, level, llm_server{running,model_name,health,props{model_path,total_slots,modalities,is_sleeping,build_info,n_ctx},slots{total,processing,idle,detail[]},model_meta{n_params,size_gb,...}}, gpu{available,devices[{index,name,vram_total_gb,vram_free_gb,vram_used_mb,util_percent,temp_c}]}, system{available,ram_total_gb,ram_available_gb,ram_percent,cpu_logical,cpu_physical,cpu_percent}, state{...}`
- `GET /status/full`: lite + `benchmark{available,n_predict,elapsed_sec,predicted_per_second,prompt_per_second,...}` + `fit{reference{...},models[{model,need_vram_gb,max_n_gpu_layers,expected_gpu_layers,is_moe,is_multimodal,fits_gpu,fits_free_now,verdict}]}`

## 필드 → UI 매핑
| 서버 필드 | UI |
|---|---|
| `llm_server.model_name/health/slots/props.n_ctx` | ServerCard KV |
| `gpu.devices[]` | `AIStatusRow.SetupGpu` |
| `system.ram_percent / cpu_percent` | SystemCard 게이지 |
| `benchmark.predicted_per_second` | BenchCard "생성 속도" |
| `fit.models[]` | `AIStatusRow.SetupFit` |

## 색상 규칙
- health `ok` → 초록(StatusOk), 그 외/미연결 → 빨강(StatusBad)
- verdict: `recommended`→초록 / `loadable_now`→Accent / `cpu_offload`→노랑(Warn) / `too_large`→빨강(Bad)
- 온도: `<70`→Ok / `<85`→Warn / else Bad

## 이중 모드
- `Awake` → `HasBakedHierarchy()`("Body" 존재)면 `BindExisting`, 아니면 `BuildHierarchy`. (SkillView/Mission 방법론)
- GPU/Fit 리스트는 비활성 템플릿(DeviceTemplate/FitTemplate)만 굽고, 런타임 `Instantiate` 클론.

## 네트워킹
- `FindObjectOfType<ServerManager>()` → `GetBaseUrl(baseUrl => ...)` → `UnityWebRequest.Get(baseUrl + "/status(/full)")` → Newtonsoft `JObject` 파싱 → `view.SetStatus`.
- ServerManager 없음/빈 baseUrl → `fallbackBaseUrl`(데모는 `127.0.0.1:5000`) 직접 호출. 실패 시 `showOfflineOnFailure`에 따라 미연결 표시 또는 현재 화면 유지.

## 리스크
- `/status/full`의 benchmark는 실제 토큰 생성을 돌리는 **부작용 있는** 호출 → 기본은 lite, full은 사용자가 토글할 때만. 자동 폴링 기본 off(`pollIntervalSec=0`).
- `ServerManager.GetBaseUrl`은 콜백(동기 return 아님). `baseUrl==""`가 미연결 신호.
