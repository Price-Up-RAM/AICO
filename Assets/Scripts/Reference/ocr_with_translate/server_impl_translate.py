"""
Translator 모듈 (Flask Blueprint)
다국어 번역 API

server_test.py의 로직을 Blueprint로 분리한 모듈
server_interface.py에서 import하여 사용
"""

from flask import Blueprint, request, jsonify
import time

import util_translator
import state

# Blueprint 생성
translate_bp = Blueprint('translate', __name__)

# 전역 Translator 인스턴스
translator_instance = None


def init_translator():
    """Translator 인스턴스 초기화"""
    global translator_instance
    
    print("[INIT] Translator 초기화 중...")
    
    translator_instance = util_translator.Translator()
    
    # DeepL API 키 로드 시도
    translator_instance.load_deep_api_key()
    
    # Free DeepL URLs 수집
    print("[INIT] Free DeepL URLs 수집 중...")
    translator_instance.get_freeDeepLFreeUrls()
    
    print("[INIT] Translator 초기화 완료!")
    
    return translator_instance


def get_translator_instance():
    """Translator 인스턴스 가져오기 (싱글톤)"""
    global translator_instance
    
    if translator_instance is None:
        init_translator()
    
    return translator_instance


def preload_translator():
    """서버 시작 시 Translator 미리 로드"""
    try:
        init_translator()
        print("[OK] Translator 사전 로드 완료")
    except Exception as e:
        print(f"[WARN] Translator 사전 로드 실패: {e}")


# ============================================================
# 번역 헬퍼 함수 (OCR 결과 번역용)
# ============================================================

def translate_text_with_retry(translator, text, target_lang, max_retries=2, retry_delay=0.3, is_formality=False):
    """
    단일 텍스트 번역 (재시도 지원) - 배치용
    
    Args:
        translator: Translator 인스턴스
        text: 번역할 텍스트
        target_lang: 목표 언어 (ko, ja, en 등)
        max_retries: 최대 재시도 횟수 (기본: 2)
        retry_delay: 재시도 간격 초 (기본: 0.3)
        is_formality: 존칭 번역 여부 (기본: False)
    
    Returns:
        dict: {"success": bool, "text": str, "source": str, "error": str}
    """
    import time as _time
    
    if not text or not text.strip():
        return {"success": True, "text": text, "source": "skip_empty", "error": None}
    
    last_error = None
    last_source = None
    
    for attempt in range(max_retries):
        try:
            # is_formality에 따라 번역 함수 선택
            if is_formality:
                result = translator.translate_formality(text, target_lang)
            else:
                result = translator.translate(text, target_lang)
            
            translated = result.get('text', '')
            source = result.get('source', 'Unknown')
            last_source = source
            
            if translated:
                return {"success": True, "text": translated, "source": source, "error": None}
            else:
                # 빈 결과 = 실패
                last_error = f"Empty result from {source}"
                if attempt < max_retries - 1:
                    _time.sleep(retry_delay)
                    continue
                    
        except Exception as e:
            last_error = str(e)
            last_source = "exception"
            if attempt < max_retries - 1:
                _time.sleep(retry_delay)
                continue
    
    # 최종 실패
    return {
        "success": False, 
        "text": text, 
        "source": last_source or "Failed",
        "error": last_error or "Max retries exceeded"
    }


