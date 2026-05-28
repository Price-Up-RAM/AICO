'''
ai_vl_agent_functions.py
VL Planer Function 호출 인터페이스
- function_vl_grounding: 현재 프레임에서 클릭 후보 좌표 도출
- function_request_frame: 새 프레임 요청
- function_request_click: 클릭 수행 요청
'''
    
from ai_vl_agent_function import vl_keyword_detect, vl_prompt_call, vl_target_find

# Function 이름 상수
## 서버 실행 - VL Function
FUNC_VL_TARGET_FIND = 'function_vl_grounding'
FUNC_VL_KEYWORD_DETECT = 'function_vl_keyword_detect'
FUNC_VL_PROMPT_CALL = 'function_vl_prompt_call'
## 유니티 요청 Function
FUNC_REQUEST_FRAME = 'function_request_frame'  # 범위 스샷
FUNC_REQUEST_CLICK = 'function_request_click'
FUNC_REQUEST_PLAY_SFX_ALERT = 'function_request_play_sfx_alert'
FUNC_REQUEST_DANCE = 'function_request_dance'
FUNC_REQUEST_SCREENSHOT = 'function_request_screenshot'  # 전체 스샷

# 최종 가능 Function (이 action 수행 시 ONE_SHOT이면 종료 가능)
FINAL_CAPABLE_FUNCTIONS = [
    FUNC_REQUEST_CLICK,
    FUNC_REQUEST_PLAY_SFX_ALERT,
    FUNC_REQUEST_DANCE,
    FUNC_REQUEST_SCREENSHOT
]


def is_final_capable(function_name):
    '''해당 function이 최종 action으로 사용 가능한지 확인'''
    return function_name in FINAL_CAPABLE_FUNCTIONS


def get_final_capable_functions():
    '''최종 가능 function 리스트 반환'''
    return FINAL_CAPABLE_FUNCTIONS.copy()

# 단일 인터페이스로 Function 호출
def call_vl_function(function_name, function_args, frame=None):
    '''
    function_name: FUNC_VL_TARGET_FIND | FUNC_REQUEST_FRAME | FUNC_REQUEST_CLICK
    function_args: dict
    frame: 현재 프레임 경로 (grounding용)
    
    return: function_result dict
    '''
    if function_name == FUNC_VL_TARGET_FIND:
        target_text = function_args.get('target_text', '')
        verbose = function_args.get('verbose', False)
        
        if not frame:
            return None
        
        # vl_target_find 직접 호출
        res = vl_target_find(frame, target_text, verbose=verbose)
        
        # scanner.py와 동일한 파싱 로직 적용
        if res and isinstance(res, dict):
            variables = res.get('variables', {})
            clicks = variables.get('clicks', [])
            if clicks:
                c0 = clicks[0]
                if isinstance(c0, dict):
                    x = c0.get('x')
                    y = c0.get('y')
                    if x is not None and y is not None:
                        return {'x': int(x), 'y': int(y), 'raw': res, 'source': 'vl_grounding'}
        
        return None
    
    elif function_name == FUNC_VL_KEYWORD_DETECT:
        keyword_list = function_args.get('keyword_list', [])
        return vl_keyword_detect(frame, keyword_list)
    
    elif function_name == FUNC_VL_PROMPT_CALL:
        prompt = function_args.get('prompt', '')
        return vl_prompt_call(frame, prompt)
        
    else:
        return {
            'status': 'error',
            'message': f'Unknown function: {function_name}'
        }


# ========================================
# Function List 및 설명 (다국어)
# ========================================

