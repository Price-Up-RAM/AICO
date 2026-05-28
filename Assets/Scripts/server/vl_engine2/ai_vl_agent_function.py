'''
https://github.com/QwenLM/Qwen3-VL/blob/main/cookbooks/2d_grounding.ipynb
'''
import base64
import io
import json
import os
import re
import sys
from datetime import datetime
from pathlib import Path
import shutil

from PIL import Image, ImageDraw

import state
from ai_singleton import get_llm
from util_image import image_file_to_base64_png


DEFAULT_IMAGE_PATH = Path("./test/image/13.webp")
DEFAULT_TARGET = "dog"

MODEL_NAME = "Qwen3VL-8B-Instruct-Q4_K_M.gguf"
OUTPUT_BASE_DIR = Path("./test/test_clicker")

IMAGE_EXTS = (".png", ".jpg", ".jpeg", ".webp", ".bmp")


def ensure_dir(p):
    p.mkdir(parents=True, exist_ok=True)


def clamp_int(v, lo, hi):
    return max(lo, min(hi, v))


def build_grounding_prompt_find_all(target_label):
    target_label = (target_label or "").strip() or DEFAULT_TARGET

    return (
        "You are a visual grounding locator.\n"
        "Task: find ALL instances of the target object in the image.\n"
        f"Target object label: {target_label}\n"
        "\n"
        "Output ONLY a JSON object in this exact schema:\n"
        '{ "found": true, "target": "<label>", "bboxes_2d": [[x1,y1,x2,y2], ...] }\n'
        "If there are no target objects, output ONLY:\n"
        '{ "found": false, "target": "<label>", "bboxes_2d": [] }\n'
        "\n"
        "Coordinate rules:\n"
        "- Each bbox_2d is [x1,y1,x2,y2]\n"
        "- Values are integers in [0, 1000]\n"
        "- (0,0) is top-left, (1000,1000) is bottom-right\n"
        "\n"
        "Important:\n"
        "- Return ALL instances you can locate, not just one.\n"
        "- Do not output any extra text, markdown, code fences, or explanations.\n"
    )


def call_vl(llm, prompt, image_b64):
    last = ""
    try:
        for out in llm.generate_with_streaming(prompt=prompt, image_data=image_b64):
            last = out
    except TypeError:
        for out in llm.generate_with_streaming(prompt, image_data=image_b64):
            last = out
    return last or ""


def extract_json_object(text):
    if not text:
        return None

    cleaned = text.strip()
    cleaned = cleaned.replace("```json", "").replace("```", "").strip()

    l = cleaned.find("{")
    r = cleaned.rfind("}")
    if l == -1 or r == -1 or r <= l:
        return None

    candidate = cleaned[l : r + 1].strip()
    try:
        return json.loads(candidate)
    except Exception:
        return None


def normalize_bbox_2d(bbox):
    if not isinstance(bbox, list) or len(bbox) != 4:
        return None
    if not all(isinstance(v, (int, float)) for v in bbox):
        return None

    x1, y1, x2, y2 = [int(round(v)) for v in bbox]
    x1 = clamp_int(x1, 0, 1000)
    y1 = clamp_int(y1, 0, 1000)
    x2 = clamp_int(x2, 0, 1000)
    y2 = clamp_int(y2, 0, 1000)

    if x2 < x1:
        x1, x2 = x2, x1
    if y2 < y1:
        y1, y2 = y2, y1

    if (x2 - x1) <= 1 or (y2 - y1) <= 1:
        return None

    return [x1, y1, x2, y2]


def normalize_bboxes(data):
    bboxes = []

    raw_bboxes = data.get("bboxes_2d")
    if isinstance(raw_bboxes, list) and len(raw_bboxes) > 0:
        # 모델이 flat array [x1,y1,x2,y2]로 반환한 경우 (첫 원소가 숫자)
        if isinstance(raw_bboxes[0], (int, float)):
            bb = normalize_bbox_2d(raw_bboxes)
            if bb:
                bboxes.append(bb)
        else:
            # 정상적인 2D array [[x1,y1,x2,y2], ...]
            for item in raw_bboxes:
                bb = normalize_bbox_2d(item)
                if bb:
                    bboxes.append(bb)

    # 모델이 단일 bbox_2d로 실수하는 경우도 승격 처리
    if not bboxes and isinstance(data.get("bbox_2d"), list):
        bb = normalize_bbox_2d(data.get("bbox_2d"))
        if bb:
            bboxes.append(bb)

    def area(bb):
        return max(0, bb[2] - bb[0]) * max(0, bb[3] - bb[1])

    bboxes.sort(key=area, reverse=True)
    print(f"[normalize_bboxes]bboxes: {bboxes}")
    return bboxes


