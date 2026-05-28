"""
PaddleOCR 핵심 모듈
PP-OCRv5 다국어 OCR 클래스

주요 기능:
- 이미지에서 텍스트 추출 (OCR)
- 텍스트 위치 정보 (bounding box) 반환
- GPU/CPU 선택 가능
- 배치 처리 지원
- 로컬 모델 경로 지정 가능 (환경변수 통한 제어)

사용법:
    from paddleOCR import PPOCRv5, check_cuda_available
    
    # 기본 사용 (기본 캐시 경로)
    ocr = PPOCRv5(use_gpu=True)
    
    # 로컬 모델 경로 지정
    ocr = PPOCRv5(use_gpu=True, model_dir="./model", use_local_model=True)
    
    result = ocr.predict("image.jpg")
    formatted = ocr.format_result(result)
"""

# ============================================================
# 환경변수 설정 (모든 import보다 먼저!)
# PaddleOCR/PaddleX는 import 시점에 캐시 경로를 결정하므로
# 반드시 import 전에 환경변수 설정 필요
# ============================================================
import os

# ============================================================
# 설정 스위치 (유지보수용)
# ============================================================
IS_LOCAL_MODEL = True # 모델 경로: True=로컬 ./model, False=기본 캐시 (~/.paddlex)
IS_GPU = True # GPU 사용: True=GPU, False=CPU

# 경로 설정
_LOCAL_MODEL_DIR = os.path.abspath("./model")
_DEFAULT_CACHE_DIR = os.path.expanduser("~/.paddlex/official_models")

def _init_model_environment():
    """모듈 import 전 환경변수 초기 설정"""
    if IS_LOCAL_MODEL:
        model_dir = _LOCAL_MODEL_DIR
        os.makedirs(model_dir, exist_ok=True)
        
        # PaddleX/PaddleOCR 캐시 환경변수 설정
        os.environ['PADDLE_PDX_CACHE_HOME'] = model_dir
        os.environ['PADDLEOCR_HOME'] = model_dir
        os.environ['PADDLEX_HOME'] = model_dir
    else:
        # 기본 캐시 경로 사용 (환경변수 건드리지 않음)
        model_dir = _DEFAULT_CACHE_DIR
    
    return model_dir

# 모듈 로드 시점에 환경변수 설정!
_ACTIVE_MODEL_DIR = _init_model_environment()

# ============================================================
# 이제 다른 모듈 import
# ============================================================
import json
import time
from pathlib import Path
from PIL import Image, ImageDraw
from util_translator_filter import count_language_chars


def get_model_dir():
    """현재 설정된 모델 디렉토리 반환"""
    return _ACTIVE_MODEL_DIR


def is_using_local_model():
    """로컬 모델 사용 여부 반환"""
    return IS_LOCAL_MODEL


def check_cuda_available():
    """
    CUDA 사용 가능 여부 확인
    
    Returns:
        bool: CUDA 사용 가능 여부
    """
    try:
        import paddle
        return paddle.device.is_compiled_with_cuda()
    except:
        return False


def get_gpu_info():
    """
    GPU 정보 반환
    
    Returns:
        dict: GPU 정보
    """
    cuda_available = check_cuda_available()
    if not cuda_available:
        return {"available": False, "count": 0, "info": "CUDA not available"}
    
    try:
        import paddle
        gpu_count = paddle.device.cuda.device_count()
        return {
            "available": True,
            "count": gpu_count,
            "info": f"{gpu_count} GPU(s) available"
        }
    except Exception as e:
        return {"available": True, "count": 0, "info": str(e)}


# ============================================================
# OCR 동적 설정
# ============================================================

# 지원 언어 목록 (lang 파라미터 값)
SUPPORTED_LANGS = {
    'ko': 'korean',      # 한국어 → korean_PP-OCRv5_mobile_rec
    'ja': 'japan',       # 일본어 → PP-OCRv5_server_rec
    'en': 'en',          # 영어 → en_PP-OCRv5_mobile_rec
    'zh': 'ch',          # 중국어 간체 → PP-OCRv5_server_rec
    'zh-tw': 'chinese_cht',  # 중국어 번체 → PP-OCRv5_server_rec
    'fr': 'fr',          # 프랑스어 → latin_PP-OCRv5_mobile_rec
    'de': 'de',          # 독일어 → latin_PP-OCRv5_mobile_rec
}

