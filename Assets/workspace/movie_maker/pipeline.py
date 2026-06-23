# -*- coding: utf-8 -*-
"""
movie_maker 파이프라인 (자기 위치 기준 동작)
  입력 : ./data/ep1_master.txt  (4줄 블록: ko / ja(한자) / ja(요미가나) / en, 화자에 선택적 [표정]태그)
         ./audio/NNN_<화자>.mp3 (요미가나 TTS. 없으면 edge-tts 프로토 자동생성)
  출력 : ./output/  (captions_greenscreen.mp4 / proto_{ko,en,ja}.srt / char_cue.csv / timing.csv
                    / narration.wav / proto.ass / preview.mp4)
사용 : venv\\Scripts\\python.exe final\\pipeline.py [N]      # N=앞 N줄(기본 8)
자세히는 HOWTO.md 참고.
"""
import asyncio, csv, io, os, subprocess, sys, re
import edge_tts
from pydub import AudioSegment
from PIL import ImageFont

ROOT = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(ROOT, "data", "ep1_master.txt")
AUD  = os.path.join(ROOT, "audio")
OUT  = os.path.join(ROOT, "output")
FONTS = os.path.join(OUT, "fonts")
os.makedirs(AUD, exist_ok=True); os.makedirs(OUT, exist_ok=True)

# ffmpeg (winget 전역 설치 경로) — 환경 바뀌면 이 두 줄만 수정
FFBIN = r"C:\Users\84884\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.1-full_build\bin"
FFMPEG, FFPROBE = os.path.join(FFBIN, "ffmpeg.exe"), os.path.join(FFBIN, "ffprobe.exe")
AudioSegment.converter, AudioSegment.ffprobe = FFMPEG, FFPROBE
os.environ["PATH"] = FFBIN + os.pathsep + os.environ["PATH"]

FONT_PATH = r"C:\Windows\Fonts\YuGothR.ttc"   # Yu Gothic (이 PC엔 meiryo 없음)
FONT_NAME = "Yu Gothic"
FONT_FILE = "YuGothR.ttc"

N_LINES = int(sys.argv[1]) if len(sys.argv) > 1 else 8
GAP = 0.35

# ── 레이아웃 config (1920x1080, 실측 기준) ──
W, H = 1920, 1080
FONT_SIZE = 58
SAFE_W = 1200          # 자막 한 줄 최대 픽셀폭(초과 시 줄바꿈)
MARGIN_V = 130         # 자막 하단 여백(px)
SIDE_MARGIN = 270      # 화자쪽 가장자리 여백
CHARBOX = {"arona": (0, 756, 250, 324), "plana": (1670, 734, 250, 346)}  # 미리보기 박스 x,y,w,h

VOICE = {  # 프로토 음성(실제 운영은 GUI TTS 음성을 ./audio 에 넣으면 재사용)
    "arona": dict(voice="ja-JP-NanamiNeural", rate="+0%", pitch="+0Hz"),
    "plana": dict(voice="ja-JP-NanamiNeural", rate="-3%", pitch="-12Hz"),
}

KINSOKU_HEAD = set("、。，．）」』】〕〉》’”！？・…ーっ")  # 일본어 행두 금지
_font = ImageFont.truetype(FONT_PATH, FONT_SIZE, index=0)

def wrap(text, lang):
    def width(s): return _font.getlength(s)
    lines, cur = [], ""
    if lang == "ja":
        for ch in text:
            if width(cur + ch) <= SAFE_W or not cur:
                cur += ch
            else:
                if ch in KINSOKU_HEAD:
                    cur += ch; lines.append(cur); cur = ""
                else:
                    lines.append(cur); cur = ch
        if cur: lines.append(cur)
    else:
        for word in text.split(" "):
            cand = (cur + " " + word).strip()
            if width(cand) <= SAFE_W or not cur:
                cur = cand
            else:
                lines.append(cur); cur = word
        if cur: lines.append(cur)
    return r"\N".join(lines)

def parse(path):
    blocks, cur = [], []
    for raw in io.open(path, encoding="utf-8"):
        s = raw.rstrip("\n")
        if not s.strip():
            if cur: blocks.append(cur); cur = []
            continue
        head, _, text = s.partition(":")
        m = re.match(r"\s*(\w+)\s*(?:\[(\w+)\])?\s*$", head)
        spk = m.group(1) if m else head.strip()
        expr = m.group(2) if (m and m.group(2)) else None
        cur.append((spk, expr, text.strip()))
    if cur: blocks.append(cur)
    rows = []
    for b in blocks:
        if len(b) < 4: continue
        expr = next((e for _, e, _ in b if e), "neutral")
        rows.append(dict(speaker=b[0][0], expr=expr,
                         ko=b[0][2], ja=b[1][2], yomi=b[2][2], en=b[3][2]))
    return rows

async def synth(rows):
    for i, r in enumerate(rows, 1):
        cfg = VOICE[r["speaker"]]
        out = os.path.join(AUD, f"{i:03d}_{r['speaker']}.mp3")
        r["audio"] = out
        if os.path.exists(out):       # 이미 있는 음성은 재사용(실제 TTS 포함)
            continue
        for attempt in range(3):
            try:
                await edge_tts.Communicate(r["yomi"], cfg["voice"], rate=cfg["rate"], pitch=cfg["pitch"]).save(out)
                break
            except Exception:
                if attempt == 2: raise

