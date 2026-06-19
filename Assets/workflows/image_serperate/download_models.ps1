# Qwen-Image-Edit-2509 모델 4종 다운로드 (decompose.json 용)
# ComfyUI 공식 템플릿이 참조하는 카논 파일들. 총 약 20GB.
#
# 사용:  powershell -ExecutionPolicy Bypass -File download_models.ps1
# 다른 ComfyUI 경로면 아래 $COMFY 만 고치면 됨. curl.exe 의 -C - 로 중단 시 이어받기 지원.

$ErrorActionPreference = "Stop"

# ComfyUI 모델 루트 (포터블 기준 자동 추정 -> 없으면 직접 지정)
$COMFY = "C:\comfyui\ComfyUI_windows_portable\ComfyUI"
if (-not (Test-Path (Join-Path $COMFY "models"))) {
    Write-Host "models 폴더를 못 찾음: $COMFY\models  -- 스크립트 안 `$COMFY` 를 ComfyUI 경로로 고치세요." -ForegroundColor Red
    exit 1
}
$M = Join-Path $COMFY "models"

# (대상 하위폴더, 파일명, URL)
$files = @(
  @{ dir = "diffusion_models"; name = "qwen_image_edit_2509_fp8_e4m3fn.safetensors";
     url = "https://huggingface.co/Comfy-Org/Qwen-Image-Edit_ComfyUI/resolve/main/split_files/diffusion_models/qwen_image_edit_2509_fp8_e4m3fn.safetensors" },
  @{ dir = "text_encoders"; name = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
     url = "https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI/resolve/main/split_files/text_encoders/qwen_2.5_vl_7b_fp8_scaled.safetensors" },
  @{ dir = "vae"; name = "qwen_image_vae.safetensors";
     url = "https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI/resolve/main/split_files/vae/qwen_image_vae.safetensors" },
  @{ dir = "loras"; name = "Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors";
     url = "https://huggingface.co/lightx2v/Qwen-Image-Lightning/resolve/main/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors" }
)

foreach ($f in $files) {
    $destDir = Join-Path $M $f.dir
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Force -Path $destDir | Out-Null }
    $dest = Join-Path $destDir $f.name
    if (Test-Path $dest) {
        Write-Host "[skip] 이미 있음: $($f.dir)/$($f.name)" -ForegroundColor DarkGray
        continue
    }
    Write-Host "[down] $($f.dir)/$($f.name)" -ForegroundColor Cyan
    & curl.exe -L -C - -o $dest $f.url
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  다운로드 실패. URL 확인(HF에서 repo 가 옮겨졌을 수 있음): $($f.url)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "완료. ComfyUI 재시작 후 decompose.json 을 드래그하면 4개 로더가 위 파일을 가리킵니다." -ForegroundColor Green
Write-Host "저VRAM(12GB 이하)이면 fp8 대신 GGUF 권장:" -ForegroundColor Green
Write-Host "  QuantStack/Qwen-Image-Edit-2509-GGUF 에서 Q4_K_M 등을 받아 models/diffusion_models 에 두고," -ForegroundColor Green
Write-Host "  custom_nodes 에 ComfyUI-GGUF 설치 후 UNETLoader 를 'Unet Loader (GGUF)' 로 교체." -ForegroundColor Green
