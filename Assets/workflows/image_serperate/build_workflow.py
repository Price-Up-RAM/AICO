# -*- coding: utf-8 -*-
"""
Qwen-Image-Edit-2509 기반 캐릭터 요소 분해 워크플로우 생성기  ->  decompose.json

elements.txt 의 요소마다 "흰 배경에 그 요소만" 추출하는 레인을 하나씩 만든다.
요소(레인)별로 KSampler seed 가 독립이라, 특정 부위만 다시 생성(재생성)할 수 있다.

설계 근거 (그라운드 트루스):
  ComfyUI 내장 공식 템플릿 image_qwen_image_edit_2509.json 의 서브그래프
  "Image Edit (Qwen 2509)" 를 평탄화한 것. 노드 구성과 위젯 값을 그대로 따랐다:
    UNETLoader(qwen_image_edit_2509_fp8) -> LoraLoaderModelOnly(Lightning 4-step, 1.0)
    -> ModelSamplingAuraFlow(shift 3) -> CFGNorm(1)
    CLIPLoader(qwen_2.5_vl_7b, type=qwen_image) / VAELoader(qwen_image_vae)
    LoadImage -> FluxKontextImageScale -> TextEncodeQwenImageEditPlus(pos/neg) + VAEEncode
    -> KSampler(euler/simple, steps 4, cfg 1, denoise 1) -> VAEDecode -> SaveImage
  커스텀 노드 0개 (전부 ComfyUI 코어). 선택적 재생성은 그룹 Mute/Bypass(코어 기능)로.
"""
import json, os, re, sys

# ---- 공식 템플릿과 동일한 카논 파일명/값 ----
UNET = "qwen_image_edit_2509_fp8_e4m3fn.safetensors"
CLIP = "qwen_2.5_vl_7b_fp8_scaled.safetensors"
VAE  = "qwen_image_vae.safetensors"
LORA = "Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors"
STEPS, CFG, SAMPLER, SCHED, SHIFT = 4, 1, "euler", "simple", 3

# HF 다운로드 노드용 (target_folder, 검증된 다운로드 URL). 워크플로우가 자동으로 받는다.
# 노드: 'Hugging Face Download Model' (custom_nodes/ComfyUI_HuggingFace_Downloader)
DL = {
    "unet": ("diffusion_models",
             "https://huggingface.co/Comfy-Org/Qwen-Image-Edit_ComfyUI/resolve/main/split_files/diffusion_models/qwen_image_edit_2509_fp8_e4m3fn.safetensors"),
    "clip": ("text_encoders",
             "https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI/resolve/main/split_files/text_encoders/qwen_2.5_vl_7b_fp8_scaled.safetensors"),
    "vae":  ("vae",
             "https://huggingface.co/Comfy-Org/Qwen-Image_ComfyUI/resolve/main/split_files/vae/qwen_image_vae.safetensors"),
    "lora": ("loras",
             "https://huggingface.co/lightx2v/Qwen-Image-Lightning/resolve/main/Qwen-Image-Edit-2509/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors"),
}

PROMPT_TMPL = (
    "Extract only {PHRASE} from this character. "
    "Place it centered on a plain pure-white background. "
    "Remove the character and all other items. "
    "Preserve the original art style, colors, and lighting. "
    "Single object, product-shot style, no text, no shadow."
)

HERE = os.path.dirname(os.path.abspath(__file__))


def slug(s):
    return re.sub(r"[^a-z0-9]+", "_", s.strip().lower()).strip("_") or "item"


def read_elements(path):
    """각 줄: 'name' 또는 'name | prompt phrase'. -> [(name, phrase), ...]"""
    out = []
    for line in open(path, encoding="utf-8").read().splitlines():
        line = line.split("#", 1)[0].strip()
        if not line:
            continue
        if "|" in line:
            name, phrase = line.split("|", 1)
            name, phrase = name.strip(), phrase.strip()
        else:
            name = line.strip()
            phrase = "the " + name  # 'hair' -> 'the hair'
        out.append((slug(name), phrase))
    return out


