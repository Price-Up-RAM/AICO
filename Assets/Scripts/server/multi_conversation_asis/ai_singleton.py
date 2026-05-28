# model_manager.py
# try:
#     import llama_cpp_binaries as llama_cpp
# except:
# try:
#     import llama_cpp_cuda as llama_cpp
# except:
#     import llama_cpp
import gc
from queue import Queue
from threading import Thread
import traceback
import state


class SingletonMeta(type):
    _instances = {}

    def __call__(cls, *args, **kwargs):
        if cls not in cls._instances:
            instance = super().__call__(*args, **kwargs)
            cls._instances[cls] = instance
        return cls._instances[cls]

    def release_instance(cls):
        if cls in cls._instances:
            del cls._instances[cls]

################################################################# LLM From oobabooga
import base64
import io
import json
import os
import pprint
import re
import socket
import subprocess
import sys
import threading
import time
from pathlib import Path

import llama_cpp_binaries
import requests
from PIL import Image

import ai_conversation_binary_shared as shared

llamacpp_valid_cache_types = {"fp16", "q8_0", "q4_0"}

# VL 모델의 mmproj 파일 경로 매핑 (모델명 -> mmproj 경로)
MMPROJ_PATH = {
    'Qwen3VL-8B-Instruct-Q4_K_M.gguf': './model/mmproj-Qwen3VL-8B-Instruct-Q8_0.gguf',
    'Qwen3VL-8B-Thinking-Q4_K_M.gguf': './model/mmproj-Qwen3VL-8B-Thinking-Q8_0.gguf',
    'Qwen3VL-30B-A3B-Instruct-Q4_K_M.gguf': './model/mmproj-Qwen3VL-30B-A3B-Instruct-Q8_0.gguf',
    # 다른 VL 모델 추가 가능
    # 'model-name.gguf': './model/model-name-mmproj.gguf',
}
class LlamaBinaryServer(metaclass=SingletonMeta):
    def __init__(self, model_path='', server_path=None, mmproj_path=None):
        """
        Initialize and start a server for llama.cpp models.
        """
        self.model_path = model_path
        self.server_path = server_path
        self.mmproj_path = mmproj_path
        self.port = self._find_available_port()
        self.process = None
        self.session = requests.Session()
        self.vocabulary_size = None
        self.bos_token = "<s>"
        self.last_prompt_token_count = 0

        # singletone chk
        self.initialized = False

        # Start the server
        if self.model_path:
            self._start_server()

    def encode(self, text, add_bos_token=False, **kwargs):
        if self.bos_token and text.startswith(self.bos_token):
            add_bos_token = False

        url = f"http://127.0.0.1:{self.port}/tokenize"
        payload = {
            "content": text,
            "add_special": add_bos_token,
        }

        response = self.session.post(url, json=payload)
        result = response.json()
        return result.get("tokens", [])

    def decode(self, token_ids, **kwargs):
        url = f"http://127.0.0.1:{self.port}/detokenize"
        payload = {
            "tokens": token_ids,
        }

        response = self.session.post(url, json=payload)
        result = response.json()
        return result.get("content", "")
    
    def from_pretrained(self, path, mmproj_path=None):
        self.model_path = path
        self.mmproj_path = mmproj_path
        if self.model_path:
            self.initialized = True
            self._start_server()

    def prepare_payload(self, state):
        # payload = {
        #     "temperature": state["temperature"] if not state["dynamic_temperature"] else (state["dynatemp_low"] + state["dynatemp_high"]) / 2,
        #     "dynatemp_range": 0 if not state["dynamic_temperature"] else (state["dynatemp_high"] - state["dynatemp_low"]) / 2,
        #     "dynatemp_exponent": state["dynatemp_exponent"],
        #     "top_k": state["top_k"],
        #     "top_p": state["top_p"],
        #     "min_p": state["min_p"],
        #     "tfs_z": state["tfs"],
        #     "typical_p": state["typical_p"],
        #     "repeat_penalty": state["repetition_penalty"],
        #     "repeat_last_n": state["repetition_penalty_range"],
        #     "presence_penalty": state["presence_penalty"],
        #     "frequency_penalty": state["frequency_penalty"],
        #     "dry_multiplier": state["dry_multiplier"],
        #     "dry_base": state["dry_base"],
        #     "dry_allowed_length": state["dry_allowed_length"],
        #     "dry_penalty_last_n": state["repetition_penalty_range"],
        #     "xtc_probability": state["xtc_probability"],
        #     "xtc_threshold": state["xtc_threshold"],
        #     "mirostat": state["mirostat_mode"],
        #     "mirostat_tau": state["mirostat_tau"],
        #     "mirostat_eta": state["mirostat_eta"],
        #     "grammar": state["grammar_string"],
        #     "seed": state["seed"],
        #     "ignore_eos": state["ban_eos_token"],
        # }

        # # DRY
        # dry_sequence_breakers = state['dry_sequence_breakers']
        # if not dry_sequence_breakers.startswith("["):
        #     dry_sequence_breakers = "[" + dry_sequence_breakers + "]"

        # dry_sequence_breakers = json.loads(dry_sequence_breakers)
        # payload["dry_sequence_breakers"] = dry_sequence_breakers

        # # Sampler order
        # if state["sampler_priority"]:
        #     samplers = state["sampler_priority"]
        #     samplers = samplers.split("\n") if isinstance(samplers, str) else samplers
        #     filtered_samplers = []

        #     penalty_found = False
        #     for s in samplers:
        #         if s.strip() in ["dry", "top_k", "typ_p", "top_p", "min_p", "xtc", "temperature"]:
        #             filtered_samplers.append(s.strip())
        #         elif not penalty_found and s.strip() == "repetition_penalty":
        #             filtered_samplers.append("penalties")
        #             penalty_found = True

        #     # Move temperature to the end if temperature_last is true and temperature exists in the list
        #     if state["temperature_last"] and "temperature" in samplers:
        #         samplers.remove("temperature")
        #         samplers.append("temperature")

        #     payload["samplers"] = filtered_samplers

        # if state['custom_token_bans']:
        #     to_ban = [[int(token_id), False] for token_id in state['custom_token_bans'].split(',')]
        #     payload["logit_bias"] = to_ban

        # 대화용 파라미터 설정 = Qwen3 No Think+VL
        payload = {
            "temperature": 1,  # 텍스트 생성의 무작위성(높을수록 다양, 낮을수록 보수적)
            "dynatemp_range": 0,  # 동적 temperature 적용 범위(0이면 비활성화)
            "dynatemp_exponent": 1,  # 동적 temperature의 지수 조정값
            "top_k": 0,  # 다음 토큰 후보 중 상위 k개만 고려(0이면 비활성화)
            "top_p": 1,  # 누적 확률 p 이하까지 누적 후보만 고려(1이면 비활성화)
            "min_p": 0.05,  # 후보 토큰 중 최소 확률 임계값(이하 제외)
            "tfs_z": 1,  # TFS(Top Free Sampling) 샘플링 관련 파라미터
            "typical_p": 1,  # typical decoding 관련 파라미터(1이면 비활성화)
            "repeat_penalty": 1,  # 반복 단어/문장 생성 억제 강도(1이면 비활성화)
            "repeat_last_n": 1024,  # 반복 억제 적용 시 최근 n개 토큰만 고려
            "presence_penalty": 0,  # 이미 등장한 단어의 등장 확률을 낮추는 정도
            "frequency_penalty": 0,  # 자주 등장한 단어의 확률을 낮추는 정도
            "dry_multiplier": 0,  # dry 샘플링의 가중치 계수
            "dry_base": 1.75,  # dry 샘플링의 기본값
            "dry_allowed_length": 2,  # dry 샘플링이 허용되는 최소 길이
            "dry_penalty_last_n": 1024,  # dry 샘플링 패널티 적용 시 최근 n개 토큰만 고려
            "xtc_probability": 0,  # XTC(확장 토큰 제어) 샘플링 확률
            "xtc_threshold": 0.1,  # XTC 샘플링 임계값
            "mirostat": 0,  # Mirostat 샘플링 알고리즘 사용 여부(0=비활성화)
            "mirostat_tau": 5,  # Mirostat의 목표 엔트로피
            "mirostat_eta": 0.1,  # Mirostat의 학습률
            "grammar": "",  # 문법 제약 규칙(비어있으면 미사용)
            "seed": -1,  # 랜덤 시드(-1이면 무작위)
            "ignore_eos": False,  # EOS(문장 끝) 토큰 무시 여부
            "dry_sequence_breakers": ['\n', ':', '"', '*'],  # dry 샘플링에서 시퀀스 분리자로 사용하는 문자열 리스트
            "samplers": ['penalties', 'dry', 'temperature', 'top_k', 'top_p', 'min_p', 'xtc']  # 사용되는 샘플링 기법들의 리스트
        }


        return payload

    def prepare_payload2(self):
        """VL(Vision Language) 모델 전용 payload"""
        payload = {
            "temperature": 0.7,
            "dynatemp_range": 0,
            "dynatemp_exponent": 1,
            "top_k": 20,
            "top_p": 0.8,
            "min_p": 0,
            "top_n_sigma": -1,
            "typical_p": 1,
            "repeat_penalty": 1,
            "repeat_last_n": 1024,
            "presence_penalty": 0,
            "frequency_penalty": 0,
            "dry_multiplier": 0,
            "dry_base": 1.75,
            "dry_allowed_length": 2,
            "dry_penalty_last_n": 1024,
            "xtc_probability": 0,
            "xtc_threshold": 0.1,
            "mirostat": 0,
            "mirostat_tau": 5,
            "mirostat_eta": 0.1,
            "grammar": "",
            "seed": -1,
            "ignore_eos": False,
            "dry_sequence_breakers": ['\n', ':', '"', '*'],
            "samplers": ['penalties', 'dry', 'top_n_sigma', 'temperature', 'top_k', 'top_p', 'typ_p', 'min_p', 'xtc']
        }
        return payload

    def _process_images_for_generation(self, state: dict):
        """여러 소스에서 이미지를 처리하고 base64 리스트 반환"""
        base64_images = []
        
        # 직접 전달된 base64 이미지
        if 'image_data' in state and state['image_data']:
            base64_images.append(state['image_data'])
        
        # PIL 이미지 리스트
        elif 'pil_images' in state and state['pil_images']:
            for img in state['pil_images']:
                base64_images.append(self._pil_to_base64(img))
        
        # 파일 경로 리스트
        elif 'image_paths' in state and state['image_paths']:
            for path in state['image_paths']:
                img = Image.open(path)
                if img.mode != 'RGB':
                    img = img.convert('RGB')
                base64_images.append(self._pil_to_base64(img))
        
        return base64_images
    
    def _pil_to_base64(self, image):
        """PIL 이미지를 base64로 변환"""
        buffered = io.BytesIO()
        image.save(buffered, format="PNG")
        return base64.b64encode(buffered.getvalue()).decode('utf-8')
    
    def is_multimodal(self) -> bool:
        """멀티모달 모델 여부 확인"""
        return self.mmproj_path not in [None, 'None', '']

    def generate_with_streaming(self, prompt, state = dict(), callback=None, image_data=None):
        state = update_to_default_state(state)
         
        url = f"http://127.0.0.1:{self.port}/completion"
        
        base64_images = []

        # 이미지 처리 (기존 image_data 파라미터 우선, 없으면 state에서 처리)
        if image_data:
            # 기존 방식: 직접 전달된 image_data 사용
            state['image_data'] = image_data
        
        base64_images = self._process_images_for_generation(state)
        
        # VL 모델 여부에 따라 payload 선택
        if base64_images:
            # VL 모델: prepare_payload2 사용
            payload = self.prepare_payload2()
        else:
            # 텍스트 전용: prepare_payload 사용
            payload = self.prepare_payload(state)
        
        # VL 모델: 이미지가 있으면 multimodal 방식으로 프롬프트 구성
        if base64_images:
            # 토큰 수 추정 (텍스트 + 이미지 토큰)
            IMAGE_TOKEN_COST_ESTIMATE = 2000  # A safe, conservative estimate per image
            
            # prompt가 이미 chat template 형식인지 확인
            if '<|im_start|>' in prompt and '<|im_end|>' in prompt:
                # 이미 완성된 chat template: 마지막 user 메시지에 <__media__> 삽입
                # 패턴: ...user\n{질문}<|im_end|>\n<|im_start|>assistant → ...user\n<__media__>\n\n{질문}<|im_end|>\n<|im_start|>assistant
                
                # 마지막 <|im_start|>user 위치 찾기
                last_user_start = prompt.rfind('<|im_start|>user\n')
                if last_user_start != -1:
                    # user 태그 이후부터 시작
                    before_user = prompt[:last_user_start + len('<|im_start|>user\n')]
                    after_user_start = prompt[last_user_start + len('<|im_start|>user\n'):]
                    
                    # <__media__> 플레이스홀더 삽입
                    prompt_string = before_user + '<__media__>\n\n' + after_user_start
                    
                    print(f"### VL 프롬프트 (chat template 유지): 이미지 플레이스홀더 삽입 완료")
                else:
                    # user 태그를 찾을 수 없는 경우: 프롬프트 그대로 사용
                    prompt_string = prompt
                    print(f"### VL 프롬프트 (chat template 유지): user 태그 없음, 프롬프트 그대로 사용")
            else:
                # raw text: 새로운 chat template 생성
                # attachment.md 기준: <|im_start|>user\n<__media__>\n\n{질문}<|im_end|>\n<|im_start|>assistant\n
                prompt_string = (
                    f"<|im_start|>user\n"
                    f"<__media__>\n\n"  # 이미지 위치 마커
                    f"{prompt}\n"
                    f"<|im_end|>\n"
                    f"<|im_start|>assistant\n"
                )
                print(f"### VL 프롬프트 (raw text): chat template 새로 생성")
            
            # 디버깅: 실제 전달되는 프롬프트 출력
            if state.get('DEV_MODE', False):
                print("=" * 80)
                print("VL MODEL PROMPT STRING:")
                print(prompt_string[:500] + "..." if len(prompt_string) > 500 else prompt_string)
                print("=" * 80)
                print(f"Number of images: {len(base64_images)}")
                print("=" * 80)
            
            text_tokens = self.encode(prompt_string)
            self.last_prompt_token_count = len(text_tokens) + (len(base64_images) * IMAGE_TOKEN_COST_ESTIMATE)
            
            if state['auto_max_new_tokens']:
                max_new_tokens = state['truncation_length'] - self.last_prompt_token_count
                # VL 모드에서는 최소 토큰 수 보장 (이미지 토큰 비용이 커서 응답이 짧아지는 것 방지)
                MIN_VL_RESPONSE_TOKENS = 4096  # 최소 응답 토큰 수 갱신
                if max_new_tokens < MIN_VL_RESPONSE_TOKENS:
                    print(f"### Warning: auto_max_new_tokens resulted in only {max_new_tokens} tokens for VL response")
                    print(f"### Forcing minimum {MIN_VL_RESPONSE_TOKENS} tokens for proper response generation")
                    max_new_tokens = MIN_VL_RESPONSE_TOKENS
            else:
                max_new_tokens = state['max_new_tokens']
            
            print(f"### VL generation: max_new_tokens={max_new_tokens}, prompt_tokens={self.last_prompt_token_count}")
            
            # llama.cpp 표준 multimodal 방식 (attachment.md 기준)
            payload.update({
                "prompt": {
                    "prompt_string": prompt_string,
                    "multimodal_data": base64_images
                },
                "n_predict": max_new_tokens,
                "stream": True,
                "cache_prompt": True
            })
        else:
            # 텍스트만 있을 때는 기존 방식 (토큰화)
            token_ids = self.encode(prompt)
            self.last_prompt_token_count = len(token_ids)
            
            if state['auto_max_new_tokens']:
                max_new_tokens = state['truncation_length'] - len(token_ids)
                # 텍스트 전용 모드에서도 최소 토큰 수 보장 (메모리가 많을 때 답변이 짧아지는 것 방지)
                MIN_TEXT_RESPONSE_TOKENS = 4096  # 최소 응답 토큰 수
                if max_new_tokens < MIN_TEXT_RESPONSE_TOKENS:
                    print(f"### Warning: auto_max_new_tokens resulted in only {max_new_tokens} tokens for text response")
                    print(f"### Forcing minimum {MIN_TEXT_RESPONSE_TOKENS} tokens for proper response generation")
                    max_new_tokens = MIN_TEXT_RESPONSE_TOKENS
            else:
                max_new_tokens = state['max_new_tokens']
            
            print(f"### Text generation: max_new_tokens={max_new_tokens}, prompt_tokens={self.last_prompt_token_count}")

            payload.update({
                "prompt": token_ids,
                "n_predict": max_new_tokens,
                "stream": True,
                "cache_prompt": True
            })

        if shared.args.verbose:
            printable_payload = {k: v for k, v in payload.items() if k != "prompt"}
            pprint.PrettyPrinter(indent=4, sort_dicts=False).pprint(printable_payload)
            print()

        # 요청 전송 (context manager 사용)
        try:
            with self.session.post(url, json=payload, stream=True, timeout=300) as response:
                # 에러 체크
                if response.status_code == 400:
                    try:
                        error_data = response.json()
                        if error_data.get("error", {}).get("type") == "exceed_context_size_error":
                            raise RuntimeError("컨텍스트 크기 초과: ctx_size를 늘려주세요")
                    except (json.JSONDecodeError, KeyError):
                        pass
                
                response.raise_for_status()
                
                full_text = ""

                # Process the streaming response
                for line in response.iter_lines():
                    if shared.stop_everything:
                        break

                    if not line:
                        continue

                    try:
                        line = line.decode('utf-8')

                        # Check if the line starts with "data: " and remove it
                        if line.startswith('data: '):
                            line = line[6:]  # Remove the "data: " prefix

                        # Parse the JSON data
                        data = json.loads(line)

                        # Extract the token content
                        if data.get('content', ''):
                            full_text += data['content']
                            yield full_text

                        # Check if generation is complete
                        if data.get('stop', False):
                            break

                    except json.JSONDecodeError as e:
                        # Log the error and the problematic line
                        print(f"JSON decode error: {e}")
                        print(f"Problematic line: {line}")
                        continue
        
        except requests.exceptions.HTTPError as e:
            print(f"HTTP 에러: {e.response.status_code}")
            print(f"응답: {e.response.text}")
            raise
        except Exception as e:
            print(f"요청 실패: {e}")
            raise

    def generate(self, prompt, state=None, callback=None):
        gc.collect()
        output = ""
        for output in self.generate_with_streaming(prompt, state):
            if callback:
                callback(output)

        return output

    def get_logits(self, prompt, state, n_probs=128, use_samplers=False):
        """Get the logits/probabilities for the next token after a prompt"""
        url = f"http://127.0.0.1:{self.port}/completion"

        payload = self.prepare_payload(state)
        payload.update({
            "prompt": self.encode(prompt, add_bos_token=state["add_bos_token"]),
            "n_predict": 0,
            "logprobs": True,
            "n_probs": n_probs,
            "stream": False,
            "post_sampling_probs": use_samplers,
        })

        if shared.args.verbose and use_samplers:
            printable_payload = {k: v for k, v in payload.items() if k != "prompt"}
            pprint.PrettyPrinter(indent=4, sort_dicts=False).pprint(printable_payload)
            print()

        response = self.session.post(url, json=payload)
        result = response.json()

        if "completion_probabilities" in result:
            if use_samplers:
                return result["completion_probabilities"][0]["top_probs"]
            else:
                return result["completion_probabilities"][0]["top_logprobs"]
        else:
            raise Exception(f"Unexpected response format: 'completion_probabilities' not found in {result}")

    def _get_vocabulary_size(self):
        """Get and store the model's maximum context length."""
        url = f"http://127.0.0.1:{self.port}/v1/models"
        response = self.session.get(url).json()

        if "data" in response and len(response["data"]) > 0:
            model_info = response["data"][0]
            if "meta" in model_info and "n_vocab" in model_info["meta"]:
                self.vocabulary_size = model_info["meta"]["n_vocab"]

    def _get_bos_token(self):
        """Get and store the model's BOS token."""
        url = f"http://127.0.0.1:{self.port}/props"
        response = self.session.get(url).json()
        if "bos_token" in response:
            self.bos_token = response["bos_token"]

    def _find_available_port(self):
        """Find an available port by letting the OS assign one."""
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            s.bind(('', 0))  # Bind to port 0 to get an available port
            return s.getsockname()[1]

    def _start_server(self):
        """Start the llama.cpp server and wait until it's ready."""
        # Determine the server path
        if self.server_path is None:
            self.server_path = llama_cpp_binaries.get_binary_path()

        n_gpu_layers = state.n_gpu_layers

        # Build the command
        cmd = [
            self.server_path,
            "--model", str(self.model_path),
            "--ctx-size", str(shared.args.ctx_size),
            "--gpu-layers", str(n_gpu_layers),
            "--batch-size", str(shared.args.batch_size),
            "--port", str(self.port),
            "--no-webui",
        ]
        
        # VL 모델: mmproj 파일 추가
        if self.mmproj_path:
            cmd += ["--mmproj", str(self.mmproj_path)]

        if shared.args.flash_attn:
            cmd += ["--flash-attn", "on"]
        if shared.args.threads > 0:
            cmd += ["--threads", str(shared.args.threads)]
        if shared.args.threads_batch > 0:
            cmd += ["--threads-batch", str(shared.args.threads_batch)]
        if shared.args.no_mmap:
            cmd.append("--no-mmap")
        if shared.args.mlock:
            cmd.append("--mlock")
        if shared.args.tensor_split:
            cmd += ["--tensor-split", shared.args.tensor_split]
        if shared.args.numa:
            cmd += ["--numa", "distribute"]
        if shared.args.no_kv_offload:
            cmd.append("--no-kv-offload")
        if shared.args.row_split:
            cmd += ["--split-mode", "row"]
        if shared.args.cache_type != "fp16" and shared.args.cache_type in llamacpp_valid_cache_types:
            cmd += ["--cache-type-k", shared.args.cache_type, "--cache-type-v", shared.args.cache_type]
        if shared.args.compress_pos_emb != 1:
            cmd += ["--rope-freq-scale", str(1.0 / shared.args.compress_pos_emb)]
        if shared.args.rope_freq_base > 0:
            cmd += ["--rope-freq-base", str(shared.args.rope_freq_base)]
        if shared.args.model_draft not in [None, 'None']:
            path = Path(shared.args.model_draft)
            if not path.exists():
                path = Path(f'{shared.args.model_dir}/{shared.args.model_draft}')

            if path.is_file():
                model_file = path
            else:
                model_file = sorted(Path(f'{shared.args.model_dir}/{shared.args.model_draft}').glob('*.gguf'))[0]

            cmd += ["--model-draft", model_file]
            if shared.args.draft_max > 0:
                cmd += ["--draft-max", str(shared.args.draft_max)]
            if shared.args.gpu_layers_draft > 0:
                cmd += ["--gpu-layers-draft", str(shared.args.gpu_layers_draft)]
            if shared.args.device_draft:
                cmd += ["--device-draft", shared.args.device_draft]
            if shared.args.ctx_size_draft > 0:
                cmd += ["--ctx-size-draft", str(shared.args.ctx_size_draft)]
        if shared.args.streaming_llm:
            cmd += ["--cache-reuse", "1"]
        if shared.args.extra_flags:
            # Clean up the input
            extra_flags = shared.args.extra_flags.strip()
            if extra_flags.startswith('"') and extra_flags.endswith('"'):
                extra_flags = extra_flags[1:-1].strip()
            elif extra_flags.startswith("'") and extra_flags.endswith("'"):
                extra_flags = extra_flags[1:-1].strip()

            for flag_item in extra_flags.split(','):
                if '=' in flag_item:
                    flag, value = flag_item.split('=', 1)
                    cmd += [f"--{flag}", value]
                else:
                    cmd.append(f"--{flag_item}")

        env = os.environ.copy()
        if os.name == 'posix':
            current_path = env.get('LD_LIBRARY_PATH', '')
            if current_path:
                env['LD_LIBRARY_PATH'] = f"{current_path}:{os.path.dirname(self.server_path)}"
            else:
                env['LD_LIBRARY_PATH'] = os.path.dirname(self.server_path)

        if shared.args.verbose:
            print(' '.join(str(item) for item in cmd[1:]))
            print()

        # Start the server with pipes for output
        print('###process_cmd', cmd)
        self.process = subprocess.Popen(
            cmd,
            stderr=subprocess.PIPE,
            bufsize=0,
            env=env
        )

        threading.Thread(target=filter_stderr_with_progress, args=(self.process.stderr,), daemon=True).start()

        # Wait for server to be healthy
        health_url = f"http://127.0.0.1:{self.port}/health"
        while True:
            # Check if process is still alive
            if self.process.poll() is not None:
                # Process has terminated
                exit_code = self.process.poll()
                raise RuntimeError(f"Server process terminated unexpectedly with exit code: {exit_code}")

            try:
                response = self.session.get(health_url)
                if response.status_code == 200:
                    break
            except:
                pass

            time.sleep(1)

        # Server is now healthy, get model info
        self._get_vocabulary_size()
        self._get_bos_token()
        return self.port

    def __enter__(self):
        """Support for context manager."""
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """Support for context manager."""
        self.stop()

    def __del__(self):
        """Cleanup when the object is deleted."""
        self.stop()

    def stop(self):
        """Stop the server process."""
        if self.process:
            self.process.terminate()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.kill()

            self.process = None
                
    def generate_web(self, *args, **kwargs):
        gc.collect()
        with Iteratorize(self.generate, args, kwargs, callback=None) as generator:
            reply = ''
            for reply in generator:
                yield reply
                
    # def generate_web(self, prompt, state):
    #     gc.collect()
    #     reply = ""
    #     for token in self.generate_with_streaming(prompt, state):
    #         reply += token
    #         yield reply
    #     # return reply

