#!/usr/bin/env python3
"""
DeepLX Python 사용 예제

이중 안전장치 시스템 (직접 API + Vercel 백업) 사용법 예제
"""

import sys
import os
import time

# 현재 디렉토리를 Python 경로에 추가
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    from translate import translate, translate_by_deeplx
    from vercel_client import translate_via_vercel
except ImportError as e:
    print(f"모듈 import 오류: {e}")
    print("필요한 패키지를 설치해주세요: pip install -r requirements.txt")
    sys.exit(1)


def basic_example():
    """기본 번역 예제 (이중 안전장치)"""
    print("=== 기본 번역 예제 (이중 안전장치) ===")
    
    test_cases = [
        ("Hello world", "KO"),
        ("Good morning", "KO"), 
        ("How are you?", "JA"),
        ("Thank you", "ES"),
        ("Python programming", "ZH"),
    ]
    
    for text, target_lang in test_cases:
        try:
            print(f"번역: {text} -> {target_lang}")
            
            # 이중 안전장치로 번역 (기본 동작)
            result = translate(text, target_lang)
            print(f"결과: {result}")
            print("-" * 40)
            
            time.sleep(1)  # API 제한 대비
            
        except Exception as e:
            print(f"번역 실패: {e}")
            print("-" * 40)


def detailed_example():
    """상세한 번역 결과 예제"""
    print("\n=== 상세한 번역 결과 예제 ===")
    
    try:
        text = "Good morning!"
        target_lang = "KO"
        
        # 상세한 번역 결과 가져오기
        result = translate_by_deeplx(None, target_lang, text)
        
        if result.code == 200:
            print(f"원문: {text}")
            print(f"번역: {result.data}")
            print(f"감지된 언어: {result.source_lang}")
            print(f"대상 언어: {result.target_lang}")
            print(f"방식: {result.method}")
            
            if result.alternatives:
                print("대안 번역:")
                for i, alt in enumerate(result.alternatives[:3], 1):  # 처음 3개만
                    print(f"  {i}. {alt}")
        else:
            print(f"번역 실패 (코드: {result.code}): {result.message}")
            
    except Exception as e:
        print(f"상세 번역 오류: {e}")


def backup_system_example():
    """백업 시스템 예제"""
    print("\n=== 백업 시스템 예제 ===")
    
    text = "Nice to meet you"
    target_lang = "KO"
    
    print("1. 이중 안전장치 (기본)")
    try:
        result1 = translate(text, target_lang, use_backup=True)
        print(f"결과: {result1}")
    except Exception as e:
        print(f"실패: {e}")
    
    print("\n2. 직접 API만 (백업 없음)")
    try:
        result2 = translate(text, target_lang, use_backup=False)
        print(f"결과: {result2}")
    except Exception as e:
        print(f"실패: {e}")
    
    print("\n3. Vercel 백업 서비스 단독")
    try:
        result3 = translate_via_vercel(text, target_lang)
        print(f"결과: {result3}")
    except Exception as e:
        print(f"실패: {e}")


def multilingual_example():
    """다국어 번역 예제"""
    print("\n=== 다국어 번역 예제 ===")
    
    text = "Have a great day!"
    languages = [
        ("KO", "한국어"),
        ("JA", "일본어"),
        ("ZH", "중국어"),
        ("FR", "프랑스어"),
        ("DE", "독일어"),
        ("ES", "스페인어")
    ]
    
    print(f"원문: {text}")
    print()
    
    for lang_code, lang_name in languages:
        try:
            result = translate(text, lang_code)
            print(f"{lang_name} ({lang_code}): {result}")
            time.sleep(0.8)  # API 제한 대비
        except Exception as e:
            print(f"{lang_name} ({lang_code}): 실패 - {e}")


def main():
    """메인 함수"""
    print("DeepLX Python 이중 안전장치 시스템 사용 예제")
    print("=" * 60)
    
    basic_example()
    detailed_example()
    backup_system_example()
    multilingual_example()
    
    print(f"\n{'='*60}")
    print("🎉 모든 예제 완료!")
    print("📖 더 많은 정보는 README.md를 참조하세요.")


if __name__ == "__main__":
    main()