# 문장/단어 모드별 Detection 설정
DETECTION_CONFIGS = {
    True: {   # is_sentence=True (문장 단위)
        'det_db_unclip_ratio': 2.5,
        'det_db_box_thresh': 0.5,
    },
    False: {  # is_sentence=False (단어 단위)
        'det_db_unclip_ratio': 1.5,
        'det_db_box_thresh': 0.6,
    }
}

# 기본 설정
DEFAULT_OCR_CONFIG = {
    'origin_lang': 'ko',      # 원본 언어 (ko, ja, en, zh, ...)
    'is_sentence': True,      # True: 문장 단위, False: 단어 단위
}


class PPOCRv5:
    """
    PP-OCRv5 다국어 OCR 클래스
    
    지원 언어: 한국어, 일본어, 중국어(간체/번체), 영어, 프랑스어, 독일어 등
    동적 설정 변경 지원: origin_lang, is_sentence
    """
    
    def __init__(self, use_gpu=None, device=None, model_dir=None, 
                 origin_lang='ko', is_sentence=True):
        """
        PP-OCRv5 초기화
        
        Args:
            use_gpu: GPU 사용 여부 (기본: None → IS_GPU 스위치 사용)
            device: 디바이스 지정 ("gpu:0", "gpu:1", "cpu", "auto", None)
            model_dir: 모델 저장 경로 (기본: None → 모듈 레벨 설정 사용)
            origin_lang: 원본 언어 ('ko', 'ja', 'en', 'zh', 'fr', 'de')
            is_sentence: True=문장 단위, False=단어 단위
        """
        # use_gpu가 None이면 IS_GPU 스위치 사용
        if use_gpu is None:
            use_gpu = IS_GPU
        
        # 모델 경로 확인
        self.model_dir = model_dir if model_dir else get_model_dir()
        os.makedirs(self.model_dir, exist_ok=True)
        
        mode_str = "로컬" if IS_LOCAL_MODEL else "기본 캐시"
        gpu_str = "GPU" if use_gpu else "CPU"
        print(f"[CONFIG] 모델 모드: {mode_str}")
        print(f"[CONFIG] 디바이스 모드: {gpu_str} (IS_GPU={IS_GPU})")
        print(f"[CONFIG] 모델 경로: {self.model_dir}")
        if IS_LOCAL_MODEL:
            print(f"  PADDLE_PDX_CACHE_HOME = {os.environ.get('PADDLE_PDX_CACHE_HOME')}")
            print(f"  PADDLEOCR_HOME = {os.environ.get('PADDLEOCR_HOME')}")
            print(f"  PADDLEX_HOME = {os.environ.get('PADDLEX_HOME')}")
        
        # 디바이스 결정
        self.device = self._resolve_device(use_gpu, device)
        self.device_type = "gpu" if "gpu" in self.device else "cpu"
        
        # 현재 설정 저장
        self.current_config = {
            'origin_lang': origin_lang,
            'is_sentence': is_sentence,
        }
        
        # OCR 초기화
        self.ocr = None
        self._init_ocr()
    
    def _init_ocr(self):
        """현재 설정으로 PaddleOCR 초기화"""
        from paddleocr import PaddleOCR
        
        origin_lang = self.current_config['origin_lang']
        is_sentence = self.current_config['is_sentence']
        
        # lang 파라미터 결정
        lang = SUPPORTED_LANGS.get(origin_lang, 'korean')
        
        # Detection 설정
        det_cfg = DETECTION_CONFIGS.get(is_sentence, DETECTION_CONFIGS[True])
        
        print(f"[INIT] PPOCRv5 초기화 중...")
        print(f"[INIT] Device: {self.device}")
        print(f"[INIT] Lang: {lang} (origin_lang={origin_lang})")
        print(f"[INIT] Mode: {'문장 단위' if is_sentence else '단어 단위'}")
        print(f"[INIT] Detection: unclip_ratio={det_cfg['det_db_unclip_ratio']}, box_thresh={det_cfg['det_db_box_thresh']}")
        
        # PaddleOCR 초기화
        self.ocr = PaddleOCR(
            lang=lang,
            use_doc_orientation_classify=False,
            use_doc_unwarping=False,
            use_textline_orientation=False,
            det_db_unclip_ratio=det_cfg['det_db_unclip_ratio'],
            det_db_box_thresh=det_cfg['det_db_box_thresh'],
            device=self.device,
        )
        
        print(f"[OK] PPOCRv5 초기화 완료!")
    
    def update_config(self, origin_lang=None, is_sentence=None):
        """
        OCR 설정 업데이트 (변경 시 재초기화)
        
        Args:
            origin_lang: 원본 언어 ('ko', 'ja', 'en', 'zh', 'fr', 'de')
            is_sentence: True=문장 단위, False=단어 단위
        
        Returns:
            bool: 설정이 변경되어 재초기화되었으면 True
        """
        changed = False
        
        if origin_lang is not None and origin_lang != self.current_config['origin_lang']:
            if origin_lang not in SUPPORTED_LANGS:
                print(f"[WARN] 지원하지 않는 언어: {origin_lang}, 기본값(ko) 사용")
                origin_lang = 'ko'
            self.current_config['origin_lang'] = origin_lang
            changed = True
        
        if is_sentence is not None and is_sentence != self.current_config['is_sentence']:
            self.current_config['is_sentence'] = is_sentence
            changed = True
        
        if changed:
            print(f"[CONFIG] OCR 설정 변경됨: {self.current_config}")
            self._init_ocr()
            return True
        
        return False
    
    def get_config(self):
        """현재 OCR 설정 반환"""
        return self.current_config.copy()
    
    def _resolve_device(self, use_gpu, device):
        """디바이스 문자열 결정"""
        if device is not None:
            device = device.lower()
            if device == "auto":
                cuda_available = check_cuda_available()
                return "gpu:0" if cuda_available else "cpu"
            elif device in ["gpu", "gpu:0", "gpu:1"]:
                if not check_cuda_available():
                    print("[WARN] CUDA 사용 불가, CPU로 전환")
                    return "cpu"
                return device if ":" in device else "gpu:0"
            else:
                return "cpu"
        else:
            # use_gpu 값에 따라 결정
            if use_gpu and check_cuda_available():
                return "gpu:0"
            return "cpu"
    
    def predict(self, image_path):
        """
        이미지 OCR 실행
        
        Args:
            image_path: 이미지 파일 경로
        
        Returns:
            list: OCR 결과 리스트
        """
        return self.ocr.predict(image_path)
    
    def extract_text_with_positions(self, image_path):
        """
        텍스트와 위치 정보를 구조화된 형태로 추출
        
        Args:
            image_path: 이미지 경로
            
        Returns:
            list: [{text, confidence, bbox, polygon}, ...]
        """
        result = self.predict(image_path)
        
        extracted_data = []
        for res in result:
            rec_texts = res.get('rec_texts', []) if isinstance(res, dict) else getattr(res, 'rec_texts', [])
            rec_scores = res.get('rec_scores', []) if isinstance(res, dict) else getattr(res, 'rec_scores', [])
            dt_polys = res.get('dt_polys', []) if isinstance(res, dict) else getattr(res, 'dt_polys', [])
            
            for text, score, poly in zip(rec_texts, rec_scores, dt_polys):
                x_coords = [p[0] for p in poly]
                y_coords = [p[1] for p in poly]
                
                item = {
                    'text': text,
                    'confidence': float(score),
                    'bbox': {
                        'xmin': int(min(x_coords)),
                        'ymin': int(min(y_coords)),
                        'xmax': int(max(x_coords)),
                        'ymax': int(max(y_coords))
                    },
                    'polygon': [[int(p[0]), int(p[1])] for p in poly]
                }
                extracted_data.append(item)
        
        return extracted_data

    def merge_nearby_texts(self, formatted_result, threshold=30):
        """
        인접한 텍스트 박스를 병합 (HunyuanOCR 전략 적용)
        
        Args:
            formatted_result: format_result()의 결과
            threshold: 병합 거리 임계값 (픽셀)
        
        Returns:
            dict: 병합된 결과 (texts, scores, boxes, quad_boxes, labels)
        """
        texts = formatted_result.get('texts', [])
        scores = formatted_result.get('scores', [])
        boxes = formatted_result.get('boxes', [])
        quad_boxes = formatted_result.get('quad_boxes', [])
        
        if not texts:
            return formatted_result
        
        # 1. 박스 데이터 구조화
        detections = []
        for i, (text, score, box, quad) in enumerate(zip(texts, scores, boxes, quad_boxes)):
            detections.append({
                'index': i,
                'text': text,
                'score': score,
                'x1': box[0],
                'y1': box[1],
                'x2': box[2],
                'y2': box[3],
                'box': box,
                'quad_box': quad
            })
        
        # 2. 인접성 판단 함수
        def are_close(box1, box2, thresh):
            """두 박스가 인접한지 확인 (확장된 박스 기준)"""
            b1_x1 = box1['x1'] - thresh
            b1_y1 = box1['y1'] - thresh
            b1_x2 = box1['x2'] + thresh
            b1_y2 = box1['y2'] + thresh
            
            # 겹침 확인
            return not (b1_x2 < box2['x1'] or b1_x1 > box2['x2'] or
                       b1_y2 < box2['y1'] or b1_y1 > box2['y2'])
        
        # 3. 인접 리스트 구축
        n = len(detections)
        adj = [[] for _ in range(n)]
        for i in range(n):
            for j in range(i + 1, n):
                if are_close(detections[i], detections[j], threshold):
                    adj[i].append(j)
                    adj[j].append(i)
        
        # 4. 연결 요소 찾기 (BFS)
        visited = [False] * n
        merged_results = []
        
        for i in range(n):
            if not visited[i]:
                # BFS로 연결된 모든 박스 찾기
                component = []
                stack = [i]
                visited[i] = True
                
                while stack:
                    curr = stack.pop()
                    component.append(detections[curr])
                    for neighbor in adj[curr]:
                        if not visited[neighbor]:
                            visited[neighbor] = True
                            stack.append(neighbor)
                
                # 5. 컴포넌트 병합
                if not component:
                    continue
                
                # 병합된 바운딩 박스 계산
                min_x1 = min(d['x1'] for d in component)
                min_y1 = min(d['y1'] for d in component)
                max_x2 = max(d['x2'] for d in component)
                max_y2 = max(d['y2'] for d in component)
                
                # 텍스트 정렬: 오른쪽→왼쪽, 위→아래 (만화 읽기 순서)
                component.sort(key=lambda d: (-d['x1'], d['y1']))
                
                # 텍스트 결합 (공백 제거)
                merged_text = "".join(d['text'] for d in component).replace(" ", "")
                
                # 평균 신뢰도 계산
                avg_score = sum(d['score'] for d in component) / len(component)
                
                # 병합된 quad_box 계산 (외접 사각형)
                merged_quad = [
                    min_x1, min_y1,  # 좌상
                    max_x2, min_y1,  # 우상
                    max_x2, max_y2,  # 우하
                    min_x1, max_y2   # 좌하
                ]
                
                merged_results.append({
                    'text': merged_text,
                    'score': avg_score,
                    'box': [min_x1, min_y1, max_x2, max_y2],
                    'quad_box': merged_quad,
                    'component_count': len(component)
                })
        
        # 6. 포맷팅된 결과로 변환
        merged_formatted = {
            'texts': [r['text'] for r in merged_results],
            'scores': [r['score'] for r in merged_results],
            'boxes': [r['box'] for r in merged_results],
            'quad_boxes': [r['quad_box'] for r in merged_results],
            'labels': [r['text'] for r in merged_results],
            'merged_count': sum(r['component_count'] for r in merged_results),
            'original_count': len(texts)
        }
        
        return merged_formatted

    def filter_formatted_texts(self, formatted_result, target_lang="ko", skip_language_filter=False):
        """
        util_translator_filter 기반 텍스트 필터링
        
        - skip_language_filter=False (기본): should_translate 기준으로 번역 대상만 남기고 나머지 제거
        - skip_language_filter=True: 쓰레기 텍스트만 제거 (언어 기반 필터 제외)
        - boxes/quad_boxes/scores를 동일 인덱스로 슬라이싱
        
        Args:
            formatted_result: format_result() 결과
            target_lang: 번역 목표 언어 (필터 기준, skip_language_filter=False일 때만 사용)
            skip_language_filter: True이면 언어 기반 필터링 생략 (OCR만 수행 시)
        
        Returns:
            tuple(filtered_result, filtered_out_count, raw_count)
        """
        texts = formatted_result.get('texts', [])
        scores = formatted_result.get('scores', [])
        boxes = formatted_result.get('boxes', [])
        quad_boxes = formatted_result.get('quad_boxes', [])

        raw_count = len(texts)
        if raw_count == 0:
            return formatted_result, 0, raw_count

        try:
            if skip_language_filter:
                from util_translator_filter import filter_garbage_only
                filter_results = filter_garbage_only(texts, include_reason=False)
                keep_indices = [r["index"] for r in filter_results if r.get("should_keep")]
                filter_mode = "쓰레기만 제거"
            else:
                from util_translator_filter import filter_translation_texts
                filter_results = filter_translation_texts(texts, target_lang=target_lang, include_reason=False)
                keep_indices = [r["index"] for r in filter_results if r.get("needs_translation")]
                filter_mode = "번역 대상만 유지"
        except Exception as e:
            print(f"[WARN] 필터 모듈 불러오기 실패, 필터링 생략: {e}")
            return formatted_result, 0, raw_count

        if len(keep_indices) == raw_count:
            # 모두 유지
            return formatted_result, 0, raw_count

        def pick(lst):
            return [lst[i] for i in keep_indices] if lst else []

        filtered = {
            "texts": pick(texts),
            "scores": pick(scores),
            "boxes": pick(boxes),
            "quad_boxes": pick(quad_boxes),
            "labels": pick(texts),
        }

        filtered_out = raw_count - len(filtered["texts"])
        print(f"[FILTER] {filter_mode}: {raw_count}개 → {len(filtered['texts'])}개 (제거 {filtered_out}개)")

        return filtered, filtered_out, raw_count

    def _calculate_language_ratios(self, texts):
        """
        OCR 텍스트를 기반으로 언어 문자 비율 계산
        """
        counts = count_language_chars(texts)
        total_chars = counts.get("total_chars", 0)
        korean_chars = counts.get("korean_chars", 0)
        japanese_chars = counts.get("japanese_chars", 0)

        ratios = {
            "total_chars": total_chars,
            "ko_ratio": korean_chars / total_chars if total_chars else 0.0,
            "ja_ratio": japanese_chars / total_chars if total_chars else 0.0
        }
        ratios["other_ratio"] = max(0.0, 1.0 - (ratios["ko_ratio"] + ratios["ja_ratio"]))
        return ratios

    def detect_language_by_ocr_score(self, image_path, threshold=0.6, is_sentence=True):
        """
        이미지에서 언어(ko/ja/en)를 순차적으로 감지
        """
        language_sequence = [
            ("ko", "korean_PP-OCRv5_mobile_rec"),
            ("ja", "PP-OCRv5_server_rec"),
            ("en", "en_PP-OCRv5_mobile_rec"),
        ]

        detection_stats = {}

        for lang, _ in language_sequence:
            temp_ocr = PPOCRv5(origin_lang=lang, is_sentence=is_sentence, use_gpu=IS_GPU)
            result = temp_ocr.predict(image_path)
            formatted = temp_ocr.format_result(result)
            ratios = self._calculate_language_ratios(formatted.get("texts", []))
            detection_stats[lang] = ratios

            if lang != "en" and ratios.get(f"{lang}_ratio", 0.0) >= threshold:
                return lang, detection_stats

        return "en", detection_stats

    def format_result(self, result):
        """
        OCR 결과를 포맷팅 (Flask API 호환)
        
        Args:
            result: predict() 결과
        
        Returns:
            dict: 포맷팅된 결과
        """
        formatted = {
            'texts': [],
            'scores': [],
            'boxes': [],
            'quad_boxes': [],
            'labels': []  # Florence 호환용
        }
        
        if not result:
            return formatted
        
        for res in result:
            if isinstance(res, dict):
                rec_texts = res.get('rec_texts', [])
                rec_scores = res.get('rec_scores', [])
                dt_polys = res.get('dt_polys', [])
            else:
                rec_texts = getattr(res, 'rec_texts', [])
                rec_scores = getattr(res, 'rec_scores', [])
                dt_polys = getattr(res, 'dt_polys', [])
            
            formatted['texts'] = rec_texts
            formatted['scores'] = [float(s) for s in rec_scores]
            formatted['labels'] = rec_texts  # Florence 호환
            
            # Bounding box 처리
            for poly in dt_polys:
                if len(poly) >= 4:
                    # 4점 좌표 → [x1, y1, x2, y2, x3, y3, x4, y4]
                    quad = []
                    for point in poly[:4]:
                        quad.extend([int(point[0]), int(point[1])])
                    formatted['quad_boxes'].append(quad)
                    
                    # 4점 좌표 → [x_min, y_min, x_max, y_max]
                    xs = [int(p[0]) for p in poly]
                    ys = [int(p[1]) for p in poly]
                    formatted['boxes'].append([min(xs), min(ys), max(xs), max(ys)])
        
        return formatted
    
    def format_parsed(self, formatted_result):
        """
        OCR 결과를 파싱된 문자열로 변환 (Florence 호환)
        
        Args:
            formatted_result: format_result()의 결과
        
        Returns:
            str: 파싱된 문자열
        """
        lines = []
        
        texts = formatted_result.get('texts', [])
        boxes = formatted_result.get('boxes', [])
        scores = formatted_result.get('scores', [])
        
        for i, (text, box, score) in enumerate(zip(texts, boxes, scores), 1):
            if text.strip():
                lines.append(f"{i}. [{box[0]}, {box[1]}, {box[2]}, {box[3]}] : {text} ({score:.2f})")
        
        return '\n'.join(lines)
    
    def process_image(self, image_path, save_dir=None, merge_threshold=None, target_lang="ko", skip_language_filter=False, is_sentence=None):
        """
        이미지 처리 (OCR + 결과 저장)
        
        Args:
            image_path: 이미지 경로
            save_dir: 결과 저장 디렉토리 (None이면 저장 안함)
            merge_threshold: 텍스트 병합 임계값 (None이면 is_sentence 설정에 따라 자동 결정)
            target_lang: 필터 기준이 되는 번역 목표 언어 (skip_language_filter=False일 때만 사용)
            skip_language_filter: True이면 언어 기반 필터링 생략 (OCR만 수행 시)
            is_sentence: 문장 단위 여부 (None이면 config 사용, False면 병합 비활성화)
        
        Returns:
            dict: {result, formatted, parsed, elapsed_time, text_count, merged, raw_count, filtered_count}
        """
        start_time = time.time()
        
        # OCR 실행
        result = self.predict(image_path)
        
        # 결과 포맷팅
        formatted_raw = self.format_result(result)
        raw_count = len(formatted_raw['texts'])

        # 1) 필터링
        # - skip_language_filter=True: 쓰레기만 제거 (언어 무관)
        # - skip_language_filter=False: 번역 대상만 유지 (언어 기반)
        formatted_filtered, filtered_out, _ = self.filter_formatted_texts(
            formatted_raw, target_lang=target_lang or "ko", skip_language_filter=skip_language_filter
        )
        filtered_count = len(formatted_filtered['texts'])

        # 2) 텍스트 병합 (is_sentence=True일 때만)
        formatted = formatted_filtered
        merged = False
        pre_merge_count = len(formatted['texts'])
        
        # is_sentence 결정 (파라미터 우선, 없으면 config 사용)
        if is_sentence is None:
            is_sentence = self.current_config['is_sentence']
        
        # merge_threshold 결정
        if merge_threshold is None and is_sentence:
            # is_sentence=True일 때 기본값 15 적용
            merge_threshold = 15
        
        # 병합 수행: is_sentence=True이고 merge_threshold가 설정된 경우 (0 이상 허용)
        if is_sentence and merge_threshold is not None and merge_threshold >= 0 and pre_merge_count > 0:
            print(f"[MERGE] 텍스트 병합 시작 (threshold={merge_threshold}px)...")
            formatted = self.merge_nearby_texts(formatted, threshold=merge_threshold)
            merged = True
            merged_count = len(formatted['texts'])
            if pre_merge_count > 0:
                merge_rate = (1 - merged_count / pre_merge_count) * 100
            else:
                merge_rate = 0.0
            print(f"[MERGE] 완료: {pre_merge_count}개 → {merged_count}개 (병합율: {merge_rate:.1f}%)")
        
        parsed = self.format_parsed(formatted)
        elapsed_time = time.time() - start_time
        
        # 병합/필터 결과 시각화 저장 (단순 박스+텍스트)
        if save_dir:
            try:
                img = Image.open(image_path).convert("RGB")
                draw = ImageDraw.Draw(img)
                boxes = formatted.get("boxes", [])
                texts = formatted.get("texts", [])
                for b, t in zip(boxes, texts):
                    x1, y1, x2, y2 = b
                    draw.rectangle([x1, y1, x2, y2], outline=(255, 0, 0), width=2)
                    draw.text((x1 + 2, y1 + 2), t, fill=(255, 0, 0))
                vis_path = os.path.join(save_dir, "merged_vis.png")
                img.save(vis_path)
                print(f"[SAVE] 병합 시각화 저장: {vis_path}")
            except Exception as e:
                print(f"[WARN] 병합 시각화 저장 실패: {e}")

            # 필터+병합 결과 JSON 별도 저장
            try:
                merged_json = {
                    "raw_count": raw_count,
                    "filtered_count": filtered_count,
                    "pre_merge_count": pre_merge_count,
                    "final_count": len(formatted.get("texts", [])),
                    "merge_threshold": merge_threshold,
                    "target_lang": target_lang,
                    "texts": formatted.get("texts", []),
                    "scores": formatted.get("scores", []),
                    "boxes": formatted.get("boxes", []),
                    "quad_boxes": formatted.get("quad_boxes", []),
                }
                json_path = os.path.join(save_dir, "merged_res.json")
                with open(json_path, "w", encoding="utf-8") as f:
                    json.dump(merged_json, f, ensure_ascii=False, indent=2)
                print(f"[SAVE] 병합 결과 JSON 저장: {json_path}")
            except Exception as e:
                print(f"[WARN] 병합 결과 JSON 저장 실패: {e}")

        
        return {
            'result': result,
            'formatted': formatted,
            'formatted_filtered': formatted_filtered,
            'formatted_raw': formatted_raw,
            'parsed': parsed,
            'elapsed_time': elapsed_time,
            'text_count': len(formatted['texts']),
            'merged': merged,
            'merge_threshold': merge_threshold,
            'raw_count': raw_count,
            'filtered_count': filtered_count,
            'filtered_out': filtered_out,
            'pre_merge_count': pre_merge_count
        }
    
    def save_ocr_result_log(self, result, image_path, log_dir, save_result=True, save_image=True):
        """PaddleOCR 내부 결과 파일 저장"""
        import shutil
        os.makedirs(log_dir, exist_ok=True)
        
        saved_files = {"json": [], "img": []}
        
        # 이미지 파일명 추출 (PaddleOCR는 {이미지명}_res.json 형태로 저장)
        basename = os.path.splitext(os.path.basename(image_path))[0]
        
        # 임시 디렉토리에 저장 후 리네임
        temp_save_dir = os.path.join(log_dir, "_temp_paddle")
        os.makedirs(temp_save_dir, exist_ok=True)
        
        try:
            for res in result:
                # JSON 파일 저장 (save_result=True일 때만)
                if save_result and hasattr(res, 'save_to_json'):
                    res.save_to_json(temp_save_dir)
                    old_json = os.path.join(temp_save_dir, f"{basename}_res.json")
                    new_json = os.path.join(log_dir, "res.json")
                    if os.path.exists(old_json):
                        shutil.move(old_json, new_json)
                        saved_files["json"].append(new_json)
                        print(f"[LOG] Saved: {new_json}")
                
                # 이미지 파일 저장 (save_image=True일 때만)
                if save_image and hasattr(res, 'save_to_img'):
                    res.save_to_img(temp_save_dir)
                    old_img = os.path.join(temp_save_dir, f"{basename}_ocr_res_img.PNG")
                    new_img = os.path.join(log_dir, "ocr_res_img.PNG")
                    if os.path.exists(old_img):
                        shutil.move(old_img, new_img)
                        saved_files["img"].append(new_img)
                        print(f"[LOG] Saved: {new_img}")
        finally:
            # 임시 디렉토리 정리
            try:
                if os.path.exists(temp_save_dir):
                    shutil.rmtree(temp_save_dir)
            except Exception as e:
                print(f"[WARN] Failed to cleanup temp directory: {e}")
        
        return saved_files

    
    def batch_process(self, image_paths, save_base_dir=None, verbose=True):
        """
        배치 이미지 처리
        
        Args:
            image_paths: 이미지 경로 리스트
            save_base_dir: 결과 저장 기본 디렉토리
            verbose: 진행 상황 출력 여부
        
        Returns:
            list: 처리 결과 리스트
        """
        results = []
        total_time = 0
        
        for idx, img_path in enumerate(image_paths, 1):
            if verbose:
                print(f"\n[{idx}/{len(image_paths)}] 처리 중: {os.path.basename(img_path)}")
            
            save_dir = None
            if save_base_dir:
                save_dir = os.path.join(save_base_dir, f"image_{idx:03d}")
            
            try:
                output = self.process_image(img_path, save_dir)
                output['image_path'] = img_path
                output['index'] = idx
                output['status'] = 'success'
                results.append(output)
                total_time += output['elapsed_time']
                
                if verbose:
                    print(f"    텍스트 {output['text_count']}개 검출, 처리시간: {output['elapsed_time']:.3f}s")
                    
            except Exception as e:
                results.append({
                    'image_path': img_path,
                    'index': idx,
                    'status': 'failed',
                    'error': str(e)
                })
                if verbose:
                    print(f"    [ERROR] 처리 실패: {e}")
        
        if verbose:
            success_count = sum(1 for r in results if r.get('status') == 'success')
            print(f"\n[완료] {success_count}/{len(image_paths)}개 성공, 총 시간: {total_time:.2f}s")
        
        return results


