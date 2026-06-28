#!/usr/bin/env python3
"""make_unity_assets.py — 프레임 PNG 폴더 → Unity 2D 애니메이션 자산 생성기.

AICO(Char2D) 폴더방식 레이아웃을 그대로 생성한다:
  <anim>/                     ← 애니메이션 폴더 (기준 PNG 이름과 동일)
    01_200.png  02_200.png …  ← 프레임 (선택적으로 표준 명명으로 리네임)
    01_200.png.meta …         ← 단일 스프라이트 TextureImporter 메타 (결정적 guid)
    <anim>.anim               ← AnimationClip (Size/Image 의 m_Sprite 를 시간별 교체)
    <anim>.anim.meta

이미지 픽셀을 읽지 않으므로(메타에 크기 정보 없음) 표준 라이브러리만 사용.
배경 투명화/시트 슬라이싱은 별도 단계(prep/slice) — 이 스크립트는 "프레임 → Unity 자산"에 집중.

CLAUDE.md §3 의 리버스 엔지니어링 규칙을 그대로 따른다.
"""
from __future__ import annotations

import argparse
import binascii
import hashlib
import re
import sys
from pathlib import Path

# --- Unity 상수 (관찰값, CLAUDE.md §3) ---------------------------------------
SPRITE_FILE_ID = 21300000          # 단일 스프라이트 sub-asset fileID (고정)
SINGLE_SPRITE_ID = "5e97eb03825dee720800000000000000"  # 단일모드 spriteID (고정)
IMAGE_SCRIPT_GUID = "fe87c0e1cc204ed48ad3b37840f39efc"  # UnityEngine.UI.Image
ATTR_M_SPRITE = 2015549526         # m_Sprite 어트리뷰트 해시
ATTR_SCALE = 3                     # Transform localScale 바인딩 어트리뷰트
TYPEID_IMAGE = 114                 # MonoBehaviour(Image)
TYPEID_TRANSFORM = 4               # Transform
CLIP_MAIN_FILE_ID = 7400000        # AnimationClip mainObjectFileID

# 기본 타깃 경로 = 2D_General 프리팹 계층 (2D_General → Size → Image)
DEFAULT_TARGET_PATH = "Size/Image"  # m_Sprite 가 바뀌는 UI Image
DEFAULT_SIZE_PATH = "Size"          # 클립 길이 확보용 스케일 커브 타깃

PIVOT_PRESETS = {  # (alignment, pivot_x, pivot_y)
    "center": (0, 0.5, 0.5),
    "bottom": (7, 0.5, 0.0),
    "top": (1, 0.5, 1.0),
}

SP = " "  # Unity 가 빈 스칼라 값 뒤에 남기는 후행 공백 (파일 일치용)


def _platform_block(target: str) -> str:
    """단일 스프라이트 meta 의 platformSettings 블록 1개 (관찰값과 동일)."""
    return (f"  - serializedVersion: 4\n"
            f"    buildTarget: {target}\n"
            f"    maxTextureSize: 2048\n"
            f"    resizeAlgorithm: 0\n"
            f"    textureFormat: -1\n"
            f"    textureCompression: 1\n"
            f"    compressionQuality: 50\n"
            f"    crunchedCompression: 0\n"
            f"    allowsAlphaSplitting: 0\n"
            f"    overridden: 0\n"
            f"    ignorePlatformSupport: 0\n"
            f"    androidETC2FallbackOverride: 0\n"
            f"    forceMaximumCompressionQuality_BC6H_BC7: 0\n")


# --- 유틸 --------------------------------------------------------------------
def crc32_path(path: str) -> int:
    """Unity 의 ClipBindingConstant path 해시 = crc32(path). (검증 완료)"""
    return binascii.crc32(path.encode("utf-8")) & 0xFFFFFFFF


def fmt_time(x: float) -> str:
    """Unity 스타일로 시간 포맷. 시간은 정수 ms 누적에서 오므로 최대 3자리 소수.
    불필요한 0 제거 (예: 0.3, 0.033)."""
    return "{:.7g}".format(round(x, 6))


def deterministic_guid(asset_abs_path: Path) -> str:
    """자산 경로 기반 결정적 32-hex guid.
    Unity Assets 루트 상대경로를 우선 사용(프로젝트 전역 유일 + 위치 독립).
    Assets 가 경로에 없으면 절대경로 사용."""
    parts = asset_abs_path.resolve().as_posix().split("/")
    if "Assets" in parts:
        i = parts.index("Assets")
        key = "/".join(parts[i:])
    else:
        key = asset_abs_path.resolve().as_posix()
    return hashlib.md5(("sprite_maker::" + key.lower()).encode("utf-8")).hexdigest()