# 모든 Function에 대한 설명 (한국어)
FUNCTION_DESCRIPTIONS_KO = {
    FUNC_VL_TARGET_FIND: {
        'name': 'function_vl_grounding',
        'description': '현재 화면에서 대상(텍스트로 표현된 목표 단서)을 찾아 클릭 후보 좌표들을 도출'
    },
    FUNC_VL_KEYWORD_DETECT: {
        'name': 'function_vl_keyword_detect',
        'description': 'UI에서 키워드 리스트 중 화면에 존재하는 것들을 탐지'
    },
    FUNC_VL_PROMPT_CALL: {
        'name': 'function_vl_prompt_call',
        'description': '커스텀 프롬프트로 VL 모델을 호출하여 특수 상황 처리 (MENU 스타일 판별, 미독 카운트 읽기 등)'
    },
    FUNC_REQUEST_FRAME: {
        'name': 'function_request_frame',
        'description': '새로운 화면 캡처를 요청'
    },
    FUNC_REQUEST_CLICK: {
        'name': 'function_request_click',
        'description': '지정된 좌표에 클릭을 수행'
    },
    FUNC_REQUEST_PLAY_SFX_ALERT: {
        'name': 'function_request_play_sfx_alert',
        'description': '"확인이 필요한 사항이 생겼어요. 선생님" 음성을 재생하여 사용자에게 알림'
    },
    FUNC_REQUEST_DANCE: {
        'name': 'function_request_dance',
        'description': '캐릭터가 춤추도록 요청'
    },
    FUNC_REQUEST_SCREENSHOT: {
        'name': 'function_request_screenshot',
        'description': '전체화면 스크린샷을 수행하고 저장 및 클립보드에 복사'
    }
}

# 모든 Function에 대한 설명 (영어)
FUNCTION_DESCRIPTIONS_EN = {
    FUNC_VL_TARGET_FIND: {
        'name': 'function_vl_grounding',
        'description': 'Find click candidate coordinates for targets on the current screen'
    },
    FUNC_VL_KEYWORD_DETECT: {
        'name': 'function_vl_keyword_detect',
        'description': 'Detect which keywords from a list are visible in the UI'
    },
    FUNC_VL_PROMPT_CALL: {
        'name': 'function_vl_prompt_call',
        'description': 'Call VL model with custom prompt for special cases (MENU style detection, unread count reading, etc.)'
    },
    FUNC_REQUEST_FRAME: {
        'name': 'function_request_frame',
        'description': 'Request a new screen capture'
    },
    FUNC_REQUEST_CLICK: {
        'name': 'function_request_click',
        'description': 'Perform a click at the specified coordinates'
    },
    FUNC_REQUEST_PLAY_SFX_ALERT: {
        'name': 'function_request_play_sfx_alert',
        'description': 'Play voice alert "Something needs your attention, Sensei" to notify the user'
    },
    FUNC_REQUEST_DANCE: {
        'name': 'function_request_dance',
        'description': 'Request the character to dance'
    },
    FUNC_REQUEST_SCREENSHOT: {
        'name': 'function_request_screenshot',
        'description': 'Capture full-screen screenshot, save and copy to clipboard'
    }
}

# 모든 Function에 대한 설명 (일본어)
FUNCTION_DESCRIPTIONS_JA = {
    FUNC_VL_TARGET_FIND: {
        'name': 'function_vl_grounding',
        'description': '現在の画面で対象（テキストで表現された目標の手がかり）を見つけてクリック候補座標を導出'
    },
    FUNC_VL_KEYWORD_DETECT: {
        'name': 'function_vl_keyword_detect',
        'description': 'UIでキーワードリストの中から画面に存在するものを検出'
    },
    FUNC_VL_PROMPT_CALL: {
        'name': 'function_vl_prompt_call',
        'description': 'カスタムプロンプトでVLモデルを呼び出し、特殊状況を処理（MENUスタイル判別、未読カウント読み取りなど）'
    },
    FUNC_REQUEST_FRAME: {
        'name': 'function_request_frame',
        'description': '新しい画面キャプチャを要求'
    },
    FUNC_REQUEST_CLICK: {
        'name': 'function_request_click',
        'description': '指定された座標にクリックを実行'
    },
    FUNC_REQUEST_PLAY_SFX_ALERT: {
        'name': 'function_request_play_sfx_alert',
        'description': '"確認が必要なことがあります。先生" という音声を再生してユーザーに知らせる'
    },
    FUNC_REQUEST_DANCE: {
        'name': 'function_request_dance',
        'description': 'キャラクターにダンスをリクエスト'
    },
    FUNC_REQUEST_SCREENSHOT: {
        'name': 'function_request_screenshot',
        'description': 'フルスクリーンのスクリーンショットを撮影し、保存してクリップボードにコピー'
    }
}


