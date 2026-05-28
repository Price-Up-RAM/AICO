"""
PaddleOCR 모듈 (Flask Blueprint)
PP-OCRv5 다국어 OCR API

main_server.py의 로직을 Blueprint로 분리한 모듈
server_interface.py에서 import하여 사용
"""

from flask import Blueprint, request, jsonify
import os
import uuid
import time
import gc

# paddleOCR 모듈에서 핵심 클래스 import
from paddleOCR import PPOCRv5, check_cuda_available, get_gpu_info

# Blueprint 생성
ocr_bp = Blueprint('ocr', __name__)

# 전역 OCR 인스턴스 및 설정
ocr_instance = None
current_device = None

# 받은 이미지 저장 on/off
SAVE_UPLOADED_IMAGE = True


def init_ocr(device=None):
    """
    OCR 인스턴스 초기화
    
    Args:
        device: 'gpu', 'cpu', 'auto', 또는 None (자동)
    
    Returns:
        PPOCRv5 인스턴스
    """
    global ocr_instance, current_device
    
    print("[INIT] PaddleOCR 초기화 중...")
    
    # 디바이스 결정
    if device is None or device == 'auto':
        device = 'auto'
    
    ocr_instance = PPOCRv5(use_gpu=(device != 'cpu'), device=device)
    current_device = ocr_instance.device_type
    
    print(f"[INIT] PaddleOCR 초기화 완료! (Device: {current_device})")
    
    return ocr_instance


def get_ocr_instance():
    """OCR 인스턴스 가져오기 (싱글톤)"""
    global ocr_instance
    
    if ocr_instance is None:
        init_ocr()
    
    return ocr_instance


def reload_ocr(device=None):
    """
    OCR 모델 리로드
    
    Args:
        device: 'gpu', 'cpu', 'auto', 또는 None
    
    Returns:
        dict: 리로드 결과
    """
    global ocr_instance, current_device
    
    old_device = current_device
    
    # 기존 인스턴스 정리
    if ocr_instance is not None:
        print("[RELOAD] 기존 모델 정리 중...")
        ocr_instance = None
        
        # 메모리 정리
        gc.collect()
        
        try:
            import paddle
            paddle.device.cuda.empty_cache()
        except:
            pass
    
    # 새 인스턴스 생성
    init_ocr(device)
    
    return {
        'old_device': old_device,
        'new_device': current_device,
        'status': 'success'
    }


def preload_ocr(device=None):
    """서버 시작 시 OCR 모델 미리 로드"""
    try:
        init_ocr(device)
        print("[OK] OCR 모델 사전 로드 완료")
    except Exception as e:
        print(f"[WARN] OCR 모델 사전 로드 실패: {e}")


def get_current_device():
    """현재 디바이스 정보 반환"""
    return current_device


# ============================================================
# Helper Functions (Internal)
# ============================================================

def _setup_logging(is_debug, endpoint_name, image_filename):
    """
    Setup logging directory and request parameters template.
    
    Args:
        is_debug: Enable debug logging
        endpoint_name: API endpoint name (e.g., "/paddle/ocr")
        image_filename: Original uploaded image filename
    
    Returns:
        tuple: (log_dir, timestamp, request_params) or (None, None, None) if is_debug=False
    """
    if not is_debug:
        return None, None, None
    
    import datetime
    import json
    
    timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    log_dir = f"./log/result/{timestamp}"
    os.makedirs(log_dir, exist_ok=True)
    print(f"[LOG] Logging to: {log_dir}")
    
    request_params = {
        "timestamp": timestamp,
        "endpoint": endpoint_name,
        "image_filename": image_filename,
        "parameters": {}  # Caller will fill this
    }
    
    return log_dir, timestamp, request_params


# ============================================================
# API 엔드포인트 (Blueprint)
# ============================================================

