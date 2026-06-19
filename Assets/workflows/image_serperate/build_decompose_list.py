# -*- coding: utf-8 -*-
"""
리스트 구동 요소 분해 워크플로우 생성기.
elements.txt 에 요소 이름을 가변적으로 적으면, 요소마다 노드 레인
(Cozy Human Parser ATR -> JoinImageWithAlpha -> SaveImage)을 자동 생성해
decompose_list.json 으로 출력한다.

핵심:
  - "head, pants, body" 처럼 친화적 이름을 적으면 ALIASES 로 ATR 클래스에 매핑.
  - 요소를 추가/삭제 = elements.txt 한 줄 수정 후 재실행. 노드가 자동으로 늘고 줄어든다.
  - 두 요소가 같은 ATR 클래스를 공유하면(예: head 와 body 가 둘 다 face) 출력이 겹치므로
    생성 시 경고를 출력하고 제목 주석에도 표시한다(상호배타 보장이 깨지는 유일한 경우).

ComfyUI는 정적 그래프라 캔버스에서 실시간 노드 생성은 불가 → "리스트 수정 후 재생성"이 그 역할.
"""
import json, os, sys

# 친화적 이름(소문자) -> ATR 파서 boolean 필드들
ALIASES = {
    "hair": ["hair"],
    "head": ["hair", "face"],
    "face": ["face"],
    "body": ["face", "left_leg", "right_leg", "left_arm", "right_arm"],
    "skin": ["face", "left_leg", "right_leg", "left_arm", "right_arm"],
    "arms": ["left_arm", "right_arm"],
    "legs": ["left_leg", "right_leg"],
    "top": ["upper_clothes"], "upper": ["upper_clothes"],
    "shirt": ["upper_clothes"], "jacket": ["upper_clothes"],
    "upper_clothes": ["upper_clothes"],
    "bottom": ["skirt", "pants", "dress"], "lower": ["skirt", "pants", "dress"],
    "pants": ["pants"], "skirt": ["skirt"], "dress": ["dress"],
    "belt": ["belt"],
    "shoes": ["left_shoe", "right_shoe"], "footwear": ["left_shoe", "right_shoe"],
    "hat": ["hat"], "bag": ["bag"],
    "sunglasses": ["sunglasses"], "glasses": ["sunglasses"],
    "scarf": ["scarf"],
}

BOOL_ORDER = ["background","hat","hair","sunglasses","upper_clothes","skirt","pants",
              "dress","belt","left_shoe","right_shoe","face","left_leg","right_leg",
              "left_arm","right_arm","bag","scarf"]

SCHP_ATR_URL = "https://huggingface.co/soonyau/visconet/resolve/main/exp-schp-201908301523-atr.pth"

HERE = os.path.dirname(os.path.abspath(__file__))

def read_elements(path):
    raw = open(path, encoding="utf-8").read()
    names = []
    for line in raw.splitlines():
        line = line.split("#", 1)[0]
        for tok in line.replace(",", " ").split():
            names.append(tok.strip().lower())
    return [n for n in names if n]

def resolve(names):
    lanes, unknown = [], []
    for n in names:
        if n in ALIASES:
            lanes.append((n, ALIASES[n]))
        else:
            unknown.append(n)
    return lanes, unknown

def find_conflicts(lanes):
    """같은 ATR 클래스를 2개 이상 요소가 쓰면 그 출력은 겹친다."""
    owner = {}
    conflicts = {}
    for name, classes in lanes:
        for c in classes:
            if c in owner:
                conflicts.setdefault(c, {owner[c]}).add(name)
            else:
                owner[c] = name
    return conflicts