# 비어있는 state 값에 default값 넣기
def update_to_default_state(state):
    default_state = {
        # Sampling parameters
        'temperature': 0.6,                # 창의성 조절 (1=기본, 높을수록 무작위성 증가)  # qwen3 추천값
        'dynatemp_low': 1,                 # 동적 온도 최소값
        'dynatemp_high': 1,                # 동적 온도 최대값
        'dynatemp_exponent': 1,            # 동적 온도 적용 곡률
        'smoothing_factor': 0,             # 확률 스무딩 정도
        'smoothing_curve': 1,             # 스무딩 커브 형태
        'min_p': 0.05,                     # 최소 확률 필터링 (안정성 필터)
        'top_p': 1,                        # nucleus sampling (상위 확률 p% 선택)
        'top_k': 0,                        # top-k sampling (상위 k개만 고려)
        'typical_p': 1,                   # typical sampling 사용 여부
        'xtc_threshold': 0.1,              # XTC 샘플링 임계값
        'xtc_probability': 0,              # XTC 샘플링 확률
        'epsilon_cutoff': 0,               # 극소 확률 제거
        'eta_cutoff': 0,                   # 에타 기반 확률 컷오프
        'tfs': 1,                          # Tail Free Sampling 비율
        'top_a': 0,                        # Top-a (기울기 기반 top sampling)
        'top_n_sigma': 0,                  # sigma 기준 top-n 필터링

        # 반복 억제
        'repetition_penalty': 1,           # 반복 페널티 (1 = 없음)
        'frequency_penalty': 0,            # 동일 단어 빈도 억제
        'presence_penalty': 0,             # 단어 존재 자체에 대한 억제
        'encoder_repetition_penalty': 1,   # 인코더 반복 페널티
        'no_repeat_ngram_size': 0,         # n-gram 반복 금지 크기
        'repetition_penalty_range': 1024,  # 반복 억제 적용 범위 (토큰 수)
        'penalty_alpha': 0,                # 반복 억제 강도 스케일

        # 제어 및 샘플링 보조
        'guidance_scale': 1,               # Classifier-Free Guidance 스케일
        'mirostat_mode': 0,                # Mirostat 샘플링 모드 (0=off)
        'mirostat_tau': 5,                 # Mirostat 타깃 entropy
        'mirostat_eta': 0.1,               # Mirostat 학습률

        # 출력 제한
        'max_new_tokens': 4096,             # 생성할 최대 토큰 수
        'prompt_lookup_num_tokens': 0,     # 프롬프트 히스토리 검색 수
        'max_tokens_second': 0,            # 초당 생성 토큰 제한 (0 = 무제한)
        'max_updates_second': 12,          # 초당 업데이트 수 제한

        # 샘플링 옵션
        'do_sample': True,                 # 샘플링 여부 (False면 그리디)
        'dynamic_temperature': False,      # 동적 온도 적용 여부
        'temperature_last': False,         # 마지막 샘플에 온도 적용 여부

        # 기타 플래그
        'auto_max_new_tokens': True,       # 자동 토큰 수 조절
        'ban_eos_token': False,            # 종료 토큰 금지
        'add_bos_token': True,             # 시작 토큰 추가
        'enable_thinking': False,          # <think> 블록 사용 여부
        'skip_special_tokens': True,       # 특수 토큰 생략
        'stream': True,                    # 스트리밍 출력 사용
        'static_cache': False,             # 캐시 고정 여부

        # 입력 제한
        'truncation_length': 32768,         # 입력 최대 길이 (토큰 수)

        # 시드 설정
        'seed': -1,                        # 랜덤 시드 (-1은 랜덤)

        # 샘플러 우선순위
        'sampler_priority': '''repetition_penalty
        presence_penalty
        frequency_penalty
        dry
        temperature
        dynamic_temperature
        quadratic_sampling
        top_n_sigma
        top_k
        top_p
        typical_p
        epsilon_cutoff
        eta_cutoff
        tfs
        top_a
        min_p
        mirostat
        xtc
        encoder_repetition_penalty
        no_repeat_ngram''',  #

        # 사용자 지정 필터 및 문자열
        'custom_stopping_strings': '',     # 사용자 정의 종료 문자열
        'custom_token_bans': '',           # 토큰 금지 목록
        'negative_prompt': '',             # 부정 프롬프트

        # Dry run 제어 (AI가 말하지 않도록 조절)
        'dry_multiplier': 0,               # dry 조건 가중치
        'dry_allowed_length': 2,           # dry 허용 길이
        'dry_base': 1.75,                  # dry 기준값
        'dry_sequence_breakers': '"\\n", ":", "\\"", "*"',  # dry 시퀀스 구분자

        # 문법 및 대화 설정
        'grammar_string': '',              # 사용자 문법 제약
        'history': {'internal': [], 'visible': []},  # 대화 기록
        'search_chat': 'state',            # 검색용 태그
        'unique_id': '20250504-22-30-38',  # 고유 세션 ID
        'textbox': 'state',                # 입력 박스 상태
        'start_with': '',                  # 시작 텍스트
        'mode': 'instruct',                # instruct 모드 (명령 기반)
        'chat_style': 'cai-chat',          # 대화 스타일
        'chat-instruct_command': 'Continue the chat dialogue below. Write a single reply for the character "<|character|>".\n\n<|prompt|>',  # 명령 템플릿

        # 캐릭터 설정
        'character_menu': 'Assistant',     # 선택된 캐릭터
        'name2': 'AI',                     # AI 이름
        'name1': 'You',                    # 사용자 이름

        # 시스템 메시지
        'context': 'The following is a conversation with an AI Large Language Model. The AI has been trained to answer questions, provide recommendations, and help with decision making. The AI follows user requests. The AI thinks outside the box.',
        'greeting': 'How can I help you today?',  # 시작 인사
        'user_bio': '',                            # 사용자 정보
        'custom_system_message': '',               # 시스템 메시지 커스터마이징

        # 템플릿 관련 (생략 가능)
        'instruction_template_str': '...',         # 프롬프트 템플릿
        'chat_template_str': '...',                # 대화 템플릿

        # 인터페이스 구성
        'textbox-default': 'Common sense questions and answers\n\nQuestion: \nFactual answer:',
        'textbox-notebook': 'Common sense questions and answers\n\nQuestion: \nFactual answer:',
        'prompt_menu-default': 'QA',
        'prompt_menu-notebook': 'QA',
        'output_textbox': '',

        # 실행 환경 설정
        'filter_by_loader': 'llama.cpp',
        'loader': 'llama.cpp',
        'cpu_memory': 0,
        'n_gpu_layers': 37,
        'threads': 0,
        'threads_batch': 0,
        'batch_size': 256,
        'hqq_backend': 'PYTORCH_COMPILE',
        'ctx_size': 16384,
        'cache_type': 'fp16',
        'tensor_split': '',
        'extra_flags': '',
        'streaming_llm': False,

        # 하드웨어 설정
        'gpu_split': '',
        'alpha_value': 1,
        'rope_freq_base': 1000000,
        'compress_pos_emb': 1,
        'compute_dtype': 'float16',
        'quant_type': 'nf4',
        'num_experts_per_token': 2,
        'load_in_8bit': False,
        'load_in_4bit': False,
        'torch_compile': False,
        'flash_attn': False,
        'use_flash_attention_2': False,
        'cpu': False,
        'disk': False,
        'row_split': False,
        'no_kv_offload': False,
        'no_mmap': False,
        'mlock': False,
        'numa': False,
        'use_double_quant': False,
        'use_eager_attention': False,
        'bf16': False,
        'autosplit': False,
        'enable_tp': False,
        'no_flash_attn': False,
        'no_xformers': False,
        'no_sdpa': False,
        'cfg_cache': False,
        'cpp_runner': False,
        'trust_remote_code': False,
        'no_use_fast': False,

        # 드래프트 모델 설정
        'model_draft': None,
        'draft_max': 4,
        'gpu_layers_draft': 256,
        'device_draft': '',
        'ctx_size_draft': 0,
    }

    updated_state = {**default_state, **state}
    return updated_state