@ocr_bp.route('/paddle/ocr', methods=['POST'])
def paddle_ocr():
    """
    통합 OCR 엔드포인트 (OCR + 선택적 번역)
    
    Parameters:
        image: 이미지 파일 (필수)
        use_translate: 번역 활성화 (true/false, 기본: false)
        
        # OCR 파라미터
        origin_lang: OCR 원본 언어 (ko, ja, en, zh, fr, de, 기본: ko)
        origin_lang_auto_detect: 자동 언어 감지 (true/false, 기본: false)
        is_sentence: 문장 단위 (true/false, 기본: true)
        merge_threshold: 병합 임계값 픽셀 (정수, 기본: is_sentence=true시 15, false시 0)
        
        # 번역 파라미터 (use_translate=true일 때만 사용)
        target_lang: 번역 목표 언어 (ko, ja, en 등)
        is_formality: 존칭 번역 (true/false, 기본: false)
        
        # 로깅 파라미터
        is_debug: 디버그 로깅 (true/false, 기본: true)
        save_result: JSON 결과 저장 (true/false, 기본: true)
        save_image: 이미지 결과 저장 (true/false, 기본: true)
    """
    try:
        # 1. 이미지 파일 체크
        if 'image' not in request.files:
            return jsonify({"error": "No image uploaded", "status": "failed"}), 400
        
        uploaded_file = request.files['image']
        if not uploaded_file or not uploaded_file.filename:
            return jsonify({"error": "Invalid image file", "status": "failed"}), 400
        
        # 2. 파라미터 파싱
        # 번역 여부
        use_translate = request.form.get('use_translate', 'false').lower() == 'true'
        
        # 로깅 파라미터
        save_result = request.form.get('save_result', 'true').lower() == 'true'
        save_image = request.form.get('save_image', 'true').lower() == 'true'
        is_debug = request.form.get('is_debug', 'true').lower() == 'true'
        
        # OCR 파라미터
        origin_lang = request.form.get('origin_lang', '').strip()
        origin_lang_auto_detect = request.form.get('origin_lang_auto_detect', 'false').lower() == 'true'
        is_sentence_str = request.form.get('is_sentence', 'true').lower()
        is_sentence = is_sentence_str == 'true'
        
        merge_threshold = request.form.get('merge_threshold', None)
        if merge_threshold is not None:
            try:
                merge_threshold = int(merge_threshold)
            except ValueError:
                merge_threshold = None
        
        # is_sentence에 따라 merge_threshold 제어
        if not is_sentence:
            merge_threshold = 0
        elif merge_threshold is None:
            merge_threshold = 15
        
        # 번역 파라미터 (use_translate=true일 때만 의미 있음)
        target_lang = request.form.get('target_lang', '').strip()
        is_formality_str = request.form.get('is_formality', 'false').lower()
        is_formality = is_formality_str == 'true'
        
        # 3. 임시 파일로 저장
        temp_dir = './files/paddle_temp'
        os.makedirs(temp_dir, exist_ok=True)
        
        file_ext = os.path.splitext(uploaded_file.filename)[1]
        temp_filename = f"paddle_{uuid.uuid4()}{file_ext}"
        temp_path = os.path.join(temp_dir, temp_filename)
        uploaded_file.save(temp_path)
        
        # 받은 이미지 저장 (on/off)
        if SAVE_UPLOADED_IMAGE:
            import shutil
            import datetime
            save_dir = './files/paddle_uploaded'
            os.makedirs(save_dir, exist_ok=True)
            timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
            saved_filename = f"{timestamp}_{uploaded_file.filename}"
            saved_path = os.path.join(save_dir, saved_filename)
            shutil.copy2(temp_path, saved_path)
            print(f"[SAVE] 이미지 저장: {saved_path}")
        
        mode_str = "[OCR+TRANSLATE]" if use_translate else "[OCR]"
        print(f"{mode_str} 분석 시작: {uploaded_file.filename}")
        print(f"{mode_str} origin_lang={origin_lang}, use_translate={use_translate}, target_lang={target_lang}, is_sentence={is_sentence}, merge_threshold={merge_threshold}")
        
        # 4. OCR 인스턴스 및 설정
        ocr = get_ocr_instance()
        detected_language = None
        language_detection = {}
        
        # 로깅 설정
        log_dir, timestamp, request_params = _setup_logging(
            is_debug, "/paddle/ocr", uploaded_file.filename
        )
        
        # 자동 언어 감지
        if origin_lang_auto_detect and not origin_lang:
            detected_language, detection_stats = ocr.detect_language_by_ocr_score(temp_path)
            language_detection = detection_stats.get(detected_language, {})
            language_detection["selected"] = detected_language
            origin_lang = detected_language
            print(f"{mode_str} 자동 언어 감지 결과: {detected_language}")
            if detected_language:
                ocr.update_config(origin_lang=detected_language)
                if request_params:
                    request_params["detected_language"] = detected_language
        
        # origin_lang 기본값 설정
        if not origin_lang:
            origin_lang = 'ko'
        
        # OCR 설정 업데이트
        current_origin_lang = ocr.current_config.get('origin_lang', 'ko')
        current_is_sentence = ocr.current_config.get('is_sentence', True)
        
        if origin_lang != current_origin_lang or is_sentence != current_is_sentence:
            if origin_lang != current_origin_lang:
                print(f"{mode_str} origin_lang 변경: {current_origin_lang} → {origin_lang}")
            if is_sentence != current_is_sentence:
                print(f"{mode_str} is_sentence 변경: {current_is_sentence} → {is_sentence}")
            ocr.update_config(origin_lang=origin_lang, is_sentence=is_sentence)
        
        # request_params 업데이트
        if request_params:
            request_params["parameters"] = {
                "use_translate": use_translate,
                "origin_lang": origin_lang,
                "origin_lang_auto_detect": origin_lang_auto_detect,
                "is_sentence": is_sentence,
                "merge_threshold": merge_threshold,
                "target_lang": target_lang if use_translate else None,
                "is_formality": is_formality if use_translate else None,
                "is_debug": is_debug
            }
            request_params["detected_language"] = detected_language
        
        # 5. 이미지 처리 (OCR)
        # use_translate=True: skip_language_filter=False (번역 대상만 유지)
        # use_translate=False: skip_language_filter=True (쓰레기만 제거, 같은 언어도 유지)
        skip_language_filter = not use_translate
        
        output = ocr.process_image(
            temp_path,
            save_dir=log_dir,
            merge_threshold=merge_threshold,
            target_lang=target_lang if use_translate else origin_lang,
            skip_language_filter=skip_language_filter,
            is_sentence=is_sentence
        )
        
        # Save PaddleOCR internal results
        if (save_result or save_image) and log_dir and output.get('result'):
            saved_files = ocr.save_ocr_result_log(
                output['result'], 
                temp_path, 
                log_dir,
                save_result=save_result,
                save_image=save_image
            )
            print(f"[LOG] PaddleOCR results saved: {saved_files}")
        
        # 6. 임시 파일 삭제
        try:
            if os.path.exists(temp_path):
                os.remove(temp_path)
        except Exception as e:
            print(f"[WARN] 임시 파일 삭제 실패: {e}")
        
        # 7. 결과 처리
        formatted = output['formatted']
        original_texts = formatted['texts']
        
        # DEV mode: Save OCR filter log
        if log_dir:
            try:
                from server_impl_OCR_test import save_ocr_log
                save_ocr_log(
                    image_filename=uploaded_file.filename,
                    formatted_raw=output.get('formatted_raw', {}),
                    formatted_filtered=output.get('formatted_filtered', formatted),
                    target_lang=target_lang if use_translate else origin_lang,
                    ocr_time=output['elapsed_time'],
                    log_dir=log_dir
                )
            except Exception as e:
                print(f"[DEV][WARN] OCR log saving failed: {e}")
        
        # 8. 번역 실행 (use_translate=true인 경우만)
        translated_texts = original_texts  # 기본값: 원본 유지
        translate_elapsed = 0
        translation_result = None
        
        if use_translate and target_lang:
            from server_impl_translate import translate_texts_batch
            
            print(f"[TRANSLATE] {len(original_texts)}개 텍스트 번역 중...")
            translate_start = time.time()
            
            translation_result = translate_texts_batch(original_texts, target_lang, is_formality=is_formality)
            translated_texts = translation_result['translated']
            
            translate_elapsed = time.time() - translate_start
            print(f"[TRANSLATE] 번역 완료: {translate_elapsed:.3f}s")
        
        # 9. 응답 생성
        response = {
            "status": "success",
            "image_filename": uploaded_file.filename,
            "processing_time": f"{output['elapsed_time']:.3f}s",
            "text_count": output['text_count'],
            "raw_count": output.get('raw_count', output['text_count']),
            "filtered_count": output.get('filtered_count', output['text_count']),
            "pre_merge_count": output.get('pre_merge_count', output['text_count']),
            "merged": output.get('merged', False),
            "merge_threshold": output.get('merge_threshold'),
            "original_count": output.get('raw_count', output['text_count']),
            "origin_lang_auto_detected": origin_lang_auto_detect and bool(detected_language),
            "detected_language": detected_language,
            "language_detection": language_detection or {},
            "ocr_config": {
                "origin_lang": origin_lang,
                "is_sentence": is_sentence,
            },
            "use_translate": use_translate,
            "results": {
                "texts": translated_texts if use_translate else original_texts,
                "scores": formatted['scores'],
                "boxes": formatted['boxes'],
                "quad_boxes": formatted['quad_boxes']
            },
            "ocr_with_region": {
                "quad_boxes": formatted['quad_boxes'],
                "labels": translated_texts if use_translate else formatted['labels']
            },
            "ocr_with_region_parsed": output['parsed']
        }
        
        # 번역 관련 응답 추가 (use_translate=true인 경우)
        if use_translate:
            response["translation_time"] = f"{translate_elapsed:.3f}s"
            response["total_time"] = f"{output['elapsed_time'] + translate_elapsed:.3f}s"
            response["target_lang"] = target_lang
            response["is_formality"] = is_formality
            response["results"]["texts_origin"] = original_texts
            response["ocr_with_region_origin"] = {
                "quad_boxes": formatted['quad_boxes'],
                "labels": original_texts
            }
            
            if translation_result:
                stats = translation_result.get('stats', {})
                response["translation_stats"] = {
                    "total": stats.get('total', len(original_texts)),
                    "filtered": stats.get('filtered', 0),
                    "translated": stats.get('translated', 0),
                    "failed": stats.get('failed', 0),
                    "filter_savings_percent": stats.get('filter_savings_percent', 0),
                    "sources_used": stats.get('sources_used', {}),
                    "translated_indices": translation_result.get('translated_indices', [])[:50],
                    "filtered_indices": translation_result.get('filtered_indices', [])[:50],
                    "failed_indices": translation_result.get('failed_indices', [])[:20],
                    "failed_details": translation_result.get('failed_details', [])[:5]
                }
        
        # Save request.json and response.json
        if log_dir:
            import json
            request_params_path = os.path.join(log_dir, "request.json")
            with open(request_params_path, 'w', encoding='utf-8') as f:
                json.dump(request_params, f, ensure_ascii=False, indent=2)
            print(f"[LOG] Request parameters saved: {request_params_path}")
            
            response_path = os.path.join(log_dir, "response.json")
            with open(response_path, 'w', encoding='utf-8') as f:
                json.dump(response, f, ensure_ascii=False, indent=2)
            print(f"[LOG] Response saved: {response_path}")
            
            # DEV_MODE 번역 테스트 로그 저장
            if use_translate and translation_result:
                try:
                    from server_impl_OCR_test import save_ocr_translate_log
                    save_ocr_translate_log(
                        image_filename=uploaded_file.filename,
                        original_texts=original_texts,
                        translation_result=translation_result,
                        target_lang=target_lang,
                        ocr_time=output['elapsed_time'],
                        translate_time=translate_elapsed,
                        log_dir=log_dir
                    )
                except Exception as e:
                    print(f"[DEV][WARN] 테스트 로그 저장 실패: {e}")
        
        if log_dir:
            response["log_dir"] = log_dir
        
        # 10. 로그 출력
        print(f"{mode_str} 완료: {uploaded_file.filename}")
        if use_translate:
            print(f"{mode_str} 텍스트 {output['text_count']}개 검출, OCR: {output['elapsed_time']:.3f}s, 번역: {translate_elapsed:.3f}s")
            for i in range(min(3, len(original_texts))):
                try:
                    print(f"  [{i+1}] {original_texts[i]} -> {translated_texts[i]} ({formatted['scores'][i]:.2f})")
                except UnicodeEncodeError:
                    print(f"  [{i+1}] {repr(original_texts[i])} -> {repr(translated_texts[i])} ({formatted['scores'][i]:.2f})")
            if len(original_texts) > 3:
                print(f"  ... 외 {len(original_texts) - 3}개")
        else:
            print(f"{mode_str} 텍스트 {output['text_count']}개 검출, 처리시간: {output['elapsed_time']:.3f}s")
            for i, (text, score) in enumerate(zip(formatted['texts'][:5], formatted['scores'][:5]), 1):
                try:
                    print(f"  [{i}] {text} ({score:.2f})")
                except UnicodeEncodeError:
                    print(f"  [{i}] {repr(text)} ({score:.2f})")
            if len(formatted['texts']) > 5:
                print(f"  ... 외 {len(formatted['texts']) - 5}개")
        
        return jsonify(response), 200
        
    except Exception as e:
        # 에러 발생 시 임시 파일 정리
        try:
            if 'temp_path' in locals() and os.path.exists(temp_path):
                os.remove(temp_path)
        except:
            pass
        
        print(f"[ERROR] OCR 분석 실패: {e}")
        import traceback
        traceback.print_exc()
        
        return jsonify({
            "error": str(e),
            "status": "failed"
        }), 500


