#!/usr/bin/env python3
"""media_to_frames.py — GIF/APNG/WebP(애니) / 영상 → 프레임 PNG 분해.

파이프라인 ③단계: 생성된 액션 GIF/영상을 프레임으로 자른다.
- 애니메이션 GIF/APNG/WebP: Pillow 로 프레임별 표시 시간(ms)까지 읽어
  `NN_<ms>.png` 로 저장 → make_unity_assets.py 의 `--from-name` 에 직결.
- 영상(mp4/mov/webm): imageio[ffmpeg] 가 있으면 target fps 로 샘플링, 없으면 안내.

스프라이트용 핵심:
- ComfyUI I2V 는 보통 5s·32fps ≈ 160프레임을 뱉음 → 스프라이트 루프엔 과다.
  `--max-frames K` 또는 `--every N` 으로 솎는다. 솎을 때 버린 프레임의 시간을
  남는 프레임에 합산해 **동작 속도(총 길이)를 유지**한다.
- FLF2V/FFLF(첫=끝) 결과는 `--drop-last` 로 마지막 중복 프레임을 버려 Unity 루프 이음새를 깔끔히.

기본 출력 폴더는 입력 파일 stem 과 동일(예: walk.gif → walk/).
이후: media_to_frames → remove_bg → make_unity_assets build --from-name
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

ANIM_EXT = {".gif", ".apng", ".png", ".webp"}
VIDEO_EXT = {".mp4", ".mov", ".webm", ".mkv", ".avi"}


def read_anim(src: Path, default_ms: int):
    """애니메이션 이미지 → [(RGBA Image, ms), ...] (저장 안 함)."""
    from PIL import Image, ImageSequence

    im = Image.open(src)
    if not getattr(im, "is_animated", False) and getattr(im, "n_frames", 1) <= 1:
        sys.exit(f"애니메이션 프레임이 1개뿐입니다(정지 이미지?): {src}")
    out = []
    for frame in ImageSequence.Iterator(im):
        ms = int(frame.info.get("duration", 0) or 0) or default_ms
        out.append((frame.convert("RGBA"), ms))
    return out


def read_dir(src: Path, fps: float):
    """PNG 프레임 폴더(ComfyUI SaveImage 출력 등) → [(RGBA Image, ms), ...].
    이름순(자연정렬) 정렬, 각 프레임 ms = 1000/fps. .meta/NN_ 산출물은 제외."""
    from PIL import Image
    import re as _re

    def natkey(p):
        return [int(t) if t.isdigit() else t.lower()
                for t in _re.split(r"(\d+)", p.name)]
    pngs = [p for p in src.iterdir()
            if p.suffix.lower() == ".png" and not p.name.endswith(".meta")]
    if not pngs:
        sys.exit(f"폴더에 PNG 가 없습니다: {src}")
    pngs.sort(key=natkey)
    ms = round(1000.0 / fps)
    return [(Image.open(p).convert("RGBA"), ms) for p in pngs]


def read_video(src: Path, target_fps: float):
    """영상 → target_fps 로 균일 샘플링한 [(RGBA Image, ms), ...]. imageio[ffmpeg] 필요."""
    try:
        import imageio.v3 as iio
    except Exception:
        sys.exit(
            "영상 분해에는 imageio[ffmpeg] 가 필요합니다.\n"
            "  pip install \"imageio[ffmpeg]\"\n"
            "또는 영상을 먼저 GIF 로 변환해 넣으세요.")
    from PIL import Image

    try:
        native = float(iio.immeta(src, plugin="pyav").get("fps", 0)) or 0.0
    except Exception:
        native = 0.0
    stride = max(1, round(native / target_fps)) if native > 0 else 1
    ms = round(1000.0 / target_fps)
    out = []
    for i, frame in enumerate(iio.imiter(src)):
        if i % stride:
            continue
        out.append((Image.fromarray(frame).convert("RGBA"), ms))
    if native <= 0:  # fps 미상 → 추출된 그대로, ms 는 target 기준
        pass
    return out


def decimate(frames, every: int | None, max_frames: int | None):
    """프레임을 솎되, 버린 프레임의 ms 를 남는 프레임에 합산(속도 유지)."""
    n = len(frames)
    if max_frames and max_frames > 0 and n > max_frames:
        # 균일 간격으로 max_frames 개 인덱스 선택
        keep = sorted({round(i * (n - 1) / (max_frames - 1)) for i in range(max_frames)}) \
            if max_frames > 1 else [0]
    elif every and every > 1:
        keep = list(range(0, n, every))
    else:
        return frames
    keep_set = set(keep)
    out = []
    cur = None
    for i, (img, ms) in enumerate(frames):
        if i in keep_set:
            cur = [img, ms]
            out.append(cur)
        elif cur is not None:
            cur[1] += ms  # 버린 프레임 시간은 직전(유지 중인) keep 프레임에 누적
        # keep 이전의 선행 스킵(드묾)은 첫 keep 에 흡수되도록 무시
    return [(img, ms) for img, ms in out]


def main():
    ap = argparse.ArgumentParser(
        description="GIF/APNG/WebP/영상 → 프레임 PNG (NN_<ms>.png)")
    ap.add_argument("input", help="입력 GIF/APNG/WebP 또는 영상 파일")
    ap.add_argument("--out", help="출력 폴더 (기본: 입력 파일 stem)")
    ap.add_argument("--default-ms", type=int, default=100,
                    help="duration 정보가 없을 때 프레임당 ms (기본 100)")
    ap.add_argument("--fps", type=float, default=12.0,
                    help="영상 분해 시 샘플링 fps (기본 12)")
    ap.add_argument("--every", type=int, default=None,
                    help="N 프레임마다 1장만 유지(솎기). 버린 시간은 합산.")
    ap.add_argument("--max-frames", type=int, default=None,
                    help="최종 프레임 수를 약 K개로 균일 솎기(스프라이트용 권장 6~16).")
    ap.add_argument("--drop-last", action="store_true",
                    help="마지막 프레임 버리기(FLF2V/FFLF 첫=끝 루프 중복 제거).")
    ap.add_argument("--snap", type=int, default=None,
                    help="프레임 ms 를 N 의 배수로 스냅(예: 10).")
    args = ap.parse_args()

    src = Path(args.input).resolve()
    if src.is_dir():
        # PNG 시퀀스 폴더 입력 (ComfyUI SaveImage 출력 등)
        frames = read_dir(src, args.fps)
        # 출력은 깨끗한 별도 폴더로(원본 PNG 와 섞이지 않게)
        out = Path(args.out).resolve() if args.out else src.with_name(src.name + "_frames")
    elif src.is_file():
        out = Path(args.out).resolve() if args.out else src.with_suffix("")
        ext = src.suffix.lower()
        if ext in VIDEO_EXT:
            frames = read_video(src, args.fps)
        elif ext in ANIM_EXT:
            frames = read_anim(src, args.default_ms)
        else:
            sys.exit(f"지원하지 않는 확장자: {ext}")
    else:
        sys.exit(f"입력 없음: {src}")
    if not frames:
        sys.exit("추출된 프레임이 없습니다.")

    raw_n = len(frames)
    frames = decimate(frames, args.every, args.max_frames)
    if args.drop_last and len(frames) > 1:
        frames = frames[:-1]
    if args.snap:
        frames = [(img, max(args.snap, round(ms / args.snap) * args.snap))
                  for img, ms in frames]

    out.mkdir(parents=True, exist_ok=True)
    durs = []
    for i, (img, ms) in enumerate(frames, start=1):
        img.save(out / f"{i:02d}_{ms}.png", "PNG")
        durs.append(ms)

    total = sum(durs) / 1000.0
    print(f"[ok] 추출 {raw_n} → 최종 {len(frames)} 프레임 → {out}")
    print(f"[ok] 총 길이 {total:.3f}s, 프레임 ms: {durs}")
    print(f"[next] Unity화: python scripts/make_unity_assets.py build "
          f"\"{out}\" --from-name")


if __name__ == "__main__":
    main()
