"""
ai_model_info.py
================
모델별 샘플링 파라미터(payload)와 메타데이터를 중앙에서 관리합니다.

사용법:
    import ai_model_info

    # 현재 로드된 모델에 맞는 최적 payload 가져오기
    payload = ai_model_info.get_payload('Qwen3VL-8B-Instruct-Q4_K_M.gguf', use_image=True)
    payload = ai_model_info.get_payload('Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf', enable_thinking=True)

    # 모델 메타데이터 조회
    mmproj  = ai_model_info.get_mmproj('Qwen3VL-8B-Instruct-Q4_K_M.gguf')
    spec    = ai_model_info.get_model_spec('Qwen3-14B-Q4_K_M.gguf')
    is_vl   = ai_model_info.is_multimodal('Qwen3VL-8B-Instruct-Q4_K_M.gguf')
"""

import copy

# ==============================================================
# 1) BASE PAYLOAD  ← qwen3 default (현재 prepare_payload() 값 그대로)
#    등록되지 않은 모든 모델은 이 값을 기본으로 사용합니다.
# ==============================================================
_BASE_PAYLOAD = {
    # --- Temperature & Sampling ---
    "temperature": 1.0,        # 출처: Qwen3-VL-8B 공식 모델카드 권장값
    "dynatemp_range": 0,       # 동적 temperature 적용 범위 (0=비활성화)
    "dynatemp_exponent": 1,    # 동적 temperature 지수
    "top_k": 40,               # 공식 권장: 40
    "top_p": 1.0,              # 누적 확률 (1=비활성화)
    "min_p": 0.05,             # 최소 확률 임계값
    "tfs_z": 1,                # Tail Free Sampling (1=비활성화)
    "typical_p": 1,            # Typical decoding (1=비활성화)

    # --- Repetition Control ---
    "repeat_penalty": 1.0,     # 반복 억제 강도
    "repeat_last_n": 1024,     # 반복 억제 적용 범위 (토큰 수)
    "presence_penalty": 2.0,   # 공식 권장: 2.0 (반복 루프 핵심 억제)
    "frequency_penalty": 0,    # 자주 등장한 단어 확률 감소

    # --- DRY Sampling ---
    "dry_multiplier": 0,       # dry 샘플링 가중치 (0=비활성화)
    "dry_base": 1.75,
    "dry_allowed_length": 2,
    "dry_penalty_last_n": 1024,
    "dry_sequence_breakers": ['\n', ':', '"', '*'],

    # --- XTC Sampling ---
    "xtc_probability": 0,
    "xtc_threshold": 0.1,

    # --- Mirostat ---
    "mirostat": 0,             # 0=비활성화
    "mirostat_tau": 5,
    "mirostat_eta": 0.1,

    # --- Misc ---
    "grammar": "",
    "seed": -1,                # -1=무작위
    "ignore_eos": False,

    # --- Sampler Order ---
    "samplers": ['penalties', 'dry', 'temperature', 'top_k', 'top_p', 'min_p', 'xtc'],
}