# ============================================================
# 테스트용 메인 함수
# ============================================================

def main():
    """메인 테스트 함수"""
    
    # 설정 (IS_GPU, IS_LOCAL_MODEL 스위치 사용)
    CONFIG = {
        'convert_to_png': True,
        'max_size': 1024,
        'image_test_dir': './test/image_test',
        'output_dir': './output'
    }
    
    print("=" * 60)
    print("PP-OCRv5 테스트")
    print("=" * 60)
    print(f"\n설정:")
    print(f"  IS_GPU: {IS_GPU}")
    print(f"  IS_LOCAL_MODEL: {IS_LOCAL_MODEL}")
    print(f"  PNG 변환: {CONFIG['convert_to_png']}")
    print(f"  최대 크기: {CONFIG['max_size']}")
    
    # GPU 정보
    gpu_info = get_gpu_info()
    print(f"  GPU 상태: {gpu_info['info']}")
    
    # OCR 초기화 (스위치 값 자동 사용)
    ocr = PPOCRv5()
    
    # 테스트 이미지 수집
    test_images = []
    image_test_dir = CONFIG['image_test_dir']
    
    if os.path.exists(image_test_dir):
        for root, dirs, files in os.walk(image_test_dir):
            for file in files:
                if file.lower().endswith(('.png', '.jpg', '.jpeg', '.webp', '.bmp')):
                    test_images.append(os.path.join(root, file))
    
    if not test_images:
        print(f"\n[WARN] 테스트할 이미지가 없습니다!")
        print(f"경로를 확인하세요: {image_test_dir}")
        return
    
    print(f"\n총 {len(test_images)}개 이미지 발견")
    
    # 이미지 전처리 (옵션)
    if CONFIG['convert_to_png']:
        try:
            from util_image import resize_image_to_png
            processed_images = []
            
            print(f"\n이미지 전처리 중... (최대 크기: {CONFIG['max_size']})")
            
            for img_path in test_images:
                try:
                    resized_img, orig_size, new_size, saved_path = resize_image_to_png(
                        img_path,
                        max_size=(CONFIG['max_size'], CONFIG['max_size']),
                        save_file=True,
                        suffix='_paddle_1024'
                    )
                    if saved_path:
                        processed_images.append(saved_path)
                    else:
                        processed_images.append(img_path)
                except Exception as e:
                    print(f"  [WARN] 이미지 변환 실패 ({img_path}): {e}")
                    processed_images.append(img_path)
            
            test_images = processed_images
            print(f"이미지 전처리 완료 ({len(test_images)}개)")
            
        except ImportError:
            print("[WARN] util_image 모듈 없음, 원본 이미지 사용")
    
    # 배치 처리
    output_dir = CONFIG['output_dir']
    results = ocr.batch_process(test_images, save_base_dir=output_dir)
    
    # 전체 결과 저장
    summary_data = {
        'config': CONFIG,
        'switches': {
            'IS_GPU': IS_GPU,
            'IS_LOCAL_MODEL': IS_LOCAL_MODEL
        },
        'device': ocr.device,
        'model_dir': ocr.model_dir,
        'total_images': len(test_images),
        'processed_images': sum(1 for r in results if r.get('status') == 'success'),
        'results': []
    }
    
    for result in results:
        if result.get('status') == 'success':
            summary_data['results'].append({
                'index': result['index'],
                'image_path': result['image_path'],
                'text_count': result['text_count'],
                'elapsed_time': result['elapsed_time'],
                'extracted_data': result['formatted']
            })
    
    summary_path = os.path.join(output_dir, 'all_images_summary.json')
    os.makedirs(output_dir, exist_ok=True)
    with open(summary_path, 'w', encoding='utf-8') as f:
        json.dump(summary_data, f, ensure_ascii=False, indent=2)
    
    print(f"\n결과 저장 완료: {summary_path}")
    print("=" * 60)


if __name__ == "__main__":
    # 환경 확인
    try:
        import paddle
        print(f"PaddlePaddle 버전: {paddle.__version__}")
    except ImportError:
        print("[ERROR] PaddlePaddle가 설치되어 있지 않습니다.")
        print("설치: pip install paddlepaddle-gpu==3.0.0")
        exit(1)
    
    try:
        import paddleocr
        print(f"PaddleOCR 설치 확인 완료")
    except ImportError:
        print("[ERROR] PaddleOCR가 설치되어 있지 않습니다.")
        print("설치: pip install paddleocr")
        exit(1)
    
    main()