def translate_texts_batch(texts, target_lang='ko', max_workers=5, is_formality=False):
    """
    여러 텍스트 병렬 번역 (OCR 결과용) - 필터링 + 캐싱 적용
    
    Args:
        texts: 번역할 텍스트 리스트
        target_lang: 목표 언어 (기본: ko)
        max_workers: 동시 번역 스레드 수 (기본: 5)
        is_formality: 존칭 번역 여부 (기본: False)
    
    Returns:
        dict: {
            "translated": [...],  # 최종 결과 (번역됨 + 필터링됨 + 실패=원본)
            "translated_indices": [...],  # 실제 번역된 인덱스
            "filtered_indices": [...],  # 필터링된 인덱스 (번역 불필요)
            "cached_indices": [...],  # 캐시에서 가져온 인덱스
            "failed_indices": [...],  # 번역 실패 인덱스
            "stats": {...}
        }
    """
    from concurrent.futures import ThreadPoolExecutor, as_completed
    import threading
    
    # 필터링 모듈 import
    try:
        from util_translator_filter import filter_translation_texts
        FILTER_AVAILABLE = True
    except ImportError:
        print("[WARN] util_translator_filter 모듈 없음, 필터링 비활성화")
        FILTER_AVAILABLE = False
    
    translator = get_translator_instance()
    
    print(f"[TRANSLATE_BATCH] {len(texts)}개 텍스트 처리 시작 (target: {target_lang}, formality: {is_formality})")
    
    # ============================================================
    # 1단계: 필터링 - 번역 필요 여부 판단
    # ============================================================
    if FILTER_AVAILABLE:
        filter_results = filter_translation_texts(texts, target_lang, include_reason=True)
        
        # 번역 필요 / 불필요 분류
        to_translate = []  # [(원본인덱스, 텍스트), ...]
        filtered_indices = []  # 번역 불필요 인덱스
        filtered_reasons = {}  # {인덱스: 이유}
        
        for item in filter_results:
            idx = item['index']
            if item['needs_translation']:
                to_translate.append((idx, texts[idx]))
            else:
                filtered_indices.append(idx)
                filtered_reasons[idx] = item.get('reason', 'filtered')
        
        print(f"[FILTER] 필터링 결과:")
        print(f"  - 전체: {len(texts)}개")
        print(f"  - 번역 필요: {len(to_translate)}개")
        print(f"  - 번역 불필요: {len(filtered_indices)}개 (절감율: {len(filtered_indices)/len(texts)*100:.1f}%)")
        
        # 필터링 이유별 통계
        reason_counts = {}
        for reason in filtered_reasons.values():
            reason_counts[reason] = reason_counts.get(reason, 0) + 1
        if reason_counts:
            print(f"  - 필터 이유: {reason_counts}")
    else:
        # 필터링 없이 전체 번역
        to_translate = [(idx, text) for idx, text in enumerate(texts)]
        filtered_indices = []
        filtered_reasons = {}
    
    # ============================================================
    # 2단계: 캐싱 - 중복 텍스트 제거
    # ============================================================
    translation_cache = {}  # {원본텍스트: 번역결과}
    unique_to_translate = []  # 실제로 번역할 고유 텍스트
    cached_indices = []  # 캐시에서 가져올 인덱스
    text_to_indices = {}  # {원본텍스트: [인덱스들]}
    
    for idx, text in to_translate:
        if text in text_to_indices:
            # 이미 같은 텍스트가 있음 → 캐시 대상
            text_to_indices[text].append(idx)
            cached_indices.append(idx)
        else:
            # 처음 보는 텍스트 → 번역 대상
            text_to_indices[text] = [idx]
            unique_to_translate.append((idx, text))
    
    if len(to_translate) != len(unique_to_translate):
        print(f"[CACHE] 중복 제거: {len(to_translate)}개 → {len(unique_to_translate)}개 (캐시: {len(cached_indices)}개)")
    
    # ============================================================
    # 3단계: 번역 API 호출 (고유 텍스트만)
    # ============================================================
    # 결과 저장 (인덱스 순서 유지)
    final_results = list(texts)  # 원본으로 초기화
    translated_indices = []
    failed_indices = []
    failed_details = []
    sources_used = {}
    sources_lock = threading.Lock()
    cache_lock = threading.Lock()
    
    if not unique_to_translate:
        print(f"[TRANSLATE_BATCH] 번역할 텍스트 없음 (모두 필터링/캐시됨)")
    else:
        print(f"[TRANSLATE_BATCH] {len(unique_to_translate)}개 고유 텍스트 병렬 번역 시작 (workers: {max_workers})")
        
        # 사용 가능한 번역 소스 표시
        print(f"[TRANSLATE_BATCH] 사용 가능한 번역기:")
        print(f"  - DeepL: {util_translator.DEEPL_AVAILABLE}")
        print(f"  - Google: {util_translator.GOOGLETRANS_AVAILABLE}")
        print(f"  - DeepLX Python: {util_translator.DEEPLX_PYTHON_AVAILABLE}")
        print(f"  - Free DeepL URLs: {len(translator.freeDeepLFreeUrls)}개")
        
        def translate_single(orig_idx, text):
            """단일 텍스트 번역 (스레드에서 실행)"""
            result = translate_text_with_retry(
                translator, text, target_lang, 
                max_retries=2, retry_delay=0.3, is_formality=is_formality
            )
            return orig_idx, text, result
        
        # 병렬 실행
        with ThreadPoolExecutor(max_workers=max_workers) as executor:
            futures = {executor.submit(translate_single, idx, text): idx for idx, text in unique_to_translate}
            
            completed = 0
            for future in as_completed(futures):
                orig_idx, orig_text, result = future.result()
                
                # 캐시에 저장 (스레드 안전)
                with cache_lock:
                    translation_cache[orig_text] = result
                
                # 소스 카운트 (스레드 안전)
                source = result.get('source', 'Unknown')
                with sources_lock:
                    sources_used[source] = sources_used.get(source, 0) + 1
                
                # 같은 텍스트를 가진 모든 인덱스에 결과 적용
                all_indices = text_to_indices.get(orig_text, [orig_idx])
                
                for idx in all_indices:
                    if result['success']:
                        final_results[idx] = result['text']
                        if idx not in translated_indices:
                            translated_indices.append(idx)
                    else:
                        # 실패 시 원본 유지
                        if idx not in failed_indices:
                            failed_indices.append(idx)
                            failed_details.append({
                                "index": idx,
                                "text": texts[idx][:50] + "..." if len(texts[idx]) > 50 else texts[idx],
                                "source": source,
                                "error": result.get('error', 'Unknown error')
                            })
                
                completed += 1
                
                # 진행 상황 출력 (10개마다)
                if completed % 10 == 0 or completed == len(unique_to_translate):
                    print(f"[TRANSLATE] 진행: {completed}/{len(unique_to_translate)}")
    
    # ============================================================
    # 4단계: 결과 정리
    # ============================================================
    # 캐시로 처리된 인덱스 정리 (translated_indices에서 중복 제거)
    actual_api_calls = len(unique_to_translate) if unique_to_translate else 0
    cache_hits = len(cached_indices)
    
    print(f"[TRANSLATE_BATCH] ========== 최종 결과 ==========")
    print(f"  - 전체: {len(texts)}개")
    print(f"  - 필터링 (번역 불필요): {len(filtered_indices)}개")
    print(f"  - 번역 성공: {len(translated_indices)}개")
    print(f"    └ API 호출: {actual_api_calls}개, 캐시 적용: {cache_hits}개")
    print(f"  - 번역 실패: {len(failed_indices)}개")
    
    total_savings = len(filtered_indices) + cache_hits
    if texts:
        print(f"  - 총 절감: {total_savings}개 ({total_savings/len(texts)*100:.1f}%)")
    
    if sources_used:
        print(f"  - 사용된 번역 소스: {sources_used}")
    
    if failed_indices:
        print(f"[TRANSLATE_BATCH] 실패 상세 (처음 5개):")
        for detail in failed_details[:5]:
            print(f"  [{detail['index']}] {detail['source']}: {detail['error'][:40]}")
    
    return {
        "translated": final_results,
        "translated_indices": translated_indices,
        "filtered_indices": filtered_indices,
        "cached_indices": cached_indices,
        "failed_indices": failed_indices,
        "failed_details": failed_details[:10],
        "stats": {
            "total": len(texts),
            "filtered": len(filtered_indices),
            "translated": len(translated_indices),
            "cached": cache_hits,
            "api_calls": actual_api_calls,
            "failed": len(failed_indices),
            "filter_savings_percent": round(len(filtered_indices) / len(texts) * 100, 1) if texts else 0,
            "cache_savings_percent": round(cache_hits / len(texts) * 100, 1) if texts else 0,
            "total_savings_percent": round(total_savings / len(texts) * 100, 1) if texts else 0,
            "sources_used": sources_used
        }
    }