def get_function_descriptions(lang='en'):
    '''언어에 따른 Function 설명 dict 반환'''
    if lang == 'ko':
        return FUNCTION_DESCRIPTIONS_KO
    elif lang == 'ja':
        return FUNCTION_DESCRIPTIONS_JA
    else:
        return FUNCTION_DESCRIPTIONS_EN


def get_all_functions_list(lang='en'):
    '''
    모든 Function에 대한 설명 프롬프트 반환
    (decide_next_step 등에서 사용)
    '''
    descriptions = get_function_descriptions(lang)
    lines = []
    for func_name, desc in descriptions.items():
        lines.append(f"- {desc['name']}: {desc['description']}")
    return '\n'.join(lines)


def get_function_list_for_goal_and_success_signal(lang='en'):
    '''
    Goal/Success Signal 추론에 필요한 Function만 설명 프롬프트 반환
    (하드코딩으로 관리 - 현재는 function_request_click만)
    
    함수 정보만 반환, ONE_SHOT/VERIFY 설명은 프롬프트 파일에서 처리
    '''
    # Goal/Signal 추론에 사용할 Function 목록 (하드코딩으로 관리)
    FUNCTIONS_FOR_GOAL_SIGNAL = [FUNC_REQUEST_CLICK, FUNC_REQUEST_PLAY_SFX_ALERT, FUNC_REQUEST_DANCE, FUNC_REQUEST_SCREENSHOT]
    
    descriptions = get_function_descriptions(lang)
    lines = []
    for func_name in FUNCTIONS_FOR_GOAL_SIGNAL:
        if func_name in descriptions:
            desc = descriptions[func_name]
            lines.append(f"- {desc['name']}: {desc['description']}")
    
    return '\n'.join(lines)


def build_classify_step1_is_choice():
    '''
    Step 1: 선택지 화면인지 아닌지만 판단
    
    return: {"is_choice": true/false}
    '''
    return (
        'You are determining if a Japanese game screenshot is a CHOICE screen or NOT.\n'
        '\n'
        'Goal:\n'
        'Return ONLY one JSON object with exactly one key:\n'
        '{"is_choice":true} or {"is_choice":false}\n'
        '\n'
        'What is a CHOICE screen?\n'
        'A choice screen has 1 or 2 wide horizontal bars/buttons with Japanese text, positioned in the UPPER or CENTER area (NOT bottom).\n'
        '\n'
        'Visual characteristics of choice bars:\n'
        '- SHAPE: Wide horizontal rectangles (button-like)\n'
        '- COLOR: Light-colored (white, light blue, translucent)\n'
        '- POSITION: Upper half or center of screen, ABOVE any bottom dialogue box\n'
        '- CONTENT: Each bar contains readable Japanese text (a sentence or phrase)\n'
        '- TEXT STYLE: May have quotation marks like "確かに、何となく分かるかも。"\n'
        '\n'
        'What is NOT a choice screen:\n'
        '- Screens with ONLY a bottom dialogue box (no choice bars above)\n'
        '- Screens with text in center but NO button-like bars (narration)\n'
        '- Screens with only characters/environment (no text)\n'
        '- Small icons, rings, target marks, decorative UI elements\n'
        '\n'
        'Decision rules:\n'
        '- If you see 1-2 wide horizontal light-colored bars with Japanese text in upper/center area → is_choice = true\n'
        '- If you see BOTH choice bars (top/middle) AND dialogue box (bottom) → is_choice = true (choice has priority)\n'
        '- If you see ONLY bottom dialogue, ONLY narration, or NO text → is_choice = false\n'
        '- ALWAYS ignore AUTO and MENU buttons in top-right corner\n'
        '\n'
        'Output:\n'
        '- JSON only, no markdown, no extra text\n'
        '- Use double quotes\n'
    )