def bbox1000_to_pixel(bbox_2d, w, h):
    x1_1000, y1_1000, x2_1000, y2_1000 = bbox_2d

    x1 = int(round(x1_1000 * w / 1000.0))
    y1 = int(round(y1_1000 * h / 1000.0))
    x2 = int(round(x2_1000 * w / 1000.0))
    y2 = int(round(y2_1000 * h / 1000.0))

    x1 = clamp_int(x1, 0, w - 1)
    y1 = clamp_int(y1, 0, h - 1)
    x2 = clamp_int(x2, 0, w - 1)
    y2 = clamp_int(y2, 0, h - 1)

    if x2 < x1:
        x1, x2 = x2, x1
    if y2 < y1:
        y1, y2 = y2, y1

    if x2 == x1:
        x2 = clamp_int(x2 + 1, 0, w - 1)
    if y2 == y1:
        y2 = clamp_int(y2 + 1, 0, h - 1)

    return [x1, y1, x2, y2]


def bbox_pixel_center(bbox_px):
    x1, y1, x2, y2 = bbox_px
    cx = int(round((x1 + x2) / 2.0))
    cy = int(round((y1 + y2) / 2.0))
    return cx, cy


def draw_boxes_and_save(image_path, bboxes_2d, out_annotated_png):
    img = Image.open(str(image_path))
    if img.mode != "RGB":
        img = img.convert("RGB")

    w, h = img.size
    draw = ImageDraw.Draw(img)

    pixel_boxes = []

    for i, bb_1000 in enumerate(bboxes_2d, 1):
        bb_px = bbox1000_to_pixel(bb_1000, w, h)
        cx, cy = bbox_pixel_center(bb_px)

        x1, y1, x2, y2 = bb_px
        draw.rectangle([x1, y1, x2, y2], outline=(255, 0, 0), width=3)

        r = 6
        draw.line([cx - r, cy, cx + r, cy], fill=(0, 255, 0), width=2)
        draw.line([cx, cy - r, cx, cy + r], fill=(0, 255, 0), width=2)

        draw.text((x1 + 3, max(0, y1 - 14)), str(i), fill=(255, 0, 0))

        pixel_boxes.append(
            {
                "idx": i,
                "bbox_2d_1000": bb_1000,
                "bbox_px": bb_px,
                "click_px": [cx, cy],
            }
        )

    ensure_dir(out_annotated_png.parent)
    img.save(str(out_annotated_png), format="PNG")

    return {
        "image_size": [w, h],
        "pixel_boxes": pixel_boxes,
    }


def write_text(path, text):
    ensure_dir(path.parent)
    path.write_text(text, encoding="utf-8")


def write_json(path, obj):
    ensure_dir(path.parent)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")


def collect_images(image_dir):
    imgs = []
    if not image_dir.exists():
        return imgs

    for root, _, files in os.walk(str(image_dir)):
        for f in files:
            if f.lower().endswith(IMAGE_EXTS):
                imgs.append(Path(root) / f)

    return sorted(imgs)