# --- 프레임 수집 / 타이밍 ----------------------------------------------------
_NUM_RE = re.compile(r"(\d+)")


def natural_key(name: str):
    return [int(t) if t.isdigit() else t.lower() for t in _NUM_RE.split(name)]


def collect_frames(folder: Path):
    pngs = [p for p in folder.iterdir()
            if p.suffix.lower() == ".png" and not p.name.endswith(".meta")]
    pngs.sort(key=lambda p: natural_key(p.name))
    return pngs


def parse_suffix_ms(name: str):
    """`NN_<ms>.png` 에서 <ms> 추출. 실패 시 None."""
    m = re.match(r"^\d+_(\d+)\.png$", name, re.IGNORECASE)
    return int(m.group(1)) if m else None


def resolve_durations(frames, args):
    """프레임별 표시 시간(ms) 리스트 반환."""
    n = len(frames)
    if args.durations:
        vals = [int(x) for x in args.durations.split(",")]
        if len(vals) != n:
            sys.exit(f"--durations 개수({len(vals)})가 프레임 수({n})와 다릅니다.")
        return vals
    if args.from_name:
        out = []
        for p in frames:
            ms = parse_suffix_ms(p.name)
            if ms is None:
                sys.exit(f"--from-name: '{p.name}' 에서 _<ms> 를 못 읽었습니다.")
            out.append(ms)
        return out
    # 기본: 균일 fps
    ms = round(1000.0 / args.fps)
    return [ms] * n


