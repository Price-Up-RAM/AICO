# 모델 다운로드 스크립트 (GPU PC에서 실행)
# 사용법:
#   1) pip install -U "huggingface_hub[cli]"
#   2) $env:COMFY = "C:\ComfyUI_windows_portable\ComfyUI"   # 자기 ComfyUI 경로로 수정
#   3) powershell -ExecutionPolicy Bypass -File download_models.ps1
#
# [A] 기본(필수): SCHP ATR 체크포인트 — decompose_all.json(요소 완전 분해)용.
#     워크플로우의 'SCHP 모델 다운로드' 노드로도 받을 수 있지만, 이 스크립트가 가장 확실.
# [B] 선택: Qwen-Image-Edit (컷아웃 후 생성형 변형/리파인 — 방식 혼합용)

$ErrorActionPreference = "Stop"
if (-not $env:COMFY) { $env:COMFY = "C:\comfyui\ComfyUI_windows_portable\ComfyUI" }
$M = Join-Path $env:COMFY "models"
Write-Host "ComfyUI models dir: $M"

function Get-HF($repo, $file, $subdir) {
    $outdir = Join-Path $M $subdir
    New-Item -ItemType Directory -Force -Path $outdir | Out-Null
    Write-Host "↓ $repo / $file  ->  $subdir"
    hf download $repo $file --local-dir $outdir
}

# =====================================================================
# [A] SCHP ATR 체크포인트 (필수) -> models/schp/exp-schp-201908301523-atr.pth
#     Cozy Human Parser ATR 노드가 이 경로를 직접 읽는다. 파일명/경로 정확히 일치 필요.
# =====================================================================
Get-HF "soonyau/visconet" "exp-schp-201908301523-atr.pth" "schp"
Write-Host "SCHP atr 체크포인트 -> models/schp/ 확인"

# fp8/Qwen 은 요소 분해엔 불필요. 생성형 리파인(방식 혼합)을 할 때만 아래 [B] 사용.
if (-not $env:WITH_QWEN) {
    Write-Host "`n[A] 완료. Qwen 모델도 받으려면:  `$env:WITH_QWEN=1; ./download_models.ps1"
    return
}

# =====================================================================
# [B] Qwen-Image-Edit-2509 (선택) — 24GB+ VRAM 권장(fp8)
# =====================================================================

# ---- 본체 (fp8, 24GB+ VRAM 권장) ----
Get-HF "Comfy-Org/Qwen-Image-Edit_ComfyUI" "split_files/diffusion_models/qwen_image_edit_2509_fp8_e4m3fn.safetensors" "diffusion_models"
Get-HF "Comfy-Org/Qwen-Image-Edit_ComfyUI" "split_files/text_encoders/qwen_2.5_vl_7b_fp8_scaled.safetensors"        "text_encoders"
Get-HF "Comfy-Org/Qwen-Image-Edit_ComfyUI" "split_files/vae/qwen_image_vae.safetensors"                              "vae"

# ---- 속도 LoRA (Lightning 4-step) ----
Get-HF "lightx2v/Qwen-Image-Lightning" "Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors" "loras"

Write-Host "`n완료. hf download가 split_files/ 하위 경로를 그대로 만들 수 있으니,"
Write-Host "각 파일이 models/diffusion_models, models/text_encoders, models/vae, models/loras 바로 아래에"
Write-Host "오도록 필요하면 이동하세요 (워크플로우는 파일명만 보고 찾습니다)."

# =====================================================================
# GGUF 대안 (12~16GB VRAM) — 위 fp8 대신 사용. ComfyUI-GGUF 노드 필요.
# 워크플로우의 UNETLoader 를 'Unet Loader (GGUF)' 로 교체하고 아래 파일 지정.
# ---------------------------------------------------------------------
# Get-HF "QuantStack/Qwen-Image-Edit-2509-GGUF" "Qwen-Image-Edit-2509-Q4_K_M.gguf" "unet"
# 텍스트인코더 GGUF: city96/Qwen2.5-VL-7B-Instruct-gguf 등. VAE/LoRA는 위와 동일.
# =====================================================================
