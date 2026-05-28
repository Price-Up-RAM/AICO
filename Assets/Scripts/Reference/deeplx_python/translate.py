"""
DeepLX Python - Core Translation Logic
"""

import json
from . import httpx_013 as httpx
from typing import Optional, Dict, Any, List, Union
from langdetect import detect

from .constants import (
    API_URL, COMMON_HEADERS, HTTP_STATUS_OK, HTTP_STATUS_NOT_FOUND, 
    HTTP_STATUS_SERVICE_UNAVAILABLE, TargetLanguage, SourceLanguage
)
from .utils import (
    get_i_count, get_random_number, get_timestamp, format_post_string,
    abbreviate_language, split_and_process
)
from .vercel_client import translate_via_vercel


class DeepLXTranslationResult:
    """번역 결과를 담는 클래스"""
    
    def __init__(self, code: int, message: str = "", data: str = "", 
                 alternatives: List[str] = None, source_lang: str = "", 
                 target_lang: str = "", method: str = "Free", request_id: int = 0):
        self.code = code
        self.message = message
        self.data = data
        self.alternatives = alternatives or []
        self.source_lang = source_lang
        self.target_lang = target_lang
        self.method = method
        self.id = request_id


def make_request(post_data: Dict[str, Any], proxy_url: Optional[str] = None, 
                dl_session: Optional[str] = None) -> Dict[str, Any]:
    """DeepL API에 HTTP 요청을 보냅니다. (httpx + User-Agent 우회 적용)"""
    
    # 🔥 핵심 수정: 차단되지 않는 User-Agent 사용
    headers = COMMON_HEADERS.copy()
    headers["User-Agent"] = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_4 like Mac OS X) AppleWebKit/605.1.15"
    
    if dl_session:
        headers["Cookie"] = f"dl_session={dl_session}"
    
    # httpx 클라이언트 설정 (더 안정적인 연결)
    client_kwargs = {
        "timeout": 30.0,
        "verify": True,  # SSL 검증
    }
    
    if proxy_url:
        client_kwargs["proxies"] = proxy_url
    
    # httpx로 요청 (더 안정적, 재시도 로직 포함)
    max_retries = 3
    for attempt in range(max_retries):
        try:
            with httpx.Client(**client_kwargs) as client:
                response = client.post(
                    API_URL,
                    data=format_post_string(post_data),  # data 사용 (구버전 호환)
                    headers=headers
                )
                
                # 429 에러 처리
                if response.status_code == 429 and attempt < max_retries - 1:
                    import time
                    wait_time = (2 ** attempt)  # 지수 백오프
                    print(f"429 에러, {wait_time}초 후 재시도 ({attempt + 1}/{max_retries})")
                    time.sleep(wait_time)
                    continue
                
                response.raise_for_status()
                return response.json()
                
        except Exception as e:
            if attempt < max_retries - 1:
                import time
                wait_time = 1 + attempt
                print(f"연결 오류, {wait_time}초 후 재시도: {e}")
                time.sleep(wait_time)
                continue
            else:
                raise
    
    raise Exception("모든 재시도 실패")


def detect_language(text: str) -> str:
    """텍스트의 언어를 감지합니다."""
    try:
        detected = detect(text)
        # langdetect의 결과를 DeepL 코드로 매핑
        lang_mapping = {
            'ko': 'KO', 'en': 'EN', 'ja': 'JA', 'zh-cn': 'ZH', 'zh': 'ZH',
            'de': 'DE', 'fr': 'FR', 'es': 'ES', 'it': 'IT', 'ru': 'RU',
            'pt': 'PT', 'nl': 'NL', 'pl': 'PL', 'sv': 'SV', 'da': 'DA',
            'fi': 'FI', 'el': 'EL', 'cs': 'CS', 'sk': 'SK', 'sl': 'SL',
            'et': 'ET', 'lv': 'LV', 'lt': 'LT', 'bg': 'BG', 'hu': 'HU',
            'ro': 'RO', 'tr': 'TR', 'id': 'ID', 'uk': 'UK'
        }
        return lang_mapping.get(detected, 'EN')
    except:
        return 'EN'  # 기본값