def filter_stderr_with_progress(process_stderr):
    """
    stderr를 읽고 프로그레스를 인라인으로 표시
    """
    progress_re = re.compile(r'slot update_slots: id.*progress = (\d+\.\d+)')
    last_was_progress = False
    
    try:
        buffer = b""
        while True:
            chunk = process_stderr.read(4096)  # 청크 단위 읽기
            if not chunk:
                break
            
            buffer += chunk
            
            while b'\n' in buffer:
                line_bytes, buffer = buffer.split(b'\n', 1)
                try:
                    line = line_bytes.decode('utf-8', errors='replace').strip('\r\n')
                    if line:
                        match = progress_re.search(line)
                        
                        if match:
                            progress = float(match.group(1))
                            
                            # 프로그레스 라인 추출
                            prompt_idx = line.find('prompt processing')
                            if prompt_idx != -1:
                                display_line = line[prompt_idx:]
                            else:
                                display_line = line
                            
                            # 진행 중이면 \r, 완료면 \n
                            end_char = '\r' if progress < 1.0 else '\n'
                            print(display_line, end=end_char, file=sys.stderr, flush=True)
                            last_was_progress = (progress < 1.0)
                        
                        # 노이즈 필터링
                        elif not (line.startswith(('srv ', 'slot ')) or 'log_server_r: request: GET /health' in line):
                            if last_was_progress:
                                print(file=sys.stderr)
                            
                            print(line, file=sys.stderr, flush=True)
                            last_was_progress = False
                
                except Exception:
                    continue
    
    except (ValueError, IOError):
        pass
    finally:
        try:
            process_stderr.close()
        except:
            pass