# ============================================================
# API 엔드포인트 (Blueprint)
# ============================================================

@translate_bp.route('/translate', methods=['POST'])
def translate():
    """텍스트 번역"""
    try:
        # JSON 또는 Form 데이터 파싱
        if request.is_json:
            data = request.get_json()
            text = data.get('text', '')
            target_lang = data.get('target_lang', 'ko')
        else:
            text = request.form.get('text', '')
            target_lang = request.form.get('target_lang', 'ko')
        
        if not text:
            return jsonify({"error": "No text provided", "status": "failed"}), 400
        
        print(f"[TRANSLATE] 번역 요청: '{text[:50]}...' -> {target_lang}")
        
        # 번역 실행
        translator = get_translator_instance()
        start_time = time.time()
        result = translator.translate(text, target_lang)
        elapsed_time = time.time() - start_time
        
        response = {
            "status": "success",
            "original_text": text,
            "translated_text": result.get('text', ''),
            "target_lang": target_lang,
            "source": result.get('source', 'Unknown'),
            "translation_time": result.get('time', ''),
            "processing_time": f"{elapsed_time:.3f}s"
        }
        
        # 로그 출력
        translated_text = result.get('text', '')[:50]
        print(f"[TRANSLATE] 완료: '{translated_text}...' (source: {result.get('source')}, {elapsed_time:.3f}s)")
        
        return jsonify(response), 200
        
    except Exception as e:
        print(f"[ERROR] 번역 실패: {e}")
        import traceback
        traceback.print_exc()
        
        return jsonify({
            "error": str(e),
            "status": "failed"
        }), 500