# 이미지에서 target_label에 해당하는 객체를 찾아 좌표를 반환(verbose가 True일 경우 시각화 이미지를 ./test/test_clicker에 저장)
def vl_target_find(image_path, target_label, verbose=False):
    # string이면 Path로 변환
    if isinstance(image_path, str):
        image_path = Path(image_path)
    
    target_label = (target_label or "").strip() or DEFAULT_TARGET

    if not image_path.exists():
        return {
            "type": "not_found",
            "variables": {"exists": False, "target": target_label, "x": None, "y": None, "clicks": []},
            "debug": {"reason": "file_not_found", "image_path": str(image_path)},
        }

    # state.set_use_gpu_percent(99999)
    # state.model_name = MODEL_NAME

    llm = get_llm(require_vl=True)

    image_b64, w, h = image_file_to_base64_png(image_path)
    prompt = build_grounding_prompt_find_all(target_label)
    raw = call_vl(llm, prompt, image_b64)

    data = extract_json_object(raw)

    debug = {
        "image_path": str(image_path),
        "image_size": [w, h],
        "raw_response": (raw[:2000] if raw else ""),
    }

    if not isinstance(data, dict):
        return {
            "type": "not_found",
            "variables": {"exists": False, "target": target_label, "x": None, "y": None, "clicks": []},
            "debug": {**debug, "reason": "json_parse_failed"},
        }

    bboxes_2d = normalize_bboxes(data)
    clicks = []
    for bb in bboxes_2d:
        bb_px = bbox1000_to_pixel(bb, w, h)
        cx, cy = bbox_pixel_center(bb_px)
        clicks.append({"x": cx, "y": cy})

    exists = bool(len(clicks) > 0)

    first_x = clicks[0]["x"] if clicks else None
    first_y = clicks[0]["y"] if clicks else None
    
    # verbose=True일 때 시각화 이미지 저장 (성공/실패 모두)
    annotated_path = None
    if verbose:
        session_ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        # 파일명으로 사용할 수 없는 문자 A로 대체
        safe_label = re.sub(r'[<>:"/\\|?*]', 'A', target_label)
        
        # 성공/실패에 따라 폴더명 다르게
        if bboxes_2d:
            out_dir = OUTPUT_BASE_DIR / f"session_{session_ts}__{safe_label}"
        else:
            out_dir = OUTPUT_BASE_DIR / f"session_{session_ts}__{safe_label}_Failed"
        ensure_dir(out_dir)
        
        try:
            # raw_response 항상 저장 (실패 시 디버깅용)
            write_text(out_dir / "raw_response.txt", raw if isinstance(raw, str) else "")
            
            # prompt도 저장
            prompt = build_grounding_prompt_find_all(target_label)
            write_text(out_dir / "prompt.txt", prompt)
            
            # 원본 이미지 복사
            shutil.copy(str(image_path), str(out_dir / f"input{image_path.suffix}"))
            
            # annotated 이미지 저장 (bboxes_2d가 있을 때만 박스 그림)
            annotated_path = out_dir / "annotated.png"
            pixel_info = draw_boxes_and_save(image_path, bboxes_2d, annotated_path)
            write_json(out_dir / "pixel_boxes.json", pixel_info)
            
            debug["annotated_path"] = str(annotated_path)
            debug["verbose_out_dir"] = str(out_dir)
        except Exception as e:
            debug["verbose_error"] = str(e)

    return {
        "type": "request_click",
        "variables": {
            "exists": exists,
            "target": target_label,
            "x": first_x,
            "y": first_y,
            "clicks": clicks,
        },
        "debug": {
            **debug,
            "parsed": data,
            "bboxes_2d": bboxes_2d,
        },
    }


# VL 키워드 검색 (UI에서 키워드 리스트 중 존재하는 것 탐지)
def vl_keyword_detect(frame, keyword_list):
    '''
    frame: 이미지 경로
    keyword_list: 검색할 키워드 리스트 (예: ['返信する', 'OK', '報酬獲得'])
    
    return: {'detected': ['OK', '報酬獲得'], 'raw': str}
    '''
    if not frame:
        return {'detected': [], 'raw': 'No frame provided'}
    
    if not keyword_list:
        return {'detected': [], 'raw': 'No keywords provided'}
    
    llm = get_llm(require_vl=True)
    image_b64, w, h = image_file_to_base64_png(frame)
    
    # 단일 키워드 vs 복수 키워드에 따라 프롬프트 분기
    if len(keyword_list) == 1:
        # 단일 키워드: 더 정밀한 프롬프트 사용
        keyword = keyword_list[0]
        prompt = (
            'You are a UI text detector.\n'
            f'Task: Check if the text "{keyword}" is visible in this game UI screenshot.\n'
            'Return ONLY JSON:\n'
            '{"visible": true} if the exact text is visible\n'
            '{"visible": false} if not visible\n'
            'Do not output any extra text.\n'
        )
        
        raw = call_vl(llm, prompt, image_b64)
        obj = extract_json_object(raw)
        
        if obj and isinstance(obj, dict):
            visible = obj.get('visible')
            if visible is True:
                return {'detected': [keyword], 'raw': raw}
        
        return {'detected': [], 'raw': raw}
    
    else:
        # 복수 키워드: 기존 리스트 기반 프롬프트
        keyword_json = json.dumps(keyword_list, ensure_ascii=False)
        
        prompt = (
            'You are a UI keyword detector.\n'
            'Given a game UI screenshot, detect which keywords from the given list are visible.\n'
            'Return ONLY JSON with this schema:\n'
            '{"detected":["報酬獲得","OK"]}\n'
            'Rules:\n'
            '- Use only items from the provided list.\n'
            '- If nothing is detected, return {"detected":[]}.\n'
            '- Do not output any extra text.\n'
            'Keyword list:\n'
            f'{keyword_json}\n'
        )
        
        raw = call_vl(llm, prompt, image_b64)
        obj = extract_json_object(raw)
        
        if obj and isinstance(obj, dict) and 'detected' in obj:
            return {'detected': obj['detected'], 'raw': raw}
        
        return {'detected': [], 'raw': raw}


