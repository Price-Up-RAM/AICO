# -*- coding: utf-8 -*-
"""
요소별 "재생성(Regenerate)" 워크플로우 생성기  ->  decompose_regen.json

목적: elements.txt 의 요소마다 Qwen-Image-Edit 로 "흰 배경에 그 요소만" 추출/변형하고,
      **요소(레인)별로 따로 재생성**할 수 있게 한다. (원본 Gemini 툴의 Accept/Regenerate 등가)

재생성 UX (전체 말고 해당 부위만):
  1) 'Fast Groups Muter (rgthree)' 패널에서 재생성할 요소 그룹만 켜고 나머지 끔.
  2) Queue Prompt → 켜진 레인의 KSampler seed 가 randomize 되어 그 요소만 새 변형 N장.
  3) 나머지 레인은 뮤트(또는 입력 불변 → ComfyUI 캐시)라 재계산 안 됨.
  ※ seed 는 KSampler 노드마다 독립(레인별 변형 seed). 특정 레인만 randomize 하면 그 레인만 변함.

검증: Qwen 편집 파이프라인은 공식 템플릿의 서브그래프 배선을 평탄화한 것(스키마 확인 완료).
      VAEEncode 는 RGBA(4ch)를 못 받으므로 컷아웃이 아니라 원본 3ch 이미지를 입력으로 쓴다.
      (무겹침 투명 분리는 decompose_all.json 담당. 이 워크플로우는 생성형 변형 = 방식 A.)
"""
import json, os, sys

# ---- Qwen 모델 파일명 (download_models.ps1 의 WITH_QWEN 으로 받음) ----
UNET = "qwen_image_edit_2509_fp8_e4m3fn.safetensors"
CLIP = "qwen_2.5_vl_7b_fp8_scaled.safetensors"
VAE  = "qwen_image_vae.safetensors"
LORA = "Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors"

# 친화적 이름 -> 추출 프롬프트에 들어갈 설명구
PHRASES = {
    "hair": "hair (the full hairstyle)",
    "head": "head (hair and face)",
    "face": "face",
    "body": "bare body / skin (torso, arms, legs)",
    "skin": "bare body / skin",
    "top": "upper-body garment (jacket / shirt / top)",
    "upper": "upper-body garment",
    "shirt": "shirt", "jacket": "jacket",
    "bottom": "lower-body garment (pants / shorts / skirt)",
    "lower": "lower-body garment",
    "pants": "pants", "skirt": "skirt", "dress": "dress",
    "belt": "belt",
    "shoes": "pair of shoes", "footwear": "pair of shoes",
    "hat": "hat", "bag": "bag",
    "sunglasses": "glasses / goggles", "glasses": "glasses / goggles",
    "scarf": "scarf",
}

def prompt_for(name):
    phrase = PHRASES.get(name, name)
    return (f"Extract only the {phrase} from this character. "
            "Place it centered on a plain pure-white background. "
            "Remove the character and everything else. "
            "Preserve the original art style, colors, and lighting. "
            "Single object, product-shot style, no text, no shadow.")

HERE = os.path.dirname(os.path.abspath(__file__))

def read_elements(path):
    raw = open(path, encoding="utf-8").read()
    out = []
    for line in raw.splitlines():
        line = line.split("#", 1)[0]
        for tok in line.replace(",", " ").split():
            t = tok.strip().lower()
            if t:
                out.append(t)
    return out