# --- 메타/클립 빌더 ----------------------------------------------------------
def build_png_meta(guid: str, alignment: int, pivot_x: float, pivot_y: float) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: {alignment}
  spritePivot: {{x: {pivot_x}, y: {pivot_y}}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
{_platform_block('DefaultTexturePlatform')}{_platform_block('Standalone')}{_platform_block('Android')}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:{SP}
    physicsShape: []
    bones: []
    spriteID: {SINGLE_SPRITE_ID}
    internalID: 0
    vertices: []
    indices:{SP}
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:{SP}
  pSDRemoveMatte: 0
  userData:{SP}
  assetBundleName:{SP}
  assetBundleVariant:{SP}
"""


def _scale_curve_block(size_path: str, length: float) -> str:
    """Size 오브젝트 스케일 커브 (값 1 고정) — 클립 길이를 length 로 확정."""
    t_end = fmt_time(length)

    def key(t):
        return (f"      - serializedVersion: 3\n"
                f"        time: {t}\n"
                f"        value: {{x: 1, y: 1, z: 1}}\n"
                f"        inSlope: {{x: 0, y: 0, z: 0}}\n"
                f"        outSlope: {{x: 0, y: 0, z: 0}}\n"
                f"        tangentMode: 0\n"
                f"        weightedMode: 0\n"
                f"        inWeight: {{x: 0.33333334, y: 0.33333334, z: 0.33333334}}\n"
                f"        outWeight: {{x: 0.33333334, y: 0.33333334, z: 0.33333334}}\n")
    return (f"  - curve:\n"
            f"      serializedVersion: 2\n"
            f"      m_Curve:\n"
            f"{key('0')}"
            f"{key(t_end)}"
            f"      m_PreInfinity: 2\n"
            f"      m_PostInfinity: 2\n"
            f"      m_RotationOrder: 4\n"
            f"    path: {size_path}\n")


def _editor_scale_curve(size_path: str, length: float, axis: str) -> str:
    t_end = fmt_time(length)

    def key(t):
        return (f"      - serializedVersion: 3\n"
                f"        time: {t}\n"
                f"        value: 1\n"
                f"        inSlope: 0\n"
                f"        outSlope: 0\n"
                f"        tangentMode: 0\n"
                f"        weightedMode: 0\n"
                f"        inWeight: 0.33333334\n"
                f"        outWeight: 0.33333334\n")
    return (f"  - serializedVersion: 2\n"
            f"    curve:\n"
            f"      serializedVersion: 2\n"
            f"      m_Curve:\n"
            f"{key('0')}"
            f"{key(t_end)}"
            f"      m_PreInfinity: 2\n"
            f"      m_PostInfinity: 2\n"
            f"      m_RotationOrder: 4\n"
            f"    attribute: m_LocalScale.{axis}\n"
            f"    path: {size_path}\n"
            f"    classID: 224\n"
            f"    script: {{fileID: 0}}\n"
            f"    flags: 0\n")


def build_anim(name, frame_guids, times, length, sample_rate,
               target_path, size_path, loop):
    """AnimationClip YAML 생성."""
    # PPtr 커브 키
    pptr_keys = "".join(
        f"    - time: {fmt_time(t)}\n"
        f"      value: {{fileID: {SPRITE_FILE_ID}, guid: {g}, type: 3}}\n"
        for t, g in zip(times, frame_guids))
    # pptrCurveMapping = 커브 값과 1:1 (중복 포함, walk.anim 관찰)
    mapping = "".join(
        f"    - {{fileID: {SPRITE_FILE_ID}, guid: {g}, type: 3}}\n"
        for g in frame_guids)

    target_hash = crc32_path(target_path)
    size_hash = crc32_path(size_path)
    stop = fmt_time(length)
    loop_time = 1 if loop else 0

    scale_curves = _scale_curve_block(size_path, length)
    editor_curves = (_editor_scale_curve(size_path, length, "x")
                     + _editor_scale_curve(size_path, length, "y")
                     + _editor_scale_curve(size_path, length, "z"))

    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!74 &7400000
AnimationClip:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 7
  m_Legacy: 0
  m_Compressed: 0
  m_UseHighQualityCurve: 1
  m_RotationCurves: []
  m_CompressedRotationCurves: []
  m_EulerCurves: []
  m_PositionCurves: []
  m_ScaleCurves:
{scale_curves}  m_FloatCurves: []
  m_PPtrCurves:
  - serializedVersion: 2
    curve:
{pptr_keys}    attribute: m_Sprite
    path: {target_path}
    classID: {TYPEID_IMAGE}
    script: {{fileID: 11500000, guid: {IMAGE_SCRIPT_GUID}, type: 3}}
    flags: 2
  m_SampleRate: {sample_rate}
  m_WrapMode: 0
  m_Bounds:
    m_Center: {{x: 0, y: 0, z: 0}}
    m_Extent: {{x: 0, y: 0, z: 0}}
  m_ClipBindingConstant:
    genericBindings:
    - serializedVersion: 2
      path: {target_hash}
      attribute: {ATTR_M_SPRITE}
      script: {{fileID: 11500000, guid: {IMAGE_SCRIPT_GUID}, type: 3}}
      typeID: {TYPEID_IMAGE}
      customType: 0
      isPPtrCurve: 1
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    - serializedVersion: 2
      path: {size_hash}
      attribute: {ATTR_SCALE}
      script: {{fileID: 0}}
      typeID: {TYPEID_TRANSFORM}
      customType: 0
      isPPtrCurve: 0
      isIntCurve: 0
      isSerializeReferenceCurve: 0
    pptrCurveMapping:
{mapping}  m_AnimationClipSettings:
    serializedVersion: 2
    m_AdditiveReferencePoseClip: {{fileID: 0}}
    m_AdditiveReferencePoseTime: 0
    m_StartTime: 0
    m_StopTime: {stop}
    m_OrientationOffsetY: 0
    m_Level: 0
    m_CycleOffset: 0
    m_HasAdditiveReferencePose: 0
    m_LoopTime: {loop_time}
    m_LoopBlend: 0
    m_LoopBlendOrientation: 0
    m_LoopBlendPositionY: 0
    m_LoopBlendPositionXZ: 0
    m_KeepOriginalOrientation: 0
    m_KeepOriginalPositionY: 1
    m_KeepOriginalPositionXZ: 0
    m_HeightFromFeet: 0
    m_Mirror: 0
  m_EditorCurves:
{editor_curves}  m_EulerEditorCurves: []
  m_HasGenericRootTransform: 0
  m_HasMotionFloatCurves: 0
  m_Events: []
"""


def build_anim_meta(guid: str) -> str:
    return f"""fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: {CLIP_MAIN_FILE_ID}
  userData:{SP}
  assetBundleName:{SP}
  assetBundleVariant:{SP}
"""


# --- 메인 커맨드 -------------------------------------------------------------
def cmd_build(args):
    folder = Path(args.folder).resolve()
    if not folder.is_dir():
        sys.exit(f"폴더 없음: {folder}")
    name = args.name or folder.name
    frames = collect_frames(folder)
    if not frames:
        sys.exit(f"프레임 PNG 없음: {folder}")
    durations = resolve_durations(frames, args)

    # 표준 명명으로 리네임 (선택)
    if args.rename:
        renamed = []
        for i, (p, ms) in enumerate(zip(frames, durations), start=1):
            newname = f"{i:02d}_{ms}.png"
            target = p.with_name(newname)
            renamed.append(target)
        plan_rename = list(zip(frames, renamed))
    else:
        plan_rename = [(p, p) for p in frames]

    final_frames = [t for _, t in plan_rename]
    alignment, pivx, pivy = PIVOT_PRESETS[args.pivot]

    # 시간(초) 누적 — 정수 ms 로 누적해 부동소수 드리프트 방지
    times = []
    acc_ms = 0
    for ms in durations:
        times.append(acc_ms / 1000.0)
        acc_ms += ms
    length = acc_ms / 1000.0

    # 결정적 guid (최종 파일 경로 기준)
    frame_guids = [deterministic_guid(fp) for fp in final_frames]
    anim_path = folder / f"{name}.anim"
    anim_guid = deterministic_guid(anim_path)

    sample_rate = args.sample_rate

    # 산출 계획 출력
    print(f"[build] 폴더: {folder}")
    print(f"[build] 애니메이션: {name}  프레임 {len(frames)}개  길이 {length:.3f}s  "
          f"SampleRate {sample_rate}  loop={'on' if args.loop else 'off'}")
    print(f"[build] 타깃 경로: '{args.target_path}'  스케일 경로: '{args.size_path}'  "
          f"pivot={args.pivot}")
    for (src, dst), ms, t in zip(plan_rename, durations, times):
        arrow = "" if src == dst else f"  ← {src.name}"
        print(f"   {dst.name:<16} {ms:>5}ms  @ t={fmt_time(t)}{arrow}")

    if args.dry_run:
        print("[dry-run] 파일을 쓰지 않았습니다. 적용하려면 --dry-run 제거.")
        return

    # 1) 리네임
    for src, dst in plan_rename:
        if src != dst:
            if dst.exists() and not args.force:
                sys.exit(f"이미 존재: {dst} (덮어쓰려면 --force)")
            src.rename(dst)

    # 2) 프레임 .png.meta
    for fp, g in zip(final_frames, frame_guids):
        meta = fp.with_name(fp.name + ".meta")
        if meta.exists() and not args.force:
            print(f"   유지(존재): {meta.name}")
            continue
        meta.write_text(build_png_meta(g, alignment, pivx, pivy), encoding="utf-8")

    # 3) .anim + .anim.meta
    if anim_path.exists() and not args.force:
        sys.exit(f"이미 존재: {anim_path} (덮어쓰려면 --force)")
    anim_path.write_text(
        build_anim(name, frame_guids, times, length, sample_rate,
                   args.target_path, args.size_path, args.loop),
        encoding="utf-8")
    (folder / f"{name}.anim.meta").write_text(build_anim_meta(anim_guid),
                                              encoding="utf-8")

    print(f"[ok] 생성: {anim_path.name}, {name}.anim.meta, "
          f"{len(final_frames)}개 .png.meta")
    print("[ok] Unity 에디터에서 폴더를 재임포트하면 적용됩니다.")


def main():
    ap = argparse.ArgumentParser(
        description="프레임 PNG 폴더 → Unity 2D 애니메이션 자산 생성 (폴더방식)")
    sub = ap.add_subparsers(dest="cmd", required=True)

    b = sub.add_parser("build", help="프레임 폴더 → .anim + .png.meta + .anim.meta")
    b.add_argument("folder", help="프레임 PNG 들이 있는 폴더 (애니메이션 폴더)")
    b.add_argument("--name", help="애니메이션 이름 (기본: 폴더명)")
    # 타이밍
    b.add_argument("--fps", type=float, default=10.0,
                   help="균일 프레임레이트 (기본 10)")
    b.add_argument("--from-name", action="store_true",
                   help="파일명 'NN_<ms>.png' 의 <ms> 를 프레임 시간으로 사용")
    b.add_argument("--durations",
                   help="프레임별 ms 를 콤마로 직접 지정 (예: 1500,120,120,1000)")
    # 명명/형상
    b.add_argument("--rename", action="store_true",
                   help="프레임을 'NN_<ms>.png' 표준 명명으로 리네임")
    b.add_argument("--pivot", choices=list(PIVOT_PRESETS), default="center",
                   help="스프라이트 피벗 (기본 center; 바닥기준은 bottom)")
    # 타깃 경로 (프리팹 계층)
    b.add_argument("--target-path", default=DEFAULT_TARGET_PATH,
                   help=f"m_Sprite 타깃 경로 (기본 '{DEFAULT_TARGET_PATH}')")
    b.add_argument("--size-path", default=DEFAULT_SIZE_PATH,
                   help=f"클립 길이용 스케일 커브 경로 (기본 '{DEFAULT_SIZE_PATH}')")
    b.add_argument("--sample-rate", type=int, default=60,
                   help="m_SampleRate (기본 60)")
    b.add_argument("--no-loop", dest="loop", action="store_false",
                   help="LoopTime 끄기 (기본 켜짐)")
    b.add_argument("--dry-run", action="store_true",
                   help="계획만 출력하고 파일은 쓰지 않음")
    b.add_argument("--force", action="store_true",
                   help="기존 파일 덮어쓰기 허용")
    b.set_defaults(func=cmd_build, loop=True)

    args = ap.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