# ==============================================================
# 2) 패밀리별 OVERRIDE 값
#    BASE_PAYLOAD에 덮어씌울 값만 정의합니다.
#    'text'  : 텍스트 전용 모드 (이미지 없음, thinking 없음)
#    'vl'    : 이미지 포함 일반 모드 (non-thinking)
#    'thinking': thinking / 정밀 모드 (enable_thinking=True)
# ==============================================================
_FAMILY_OVERRIDES = {

    # ----------------------------------------------------------
    # qwen3 — Qwen3 계열 기본 (Qwen3-VL 공식 모델카드 권장값)
    #          출처: https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct
    # ----------------------------------------------------------
    'qwen3': {
        'text': {
            # BASE_PAYLOAD 그대로 사용 (override 없음)
        },
        'vl': {
            # VL 모드: 이미지 포함 시 더 보수적인 값 권장
            "temperature": 0.7,
            "top_k": 20,
            "top_p": 0.8,
            "min_p": 0,
            "top_n_sigma": -1,
            "presence_penalty": 1.5,
            "repeat_penalty": 1,
            "samplers": ['penalties', 'dry', 'top_n_sigma', 'temperature', 'top_k', 'top_p', 'typ_p', 'min_p', 'xtc'],
        },
        'thinking': {
            # thinking 모드: qwen3는 별도 thinking preset 미정의 → text 값 사용
            # (thinking 활성화는 프롬프트 레벨에서 /no_think 제거로 제어)
        },
    },

    # ----------------------------------------------------------
    # qwen3.5 — Qwen3.5 계열
    #            출처: https://huggingface.co/Qwen/Qwen3.5-9B
    # ----------------------------------------------------------
    # qwen3.5 — Qwen3.5 계열
    #            출처: https://huggingface.co/Qwen/Qwen3.5-9B
    #
    # 공식 4종 권장 → 실사용 3종으로 정리:
    #   'text'    : non-thinking, 일반 대화  (temp=0.7)
    #   'vl'      : non-thinking + 이미지    (temp=0.7, text와 동일)
    #   'thinking': thinking mode, 일반 추론 (temp=1.0)  ← enable_thinking=True 기본 선택
    # ----------------------------------------------------------
    'qwen3.5': {
        'text': {
            # Instruct(non-thinking) mode for general tasks
            "temperature": 0.7,
            "top_p": 0.8,
            "top_k": 20,
            "min_p": 0.0,
            "presence_penalty": 1.5,
            "repeat_penalty": 1.0,
        },
        'vl': {
            # 이미지 포함 일반 질의(non-thinking) — text와 동일
            "temperature": 0.7,
            "top_p": 0.8,
            "top_k": 20,
            "min_p": 0.0,
            "presence_penalty": 1.5,
            "repeat_penalty": 1.0,
        },
        'thinking': {
            # Thinking mode for general tasks
            "temperature": 1.0,
            "top_p": 0.95,
            "top_k": 20,
            "min_p": 0.0,
            "presence_penalty": 1.5,
            "repeat_penalty": 1.0,
        },
    },



    # ----------------------------------------------------------
    # gemma3 — Gemma 3 계열 (Text = VL 동일)
    #           출처: https://ai.google.dev/gemma/docs/core/model_card_3
    # ----------------------------------------------------------
    'gemma3': {
        'text': {
            "temperature": 1.0,
            "top_p": 0.95,
            "top_k": 64,
            "min_p": 0.0,
        },
        'vl': {
            # Gemma는 Text와 VL에 동일한 값을 권장
            "temperature": 1.0,
            "top_p": 0.95,
            "top_k": 64,
            "min_p": 0.0,
        },
        'thinking': {
            # Gemma는 thinking 미지원 → text 값 그대로 사용
        },
    },

    # ----------------------------------------------------------
    # gemma4 — Gemma 4 계열 (Text = VL 동일, gemma3와 동일 값)
    #           출처: https://ai.google.dev/gemma/docs/core/model_card_gemma4
    # ----------------------------------------------------------
    'gemma4': {
        'text': {
            "temperature": 1.0,
            "top_p": 0.95,
            "top_k": 64,
            "min_p": 0.0,
        },
        'vl': {
            "temperature": 1.0,
            "top_p": 0.95,
            "top_k": 64,
            "min_p": 0.0,
        },
        'thinking': {
            # Gemma는 thinking 미지원
        },
    },
}