def build_classify_step2_non_choice():
    '''
    Step 2: 선택지가 아닌 경우 4가지 중 하나로 분류
    
    return: {"type": "dialogue_with_actor" | "dialogue_no_actor" | "narration" | "none"}
    '''
    return (
        'You are classifying a NON-CHOICE Japanese game screenshot into exactly one of four categories.\n'
        '\n'
        'Goal:\n'
        'Return ONLY one JSON object with exactly one key:\n'
        '{"type":"..."}\n'
        '\n'
        'Important: This screen has NO choice bars. Classify by UI position and content.\n'
        '\n'
        'Categories:\n'
        '\n'
        '1) "dialogue_with_actor" - Dialogue with speaker\n'
        '   Must satisfy ALL:\n'
        '   - A dialogue UI frame/box is anchored at the BOTTOM of the screen (bottom quarter)\n'
        '   - A nameplate (speaker label) is visible as a small box/tag, usually on the left or top edge of the dialogue box\n'
        '   - Dialogue text is inside the bottom dialogue box\n'
        '   Example: Bottom box with "先生" nameplate and dialogue text\n'
        '\n'
        '2) "dialogue_no_actor" - Dialogue without speaker\n'
        '   Must satisfy ALL:\n'
        '   - A dialogue UI frame/box is anchored at the BOTTOM of the screen (bottom quarter)\n'
        '   - Dialogue text is inside that bottom box\n'
        '   - NO nameplate (speaker label) is visible\n'
        '   Notes:\n'
        '   - Text may be in parentheses like (アリスが...) but still inside the bottom dialogue box\n'
        '\n'
        '3) "narration" - Pure narration\n'
        '   Must satisfy ALL:\n'
        '   - Text is shown as a plain overlay in the CENTER area of the screen\n'
        '   - NO bottom dialogue UI frame/box\n'
        '   - Often displayed on black or simple background\n'
        '   Example: Centered text with no UI frame around it\n'
        '\n'
        '4) "none" - No text / Scene / Illustration\n'
        '   Use this when:\n'
        '   - No Japanese game text is visible (only characters, environment, background)\n'
        '   - Scene transition or fade\n'
        '   - ALWAYS ignore AUTO/MENU buttons in corner (they do not count as text)\n'
        '\n'
        'Rules:\n'
        '- Decide by BOTTOM dialogue box presence and nameplate presence\n'
        '- ALWAYS ignore AUTO and MENU in the top-right\n'
        '- Output JSON only, double quotes only, no extra keys, no extra text\n'
    )