def dur(path):
    r = subprocess.run([FFPROBE, "-v", "error", "-show_entries", "format=duration",
                        "-of", "csv=p=0", path], capture_output=True, text=True)
    return float(r.stdout.strip())

def ass_ts(t):
    cs = int(round(t * 100)); h = cs // 360000; cs %= 360000
    m = cs // 6000; cs %= 6000; s = cs // 100; cs %= 100
    return f"{h}:{m:02d}:{s:02d}.{cs:02d}"

def srt_ts(t):
    ms = int(round(t * 1000)); h = ms // 3600000; ms %= 3600000
    m = ms // 60000; ms %= 60000; s = ms // 1000; ms %= 1000
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"

ASS_HEADER = f"""[Script Info]
ScriptType: v4.00+
PlayResX: {W}
PlayResY: {H}
WrapStyle: 2
ScaledBorderAndShadow: yes

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Arona,{FONT_NAME},{FONT_SIZE},&H00FFFFFF,&H000000FF,&H00FF3008,&H64000000,-1,0,0,0,100,100,0,0,1,4.5,2,1,{SIDE_MARGIN},40,{MARGIN_V},1
Style: Plana,{FONT_NAME},{FONT_SIZE},&H00FFFFFF,&H000000FF,&H000820FF,&H64000000,-1,0,0,0,100,100,0,0,1,4.5,2,3,40,{SIDE_MARGIN},{MARGIN_V},1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""

def build(rows):
    t = 0.0
    for r in rows:
        d = dur(r["audio"]); r["start"] = t; r["end"] = t + d; r["dur"] = d
        t += d + GAP
    total = t

    with io.open(os.path.join(OUT, "proto.ass"), "w", encoding="utf-8-sig") as f:
        f.write(ASS_HEADER)
        for r in rows:
            style = "Arona" if r["speaker"] == "arona" else "Plana"
            f.write(f"Dialogue: 0,{ass_ts(r['start'])},{ass_ts(r['end'])},{style},,0,0,0,,{wrap(r['ja'],'ja')}\n")

    for lang in ("ko", "en", "ja"):
        with io.open(os.path.join(OUT, f"proto_{lang}.srt"), "w", encoding="utf-8") as f:
            for i, r in enumerate(rows, 1):
                f.write(f"{i}\n{srt_ts(r['start'])} --> {srt_ts(r['end'])}\n{r[lang]}\n\n")

    with io.open(os.path.join(OUT, "char_cue.csv"), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f); w.writerow(["idx", "speaker", "expr", "start", "end", "png"])
        for i, r in enumerate(rows, 1):
            png = f"{r['speaker']}.png" if r["expr"] == "neutral" else f"{r['speaker']}_{r['expr']}.png"
            w.writerow([i, r["speaker"], r["expr"], f"{r['start']:.2f}", f"{r['end']:.2f}", png])

    with io.open(os.path.join(OUT, "timing.csv"), "w", encoding="utf-8-sig", newline="") as f:
        w = csv.writer(f); w.writerow(["idx", "speaker", "start", "end", "dur", "ja"])
        for i, r in enumerate(rows, 1):
            w.writerow([i, r["speaker"], f"{r['start']:.2f}", f"{r['end']:.2f}", f"{r['dur']:.2f}", r["ja"]])

    track = AudioSegment.silent(duration=0)
    for r in rows:
        track += AudioSegment.from_file(r["audio"]) + AudioSegment.silent(duration=int(GAP * 1000))
    track.export(os.path.join(OUT, "narration.wav"), format="wav")
    return total

def _ensure_font():
    os.makedirs(FONTS, exist_ok=True)
    dst = os.path.join(FONTS, FONT_FILE)
    if not os.path.exists(dst):
        import shutil; shutil.copy(FONT_PATH, dst)

def render(total):
    _ensure_font()
    ax, ay, aw, ah = CHARBOX["arona"]; px, py, pw, ph = CHARBOX["plana"]
    vf = (f"drawbox=x={ax}:y={ay}:w={aw}:h={ah}:color=0x3060FF@0.45:t=fill,"
          f"drawbox=x={px}:y={py}:w={pw}:h={ph}:color=0xFF3030@0.45:t=fill,"
          "ass=proto.ass:fontsdir=fonts")
    cmd = [FFMPEG, "-y", "-loglevel", "error", "-f", "lavfi",
           "-i", f"color=c=0x0E1424:s={W}x{H}:d={total:.2f}", "-i", "narration.wav",
           "-vf", vf, "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", "preview.mp4"]
    subprocess.run(cmd, cwd=OUT, check=True, capture_output=True, text=True)

def render_green(total):
    _ensure_font()
    cmd = [FFMPEG, "-y", "-loglevel", "error", "-f", "lavfi",
           "-i", f"color=c=0x00FF00:s={W}x{H}:r=30:d={total:.2f},format=rgb24",
           "-vf", "ass=proto.ass:fontsdir=fonts",
           "-c:v", "libx264", "-pix_fmt", "yuv420p", "captions_greenscreen.mp4"]
    subprocess.run(cmd, cwd=OUT, check=True, capture_output=True, text=True)

def main():
    rows = parse(DATA)[:N_LINES]
    print(f"lines: {len(rows)}")
    asyncio.run(synth(rows))
    total = build(rows)
    print(f"total: {total:.2f}s -> rendering")
    render(total)
    render_green(total)
    print(f"done -> {OUT}")

if __name__ == "__main__":
    main()