# ==============================================================
# 3) MODEL REGISTRY
#    모델 파일명을 키로 하여 메타데이터를 등록합니다.
#    여기에 없는 모델은 qwen3 default로 처리됩니다.
#
#    등록 형식:
#    'model-filename.gguf': {
#        'family'           : (str)  패밀리명 — 'qwen3' / 'qwen3.5' / 'gemma3' / 'gemma4'
#        'mmproj'           : (str|None)  mmproj 파일 경로 (VL 모델만, 없으면 None)
#        'max_vram'         : (int)  권장 VRAM (GB)
#        'max_n_gpu_layers' : (int)  최대 GPU offload 레이어 수
#    }
# ==============================================================
MODEL_REGISTRY = {

    # ── Qwen3 VL ──────────────────────────────────────────────
    'Qwen3VL-8B-Instruct-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : './model/mmproj-Qwen3VL-8B-Instruct-Q8_0.gguf',
        'max_vram'         : 8,
        'max_n_gpu_layers' : 37,
    },
    'Qwen3VL-8B-Thinking-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : './model/mmproj-Qwen3VL-8B-Thinking-Q8_0.gguf',
        'max_vram'         : 8,
        'max_n_gpu_layers' : 37,
    },
    'Qwen3VL-30B-A3B-Instruct-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : './model/mmproj-Qwen3VL-30B-A3B-Instruct-Q8_0.gguf',
        'max_vram'         : 24,
        'max_n_gpu_layers' : 999,
    },

    # ── Qwen3.5 (VL 지원) ─────────────────────────────────────
    'Qwen3.5-9B-Q4_K_M.gguf': {
        'family'           : 'qwen3.5',
        'mmproj'           : './model/mmproj-Qwen3.5-BF16.gguf',
        'max_vram'         : 8,
        'max_n_gpu_layers' : 37,
    },

    'Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf': {
        'family'           : 'qwen3.5',
        'mmproj'           : './model/mmproj-Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-BF16.gguf',
        'max_vram'         : 8,
        'max_n_gpu_layers' : 37,
    },

    # ── Qwen3 Text-only ───────────────────────────────────────
    'Qwen3-0.6B-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : None,
        'max_vram'         : 2,
        'max_n_gpu_layers' : 29,
    },
    'Qwen3-1.7B-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : None,
        'max_vram'         : 3,
        'max_n_gpu_layers' : 29,
    },
    'Qwen3-4B-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : None,
        'max_vram'         : 4,
        'max_n_gpu_layers' : 37,
    },
    'Qwen3-8B-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : None,
        'max_vram'         : 8,
        'max_n_gpu_layers' : 37,
    },
    'Qwen3-14B-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : None,
        'max_vram'         : 12,
        'max_n_gpu_layers' : 41,
    },
    'Qwen3-32B-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : None,
        'max_vram'         : 24,
        'max_n_gpu_layers' : 65,
    },
    'Qwen3-30B-A3B-Instruct-2507-Q4_K_M.gguf': {
        'family'           : 'qwen3',
        'mmproj'           : None,
        'max_vram'         : 24,
        'max_n_gpu_layers' : 49,
    },

    # ── Gemma 4 ───────────────────────────────────────────────
    'gemma-4-26B-A4B-it-UD-Q4_K_M.gguf': {
        'family'           : 'gemma4',
        'mmproj'           : './model/mmproj-BF16.gguf',
        'max_vram'         : 24,
        'max_n_gpu_layers' : 999,
    },

    'supergemma4-26b-uncensored-fast-v2-Q4_K_M.gguf': {
        'family'           : 'gemma4',
        'mmproj'           : './model/mmproj-BF16.gguf',
        'max_vram'         : 24,
        'max_n_gpu_layers' : 999,
    },

    # 새 모델 추가 시 여기에 동일한 형식으로 등록하세요.
    # 'new-model-name.gguf': {
    #     'family'           : 'qwen3',    # 'qwen3' / 'qwen3.5' / 'gemma3' / 'gemma4'
    #     'mmproj'           : None,        # VL 미지원이면 None
    #     'max_vram'         : 8,
    #     'max_n_gpu_layers' : 37,
    # },
}

# 기본 VL 폴백 모델 (VL 모델이 필요한데 현재 모델이 VL 미지원일 때)
_DEFAULT_VL_MODEL = 'Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf'


# ==============================================================
# 내부 헬퍼: 모델명에서 패밀리 자동 추론
# MODEL_REGISTRY에 등록되지 않은 모델에 사용됩니다.
# ==============================================================
def _infer_family(model_name: str) -> str:
    """모델명 키워드로 패밀리를 추론합니다. 매칭 실패 시 'qwen3'(default) 반환."""
    name = model_name.lower()
    if 'qwen3.5' in name or 'qwen3-5' in name:
        return 'qwen3.5'
    if 'gemma-4' in name or 'gemma4' in name:
        return 'gemma4'
    if 'gemma-3' in name or 'gemma3' in name:
        return 'gemma3'
    # qwen3, qwen2.5 등 기타 Qwen 계열 → qwen3 default
    return 'qwen3'


# ==============================================================
# PUBLIC API
# ==============================================================

def get_payload(model_name: str, use_image: bool = False, enable_thinking: bool = False) -> dict:
    """
    모델명과 실행 조건에 맞는 최적 샘플링 payload를 반환합니다.

    Args:
        model_name     : GGUF 파일명 (예: 'Qwen3VL-8B-Instruct-Q4_K_M.gguf')
        use_image      : 이미지 포함 요청 여부 (True → VL 파라미터 사용)
        enable_thinking: thinking 모드 활성화 여부 (True → thinking 파라미터 사용)

    Returns:
        dict: 병합된 sampling payload. BASE_PAYLOAD를 base로, 패밀리별 override를 덮어씌운 값.

    우선순위:
        enable_thinking=True → 'thinking' preset (있으면)
        use_image=True       → 'vl' preset
        그 외                → 'text' preset
    """
    # BASE 복사
    payload = copy.deepcopy(_BASE_PAYLOAD)

    # 패밀리 결정
    info = MODEL_REGISTRY.get(model_name)
    family = info['family'] if info else _infer_family(model_name)

    # 패밀리 override 가져오기
    family_presets = _FAMILY_OVERRIDES.get(family, {})

    # 프리셋 선택 (우선순위: thinking > vl > text)
    if enable_thinking and family_presets.get('thinking'):
        preset = family_presets['thinking']
    elif use_image and family_presets.get('vl'):
        preset = family_presets['vl']
    else:
        preset = family_presets.get('text', {})

    # override 적용
    payload.update(preset)

    return payload