def build_ocr_dialogue_prompt(dialogue_type):
    '''
    dialogue_type에 따라 적절한 OCR 프롬프트 반환
    
    dialogue_type: 'dialogue_with_actor' | 'dialogue_no_actor' | 'choice' | 'narration'
    
    return: prompt string
    '''
    
    if dialogue_type == 'dialogue_with_actor':
        # actor + txt 모두 추출
        return (
            'You are extracting dialogue from a Japanese game screenshot.\n'
            '\n'
            'Goal:\n'
            'Return ONLY one JSON object with exactly two keys:\n'
            '{"actor":"...","txt":"..."}\n'
            '\n'
            'What to read:\n'
            '- Read ONLY the bottom dialogue UI: the nameplate (speaker) and the dialogue text area.\n'
            '- This is a Japanese game, so both actor and txt will be in Japanese.\n'
            '- Ignore AUTO, MENU, background signs, logos, and any other UI text.\n'
            '\n'
            'actor rules:\n'
            '- actor must be the Japanese speaker identifier ONLY.\n'
            '- If the nameplate contains Japanese affiliation/department text, remove it.\n'
            '  Remove tokens like: 新素材開発部, 開発部, 研究部, 部, 所属, チーム, 学科, 学院, etc.\n'
            '- If the nameplate is like "<name> <affiliation>", keep only "<name>".\n'
            '\n'
            'txt rules:\n'
            '- txt must contain ONLY the Japanese dialogue text.\n'
            '- Do not translate.\n'
            '- Do not add meaning.\n'
            '- Make the sentence natural by merging layout line breaks.\n'
            '  If there are multiple sentences, keep them as multiple sentences, but do not keep layout-only line breaks.\n'
            '\n'
            'Hard output constraints:\n'
            '- Output JSON only. No markdown. No extra text.\n'
            '- Use double quotes for JSON keys and values.\n'
            '- Do not include trailing commentary.\n'
        )
    
    elif dialogue_type == 'dialogue_no_actor':
        # txt만 추출, actor는 빈 문자열
        return (
            'You are extracting dialogue from a Japanese game screenshot.\n'
            '\n'
            'Goal:\n'
            'Return ONLY one JSON object with exactly two keys:\n'
            '{"actor":"","txt":"..."}\n'
            '\n'
            'What to read:\n'
            '- Read ONLY the bottom dialogue UI text area (NO nameplate visible).\n'
            '- This is a Japanese game, so txt will be in Japanese.\n'
            '- Ignore AUTO, MENU, background signs, logos, and any other UI text.\n'
            '\n'
            'actor rules:\n'
            '- ALWAYS set actor to empty string "".\n'
            '\n'
            'txt rules:\n'
            '- txt must contain ONLY the Japanese dialogue or narration text from the dialogue box.\n'
            '- Text may be in parentheses like (アリスが...).\n'
            '- Do not translate.\n'
            '- Do not add meaning.\n'
            '- Make the sentence natural by merging layout line breaks.\n'
            '  If there are multiple sentences, keep them as multiple sentences, but do not keep layout-only line breaks.\n'
            '\n'
            'Hard output constraints:\n'
            '- Output JSON only. No markdown. No extra text.\n'
            '- Use double quotes for JSON keys and values.\n'
            '- Do not include trailing commentary.\n'
        )
    
    elif dialogue_type == 'choice':
        # 선택지 추출
        return (
            'You are extracting user choice options from a Japanese game screenshot.\n'
            '\n'
            'Goal:\n'
            'Return ONLY one JSON object with these keys:\n'
            '{"actor":"","txt":"...","choice_count":N,"choices":["choice1","choice2"]}\n'
            '\n'
            'What to read:\n'
            '- Look for WIDE HORIZONTAL LIGHT-COLORED BARS (rectangles) in the MIDDLE or UPPER area of the screen.\n'
            '- These choice bars are usually STACKED VERTICALLY (one above the other).\n'
            '- Each bar is a separate choice option with Japanese text inside.\n'
            '- Choice bars are LIGHT-COLORED (white, light blue, translucent) and clearly visible.\n'
            '- The text inside choice bars often has quotation marks like "やあ。".\n'
            '- There can be 1 or 2 choice bars.\n'
            '\n'
            'Visual example:\n'
            '- TOP bar: "やあ。" (first choice)\n'
            '- BOTTOM bar: "何してる？" (second choice)\n'
            '\n'
            'What to IGNORE:\n'
            '- Bottom dialogue box (separate from choice bars)\n'
            '- AUTO, MENU buttons in corner\n'
            '- Background elements\n'
            '\n'
            'actor rules:\n'
            '- ALWAYS set actor to empty string "".\n'
            '\n'
            'txt rules:\n'
            '- Read the text INSIDE the choice bar(s), including any quotation marks.\n'
            '- If there is 1 choice bar: txt = the text inside that bar.\n'
            '- If there are 2 choice bars:\n'
            '  * Try merging both texts into one sentence.\n'
            '  * If the merged result sounds NATURAL (flows well as one sentence), txt = the merged sentence.\n'
            '  * If the merged result sounds UNNATURAL (two separate sentences), txt = the TOP/FIRST choice only.\n'
            '- Do not translate.\n'
            '\n'
            'choice_count and choices rules:\n'
            '- choice_count: Count how many choice bars you see (1 or 2).\n'
            '- choices: Array of ALL choice texts as they appear, INCLUDING quotation marks.\n'
            '- Example for 2 choices: choices = ["やあ。", "何してる？"]\n'
            '- Example for 1 choice: choices = ["確かに、何となく分かるかも。"]\n'
            '- If you cannot find any choice bars (unlikely if classification was correct), set choice_count=0 and choices=[].\n'
            '\n'
            'Hard output constraints:\n'
            '- Output JSON only. No markdown. No extra text.\n'
            '- Use double quotes for JSON keys and values.\n'
            '- Do not include trailing commentary.\n'
        )
    
    elif dialogue_type == 'narration':
        # 나레이션 추출
        return (
            'You are extracting narration text from a Japanese game screenshot.\n'
            '\n'
            'Goal:\n'
            'Return ONLY one JSON object with exactly two keys:\n'
            '{"actor":"","txt":"..."}\n'
            '\n'
            'What to read:\n'
            '- Read ONLY the centered narration text (NO dialogue UI box/frame).\n'
            '- This is a Japanese game, so txt will be in Japanese.\n'
            '- Often displayed on black or simple backgrounds.\n'
            '- Ignore AUTO, MENU buttons.\n'
            '\n'
            'actor rules:\n'
            '- ALWAYS set actor to empty string "".\n'
            '\n'
            'txt rules:\n'
            '- txt must contain ONLY the Japanese narration text displayed in the center.\n'
            '- Do not translate.\n'
            '- Do not add meaning.\n'
            '- Make the sentence natural by merging layout line breaks.\n'
            '  If there are multiple sentences, keep them as multiple sentences, but do not keep layout-only line breaks.\n'
            '\n'
            'Hard output constraints:\n'
            '- Output JSON only. No markdown. No extra text.\n'
            '- Use double quotes for JSON keys and values.\n'
            '- Do not include trailing commentary.\n'
        )
    
    else:
        # 알 수 없는 타입
        return None