class Iteratorize:
    """
    Transforms a function that takes a callback
    into a lazy iterator (generator).

    Adapted from: https://stackoverflow.com/a/9969000
    """

    def __init__(self, func, args=None, kwargs=None, callback=None):
        self.mfunc = func
        self.c_callback = callback
        self.q = Queue()
        self.sentinel = object()
        self.args = args or []
        self.kwargs = kwargs or {}
        self.stop_now = False

        def _callback(val):
            if self.stop_now: # or shared.stop_everything:
                raise StopNowException
            self.q.put(val)

        def gentask():
            try:
                ret = self.mfunc(callback=_callback, *args, **self.kwargs)
            except StopNowException:
                pass
            except:
                traceback.print_exc()
                pass

            # clear_torch_cache()
            self.q.put(self.sentinel)
            if self.c_callback:
                self.c_callback(ret)

        self.thread = Thread(target=gentask)
        self.thread.start()

    def __iter__(self):
        return self

    def __next__(self):
        obj = self.q.get(True, None)
        if obj is self.sentinel:
            raise StopIteration
        else:
            return obj

    def __del__(self):
        pass
        # clear_torch_cache()

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.stop_now = True
        # clear_torch_cache()

class StopNowException(Exception):
    pass

def check_llm():
    try:
        llm = LlamaBinaryServer()
        return llm.initialized
    except:
        return False