@translate_bp.route('/translate/formality', methods=['POST'])
def translate_formality():
    """존칭 번역 (formality 적용)"""
    try:
        # JSON 또는 Form 데이터 파싱
        if request.is_json:
            data = request.get_json()
            text = data.get('text', '')
            target_lang = data.get('target_lang', 'ko')
        else:
            text = request.form.get('text', '')
            target_lang = request.form.get('target_lang', 'ko')
        
        if not text:
            return jsonify({"error": "No text provided", "status": "failed"}), 400
        
        print(f"[TRANSLATE_FORMALITY] 존칭 번역 요청: '{text[:50]}...' -> {target_lang}")
        
        # 존칭 번역 실행
        translator = get_translator_instance()
        start_time = time.time()
        result = translator.translate_formality(text, target_lang)
        elapsed_time = time.time() - start_time
        
        response = {
            "status": "success",
            "original_text": text,
            "translated_text": result.get('text', ''),
            "target_lang": target_lang,
            "source": result.get('source', 'Unknown'),
            "translation_time": result.get('time', ''),
            "processing_time": f"{elapsed_time:.3f}s"
        }
        
        # 로그 출력
        translated_text = result.get('text', '')[:50]
        print(f"[TRANSLATE_FORMALITY] 완료: '{translated_text}...' (source: {result.get('source')}, {elapsed_time:.3f}s)")
        
        return jsonify(response), 200
        
    except Exception as e:
        print(f"[ERROR] 존칭 번역 실패: {e}")
        import traceback
        traceback.print_exc()
        
        return jsonify({
            "error": str(e),
            "status": "failed"
        }), 500


@translate_bp.route('/translate/formality_test', methods=['GET'])
def translate_formality_test():
    """존칭 번역 테스트 (GET 방식)"""
    try:
        # Query Parameter로 데이터 파싱
        text = request.args.get('text', '')
        target_lang = request.args.get('target_lang', 'ko')
        
        if not text:
            return jsonify({"error": "No text provided", "status": "failed"}), 400
        
        print(f"[TRANSLATE_FORMALITY_TEST] 존칭 번역 요청: '{text[:50]}...' -> {target_lang}")
        
        # 존칭 번역 실행
        translator = get_translator_instance()
        start_time = time.time()
        result = translator.translate_formality(text, target_lang)
        elapsed_time = time.time() - start_time
        
        response = {
            "status": "success",
            "original_text": text,
            "translated_text": result.get('text', ''),
            "target_lang": target_lang,
            "source": result.get('source', 'Unknown'),
            "translation_time": result.get('time', ''),
            "processing_time": f"{elapsed_time:.3f}s"
        }
        
        # 로그 출력
        translated_text = result.get('text', '')[:50]
        print(f"[TRANSLATE_FORMALITY_TEST] 완료: '{translated_text}...' (source: {result.get('source')}, {elapsed_time:.3f}s)")
        
        return jsonify(response), 200
        
    except Exception as e:
        print(f"[ERROR] 존칭 번역 테스트 실패: {e}")
        import traceback
        traceback.print_exc()
        
        return jsonify({
            "error": str(e),
            "status": "failed"
        }), 500