def build(lanes, conflicts):
    nodes, links, groups = [], [], []
    _lid = [0]
    def link(o, os_, t, ts, typ):
        _lid[0] += 1; links.append([_lid[0], o, os_, t, ts, typ]); return _lid[0]
    def inp(name, typ, l=None): return {"name": name, "type": typ, "link": l}
    def out(name, typ, ls): return {"name": name, "type": typ, "links": list(ls), "slot_index": 0}
    def node(nid, typ, pos, widgets, inputs, outputs, title=None, size=(300,100), color=None):
        n = {"id": nid, "type": typ, "pos": list(pos), "size": list(size), "flags": {},
             "order": nid, "mode": 0, "inputs": inputs, "outputs": outputs,
             "properties": {"Node name for S&R": typ}, "widgets_values": widgets}
        if title: n["title"] = title
        if color: n["color"] = color
        return n
    def note(nid, pos, text, size=(330,150)):
        return node(nid, "MarkdownNote", pos, [text], [], [], "📝 주석", size, color="#432")

    nid = [0]
    def newid(): nid[0] += 1; return nid[0]

    conflict_txt = ""
    if conflicts:
        parts = [f"`{c}` ↔ {', '.join(sorted(v))}" for c, v in conflicts.items()]
        conflict_txt = ("\n\n⚠️ **겹침 경고**: 아래 클래스를 여러 요소가 공유 → 해당 PNG가 겹침:\n- "
                        + "\n- ".join(parts))

    # 제목 주석
    nodes.append(note(newid(), (40, 20),
        "## 리스트 구동 요소 분해\n"
        f"`elements.txt` 의 목록으로 자동 생성됨 (요소 {len(lanes)}개).\n"
        "요소 추가/삭제 = elements.txt 수정 후 `python build_decompose_list.py` 재실행.\n"
        "입력: 이미 T 포즈인 캐릭터 1장. SCHP(ATR) 분할 → 요소별 투명 PNG." + conflict_txt,
        (580, 220)))

    # LoadImage
    N_LOAD = newid()
    load_out_links = []
    nodes.append(note(newid(), (40, 260),
        "**입력 이미지**: 분해할 캐릭터(정면 T 포즈). IMAGE 출력이 모든 레인으로 분기.", (330, 110)))
    load_node = node(N_LOAD, "LoadImage", (40, 400), ["input.png", "image"], [],
        [out("IMAGE", "IMAGE", []), out("MASK", "MASK", [])], "Load Input Image", (330, 330))
    nodes.append(load_node)

    # 다운로드 노드
    N_DL = newid()
    nodes.append(note(newid(), (40, 760),
        "**SCHP 모델 다운로드 (최초 1회)** → `models/schp/exp-schp-201908301523-atr.pth`.\n"
        "이 노드를 큐에 한 번 넣으면 자동 다운로드(약 267MB).", (330, 150)))
    nodes.append(node(N_DL, "Hugging Face Download Model", (40, 950),
        ["custom", SCHP_ATR_URL, "schp"], [], [out("model name", "*", [])],
        "⬇ SCHP 모델 다운로드", (360, 130), color="#353"))

    # 요소 레인
    lane_y, base_x = 20, 680
    for name, classes in lanes:
        py = lane_y
        on = set(classes)
        warn = ""
        if any(c in conflicts for c in classes):
            warn = "\n⚠️ 다른 요소와 클래스 공유 → 겹침 주의."
        nodes.append(note(newid(), (base_x, py),
            f"### {name}\nATR 클래스: **{', '.join(classes)}**\n"
            f"파서(해당 클래스만 ON) → JoinImageWithAlpha → SaveImage(`elements/{name}`)." + warn,
            (330, 150)))
        # 파서
        N_P = newid()
        widgets = [(b in on) for b in BOOL_ORDER]
        l_img_p = link(N_LOAD, 0, N_P, 0, "IMAGE"); load_out_links.append(l_img_p)
        parser = node(N_P, "Cozy Human Parser ATR", (base_x, py+170), widgets,
            [inp("image", "IMAGE", l_img_p)],
            [out("mask", "MASK", []), out("map", "IMAGE", [])], f"Parse: {name}", (300, 430))
        nodes.append(parser)
        # Join
        N_J = newid()
        l_img_j = link(N_LOAD, 0, N_J, 0, "IMAGE"); load_out_links.append(l_img_j)
        l_mask = link(N_P, 0, N_J, 1, "MASK"); parser["outputs"][0]["links"].append(l_mask)
        join = node(N_J, "JoinImageWithAlpha", (base_x+360, py+170), [],
            [inp("image", "IMAGE", l_img_j), inp("alpha", "MASK", l_mask)],
            [out("IMAGE", "IMAGE", [])], f"RGBA: {name}", (280, 100))
        nodes.append(join)
        # Save
        N_S = newid()
        l_save = link(N_J, 0, N_S, 0, "IMAGE"); join["outputs"][0]["links"].append(l_save)
        nodes.append(node(N_S, "SaveImage", (base_x+660, py+170), [f"elements/{name}"],
            [inp("images", "IMAGE", l_save)], [], f"Save: {name}", (320, 300)))
        groups.append({"title": f"요소: {name}", "bounding": [base_x-20, py-10, 1020, 620],
                       "color": "#3f789e", "font_size": 24, "flags": {}})
        lane_y += 660

    load_node["outputs"][0]["links"] = load_out_links
    return {"last_node_id": nid[0], "last_link_id": _lid[0], "nodes": nodes,
            "links": links, "groups": groups, "config": {}, "extra": {}, "version": 0.4}

def main():
    names = read_elements(os.path.join(HERE, "elements.txt"))
    lanes, unknown = resolve(names)
    if unknown:
        print("⚠️ 알 수 없는 이름(무시됨):", unknown)
        print("   사용 가능한 이름:", ", ".join(sorted(ALIASES.keys())))
    if not lanes:
        print("요소가 없습니다. elements.txt 를 확인하세요."); sys.exit(1)
    conflicts = find_conflicts(lanes)
    if conflicts:
        print("⚠️ 클래스 겹침:", {c: sorted(v) for c, v in conflicts.items()})
    wf = build(lanes, conflicts)
    out = os.path.join(HERE, "decompose_list.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(wf, f, ensure_ascii=False, indent=2)
    print(f"wrote decompose_list.json | 요소 {len(lanes)}개 | nodes {len(wf['nodes'])} | links {len(wf['links'])}")
    print("요소:", ", ".join(n for n, _ in lanes))

if __name__ == "__main__":
    main()