# VL 기능이 필요할 때 사용할 기본 VL 모델
DEFAULT_VL_MODEL = 'Qwen3VL-8B-Instruct-Q4_K_M.gguf'

def get_llm(model_name='example:KKwen3-14B-Q4_K_M.gguf', mmproj_path=None, require_vl=False):
    llm = LlamaBinaryServer()
    
    # VL 모델이 필요한데 현재 모델이 VL 모델이 아닌 경우 → 자동 전환
    if require_vl:
        current_model = state.model_name
        is_current_vl = current_model in MMPROJ_PATH if current_model else False
        
        if not is_current_vl:
            print(f"### [get_llm] VL 모델 필요! 현재 모델 '{current_model}'은(는) VL 모델이 아닙니다.")
            print(f"### [get_llm] VL 모델 '{DEFAULT_VL_MODEL}'로 자동 전환 중...")
            
            # 기존 모델 release
            if llm.initialized:
                print(f"### [get_llm] 기존 모델 release...")
                llm.stop()
                llm.initialized = False
                llm.process = None
            
            # VL 모델로 전환
            state.model_name = DEFAULT_VL_MODEL
            mmproj_path = MMPROJ_PATH.get(DEFAULT_VL_MODEL)
            print(f"### [get_llm] state.model_name을 '{DEFAULT_VL_MODEL}'로 변경")
    
    # mmproj_path가 명시적으로 전달되지 않았으면 모델명으로 자동 판단
    if mmproj_path is None and state.model_name:
        mmproj_path = MMPROJ_PATH.get(state.model_name)
        if mmproj_path:
            if os.path.exists(mmproj_path):
                print(f"### Auto-detected VL model: {state.model_name}")
                print(f"### Using mmproj: {mmproj_path}")
            else:
                print(f"### WARNING: mmproj file not found: {mmproj_path}")
                print(f"### Model will start in text-only mode")
                mmproj_path = None
    
    # 초기화 안 됐거나 프로세스가 죽었으면 재로드
    needs_reload = (
        not llm.initialized or 
        llm.process is None or 
        llm.process.poll() is not None  # 프로세스 종료 체크
    )
    
    # 모델 변경 감지: state.model_name이 현재 로드된 모델과 다르면 재로드
    if llm.initialized and state.model_name:
        current_loaded_model = os.path.basename(llm.model_path) if llm.model_path else None
        if current_loaded_model != state.model_name:
            print(f"### [get_llm] 모델 변경 감지: {current_loaded_model} -> {state.model_name}")
            print(f"### [get_llm] 모델 재로드 중...")
            llm.stop()
            llm.initialized = False
            llm.process = None
            needs_reload = True
            # 새 모델의 mmproj 경로 재확인
            mmproj_path = MMPROJ_PATH.get(state.model_name)
    
    # mmproj 변경 감지: 기존과 다른 mmproj가 요청되면 재로드
    if llm.initialized and llm.mmproj_path != mmproj_path:
        print(f"### mmproj changed: {llm.mmproj_path} -> {mmproj_path}")
        print(f"### Reloading model with new mmproj...")
        needs_reload = True
        
    if needs_reload:
        if state.model_name:  # 이미 세팅한 모델이 있을 경우 그걸로 로딩
            llm.from_pretrained('./model/'+state.model_name, mmproj_path=mmproj_path)
        else:   
            try:
                # LlamaBinaryServer - 기본 VL 모델로 로드
                state.model_name = DEFAULT_VL_MODEL
                mmproj_path = MMPROJ_PATH.get(DEFAULT_VL_MODEL)
                llm.from_pretrained('./model/' + DEFAULT_VL_MODEL, mmproj_path=mmproj_path)
            except:
                # LlamaCppModel
                # llm.from_pretrained('./model/Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf')
                # llm.from_pretrained('./model/DeepSeek-R1-Distill-Qwen-7B-Q4_K_M.gguf')
                # llm.from_pretrained('./model/DeepSeek-R1-Distill-Qwen-14B-Q4_K_M.gguf')
                # llm.from_pretrained('./model/DeepSeek-R1-Distill-Llama-8B-Q4_K_M.gguf')
                # llm.from_pretrained('./model/Qwen2.5-7B-Instruct-1M-Q4_K_M.gguf')
                # llm.from_pretrained('./model/Qwen2.5-14B-Instruct-1M-Q4_K_M.gguf')
                # llm.from_pretrained('./model/gemma-3-12b-it-Q4_K_M.gguf')
                llm.from_pretrained('./model/google_gemma-3-27b-it-Q4_K_M.gguf', mmproj_path=mmproj_path)
                # llm.from_pretrained('./model/huihui-ai.DeepSeek-R1-Distill-Qwen-14B-abliterated-v2.Q4_K_M.gguf')
                # llm.from_pretrained('./model/gemma-2-2b-jpn-it.Q4_K_M.gguf')
                # llm.from_pretrained('./model/Qwen2-7B-Multilingual-RP.Q4_K_M.gguf')
                # llm.from_pretrained('./model/EXAONE-3.0-7.8B-Instruct-Q5_K_L.gguf')
                # llm.from_pretrained('./model/Qwen2.5-7B-Instruct-Q4_K_M.gguf') 
                # llm.from_pretrained('./model/Qwen2.5-14B-Instruct-Q4_K_M.gguf')     
    return llm