@ocr_bp.route('/paddle/reload', methods=['POST'])
def paddle_reload():
    """모델 리로드 (GPU/CPU 전환)"""
    try:
        # 파라미터 파싱
        device = request.form.get('device', request.args.get('device', None))
        
        if device:
            device = device.lower()
            if device not in ['gpu', 'cpu', 'auto']:
                return jsonify({
                    "error": "Invalid device. Use 'gpu', 'cpu', or 'auto'",
                    "status": "failed"
                }), 400
        
        print(f"[RELOAD] 모델 리로드 요청 (device: {device or 'auto'})")
        
        start_time = time.time()
        result = reload_ocr(device)
        elapsed_time = time.time() - start_time
        
        result['reload_time'] = f"{elapsed_time:.2f}s"
        
        print(f"[RELOAD] 완료: {result['old_device']} -> {result['new_device']} ({elapsed_time:.2f}s)")
        
        return jsonify(result), 200
        
    except Exception as e:
        print(f"[ERROR] 모델 리로드 실패: {e}")
        import traceback
        traceback.print_exc()
        
        return jsonify({
            "error": str(e),
            "status": "failed"
        }), 500


@ocr_bp.route('/paddle/health', methods=['GET'])
def paddle_health():
    """서버 상태 확인"""
    try:
        global ocr_instance, current_device
        model_loaded = ocr_instance is not None
        
        # GPU 상태 확인
        gpu_info = get_gpu_info()
        
        return jsonify({
            "status": "healthy",
            "model_loaded": model_loaded,
            "current_device": current_device or "not initialized",
            "cuda": gpu_info['info']
        }), 200
        
    except Exception as e:
        return jsonify({
            "status": "unhealthy",
            "error": str(e)
        }), 500