def translate_by_deeplx(
    source_lang: Optional[SourceLanguage],
    target_lang: TargetLanguage,
    text: str,
    formal: Optional[bool] = None,
    tag_handling: str = "plaintext",
    proxy_url: Optional[str] = None,
    dl_session: Optional[str] = None
) -> DeepLXTranslationResult:
    """
    DeepLX를 사용하여 텍스트를 번역합니다.
    
    Args:
        source_lang: 소스 언어 (None 또는 'auto'인 경우 자동 감지)
        target_lang: 대상 언어
        text: 번역할 텍스트
        formal: 격식 여부 (True: 격식, False: 비격식, None: 기본값)
        tag_handling: 태그 처리 방식
        proxy_url: 프록시 URL
        dl_session: DeepL 세션
        
    Returns:
        DeepLXTranslationResult: 번역 결과
    """
    
    if not text:
        return DeepLXTranslationResult(
            code=HTTP_STATUS_NOT_FOUND,
            message="번역할 텍스트가 없습니다"
        )
    
    # 텍스트를 줄 단위로 분할
    text_parts = split_and_process(text)
    translated_parts = []
    all_alternatives = []
    
    for part in text_parts:
        if not part.strip():
            translated_parts.append("")
            all_alternatives.append([""])
            continue
        
        # 소스 언어 감지
        detected_source_lang = source_lang
        if not detected_source_lang or detected_source_lang == 'auto':
            detected_source_lang = detect_language(part)
        
        source_lang_code = abbreviate_language(detected_source_lang)
        if not source_lang_code:
            source_lang_code = detected_source_lang.upper()
        
        # 대상 언어 코드 처리
        target_lang_code = abbreviate_language(target_lang)
        if not target_lang_code:
            target_lang_code = target_lang.upper()
        
        has_regional_variant = False
        if '-' in target_lang:
            target_lang_parts = target_lang.split('-')
            target_lang_code = target_lang_parts[0].upper()
            has_regional_variant = True
        
        # 작업 준비
        jobs = [{
            "kind": "default",
            "preferred_num_beams": 4,
            "raw_en_context_before": [],
            "raw_en_context_after": [],
            "sentences": [{"prefix": "", "text": part, "id": 0}]
        }]
        
        # 요청 ID 생성
        request_id = get_random_number()
        
        # 격식 설정
        formality = "undefined"
        if formal is not None:
            formality = "formal" if formal else "informal"
        
        # 번역 요청 데이터 준비
        post_data = {
            "jsonrpc": "2.0",
            "method": "LMT_handle_jobs",
            "id": request_id,
            "params": {
                "commonJobParams": {
                    "mode": "translate",
                    "formality": formality,
                    "transcribe_as": "romanize",
                    "advancedMode": False,
                    "textType": tag_handling,
                    "wasSpoken": False
                },
                "lang": {
                    "source_lang_user_selected": "auto",
                    "target_lang": target_lang_code,
                    "source_lang_computed": source_lang_code
                },
                "jobs": jobs,
                "timestamp": get_timestamp(get_i_count(part))
            }
        }
        
        # 지역 변형 추가
        if has_regional_variant:
            post_data["params"]["commonJobParams"]["regionalVariant"] = target_lang
        
        try:
            # 번역 요청
            response = make_request(post_data, proxy_url, dl_session)
            translations = response["result"]["translations"]
        except Exception as e:
            return DeepLXTranslationResult(
                code=HTTP_STATUS_SERVICE_UNAVAILABLE,
                message=f"번역 요청 실패: {str(e)}"
            )
        
        # 번역 결과 처리
        part_translation = ""
        part_alternatives = []
        
        if translations:
            # 주 번역
            for translation in translations:
                part_translation += translation["beams"][0]["sentences"][0]["text"] + " "
            part_translation = part_translation.strip()
            
            # 대안 번역
            if translations[0]["beams"]:
                num_beams = len(translations[0]["beams"])
                for i in range(1, num_beams):  # 0번은 주 번역이므로 1번부터
                    alt_text = ""
                    for translation in translations:
                        beams = translation["beams"]
                        if i < len(beams):
                            alt_text += beams[i]["sentences"][0]["text"] + " "
                    if alt_text.strip():
                        part_alternatives.append(alt_text.strip())
        
        if not part_translation:
            return DeepLXTranslationResult(
                code=HTTP_STATUS_SERVICE_UNAVAILABLE,
                message="번역에 실패했습니다"
            )
        
        translated_parts.append(part_translation)
        all_alternatives.append(part_alternatives)
    
    # 모든 번역 부분을 결합
    translated_text = '\n'.join(translated_parts)
    
    # 대안 번역 결합
    combined_alternatives = []
    max_alts = max(len(alts) for alts in all_alternatives) if all_alternatives else 0
    
    for i in range(max_alts):
        alt_parts = []
        for j, alts in enumerate(all_alternatives):
            if i < len(alts):
                alt_parts.append(alts[i])
            elif len(translated_parts[j]) == 0:
                alt_parts.append("")
            else:
                alt_parts.append(translated_parts[j])
        combined_alternatives.append('\n'.join(alt_parts))
    
    return DeepLXTranslationResult(
        code=HTTP_STATUS_OK,
        request_id=get_random_number(),
        data=translated_text,
        alternatives=combined_alternatives,
        source_lang=detected_source_lang,
        target_lang=target_lang,
        method="Pro" if dl_session else "Free"
    )


def translate(
    text: str,
    target_lang: TargetLanguage,
    source_lang: Optional[SourceLanguage] = None,
    formal: Optional[bool] = None,
    use_backup: bool = True
) -> str:
    """
    이중 안전장치 번역 함수입니다. (직접 API + Vercel 백업)
    
    Args:
        text: 번역할 텍스트
        target_lang: 대상 언어
        source_lang: 소스 언어 (None인 경우 자동 감지)
        formal: 격식 여부
        use_backup: Vercel 백업 서비스 사용 여부 (기본: True)
        
    Returns:
        str: 번역된 텍스트
        
    Raises:
        Exception: 모든 번역 방법 실패 시
    """
    
    # 1차: 직접 DeepL API 호출 (httpx + 재시도)
    try:
        result = translate_by_deeplx(source_lang, target_lang, text, formal)
        
        if result.code == HTTP_STATUS_OK:
            return result.data
        else:
            raise Exception(result.message)
            
    except Exception as direct_error:
        if not use_backup:
            raise direct_error
        
        print(f"🔄 직접 API 실패, Vercel 백업 서비스로 전환: {direct_error}")
        
        # 2차: Vercel 백업 서비스 호출
        try:
            return translate_via_vercel(text, target_lang, source_lang)
            
        except Exception as vercel_error:
            # 모든 방법 실패
            raise Exception(
                f"모든 번역 방법 실패 - "
                f"직접 API: {direct_error}, "
                f"Vercel 백업: {vercel_error}"
            )
