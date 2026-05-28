"""
DeepLX Python - Constants
"""

from typing import List, Dict, Set, Union

# 지원되는 언어 목록
SUPPORTED_LANGUAGES = [
    {"code": "BG", "language": "Bulgarian"},
    {"code": "ZH", "language": "Chinese"},
    {"code": "CS", "language": "Czech"},
    {"code": "DA", "language": "Danish"},
    {"code": "NL", "language": "Dutch"},
    {"code": "EN", "language": "English"},
    {"code": "ET", "language": "Estonian"},
    {"code": "FI", "language": "Finnish"},
    {"code": "FR", "language": "French"},
    {"code": "DE", "language": "German"},
    {"code": "EL", "language": "Greek"},
    {"code": "HU", "language": "Hungarian"},
    {"code": "IT", "language": "Italian"},
    {"code": "JA", "language": "Japanese"},
    {"code": "LV", "language": "Latvian"},
    {"code": "LT", "language": "Lithuanian"},
    {"code": "PL", "language": "Polish"},
    {"code": "PT", "language": "Portuguese"},
    {"code": "RO", "language": "Romanian"},
    {"code": "RU", "language": "Russian"},
    {"code": "SK", "language": "Slovak"},
    {"code": "SL", "language": "Slovenian"},
    {"code": "ES", "language": "Spanish"},
    {"code": "SV", "language": "Swedish"},
    {"code": "TR", "language": "Turkish"},
    {"code": "ID", "language": "Indonesian"},
    {"code": "UK", "language": "Ukrainian"},
    {"code": "KO", "language": "Korean"},  # 한국어 추가
]

# 타입 정의
SupportedCode = Union[
    "BG", "ZH", "CS", "DA", "NL", "EN", "ET", "FI", "FR", "DE", "EL", 
    "HU", "IT", "JA", "LV", "LT", "PL", "PT", "RO", "RU", "SK", "SL", 
    "ES", "SV", "TR", "ID", "UK", "KO"
]

TargetLanguage = str  # 대상 언어 (대소문자 구분 없음)
SourceLanguage = Union[str, None]  # 소스 언어 또는 "auto" 또는 None

# 격식 톤
FORMALITY_TONES = {"formal", "informal", "undefined"}

# DeepL API URL
API_URL = "https://www2.deepl.com/jsonrpc"

# HTTP 상태 코드
HTTP_STATUS_OK = 200
HTTP_STATUS_BAD_REQUEST = 400
HTTP_STATUS_NOT_FOUND = 404
HTTP_STATUS_NOT_ALLOWED = 405
HTTP_STATUS_INTERNAL_ERROR = 500
HTTP_STATUS_SERVICE_UNAVAILABLE = 503

# 공통 헤더 (iOS 앱을 흉내)
COMMON_HEADERS = {
    "Content-Type": "application/json",
    "User-Agent": "DeepL/1627620 CFNetwork/3826.500.62.2.1 Darwin/24.4.0",
    "Accept": "*/*",
    "X-App-Os-Name": "iOS",
    "X-App-Os-Version": "18.4.0",
    "Accept-Language": "en-US,en;q=0.9",
    "Accept-Encoding": "gzip, deflate, br",
    "X-App-Device": "iPhone16,2",
    "Referer": "https://www.deepl.com/",
    "X-Product": "translator",
    "X-App-Build": "1627620",
    "X-App-Version": "25.1",
}