@ocr_bp.route('/paddle/info', methods=['GET'])
def paddle_info():
    """PaddleOCR 모델 정보"""
    global current_device
    
    return jsonify({
        "model": "PP-OCRv5",
        "type": "Multilingual OCR (한국어, 일본어, 중국어, 영어 등)",
        "current_device": current_device or "not initialized",
        "features": [
            "텍스트 검출 (Text Detection)",
            "텍스트 인식 (Text Recognition)",
            "다국어 지원 (Multilingual)",
            "GPU/CPU 전환 가능",
            "OCR + 선택적 번역 통합 (use_translate 파라미터)",
            "인접 텍스트 자동 병합 (HunyuanOCR 전략)",
            "자동 언어 감지 (ko/ja/en)"
        ],
        "api": {
            "ocr": {
                "endpoint": "/paddle/ocr",
                "method": "POST",
                "parameters": {
                    "image": "이미지 파일 (필수)",
                    "use_translate": "번역 활성화 여부 (true/false, 기본: false)",
                    "origin_lang": "OCR 원본 언어 (ko, ja, en, zh, fr, de 등, 기본: ko)",
                    "origin_lang_auto_detect": "자동 언어 감지 (true/false, 기본: false)",
                    "target_lang": "번역 목표 언어 (ko, ja, en 등, use_translate=true일 때만 사용)",
                    "is_formality": "존칭 번역 여부 (true/false, 기본: false, use_translate=true일 때만 사용)",
                    "is_sentence": "문장 단위 여부 (true/false, 기본: true, false시 병합 비활성화)",
                    "merge_threshold": "텍스트 병합 임계값 픽셀 (정수, 기본: is_sentence=true시 15, false시 0)",
                    "is_debug": "디버그 로깅 (true/false, 기본: true)",
                    "save_result": "결과 JSON 저장 (true/false, 기본: true)",
                    "save_image": "결과 이미지 저장 (true/false, 기본: true)"
                },
                "description": "통합 OCR 엔드포인트 (use_translate=false: OCR만, use_translate=true: OCR + 번역)"
            },
            "reload": {
                "endpoint": "/paddle/reload",
                "method": "POST",
                "parameters": {
                    "device": "gpu, cpu, 또는 auto (선택)"
                },
                "description": "모델 리로드 및 GPU/CPU 전환"
            },
            "health": {
                "endpoint": "/paddle/health",
                "method": "GET",
                "description": "서버 상태 확인"
            }
        },
        "response_format": {
            "texts": "검출된 텍스트 리스트 (use_translate=true인 경우 번역됨)",
            "texts_origin": "원본 텍스트 리스트 (use_translate=true인 경우에만 포함)",
            "scores": "신뢰도 리스트 (0~1)",
            "boxes": "바운딩 박스 [x1, y1, x2, y2]",
            "quad_boxes": "4점 좌표 [x1,y1,x2,y2,x3,y3,x4,y4]",
            "ocr_with_region": "Florence 호환 형식",
            "ocr_with_region_origin": "원본 텍스트의 Florence 호환 형식 (use_translate=true인 경우에만 포함)",
            "ocr_with_region_parsed": "파싱된 텍스트",
            "translation_stats": "번역 통계 (use_translate=true인 경우에만 포함)"
        },
        "usage": {
            "ocr_only": "curl -X POST -F 'image=@test.jpg' -F 'origin_lang=ja' http://localhost:5001/paddle/ocr",
            "ocr_with_translate": "curl -X POST -F 'image=@test.jpg' -F 'use_translate=true' -F 'origin_lang=ja' -F 'target_lang=ko' http://localhost:5001/paddle/ocr",
            "ocr_translate_formality": "curl -X POST -F 'image=@test.jpg' -F 'use_translate=true' -F 'origin_lang=ja' -F 'target_lang=ko' -F 'is_formality=true' http://localhost:5001/paddle/ocr",
            "ocr_with_auto_detect": "curl -X POST -F 'image=@test.jpg' -F 'origin_lang_auto_detect=true' http://localhost:5001/paddle/ocr",
            "ocr_with_save": "curl -X POST -F 'image=@test.jpg' -F 'save_result=true' http://localhost:5001/paddle/ocr",
            "reload_gpu": "curl -X POST -F 'device=gpu' http://localhost:5001/paddle/reload",
            "reload_cpu": "curl -X POST -F 'device=cpu' http://localhost:5001/paddle/reload"
        }
    }), 200