def get_mmproj(model_name: str):
    """
    모델의 mmproj 파일 경로를 반환합니다.

    Returns:
        str | None: mmproj 경로. 등록되지 않았거나 VL 미지원이면 None.
    """
    info = MODEL_REGISTRY.get(model_name)
    if info:
        return info.get('mmproj')
    return None


def get_mmproj_map() -> dict:
    """
    mmproj가 있는 모델들의 {모델명: mmproj경로} 딕셔너리를 반환합니다.
    ai_singleton.py의 MMPROJ_PATH 하위 호환용.
    """
    return {
        name: info['mmproj']
        for name, info in MODEL_REGISTRY.items()
        if info.get('mmproj')
    }


def is_multimodal(model_name: str) -> bool:
    """
    모델이 멀티모달(Vision-Language)을 지원하는지 여부를 반환합니다.

    Returns:
        bool: mmproj 경로가 등록되어 있으면 True.
    """
    return bool(get_mmproj(model_name))


def get_model_spec(model_name: str) -> dict:
    """
    모델의 VRAM / GPU 레이어 스펙을 반환합니다.

    Returns:
        dict: {'max_vram': int, 'max_n_gpu_layers': int}
              등록되지 않은 모델은 {'max_vram': 99999, 'max_n_gpu_layers': 999} 반환.
    """
    info = MODEL_REGISTRY.get(model_name)
    if info:
        return {
            'max_vram'         : info.get('max_vram', 99999),
            'max_n_gpu_layers' : info.get('max_n_gpu_layers', 999),
        }
    # 등록되지 않은 모델 → 제한 없음 (기존 else 브랜치와 동일)
    return {'max_vram': 99999, 'max_n_gpu_layers': 999}


def get_multimodal_models() -> list:
    """
    멀티모달 모델 목록을 반환합니다.
    constants.py의 MULTIMODAL_MODELS를 대체합니다.

    Returns:
        list[str]: mmproj가 등록된 모델명 리스트.
    """
    return [name for name, info in MODEL_REGISTRY.items() if info.get('mmproj')]


def get_default_vl_model() -> str:
    """
    기본 VL 폴백 모델명을 반환합니다.
    ai_singleton.py의 DEFAULT_VL_MODEL을 대체합니다.
    """
    return _DEFAULT_VL_MODEL


def get_family(model_name: str) -> str:
    """
    모델의 패밀리명을 반환합니다.

    Returns:
        str: 'qwen3' / 'qwen3.5' / 'gemma3' / 'gemma4' 중 하나.
    """
    info = MODEL_REGISTRY.get(model_name)
    if info:
        return info['family']
    return _infer_family(model_name)


# ==============================================================
# 간단한 자가 테스트 (python ai_model_info.py 로 직접 실행 시)
# ==============================================================
if __name__ == '__main__':
    import json

    tests = [
        ('Qwen3VL-8B-Instruct-Q4_K_M.gguf',                         False, False),  # qwen3, text
        ('Qwen3VL-8B-Instruct-Q4_K_M.gguf',                         True,  False),  # qwen3, vl
        ('Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf',   False, False),  # qwen3.5, text
        ('Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf',   True,  False),  # qwen3.5, vl
        ('Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf',   True,  True),   # qwen3.5, thinking
        ('gemma-4-26B-A4B-it-UD-Q4_K_M.gguf',                       False, False),  # gemma4, text
        ('gemma-4-26B-A4B-it-UD-Q4_K_M.gguf',                       True,  False),  # gemma4, vl
        ('unknown-model-qwen3.5-7B.gguf',                            False, True),   # 미등록, 자동추론
    ]

    for model, img, think in tests:
        p = get_payload(model, use_image=img, enable_thinking=think)
        tag = f"use_image={img}, enable_thinking={think}"
        print(f"\n{'─'*60}")
        print(f"  모델 : {model}")
        print(f"  조건 : {tag}")
        print(f"  패밀리: {get_family(model)}")
        print(f"  temperature={p['temperature']}, top_k={p['top_k']}, "
              f"top_p={p['top_p']}, presence_penalty={p['presence_penalty']}")

    print(f"\n{'─'*60}")
    print("\n[멀티모달 모델 목록]")
    for m in get_multimodal_models():
        print(f"  {m}  →  {get_mmproj(m)}")

    print(f"\n[기본 VL 모델] {get_default_vl_model()}")

    print(f"\n[모델 스펙] Qwen3-14B-Q4_K_M.gguf")
    print(f"  {get_model_spec('Qwen3-14B-Q4_K_M.gguf')}")

    print(f"\n[모델 스펙] 미등록 모델")
    print(f"  {get_model_spec('some-unknown-model.gguf')}")
