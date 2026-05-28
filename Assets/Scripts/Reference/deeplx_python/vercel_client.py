"""
DeepLX Python - Vercel 백업 서비스 클라이언트

직접 API 실패시 백업으로 사용되는 Vercel 온라인 서비스
"""

import json
from . import httpx_013 as httpx
from typing import Optional

from .constants import HTTP_STATUS_OK, TargetLanguage, SourceLanguage


def translate_via_vercel(
    text: str,
    target_lang: TargetLanguage,
    source_lang: Optional[SourceLanguage] = None
) -> str:
    """
    Vercel 온라인 서비스를 통해 번역합니다. (백업용)
    
    Args:
        text: 번역할 텍스트
        target_lang: 대상 언어
        source_lang: 소스 언어 (None인 경우 "auto"로 처리)
        
    Returns:
        str: 번역된 텍스트
        
    Raises:
        Exception: 번역 실패 시
    """
    
    # Vercel 서비스 URL
    vercel_url = "https://deeplx.vercel.app/translate"
    
    # 요청 데이터 준비
    request_data = {
        "text": text,
        "target_lang": target_lang.upper()
    }
    
    if source_lang and source_lang != "auto":
        request_data["source_lang"] = source_lang.upper()
    
    # 헤더 설정
    headers = {
        "Content-Type": "application/json",
        "User-Agent": "DeepLX-Python-Backup/1.0.0"
    }
    
    try:
        # httpx로 POST 요청 전송
        with httpx.Client(timeout=30.0) as client:
            response = client.post(
                vercel_url,
                json=request_data,
                headers=headers
            )
            
            response.raise_for_status()
            
            # 응답 파싱
            result = response.json()
            
            if result.get("code") == HTTP_STATUS_OK:
                return result.get("data", "")
            else:
                raise Exception(f"Vercel 번역 실패: {result.get('data', '알 수 없는 오류')}")
                
    except Exception as e:
        raise Exception(f"Vercel 서비스 요청 실패: {str(e)}")
