"""
DeepLX Python - Utility Functions
"""

import json
import random
import time
from typing import Optional, Dict, Any

from .constants import SUPPORTED_LANGUAGES, SupportedCode


def get_i_count(text: str) -> int:
    """텍스트에서 'i' 문자의 개수를 반환합니다."""
    return text.count('i')


def get_random_number() -> int:
    """요청 ID용 랜덤 번호를 생성합니다."""
    base = random.randint(0, 99999) + 8300000
    return base * 1000


def get_timestamp(i_count: int) -> int:
    """i 개수를 기반으로 타임스탬프를 생성합니다."""
    ts = int(time.time() * 1000)  # 밀리초 단위
    if i_count != 0:
        adjusted_count = i_count + 1
        return ts - (ts % adjusted_count) + adjusted_count
    return ts


def format_post_string(post_data: Dict[str, Any]) -> str:
    """특정 간격 규칙에 따라 요청 JSON 문자열을 포맷합니다."""
    # TypeScript와 동일하게 JSON.stringify 방식 사용
    post_str = json.dumps(post_data, separators=(',', ':'), ensure_ascii=False)
    
    # 특정 조건에 따라 공백 추가 (TypeScript와 동일한 로직)
    should_add_space = (
        (post_data["id"] + 5) % 29 == 0 or 
        (post_data["id"] + 3) % 13 == 0
    )
    
    # TypeScript의 replaceAll과 동일하게 모든 occurrence 교체
    if should_add_space:
        post_str = post_str.replace('"method":"', '"method" : "')
    else:
        post_str = post_str.replace('"method":"', '"method": "')
    
    return post_str


# 언어 약어 변환 사전 캐시
_abbreviate_language_dict: Optional[Dict[str, SupportedCode]] = None


def _get_abbreviate_languages() -> Dict[str, SupportedCode]:
    """언어 약어 변환 사전을 가져옵니다."""
    global _abbreviate_language_dict
    
    if _abbreviate_language_dict is None:
        _abbreviate_language_dict = {}
        for lang in SUPPORTED_LANGUAGES:
            code_lower = lang["code"].lower()
            language_lower = lang["language"].lower()
            _abbreviate_language_dict[code_lower] = lang["code"]
            _abbreviate_language_dict[language_lower] = lang["code"]
    
    return _abbreviate_language_dict


def abbreviate_language(language: str) -> Optional[SupportedCode]:
    """언어 이름이나 코드를 표준 언어 코드로 변환합니다."""
    if not language:
        return None
    
    # 하이픈이 있는 경우 첫 번째 부분만 사용 (예: en-US -> en)
    lang_code = language.split('-')[0].lower()
    
    lang_dict = _get_abbreviate_languages()
    return lang_dict.get(lang_code)


def split_and_process(text: str) -> list:
    """텍스트를 줄 단위로 분할하고 처리합니다. (TypeScript와 정확히 동일한 로직)"""
    # TypeScript: text.split('\n').map(line => (line.trim() === '' ? '\n' : line))
    return ['\n' if line.strip() == '' else line for line in text.split('\n')]