def release():        
    llm = LlamaBinaryServer()
    if llm.initialized:
        llm.stop()
        llm.initialized = False  # 플래그 초기화
        llm.process = None  # 프로세스 참조 제거
        LlamaBinaryServer.release_instance()  # Singleton 인스턴스 제거

################################################################ Vision
import os
from PIL import Image
from transformers import AutoProcessor, AutoModelForCausalLM 

from unittest.mock import patch
from transformers.dynamic_module_utils import get_imports

# VisionModel 싱글톤 클래스
class VisionModel(metaclass=SingletonMeta):
    def __init__(self):
        self.model = None
        self.initialized = False

    def __del__(self):
        del self.model

    def from_pretrained(self, model_name):
        with patch("transformers.dynamic_module_utils.get_imports", self.fixed_get_imports):  # Workaround for unnecessary flash_attn requirement
            self.model = AutoModelForCausalLM.from_pretrained(
                            model_name, 
                            trust_remote_code=True, 
                            cache_dir='./model/', 
                            local_files_only=False,
                            attn_implementation='eager'  # SDPA 체크 우회
                        )
        self.initialized = True

    @staticmethod
    def fixed_get_imports(filename):
        if not str(filename).endswith("modeling_florence2.py"):
            return get_imports(filename)
        imports = get_imports(filename)
        if "flash_attn" in imports:
            imports.remove("flash_attn")
        return imports

    def release(self):
        self.model = None
        self.initialized = False