if __name__ == '__main__':
    print('=== ai_vl_agent_functions 테스트 ===')
    
    # function_vl_grounding 테스트 (이미지 없이)
    if False:
        result = call_vl_function(
            FUNC_VL_TARGET_FIND,
            {'target_text': 'yellow button'},
            frame=None
        )
        print(f'vl_grounding (no frame): {result}')
    
    # function vl_prompt_call
    if False:
        import ai_vl_engine_keywords
        keyword = ai_vl_engine_keywords.KEYWORD_MENU
        prompt = (
        'You are a UI text detector.\n'
        f'Task: Check if the text "{keyword}" is visible in this game UI screenshot.\n'
        'Return ONLY JSON:\n'
        '{"visible": true} if the exact text is visible\n'
        '{"visible": false} if not visible\n'
        'Do not output any extra text.\n'
        )
        image_path = ''

        result = call_vl_function(
            function_name=FUNC_VL_PROMPT_CALL,
            function_args={'prompt': prompt},
            frame=image_path
        )
    
    # funciotn vl_prompt_call2
    if False:
        image_path = ''
        prompt = (
            'You are a UI text reader.\n'
            'Task: read the unread message count in the header.\n'
            'The header usually looks like: 未読メッセージ(83)\n'
            'Output ONLY JSON, no extra text.\n'
            'If the header is visible and you can read a number, output:\n'
            '{"status":"found_with_count","count":83}\n'
            'If the header is visible but you cannot read the number, output:\n'
            '{"status":"found_no_count","count":null}\n'
            'If the header is not visible, output:\n'
            '{"status":"not_found","count":null}\n'
        )

        result = call_vl_function(
            function_name=FUNC_VL_PROMPT_CALL,
            function_args={'prompt': prompt},
            frame=image_path
        )
    
    # VL 테스트 결과를 파일로 저장하는 헬퍼 함수
    def save_vl_test_results(test_name, image_paths, prompt):
        from pathlib import Path
        import json
        
        output_dir = Path('./test/vl_image')
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # 결과 저장용 리스트
        results = []
        
        print(f'\n[{test_name}]')
        for image_path in image_paths:
            result = call_vl_function(
                function_name=FUNC_VL_PROMPT_CALL,
                function_args={'prompt': prompt},
                frame=image_path
            )
            
            results.append({
                'image': image_path,
                'result': result
            })
            
            print(f'Image: {image_path}')
            print(f'Result: {result}')
            print('======')
        
        # 결과를 txt 파일로 저장
        output_file = output_dir / f'{test_name}.txt'
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(f'=== {test_name} ===\n')
            f.write(f'Total images: {len(image_paths)}\n')
            f.write(f'\nPrompt:\n{prompt}\n')
            f.write('\n' + '='*80 + '\n\n')
            
            for item in results:
                f.write(f'Image: {item["image"]}\n')
                f.write(f'Result: {json.dumps(item["result"], ensure_ascii=False, indent=2)}\n')
                f.write('\n' + '-'*80 + '\n\n')
        
        print(f'\n결과 저장됨: {output_file}')
        return results
    


    # function vl_prompt_call3: Extract Japanese dialogue from game UI
    if False:
        image_paths = [
            './test/image_test3/1.png',
            './test/image_test3/2.jpg',
            './test/image_test3/3.png',
            './test/image_test3/4.png',
            './test/image_test3/5.png',
            './test/image_test3/6.png',
            './test/image_test3/7.png',
            './test/image_test3/8.png',
            './test/image_test3/9.png'
        ]
        
        prompt = (
            'You are extracting dialogue from a Japanese game screenshot.\n'
        '\n'
        'Goal:\n'
        'Return ONLY one JSON object with exactly two keys:\n'
        '{"actor":"...","txt":"..."}\n'
        '\n'
        'What to read:\n'
        '- Read ONLY the bottom dialogue UI: the nameplate (speaker) and the dialogue text area.\n'
        '- This is a Japanese game, so both actor and txt will be in Japanese.\n'
        '- Ignore AUTO, MENU, background signs, logos, and any other UI text.\n'
        '\n'
        'actor rules:\n'
        '- If there is NO nameplate visible (e.g., narration text only), set actor to empty string "".\n'
        '- If there is a nameplate, actor must be the Japanese speaker identifier ONLY.\n'
        '- If the nameplate contains Japanese affiliation/department text, remove it.\n'
        '  Remove tokens like: 新素材開発部, 開発部, 研究部, 部, 所属, チーム, 学科, 学院, etc.\n'
        '- If the nameplate is like "<name> <affiliation>", keep only "<name>".\n'
        '\n'
        'txt rules:\n'
        '- txt must contain ONLY the Japanese dialogue or narration text.\n'
        '- Do not translate.\n'
        '- Do not add meaning.\n'
        '- Make the sentence natural by merging layout line breaks.\n'
        '  If there are multiple sentences, keep them as multiple sentences, but do not keep layout-only line breaks.\n'
        '\n'
        'Hard output constraints:\n'
        '- Output JSON only. No markdown. No extra text.\n'
        '- Use double quotes for JSON keys and values.\n'
        '- Do not include trailing commentary.\n'
        )
        
        save_vl_test_results('Japanese_dialogue_extraction_test', image_paths, prompt)

    # 복합 테스트: 2단계 분류 -> 타입별 OCR 추출
    if True:
        image_paths = [
            './test/image_test3/1.png',
            './test/image_test3/2.jpg',
            './test/image_test3/3.png',
            './test/image_test3/4.png',
            './test/image_test3/5.png',
            './test/image_test3/6.png',
            './test/image_test3/7.png',
            './test/image_test3/8.png',
            './test/image_test3/9.png'
        ]
        
        # Step 1 프롬프트: 선택지 여부 판단
        step1_prompt = build_classify_step1_is_choice()
        
        # Step 2 프롬프트: 선택지가 아닌 경우 4가지 중 분류
        step2_prompt = build_classify_step2_non_choice()
        
        from pathlib import Path
        import json
        
        output_dir = Path('./test/vl_image')
        output_dir.mkdir(parents=True, exist_ok=True)
        
        results = []
        
        print(f'\n[2-stage classify and extract test]')
        for image_path in image_paths:
            # Step 1: 선택지 여부 판단
            step1_result = call_vl_function(
                function_name=FUNC_VL_PROMPT_CALL,
                function_args={'prompt': step1_prompt},
                frame=image_path
            )
            
            is_choice = False
            if step1_result and isinstance(step1_result, dict):
                result_obj = step1_result.get('result')
                if result_obj and isinstance(result_obj, dict):
                    is_choice = result_obj.get('is_choice', False)
            
            print(f'\nImage: {image_path}')
            print(f'Step 1 - Is Choice: {is_choice}')
            
            # Step 2 or determine final type
            dialogue_type = None
            step2_result = None
            
            if is_choice:
                dialogue_type = 'choice'
            else:
                # Step 2: 선택지가 아닌 경우 4가지 중 분류
                step2_result = call_vl_function(
                    function_name=FUNC_VL_PROMPT_CALL,
                    function_args={'prompt': step2_prompt},
                    frame=image_path
                )
                
                if step2_result and isinstance(step2_result, dict):
                    result_obj = step2_result.get('result')
                    if result_obj and isinstance(result_obj, dict):
                        dialogue_type = result_obj.get('type')
                
                print(f'Step 2 - Type: {dialogue_type}')
            
            # OCR 추출
            ocr_result = None
            if dialogue_type and dialogue_type != 'none':
                ocr_prompt = build_ocr_dialogue_prompt(dialogue_type)
                if ocr_prompt:
                    ocr_result = call_vl_function(
                        function_name=FUNC_VL_PROMPT_CALL,
                        function_args={'prompt': ocr_prompt},
                        frame=image_path
                    )
                    print(f'OCR Result: {ocr_result}')
                else:
                    print(f'No OCR prompt for type: {dialogue_type}')
            else:
                print(f'Skipping OCR (type is none or invalid)')
            
            results.append({
                'image': image_path,
                'is_choice': is_choice,
                'step1_result': step1_result,
                'step2_result': step2_result,
                'final_type': dialogue_type,
                'ocr_result': ocr_result
            })
            print('======')
        
        # 결과를 txt 파일로 저장
        output_file = output_dir / 'Combined_2stage_classify_and_extract_test.txt'
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write('=== 2-stage classify and extract test ===\n')
            f.write(f'Total images: {len(image_paths)}\n')
            f.write('\n' + '='*80 + '\n\n')
            
            for item in results:
                f.write(f'Image: {item["image"]}\n')
                f.write(f'Step 1 - Is Choice: {item["is_choice"]}\n')
                f.write(f'Step 1 Result: {json.dumps(item["step1_result"], ensure_ascii=False, indent=2)}\n')
                if item["step2_result"]:
                    f.write(f'Step 2 Result: {json.dumps(item["step2_result"], ensure_ascii=False, indent=2)}\n')
                f.write(f'Final Type: {item["final_type"]}\n')
                f.write(f'OCR Result: {json.dumps(item["ocr_result"], ensure_ascii=False, indent=2)}\n')
                f.write('\n' + '-'*80 + '\n\n')
        
        print(f'\n결과 저장됨: {output_file}')




    print('\n=== 완료 ===')
