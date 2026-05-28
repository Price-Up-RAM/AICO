"""
HTTPX 0.13.3 전용 Import 모듈

deeplx_python 내부에 포함된 0.13.3 버전을 사용합니다.
"""

import sys

try:
    # httpcore_old를 import하고 백업
    from . import httpcore_old
    
    # 기존 httpcore 백업 (있을 경우)
    _original_httpcore = sys.modules.get('httpcore', None)
    
    # 임시로 httpcore_old를 httpcore로 설정
    sys.modules['httpcore'] = httpcore_old
    
    # httpx_old import (위에서 설정한 httpcore_old를 사용)
    from . import httpx_old
    
    # 즉시 원래 상태로 복원
    if _original_httpcore is not None:
        sys.modules['httpcore'] = _original_httpcore
    else:
        sys.modules.pop('httpcore', None)
    
    # httpx_old의 모든 속성을 현재 모듈에 노출
    Client = httpx_old.Client
    AsyncClient = httpx_old.AsyncClient
    get = httpx_old.get
    post = httpx_old.post
    put = httpx_old.put
    delete = httpx_old.delete
    patch = httpx_old.patch
    head = httpx_old.head
    options = httpx_old.options
    request = httpx_old.request
    stream = httpx_old.stream
    
    # 예외 클래스들
    HTTPError = httpx_old.HTTPError
    ConnectTimeout = httpx_old.ConnectTimeout
    ReadTimeout = httpx_old.ReadTimeout
    WriteTimeout = httpx_old.WriteTimeout
    PoolTimeout = httpx_old.PoolTimeout
    NetworkError = httpx_old.NetworkError
    TooManyRedirects = httpx_old.TooManyRedirects
    RedirectBodyUnavailable = getattr(httpx_old, 'RedirectBodyUnavailable', None)
    RequestBodyUnavailable = getattr(httpx_old, 'RequestBodyUnavailable', None)
    
    # Response 클래스
    Response = httpx_old.Response
    
    # 기타 유용한 클래스들
    URL = httpx_old.URL
    Headers = httpx_old.Headers
    Cookies = httpx_old.Cookies
    
    # 버전 정보
    __version__ = httpx_old.__version__
    
    def version_info():
        """현재 사용 중인 httpx 버전 정보"""
        return f"HTTPX 0.13.3 (실제 버전: {httpx_old.__version__}) with httpcore {httpcore_old.__version__}"
        
    # 성공적으로 로드됨을 표시
    _LOADED_SUCCESSFULLY = True
    
except ImportError as e:
    print(f"❌ HTTPX 0.13.3 로드 실패: {e}")
    print("   일반적인 httpx를 fallback으로 사용합니다.")
    
    # fallback: 일반 httpx 사용
    import httpx as _httpx
    
    Client = _httpx.Client
    AsyncClient = _httpx.AsyncClient
    get = _httpx.get
    post = _httpx.post
    put = _httpx.put
    delete = _httpx.delete
    patch = _httpx.patch
    head = _httpx.head
    options = _httpx.options
    request = _httpx.request
    stream = _httpx.stream
    
    HTTPError = _httpx.HTTPError
    ConnectTimeout = _httpx.ConnectTimeout
    ReadTimeout = _httpx.ReadTimeout
    WriteTimeout = _httpx.WriteTimeout
    PoolTimeout = _httpx.PoolTimeout
    NetworkError = _httpx.NetworkError
    TooManyRedirects = _httpx.TooManyRedirects
    
    Response = _httpx.Response
    URL = _httpx.URL
    Headers = _httpx.Headers
    Cookies = _httpx.Cookies
    
    __version__ = _httpx.__version__
    
    def version_info():
        return f"HTTPX Fallback (버전: {_httpx.__version__})"
        
    _LOADED_SUCCESSFULLY = False

finally:
    # httpcore 모듈 상태를 안전하게 정리
    # (위에서 이미 복원했지만, 예외 발생 시를 대비)
    pass