# VisionProcessor 싱글톤 클래스
class VisionProcessor(metaclass=SingletonMeta):
    def __init__(self):
        self.processor = None
        self.initialized = False

    def __del__(self):
        del self.processor

    def from_pretrained(self, processor_name):
        try:
            self.processor = AutoProcessor.from_pretrained(processor_name, trust_remote_code=True, cache_dir='./model/', local_files_only=True)
        except OSError:
            # 로컬 파일이 없으면 온라인에서 다운로드
            self.processor = AutoProcessor.from_pretrained(processor_name, trust_remote_code=True, cache_dir='./model/')
        self.initialized = True

    def release(self):
        self.processor = None
        self.initialized = False

# getter 함수
def get_vision_model():
    vision_model = VisionModel()
    if not vision_model.initialized:
        vision_model.from_pretrained("microsoft/Florence-2-base")
        # vision_model.from_pretrained("microsoft/Florence-2-large")
    return vision_model

def get_vision_processor():
    vision_processor = VisionProcessor()
    if not vision_processor.initialized:
        vision_processor.from_pretrained("microsoft/Florence-2-base")
    return vision_processor

# 리소스 해제 함수
def release_vision_resources():
    vision_model = VisionModel()
    if vision_model.initialized:
        vision_model.release()
        VisionModel.release_instance()

    vision_processor = VisionProcessor()
    if vision_processor.initialized:
        vision_processor.release()
        VisionProcessor.release_instance()