@translate_bp.route('/translate/health', methods=['GET'])
def translate_health():
    """서버 상태 확인"""
    try:
        global translator_instance
        translator_loaded = translator_instance is not None
        
        # 모듈 상태 확인
        modules_status = {
            "deepl": util_translator.DEEPL_AVAILABLE,
            "googletrans": util_translator.GOOGLETRANS_AVAILABLE,
            "pydeeplx": util_translator.PYDEEPLX_AVAILABLE,
            "deeplx_python": util_translator.DEEPLX_PYTHON_AVAILABLE
        }
        
        free_urls_count = 0
        if translator_instance:
            free_urls_count = len(translator_instance.freeDeepLFreeUrls)
        
        return jsonify({
            "status": "healthy",
            "translator_loaded": translator_loaded,
            "modules": modules_status,
            "free_deepl_urls_count": free_urls_count
        }), 200
        
    except Exception as e:
        return jsonify({
            "status": "unhealthy",
            "error": str(e)
        }), 500


@translate_bp.route('/translate/info', methods=['GET'])
def translate_info():
    """번역기 정보"""
    global translator_instance
    
    free_urls = []
    if translator_instance:
        free_urls = translator_instance.freeDeepLFreeUrls
    
    return jsonify({
        "service": "Translator API",
        "description": "다국어 번역 API (DeepL, Google 등 지원)",
        "modules": {
            "deepl": util_translator.DEEPL_AVAILABLE,
            "googletrans": util_translator.GOOGLETRANS_AVAILABLE,
            "pydeeplx": util_translator.PYDEEPLX_AVAILABLE,
            "deeplx_python": util_translator.DEEPLX_PYTHON_AVAILABLE
        },
        "free_deepl_urls": free_urls,
        "supported_languages": ["ko", "ja", "en", "zh", "de", "fr", "es"],
        "api": {
            "translate": {
                "endpoint": "/translate",
                "method": "POST",
                "parameters": {
                    "text": "번역할 텍스트 (필수)",
                    "target_lang": "목표 언어 (ko, ja, en 등, 기본: ko)"
                }
            },
            "translate_formality": {
                "endpoint": "/translate/formality",
                "method": "POST",
                "parameters": {
                    "text": "번역할 텍스트 (필수)",
                    "target_lang": "목표 언어 (ko, ja, en 등, 기본: ko)"
                },
                "description": "존칭/격식체 번역"
            }
        },
        "usage": {
            "translate": "curl -X POST -H 'Content-Type: application/json' -d '{\"text\": \"Hello\", \"target_lang\": \"ko\"}' http://localhost:5000/translate",
            "formality": "curl -X POST -H 'Content-Type: application/json' -d '{\"text\": \"I am ready\", \"target_lang\": \"ja\"}' http://localhost:5000/translate/formality"
        }
    }), 200


@translate_bp.route('/translate/refresh', methods=['POST'])
def translate_refresh():
    """Free DeepL URLs 새로고침"""
    try:
        translator = get_translator_instance()
        
        print("[REFRESH] Free DeepL URLs 새로고침 중...")
        start_time = time.time()
        translator.get_freeDeepLFreeUrls()
        elapsed_time = time.time() - start_time
        
        return jsonify({
            "status": "success",
            "free_deepl_urls_count": len(translator.freeDeepLFreeUrls),
            "free_deepl_urls": translator.freeDeepLFreeUrls,
            "refresh_time": f"{elapsed_time:.2f}s"
        }), 200
        
    except Exception as e:
        print(f"[ERROR] URLs 새로고침 실패: {e}")
        import traceback
        traceback.print_exc()
        
        return jsonify({
            "error": str(e),
            "status": "failed"
        }), 500

