'''
프로그램 내 변수 형상관리
'''
import json

# 상태
DEV_MODE = False
is_stop_requested = False  # Stream 대답 중, 대답 중지
is_screenshot_area_selecting = False  # 현재 스크린샷 범위 설정 중
is_write_log_file = False  # 모든 내용을 로그로 저장
is_enable_thinking_possible = False  # qwen에서 쓰는 <think>태그 관련 가능성
is_unity = False  # 실행지점이 Unity인지 직접실행인지

# 변수
write_log_file_name = ''
model_name = ''
max_vram = 8

use_gpu_percent = 0 
max_n_gpu_layers = 33
n_gpu_layers = 999 # gpu 최대로
use_vram = 0

g_language = 'ko'  # 언어 : ["日本語", "English", "한국어"] to ['ja', 'en', 'ko']
g_language_init = ''  # 최초 프로그램 기동시의 언어

def get_is_stop_requested():
    global is_stop_requested
    return is_stop_requested

def set_is_stop_requested(value=True):
    global is_stop_requested
    is_stop_requested = value

def get_is_screenshot_area_selecting():
    global is_screenshot_area_selecting
    return is_screenshot_area_selecting

def set_is_screenshot_area_selecting(value=True):
    global is_screenshot_area_selecting
    is_screenshot_area_selecting = value

# setting의 UI 언어
def get_g_language():
    global g_language
    g_language = 'ko'  # ["日本語", "English", "한국어"]
    try:
        with open('config/setting.json', 'r', encoding='utf-8') as file:
            settings = json.load(file)
            if 'setting_language' in settings:
                if settings['setting_language'] == '한국어':
                    g_language = 'ko'
                elif settings['setting_language'] == '日本語':
                    g_language = 'ja'
    except:
        pass
    return g_language

# 최초 로딩시 언어 세팅 후 그 언어만 사용 / 메뉴용
def get_g_language_init():
    global g_language_init
    if not g_language_init:
        g_language_init = 'ko'  # ["日本語", "English", "한국어"]
        try:
            with open('config/setting.json', 'r', encoding='utf-8') as file:
                settings = json.load(file)
                if 'setting_language' in settings:
                    if settings['setting_language'] == '한국어':
                        g_language_init = 'ko'
                    elif settings['setting_language'] == '日本語':
                        g_language_init = 'ja'
                    elif settings['setting_language'] == 'English':
                        g_language_init = 'en'
        except:
            pass
    return g_language_init


def get_use_gpu_percent():
    global use_gpu_percent
    return use_gpu_percent

# 8(33)을 기준으로 12(49)는 150% 7은 87.5
def set_use_gpu_percent(use_vram):
    global use_gpu_percent, max_vram
    use_gpu_percent = use_vram * 100 / max_vram
    
def get_DEV_MODE():
    global DEV_MODE
    return DEV_MODE

def set_DEV_MODE(value=True):
    global DEV_MODE
    DEV_MODE = value    
    
# 로그 관련. 길어지면 util로 분리
import os
from datetime import datetime
def write_log(text):
    global write_log_file_name, DEV_MODE
    if not DEV_MODE and not is_write_log_file:
        return
    if not write_log_file_name:
        write_log_file_name = "log_" + str(datetime.now().strftime("%y%m%d_%H%M")) + ".txt"
    os.makedirs('./log', exist_ok=True)
    with open(f"./log/{write_log_file_name}", "a", encoding='utf-8') as f:  # (a)ppend
        f.write(text + "\n")  # 각 로그를 새 줄로 구분

def set_var_from_model(model_type):
    global max_vram, max_n_gpu_layers, DEV_MODE
    if model_type == 'Qwen3-8B-Q4_K_M.gguf':
        max_vram = 8
        max_n_gpu_layers = 37
    elif model_type == 'Qwen3-14B-Q4_K_M.gguf':
        max_vram = 12
        max_n_gpu_layers = 41
    elif model_type == 'Qwen3-32B-Q4_K_M.gguf':
        max_vram = 24
        max_n_gpu_layers = 65
    elif model_type == 'Qwen3-4B-Q4_K_M.gguf':
        max_vram = 4
        max_n_gpu_layers = 37
    elif model_type == 'Qwen3-1.7B-Q4_K_M.gguf':
        max_vram = 3
        max_n_gpu_layers = 29
    elif model_type == 'Qwen3-0.6B-Q4_K_M.gguf':
        max_vram = 2
        max_n_gpu_layers = 29
    elif model_type == 'Qwen3VL-8B-Instruct-Q4_K_M.gguf':
        max_vram = 8
        max_n_gpu_layers = 37
    else:
        max_vram = 99999
        max_n_gpu_layers = 999

def set_n_gpu_layers():
    global max_n_gpu_layers, n_gpu_layers, max_vram, use_vram
    n_gpu_layers = 0
    if max_vram:
        n_gpu_layers = max(0, int(max_n_gpu_layers*use_vram/max_vram))
    print(f'### state.n_gpu_layers : {n_gpu_layers}/{max_n_gpu_layers}')
    print(f'### state.vram : {use_vram}/{max_vram}')
