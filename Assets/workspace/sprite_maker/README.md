# sprite_maker

캐릭터 PNG 1장 → Unity 2D 애니메이션 자산 파이프라인.
생성·투명화는 ComfyUI 워크플로, Unity 자산화는 파이썬 스크립트 2개.

## 👉 사용법 & 현황: **[HowTo.md](HowTo.md)**

```
comfyui/   ← ComfyUI 워크플로 (Wan2.2, 생성+투명화)
scripts/   ← media_to_frames.py (솎기·명명), make_unity_assets.py (.anim/.meta 생성)
HowTo.md   ← 사용법·현황·할 수 있는 것
CLAUDE.md  ← Unity 자산 내부 규칙(개발 참고)
```
