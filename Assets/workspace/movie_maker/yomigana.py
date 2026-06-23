# -*- coding: utf-8 -*-
"""일본어(한자) -> 요미가나(읽기 가나) 변환 보조. 대본 3번째 줄 만들 때 참고용.
사용: venv\\Scripts\\python.exe final\\yomigana.py "日本語のテキスト"
인자가 없으면 샘플로 동작."""
import sys
import pykakasi

kks = pykakasi.kakasi()

def to_yomi(text):
    """전체 히라가나 읽기 (TTS 입력용)"""
    return "".join(b["hira"] for b in kks.convert(text))

def to_inline(text):
    """한자에만 (읽기) 괄호를 붙인 인라인 표기"""
    out = []
    for b in kks.convert(text):
        orig, hira = b["orig"], b["hira"]
        if orig != hira and any("一" <= c <= "鿿" for c in orig):
            out.append(f"{orig}({hira})")
        else:
            out.append(orig)
    return "".join(out)

if __name__ == "__main__":
    samples = sys.argv[1:] or ["生徒たちの大切な絆ストーリーを青輝石に変えています。"]
    for s in samples:
        print("원문    :", s)
        print("요미가나:", to_yomi(s))
        print("인라인  :", to_inline(s))
        print("-" * 50)
