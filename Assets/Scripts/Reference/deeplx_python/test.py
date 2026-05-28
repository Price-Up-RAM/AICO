#!/usr/bin/env python3
"""
DeepLX Python 테스트 스크립트

이 스크립트는 번역 기능을 테스트합니다.
"""

import sys
import os

# 현재 디렉토리를 Python 경로에 추가
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    from translate import translate, translate_by_deeplx, DeepLXTranslationResult
    from constants import SUPPORTED_LANGUAGES
except ImportError as e:
    print(f"모듈 import 오류: {e}")
    print("필요한 패키지를 설치해주세요: pip install -r requirements.txt")
    sys.exit(1)


def test_basic_translation():
    """기본 번역 테스트"""
    print("=== 기본 번역 테스트 ===")
    
    test_cases = [
        ("Hello world", "KO"),
        ("안녕하세요", "EN"), 
        ("This is a test", "JA"),
        ("Python programming", "ZH"),
        ("How are you?", "FR"),
    ]
    
    for text, target_lang in test_cases:
        try:
            print(f"원문: {text}")
            print(f"대상 언어: {target_lang}")
            
            result = translate(text, target_lang)
            print(f"번역 결과: {result}")
            print("-" * 50)
            
        except Exception as e:
            print(f"번역 실패: {e}")
            print("-" * 50)


def test_detailed_translation():
    """상세 번역 결과 테스트"""
    print("\n=== 상세 번역 결과 테스트 ===")
    
    text = "Hello world! How are you today?"
    target_lang = "KO"
    
    try:
        result = translate_by_deeplx(None, target_lang, text)
        
        print(f"원문: {text}")
        print(f"대상 언어: {target_lang}")
        print(f"상태 코드: {result.code}")
        print(f"번역 결과: {result.data}")
        print(f"소스 언어: {result.source_lang}")
        print(f"메서드: {result.method}")
        
        if result.alternatives:
            print("대안 번역:")
            for i, alt in enumerate(result.alternatives, 1):
                print(f"  {i}. {alt}")
        
    except Exception as e:
        print(f"상세 번역 실패: {e}")


def test_multiline_translation():
    """여러 줄 번역 테스트"""
    print("\n=== 여러 줄 번역 테스트 ===")
    
    text = """Hello world!
This is a test.

How are you today?
Nice to meet you."""
    
    target_lang = "KO"
    
    try:
        result = translate(text, target_lang)
        print(f"원문:\n{text}")
        print(f"\n번역 결과:\n{result}")
        
    except Exception as e:
        print(f"여러 줄 번역 실패: {e}")


def test_language_detection():
    """언어 감지 테스트"""
    print("\n=== 언어 감지 테스트 ===")
    
    test_cases = [
        ("Hello world", "KO"),
        ("안녕하세요", "EN"),
        ("こんにちは", "EN"),
        ("Bonjour le monde", "KO"),
    ]
    
    for text, target_lang in test_cases:
        try:
            result = translate_by_deeplx(None, target_lang, text)  # 자동 감지
            print(f"원문: {text}")
            print(f"감지된 언어: {result.source_lang}")
            print(f"번역 결과: {result.data}")
            print("-" * 30)
            
        except Exception as e:
            print(f"언어 감지 테스트 실패: {e}")


def show_supported_languages():
    """지원 언어 목록 표시"""
    print("\n=== 지원 언어 목록 ===")
    
    for lang in SUPPORTED_LANGUAGES:
        print(f"{lang['code']}: {lang['language']}")


def main():
    """메인 테스트 함수"""
    print("DeepLX Python 번역 테스트를 시작합니다...")
    print("=" * 60)
    
    # 지원 언어 목록 표시
    show_supported_languages()
    
    # 각 테스트 실행
    test_basic_translation()
    test_detailed_translation() 
    test_multiline_translation()
    test_language_detection()
    
    print("\n테스트가 완료되었습니다!")


if __name__ == "__main__":
    main()
