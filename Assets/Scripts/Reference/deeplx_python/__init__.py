"""
DeepLX Python - DeepL 무료 번역 라이브러리

이 라이브러리는 DeepL의 내부 API를 사용하여 무료로 번역 서비스를 제공합니다.

사용법:
    from deeplx import translate
    
    result = translate("Hello world", "KO")
    print(result)  # "안녕하세요"
"""

from .translate import translate, translate_by_deeplx, DeepLXTranslationResult
from .vercel_client import translate_via_vercel
from .constants import SUPPORTED_LANGUAGES, SupportedCode, TargetLanguage, SourceLanguage

__version__ = "1.0.0"
__author__ = "DeepLX Python Team"

__all__ = [
    "translate",
    "translate_by_deeplx", 
    "translate_via_vercel",
    "DeepLXTranslationResult",
    "SUPPORTED_LANGUAGES",
    "SupportedCode",
    "TargetLanguage", 
    "SourceLanguage"
]