################################################################ OCR (PaddleOCR 싱글톤)
# OCRInstance 싱글톤 클래스
class OCRInstance(metaclass=SingletonMeta):
    def __init__(self):
        self.ocr = None
        self.initialized = False
        self.device_type = None
        self.current_config = {'origin_lang': None, 'is_sentence': None}  # 현재 설정 저장
    
    def __del__(self):
        self.release()
    
    def from_pretrained(self, device='auto', origin_lang='ja'):
        """
        OCR 인스턴스 초기화
        
        Args:
            device: 'gpu', 'cpu', 'auto', 또는 None (자동)
            origin_lang: OCR 대상 언어 (기본: 'ja' 일본어)
        """
        try:
            # Lazy import to avoid circular import issues
            from paddleOCR import PPOCRv5
            
            print("[AI_SINGLETON] PaddleOCR 초기화 중...")
            
            if device is None or device == 'auto':
                device = 'auto'
            
            self.ocr = PPOCRv5(
                use_gpu=(device != 'cpu'), 
                device=device,
                origin_lang=origin_lang,
                is_sentence=False  # 키워드 탐지용이므로 단어 단위
            )
            self.device_type = self.ocr.device_type
            self.initialized = True
            self.current_config = {'origin_lang': origin_lang, 'is_sentence': False}  # 설정 저장
            
            print(f"[AI_SINGLETON] PaddleOCR 초기화 완료! (Device: {self.device_type}, Lang: {origin_lang})")
            
        except ImportError as e:
            print(f"[AI_SINGLETON] PaddleOCR 라이브러리 없음: {e}")
            self.initialized = False
        except Exception as e:
            print(f"[AI_SINGLETON] PaddleOCR 초기화 실패: {e}")
            
            # GPU 모드에서 실패 시 CPU로 폴백 시도
            if device != 'cpu':
                print("[AI_SINGLETON] GPU 모드 실패, CPU 모드로 재시도 중...")
                try:
                    from paddleOCR import PPOCRv5
                    
                    self.ocr = PPOCRv5(
                        use_gpu=False, 
                        device='cpu',
                        origin_lang=origin_lang,
                        is_sentence=False
                    )
                    self.device_type = self.ocr.device_type
                    self.initialized = True
                    self.current_config = {'origin_lang': origin_lang, 'is_sentence': False}  # 설정 저장
                    
                    print(f"[AI_SINGLETON] PaddleOCR CPU 모드 초기화 완료! (Device: {self.device_type}, Lang: {origin_lang})")
                    return
                    
                except Exception as cpu_e:
                    print(f"[AI_SINGLETON] PaddleOCR CPU 폴백도 실패: {cpu_e}")
            
            self.initialized = False
    
    def reload(self, device=None):
        """OCR 모델 리로드 (GPU/CPU 전환)"""
        old_device = self.device_type
        self.release()
        gc.collect()
        
        try:
            import paddle
            paddle.device.cuda.empty_cache()
        except:
            pass
        
        self.from_pretrained(device)
        
        return {
            'old_device': old_device,
            'new_device': self.device_type,
            'status': 'success' if self.initialized else 'failed'
        }
    
    def update_config(self, **kwargs):
        """OCR 설정 업데이트 (동일 설정이면 스킵)"""
        # OCR이 초기화 안 됐으면 먼저 초기화 시도
        if not self.initialized or self.ocr is None:
            print("[AI_SINGLETON] OCR 초기화 안됨, update_config 전 초기화 시도...")
            self.from_pretrained()
        
        # 현재 설정과 동일하면 스킵
        if self.current_config:
            is_same = True
            for key, value in kwargs.items():
                if self.current_config.get(key) != value:
                    is_same = False
                    break
            
            if is_same:
                # 동일 설정이면 스킵
                return
        
        if self.ocr and hasattr(self.ocr, 'update_config'):
            print(f"[AI_SINGLETON] OCR config 업데이트: {kwargs}")
            self.ocr.update_config(**kwargs)
            # 설정 업데이트 반영
            for key, value in kwargs.items():
                self.current_config[key] = value
        else:
            print(f"[AI_SINGLETON] OCR update_config 실패: ocr={self.ocr}, initialized={self.initialized}")
    
    def process_image(self, image_path, **kwargs):
        """이미지 OCR 처리"""
        if not self.initialized or self.ocr is None:
            return None
        return self.ocr.process_image(image_path, **kwargs)
    
    def release(self):
        """리소스 해제"""
        if self.ocr is not None:
            print("[AI_SINGLETON] PaddleOCR 리소스 해제")
            self.ocr = None
        self.initialized = False
        self.device_type = None


def get_ocr(device='auto'):
    """OCR 인스턴스 가져오기 (싱글톤)"""
    ocr_instance = OCRInstance()
    if not ocr_instance.initialized:
        ocr_instance.from_pretrained(device)
    return ocr_instance


def release_ocr():
    """OCR 리소스 해제"""
    ocr_instance = OCRInstance()
    if ocr_instance.initialized:
        ocr_instance.release()
        OCRInstance.release_instance()
        gc.collect()
        print("[AI_SINGLETON] OCR 리소스 완전 해제")

        
if __name__ == "__main__":
    # Model Loading Test
    state.set_use_gpu_percent(8)  # 8 = GPU 100%
    # state.model_name='Qwen2.5-14B-Instruct-1M-Q4_K_M.gguf'
    # state.model_name='Qwen3-0.6B-Q4_K_M.gguf'  # 29 / 1.5GB
    # state.model_name='Qwen3-1.7B-Q4_K_M.gguf'  # 29 / 2.2GB
    # state.model_name='Qwen3-4B-Q4_K_M.gguf'  # 37 / 3.8GB
    # state.model_name='Qwen3-8B-Q4_K_M.gguf'  # 37 / 6GB
    # state.model_name='Qwen3-14B-Q4_K_M.gguf'  # 41 / 9.8GB
    # state.model_name='Qwen3-32B-Q4_K_M.gguf'  # 65 / 20.8GB
    get_llm()