def build(elements):
    nodes, links, groups = [], [], []
    _lid = [0]
    def link(o, oslot, t, tslot, typ):
        _lid[0] += 1; links.append([_lid[0], o, oslot, t, tslot, typ]); return _lid[0]
    def inp(name, typ, l=None): return {"name": name, "type": typ, "link": l}
    def winp(name, l, typ="COMBO"):  # 위젯을 input 으로 변환한 슬롯 (다운로드 노드가 파일명 주입)
        return {"localized_name": name, "name": name, "type": typ, "widget": {"name": name}, "link": l}
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
        "## 캐릭터 요소 분해 (Qwen-Image-Edit 2509)\n"
        f"`elements.txt` 요소 {len(elements)}개. 각 요소 = '흰 배경에 그것만' 추출 레인 1개.\n\n"
        "**전체 분해**: 입력 이미지 올리고 → Queue.\n"
        "**특정 부위만 재생성**: 그 요소 그룹만 남기고 나머지 그룹을 우클릭 → *Mute Group* "
        "(또는 노드 선택 후 Ctrl+M) → Queue. 켜진 레인 KSampler seed 가 randomize 되어 새 변형.\n"
        "**같은 부위 N장**: 상단 Queue 옆 batch count 를 N 으로 두고 Queue (seed 가 매번 달라짐).\n\n"
        "_커스텀 노드 불필요(전부 코어). 모델은 download_models.ps1 로 받는다._",
        (620, 300)))

    # ---- 공유: 모델 로더 체인 (+ 자동 다운로드 노드) ----
    N_UNET, N_LORA, N_MSAF, N_CFG = newid(), newid(), newid(), newid()
    N_CLIP, N_VAE, N_LOAD = newid(), newid(), newid()
    N_DLU, N_DLC, N_DLV, N_DLL = newid(), newid(), newid(), newid()

    nodes.append(note(newid(), (20, 340),
        "**Qwen 모델 — 워크플로우가 자동 다운로드.**\n"
        "왼쪽 `HF Download` 노드 4개가 모델을 받아 각 로더에 파일명을 주입한다.\n"
        "Queue 한 번이면 없는 파일만 받고(있으면 skip) 그 다음 로더가 로드.\n"
        "필요 노드: `ComfyUI_HuggingFace_Downloader` (Manager 에서 설치).\n"
        "총 ~20GB · VRAM fp8 16GB+ 권장(저VRAM은 GGUF 로 교체).",
        (440, 180)))

    # 다운로드 노드 (왼쪽 컬럼) -> 로더 파일명 input 으로
    l_dlu = link(N_DLU, 0, N_UNET, 0, "*")
    l_dlc = link(N_DLC, 0, N_CLIP, 0, "*")
    l_dlv = link(N_DLV, 0, N_VAE, 0, "*")
    l_dll = link(N_DLL, 0, N_LORA, 1, "*")  # slot 1 (slot 0 = model)
    def dl(nid, key, pos, title):
        tgt, url = DL[key]
        return node(nid, "Hugging Face Download Model", pos, [tgt, url, ""], [],
                    [out("model name", "*", [])], title, (430, 120), color="#253")
    nodes.append(dl(N_DLU, "unet", (-470, 520), "⬇ HF: diffusion (Qwen Edit 2509)"))
    nodes.append(dl(N_DLL, "lora", (-470, 670), "⬇ HF: Lightning 4-step LoRA"))
    nodes.append(dl(N_DLC, "clip", (-470, 1000), "⬇ HF: text encoder (Qwen2.5-VL)"))
    nodes.append(dl(N_DLV, "vae",  (-470, 1150), "⬇ HF: VAE"))

    l_unet = link(N_UNET, 0, N_LORA, 0, "MODEL")
    l_lora = link(N_LORA, 0, N_MSAF, 0, "MODEL")
    l_msaf = link(N_MSAF, 0, N_CFG, 0, "MODEL")
    # 로더: 파일명 위젯을 input 으로 변환(다운로드 노드가 주입). widgets_values 는 fallback 으로 유지.
    nodes.append(node(N_UNET, "UNETLoader", (20, 540), [UNET, "default"],
        [winp("unet_name", l_dlu)],
        [out("MODEL", "MODEL", [l_unet])], "Load Diffusion (Qwen Edit 2509)", (340, 100)))
    nodes.append(node(N_LORA, "LoraLoaderModelOnly", (20, 690), [LORA, 1.0],
        [inp("model", "MODEL", l_unet), winp("lora_name", l_dll)],
        [out("MODEL", "MODEL", [l_lora])], "Lightning 4-step LoRA", (340, 120)))
    nodes.append(node(N_MSAF, "ModelSamplingAuraFlow", (20, 840), [float(SHIFT)],
        [inp("model", "MODEL", l_lora)], [out("MODEL", "MODEL", [l_msaf])], None, (340, 80)))
    cfg_out_links = []
    nodes.append(node(N_CFG, "CFGNorm", (20, 950), [float(CFG)],
        [inp("model", "MODEL", l_msaf)], [out("MODEL", "MODEL", cfg_out_links)], None, (340, 80)))
    clip_out_links = []
    nodes.append(node(N_CLIP, "CLIPLoader", (20, 1060), [CLIP, "qwen_image", "default"],
        [winp("clip_name", l_dlc)],
        [out("CLIP", "CLIP", clip_out_links)], "Load CLIP (Qwen2.5-VL)", (340, 100)))
    vae_out_links = []
    nodes.append(node(N_VAE, "VAELoader", (20, 1190), [VAE],
        [winp("vae_name", l_dlv)],
        [out("VAE", "VAE", vae_out_links)], "Load VAE", (340, 80)))
    load_out_links = []
    nodes.append(node(N_LOAD, "LoadImage", (20, 1300), ["input.png", "image"], [],
        [out("IMAGE", "IMAGE", load_out_links), out("MASK", "MASK", [])], "Load Input (T-pose)", (340, 330)))

    # ---- 요소 레인 ----
    base_x, lane_y, seed0 = 700, 20, 1000
    for idx, (name, phrase) in enumerate(elements):
        py = lane_y
        N_POS, N_NEG, N_SCALE, N_VENC, N_KS, N_VDEC, N_SAVE = (newid() for _ in range(7))

        nodes.append(note(newid(), (base_x, py),
            f"### {name}\n`extract only {phrase} on white bg`\n"
            "이 그룹만 켜고 Queue = 이 부위만 (재)생성.", (380, 130)))

        # 공유 입력
        l_img_scale = link(N_LOAD, 0, N_SCALE, 0, "IMAGE"); load_out_links.append(l_img_scale)
        l_scale_pos = link(N_SCALE, 0, N_POS, 2, "IMAGE")
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

        nodes.append(node(N_SCALE, "FluxKontextImageScale", (base_x, py + 150), [],
            [inp("image", "IMAGE", l_img_scale)],
            [out("IMAGE", "IMAGE", [l_scale_pos, l_scale_neg, l_scale_ven])], f"Scale: {name}", (260, 80)))
        nodes.append(node(N_POS, "TextEncodeQwenImageEditPlus", (base_x + 290, py + 40),
            [PROMPT_TMPL.format(PHRASE=phrase)],
            [inp("clip", "CLIP", l_clip_p), inp("vae", "VAE", l_vae_p),
             inp("image1", "IMAGE", l_scale_pos), inp("image2", "IMAGE", None), inp("image3", "IMAGE", None)],
            [out("CONDITIONING", "CONDITIONING", [l_pos])], f"Positive: {name}", (430, 200)))
        nodes.append(node(N_NEG, "TextEncodeQwenImageEditPlus", (base_x + 290, py + 260), [""],
            [inp("clip", "CLIP", l_clip_n), inp("vae", "VAE", l_vae_n),
             inp("image1", "IMAGE", l_scale_neg), inp("image2", "IMAGE", None), inp("image3", "IMAGE", None)],
            [out("CONDITIONING", "CONDITIONING", [l_neg])], "Negative (empty)", (430, 140)))
        nodes.append(node(N_VENC, "VAEEncode", (base_x + 290, py + 420), [],
            [inp("pixels", "IMAGE", l_scale_ven), inp("vae", "VAE", l_vae_e)],
            [out("LATENT", "LATENT", [l_lat])], None, (220, 60)))
        nodes.append(node(N_KS, "KSampler", (base_x + 740, py + 40),
            [seed0 + idx, "randomize", STEPS, CFG, SAMPLER, SCHED, 1.0],
            [inp("model", "MODEL", l_model), inp("positive", "CONDITIONING", l_pos),
             inp("negative", "CONDITIONING", l_neg), inp("latent_image", "LATENT", l_lat)],
            [out("LATENT", "LATENT", [l_ks])], f"KSampler (seed: {name})", (300, 260)))
        nodes.append(node(N_VDEC, "VAEDecode", (base_x + 740, py + 330), [],
            [inp("samples", "LATENT", l_ks), inp("vae", "VAE", l_vae_d)],
            [out("IMAGE", "IMAGE", [l_dec])], None, (220, 60)))
        nodes.append(node(N_SAVE, "SaveImage", (base_x + 1060, py + 40), [f"decompose/{name}"],
            [inp("images", "IMAGE", l_dec)], [], f"Save: {name}", (360, 320)))

        groups.append({"title": f"요소: {name}", "bounding": [base_x - 20, py - 10, 1470, 540],
                       "color": "#3f789e", "font_size": 24, "flags": {}})
        lane_y += 580

    # finalize: 출력 links 테이블을 전역 links 로부터 재계산(일관성 보장)
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
    out = os.path.join(HERE, "decompose.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(wf, f, ensure_ascii=False, indent=2)
    print(f"wrote decompose.json | 요소 {len(elements)}개 | nodes {len(wf['nodes'])} | links {len(wf['links'])}")
    print("요소:", ", ".join(n for n, _ in elements))


if __name__ == "__main__":
    main()