def build(elements):
    nodes, links, groups = [], [], []
    _lid = [0]
    def link(o, os_, t, ts, typ):
        _lid[0] += 1; links.append([_lid[0], o, os_, t, ts, typ]); return _lid[0]
    def inp(name, typ, l=None): return {"name": name, "type": typ, "link": l}
    def out(name, typ, ls): return {"name": name, "type": typ, "links": list(ls), "slot_index": 0}
    def node(nid, typ, pos, widgets, inputs, outputs, title=None, size=(300, 100), color=None):
        n = {"id": nid, "type": typ, "pos": list(pos), "size": list(size), "flags": {},
             "order": nid, "mode": 0, "inputs": inputs, "outputs": outputs,
             "properties": {"Node name for S&R": typ}, "widgets_values": widgets}
        if title: n["title"] = title
        if color: n["color"] = color
        return n
    def note(nid, pos, text, size=(360, 170)):
        return node(nid, "MarkdownNote", pos, [text], [], [], "📝 주석", size, color="#432")

    nid = [0]
    def newid(): nid[0] += 1; return nid[0]

    # ---- 제목/사용법 주석 ----
    nodes.append(note(newid(), (20, 20),
        "## 요소별 재생성 (Regenerate per part)\n"
        f"`elements.txt` 기준 요소 {len(elements)}개. 각 요소 = Qwen 추출 레인 1개.\n\n"
        "**해당 부위만 재생성**:\n"
        "1) 오른쪽 `Fast Groups Muter` 에서 재생성할 요소만 ON, 나머지 OFF.\n"
        "2) Queue → 켜진 레인 KSampler seed 가 randomize 되어 그 요소만 새 변형.\n"
        "3) 꺼진 레인은 실행 안 됨(전체 재생성 아님).\n\n"
        "_무겹침 투명 분리가 필요하면 decompose_all.json 사용. 이건 생성형 변형용._",
        (560, 280)))

    # ---- 공유: 모델 로더 체인 ----
    N_UNET, N_LORA, N_MSAF, N_CFG = newid(), newid(), newid(), newid()
    N_CLIP, N_VAE, N_LOAD = newid(), newid(), newid()

    nodes.append(note(newid(), (20, 320),
        "**Qwen 모델 (공유 로더)**: 모든 레인이 이 MODEL/CLIP/VAE 를 공유.\n"
        "다운로드: `\\$env:WITH_QWEN=1; ./download_models.ps1`\n"
        "VRAM 12GB(GGUF)~24GB(fp8). 저VRAM이면 UNETLoader→Unet Loader(GGUF) 교체.",
        (360, 150)))

    l_unet = link(N_UNET, 0, N_LORA, 0, "MODEL")
    l_lora = link(N_LORA, 0, N_MSAF, 0, "MODEL")
    l_msaf = link(N_MSAF, 0, N_CFG, 0, "MODEL")
    nodes.append(node(N_UNET, "UNETLoader", (20, 500), [UNET, "default"], [],
        [out("MODEL", "MODEL", [l_unet])], "Load Diffusion (Qwen Edit 2509)", (330, 100)))
    nodes.append(node(N_LORA, "LoraLoaderModelOnly", (20, 630), [LORA, 1.0],
        [inp("model", "MODEL", l_unet)], [out("MODEL", "MODEL", [l_lora])], "Lightning 4-step LoRA", (330, 100)))
    nodes.append(node(N_MSAF, "ModelSamplingAuraFlow", (20, 760), [3.0],
        [inp("model", "MODEL", l_lora)], [out("MODEL", "MODEL", [l_msaf])], None, (330, 80)))
    cfg_out_links = []
    nodes.append(node(N_CFG, "CFGNorm", (20, 870), [1.0],
        [inp("model", "MODEL", l_msaf)], [out("MODEL", "MODEL", cfg_out_links)], None, (330, 80)))
    clip_out_links = []
    nodes.append(node(N_CLIP, "CLIPLoader", (20, 980), [CLIP, "qwen_image", "default"], [],
        [out("CLIP", "CLIP", clip_out_links)], "Load CLIP (Qwen2.5-VL)", (330, 100)))
    vae_out_links = []
    nodes.append(node(N_VAE, "VAELoader", (20, 1110), [VAE], [],
        [out("VAE", "VAE", vae_out_links)], "Load VAE", (330, 80)))
    load_out_links = []
    nodes.append(node(N_LOAD, "LoadImage", (20, 1210), ["input.png", "image"], [],
        [out("IMAGE", "IMAGE", load_out_links), out("MASK", "MASK", [])], "Load Input (T-pose)", (330, 320)))

    # ---- Fast Groups Muter (요소별 토글) ----
    N_MUTE = newid()
    nodes.append(node(N_MUTE, "Fast Groups Muter (rgthree)", (600, 320), [],
        [], [out("OPT_CONNECTION", "*", [])], "🔇 요소별 토글 (재생성 선택)", (300, 380), color="#333"))
    nodes[-1]["properties"] = {"matchColors": "", "matchTitle": "", "showNav": True,
                              "sort": "position", "toggleRestriction": "default"}

    # ---- 요소 레인 ----
    base_x, lane_y = 980, 20
    seed0 = 1000
    for idx, name in enumerate(elements):
        py = lane_y
        N_POS, N_NEG = newid(), newid()
        N_SCALE, N_VENC, N_KS, N_VDEC, N_SAVE = newid(), newid(), newid(), newid(), newid()

        # 주석
        nodes.append(note(newid(), (base_x, py),
            f"### {name}\n프롬프트: extract only **{PHRASES.get(name, name)}** on white bg.\n"
            "KSampler seed 가 이 요소의 변형 seed. 이 그룹만 켜고 Queue=이 부위만 재생성.",
            (360, 150)))

        # 공유 입력 링크
        l_img_scale = link(N_LOAD, 0, N_SCALE, 0, "IMAGE"); load_out_links.append(l_img_scale)
        l_scale_pos = link(N_SCALE, 0, N_POS, 2, "IMAGE")   # image1
        l_scale_neg = link(N_SCALE, 0, N_NEG, 2, "IMAGE")
        l_scale_ven = link(N_SCALE, 0, N_VENC, 0, "IMAGE")
        l_clip_p = link(N_CLIP, 0, N_POS, 0, "CLIP"); clip_out_links.append(l_clip_p)
        l_clip_n = link(N_CLIP, 0, N_NEG, 0, "CLIP"); clip_out_links.append(l_clip_n)
        l_vae_p = link(N_VAE, 0, N_POS, 1, "VAE"); vae_out_links.append(l_vae_p)
        l_vae_n = link(N_VAE, 0, N_NEG, 1, "VAE"); vae_out_links.append(l_vae_n)
        l_vae_e = link(N_VAE, 0, N_VENC, 1, "VAE"); vae_out_links.append(l_vae_e)
        l_vae_d = link(N_VAE, 0, N_VDEC, 1, "VAE"); vae_out_links.append(l_vae_d)
        l_model = link(N_CFG, 0, N_KS, 0, "MODEL"); cfg_out_links.append(l_model)
        # 내부 링크
        l_pos = link(N_POS, 0, N_KS, 1, "CONDITIONING")
        l_neg = link(N_NEG, 0, N_KS, 2, "CONDITIONING")
        l_lat = link(N_VENC, 0, N_KS, 3, "LATENT")
        l_ks = link(N_KS, 0, N_VDEC, 0, "LATENT")
        l_dec = link(N_VDEC, 0, N_SAVE, 0, "IMAGE")

        # FluxKontextImageScale (원본 -> 최적 크기)
        nodes.append(node(N_SCALE, "FluxKontextImageScale", (base_x, py + 170), [],
            [inp("image", "IMAGE", l_img_scale)],
            [out("IMAGE", "IMAGE", [l_scale_pos, l_scale_neg, l_scale_ven])], f"Scale: {name}", (260, 80)))
        # 인코더 pos/neg (clip, vae, image1, image2, image3 ; prompt=widget)
        nodes.append(node(N_POS, "TextEncodeQwenImageEditPlus", (base_x + 290, py + 60), [prompt_for(name)],
            [inp("clip", "CLIP", l_clip_p), inp("vae", "VAE", l_vae_p),
             inp("image1", "IMAGE", l_scale_pos), inp("image2", "IMAGE", None), inp("image3", "IMAGE", None)],
            [out("CONDITIONING", "CONDITIONING", [l_pos])], f"Positive: {name}", (420, 180)))
        nodes.append(node(N_NEG, "TextEncodeQwenImageEditPlus", (base_x + 290, py + 270), [""],
            [inp("clip", "CLIP", l_clip_n), inp("vae", "VAE", l_vae_n),
             inp("image1", "IMAGE", l_scale_neg), inp("image2", "IMAGE", None), inp("image3", "IMAGE", None)],
            [out("CONDITIONING", "CONDITIONING", [l_neg])], "Negative (empty)", (420, 140)))
        nodes.append(node(N_VENC, "VAEEncode", (base_x + 290, py + 430), [],
            [inp("pixels", "IMAGE", l_scale_ven), inp("vae", "VAE", l_vae_e)],
            [out("LATENT", "LATENT", [l_lat])], None, (200, 60)))
        # KSampler : 레인별 seed, randomize (해당 부위 재생성)
        nodes.append(node(N_KS, "KSampler", (base_x + 730, py + 60),
            [seed0 + idx, "randomize", 4, 1, "euler", "simple", 1.0],
            [inp("model", "MODEL", l_model), inp("positive", "CONDITIONING", l_pos),
             inp("negative", "CONDITIONING", l_neg), inp("latent_image", "LATENT", l_lat)],
            [out("LATENT", "LATENT", [l_ks])], f"KSampler (seed: {name})", (300, 260)))
        nodes.append(node(N_VDEC, "VAEDecode", (base_x + 730, py + 340), [],
            [inp("samples", "LATENT", l_ks), inp("vae", "VAE", l_vae_d)],
            [out("IMAGE", "IMAGE", [l_dec])], None, (200, 60)))
        nodes.append(node(N_SAVE, "SaveImage", (base_x + 1040, py + 60), [f"regen/{name}"],
            [inp("images", "IMAGE", l_dec)], [], f"Save: {name}", (340, 300)))

        groups.append({"title": f"요소: {name}", "bounding": [base_x - 20, py - 10, 1430, 540],
                       "color": "#a1309b", "font_size": 24, "flags": {}})
        lane_y += 580

    # finalize: 전역 links 테이블에서 모든 출력 links 를 재계산(누적 버그 방지·일관성 보장)
    by_id = {n["id"]: n for n in nodes}
    for n in nodes:
        for o in n.get("outputs", []):
            o["links"] = []
    for l in links:
        src = by_id.get(l[1])
        if src and l[2] < len(src.get("outputs", [])):
            src["outputs"][l[2]]["links"].append(l[0])

    return {"last_node_id": nid[0], "last_link_id": _lid[0], "nodes": nodes,
            "links": links, "groups": groups, "config": {}, "extra": {}, "version": 0.4}

def main():
    elements = read_elements(os.path.join(HERE, "elements.txt"))
    if not elements:
        print("elements.txt 에 요소가 없습니다."); sys.exit(1)
    wf = build(elements)
    out = os.path.join(HERE, "decompose_regen.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(wf, f, ensure_ascii=False, indent=2)
    print(f"wrote decompose_regen.json | 요소 {len(elements)}개 | nodes {len(wf['nodes'])} | links {len(wf['links'])}")
    print("요소:", ", ".join(elements))

if __name__ == "__main__":
    main()