# VL 커스텀 프롬프트 호출 (특수 상황 대응)
def vl_prompt_call(frame, prompt):
    '''
    frame: 이미지 경로
    prompt: 커스텀 프롬프트 (예: MENU 버튼 스타일 판별, 미독 카운트 읽기 등)
    
    return: {'result': dict|None, 'raw': str}
    '''
    if not frame:
        return {'result': None, 'raw': 'No frame provided'}
    
    if not prompt:
        return {'result': None, 'raw': 'No prompt provided'}
    
    llm = get_llm(require_vl=True)
    image_b64, w, h = image_file_to_base64_png(frame)
    
    raw = call_vl(llm, prompt, image_b64)
    obj = extract_json_object(raw)
    
    return {'result': obj, 'raw': raw}


if __name__ == "__main__":
    import ai_vl_engine_icons
    # 사용 예
    # DEFAULT_TARGET = "モモトーク"
    # DEFAULT_TARGET = "pink phone icon with peach"
    DEFAULT_TARGET = "red notification badge"
    # DEFAULT_TARGET = ai_vl_engine_icons.TARGET_MOMOTALK_ICON[0]
    DEFAULT_TARGET = ai_vl_engine_icons.TARGET_MOMOTALK_ICON2[0]
    print('### DEFAULT_TARGET', DEFAULT_TARGET)
    # DEFAULT_TARGET = ai_vl_engine_icons.TARGET_MOMOTALK_ICON2[0]
    DEFAULT_IMAGE_PATH = Path("./test/image/pink_phone.png") # pink phone icon with peach / モモトーク
    # DEFAULT_IMAGE_PATH = Path("./test/image/red_badge.png") # red_badge icon / RED rectangle badge
    # DEFAULT_IMAGE_PATH = Path("./test/image/story.png") # 絆ストーリー
    # DEFAULT_IMAGE_PATH = Path("./test/image/16.png") # button
    # DEFAULT_IMAGE_PATH = Path("./test/image/9.webp") # cat
    # DEFAULT_IMAGE_PATH = Path("./test/image/13.webp") # dog

    target = DEFAULT_TARGET
    image_path = DEFAULT_IMAGE_PATH
    image_dir = None

    session_ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    session_dir = OUTPUT_BASE_DIR / f"session_{session_ts}"
    ensure_dir(session_dir)

    log_lines = []
    log_lines.append(f"session: {session_ts}")
    log_lines.append(f"model_name: {MODEL_NAME}")
    log_lines.append(f"target: {target}")

    images = []
    if image_dir is not None:
        images = collect_images(image_dir)
        log_lines.append(f"mode: dir")
        log_lines.append(f"dir: {str(image_dir)}")
        log_lines.append(f"image_count: {len(images)}")
    else:
        images = [image_path]
        log_lines.append(f"mode: single")
        log_lines.append(f"image: {str(image_path)}")

    log_lines.append("")

    for idx, img_path in enumerate(images, 1):
        try:
            result = vl_target_find(img_path, target)

            stem = img_path.stem
            out_dir = session_dir / f"{stem}__{target}"
            ensure_dir(out_dir)

            write_json(out_dir / "result.json", result)

            debug = result.get("debug", {}) if isinstance(result, dict) else {}
            bboxes_2d = debug.get("bboxes_2d", []) if isinstance(debug, dict) else []
            if not isinstance(bboxes_2d, list):
                bboxes_2d = []

            annotated_path = out_dir / "annotated.png"
            pixel_info = draw_boxes_and_save(img_path, bboxes_2d, annotated_path)
            write_json(out_dir / "pixel_boxes.json", pixel_info)

            prompt = build_grounding_prompt_find_all(target)
            write_text(out_dir / "prompt.txt", prompt)
            raw_resp = debug.get("raw_response", "")
            write_text(out_dir / "raw_response.txt", raw_resp if isinstance(raw_resp, str) else "")

            exists = bool(result.get("variables", {}).get("exists", False))
            box_count = len(pixel_info.get("pixel_boxes", []))

            log_lines.append(f"[{idx}/{len(images)}] image={str(img_path)}")
            log_lines.append(f"  exists={exists}, bboxes={box_count}")
            log_lines.append(f"  out_dir={str(out_dir)}")
            log_lines.append("")

        except Exception as e:
            log_lines.append(f"[{idx}/{len(images)}] image={str(img_path)}")
            log_lines.append(f"  ERROR: {e}")
            log_lines.append("")

    write_text(session_dir / "run.log.txt", "\n".join(log_lines))

    # print는 최소만
    print(str(session_dir))
