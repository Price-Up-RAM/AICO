# -*- coding: utf-8 -*-
"""
단일 워크플로우 "decompose_all.json" 생성기 — 캐릭터 1장(이미 T 포즈)을
의미 분할(Cozy Human Parser ATR / SCHP)로 **상호배타** 마스크로 나눠
요소별 투명 PNG로 완전 분리한다. (겹침 구조적으로 불가능)

설계 근거(검증 완료):
  - Cozy Human Parser ATR : image + 18개 class boolean -> (MASK, IMAGE map)
        mask = argmax 파싱에서 '켠 클래스'에만 흰색. 픽셀당 라벨 1개라 겹침 없음.
        라벨: 0 bg,1 hat,2 hair,3 sunglasses,4 upper_clothes,5 skirt,6 pants,
              7 dress,8 belt,9 left_shoe,10 right_shoe,11 face,12 left_leg,
              13 right_leg,14 left_arm,15 right_arm,16 bag,17 scarf
  - JoinImageWithAlpha (코어) : image + alpha(MASK) -> RGBA IMAGE (투명)
  - SaveImage : RGBA 그대로 투명 PNG 저장
  - Hugging Face Download Model (jnxmx 노드) : OUTPUT_NODE.
        SCHP atr 체크포인트를 models/schp/ 에 받는다(큐 1회). 파서가 그 파일을 읽음.

요소 분리 = 파서 노드를 요소마다 따로 두고 해당 클래스만 켠다.
  벨트는 독립 클래스(8) → 상의/하의 어디에도 중복되지 않음 (사용자 요구 핵심).
"""
import json, os

# 파서 boolean 입력 순서(INPUT_TYPES와 정확히 일치해야 함)
BOOL_ORDER = ["background","hat","hair","sunglasses","upper_clothes","skirt","pants",
              "dress","belt","left_shoe","right_shoe","face","left_leg","right_leg",
              "left_arm","right_arm","bag","scarf"]

# (이름, 저장 prefix, 켜는 클래스들, 설명)
LANES = [
    ("hair",       "elements/hair",       {"hair"},                                   "머리카락(2)"),
    ("body",       "elements/body",       {"face","left_leg","right_leg","left_arm","right_arm"}, "몸체/체형 = 피부(face11,leg12·13,arm14·15)"),
    ("top",        "elements/top",        {"upper_clothes"},                          "상의(4) upper-clothes"),
    ("bottom",     "elements/bottom",     {"skirt","pants","dress"},                  "하의(5·6·7) skirt/pants/dress"),
    ("belt",       "elements/belt",       {"belt"},                                   "벨트(8) — 독립 클래스, 상/하의와 절대 안 겹침"),
    ("shoes",      "elements/shoes",      {"left_shoe","right_shoe"},                 "신발(9·10)"),
    ("hat",        "elements/hat",        {"hat"},                                    "모자(1)"),
    ("bag",        "elements/bag",        {"bag"},                                    "가방(16)"),
    ("sunglasses", "elements/sunglasses", {"sunglasses"},                             "선글라스(3)"),
    ("scarf",      "elements/scarf",      {"scarf"},                                  "스카프(17)"),
]

SCHP_ATR_URL = "https://huggingface.co/soonyau/visconet/resolve/main/exp-schp-201908301523-atr.pth"

def main():
    nodes, links, groups = [], [], []
    _lid = [0]
    def link(o,os_,t,ts,typ):
        _lid[0]+=1; links.append([_lid[0],o,os_,t,ts,typ]); return _lid[0]
    def inp(name,typ,l=None): return {"name":name,"type":typ,"link":l}
    def out(name,typ,ls):    return {"name":name,"type":typ,"links":list(ls),"slot_index":0}
    def node(nid,typ,pos,widgets,inputs,outputs,title=None,size=(300,100),color=None):
        n={"id":nid,"type":typ,"pos":list(pos),"size":list(size),"flags":{},
           "order":nid,"mode":0,"inputs":inputs,"outputs":outputs,
           "properties":{"Node name for S&R":typ},"widgets_values":widgets}
        if title: n["title"]=title
        if color: n["color"]=color
        return n
    def note(nid,pos,text,size=(330,150)):
        return node(nid,"MarkdownNote",pos,[text],[],[],"📝 주석",size,color="#432")

    nid=[0]
    def newid(): nid[0]+=1; return nid[0]

    # ---- 공통: 제목 주석 ----
    N_TITLE=newid()
    nodes.append(note(N_TITLE,(40,20),
        "## 요소 완전 분해 (단일 워크플로우)\n"
        "입력: **이미 T 포즈인** 캐릭터 1장.\n"
        "방식: SCHP(ATR) 의미 분할 → 픽셀당 라벨 1개 → **요소 간 겹침 없음**.\n"
        "각 레인 = 한 요소. 파서에서 해당 클래스만 켜고 → 알파 합성 → 투명 PNG 저장.\n\n"
        "**처음 1회**: 아래 'SCHP 모델 다운로드' 노드를 큐에 넣어 체크포인트를 받은 뒤 전체 실행.",
        (560,200)))

    # ---- 공통: LoadImage ----
    N_LOAD=newid()
    l_load_holder=[]  # IMAGE 출력 링크들 누적
    # outputs 채울 링크는 뒤에서 만들고 합쳐 넣음 → 먼저 노드 객체 만들고 outputs 갱신
    load_out_links=[]
    N_LOAD_IMG_OUT=load_out_links  # alias
    nodes.append(note(newid(),(40,240),
        "**입력 이미지**: 분해할 캐릭터(정면 T 포즈). `Load Input Image`에 업로드.\n"
        "IMAGE 출력이 모든 파서와 알파합성으로 분기됨.",(330,120)))
    load_node=node(N_LOAD,"LoadImage",(40,400),["input.png","image"],[],
        [out("IMAGE","IMAGE",[]),out("MASK","MASK",[])],"Load Input Image",(330,330))
    nodes.append(load_node)

    # ---- 공통: 다운로드 노드 ----
    N_DL=newid()
    nodes.append(note(newid(),(40,760),
        "**SCHP 모델 다운로드 (최초 1회)**\n"
        "Cozy Human Parser는 `models/schp/exp-schp-201908301523-atr.pth` 를 읽는다.\n"
        "이 노드를 큐에 한 번 넣으면 위 파일을 자동으로 받음(약 267MB).\n"
        "target_folder=custom, custom_path=`schp`.\n"
        "_실패 시 download_models.ps1 의 SCHP 블록으로 수동 다운로드._",(330,200)))
    nodes.append(node(N_DL,"Hugging Face Download Model",(40,990),
        ["custom",SCHP_ATR_URL,"schp"],[],
        [out("model name","*",[])],"⬇ SCHP 모델 다운로드",(360,130),color="#353"))

    # ---- 요소 레인들 ----
    lane_y=20
    base_x=680
    for name,prefix,on_classes,desc in LANES:
        py=lane_y
        # 주석
        bool_summary=", ".join(sorted(on_classes))
        nodes.append(note(newid(),(base_x,py),
            f"### {name}\n{desc}\n켜는 입력: **{bool_summary}**\n"
            f"파서(MASK) → JoinImageWithAlpha(원본+마스크=투명) → SaveImage(`{prefix}`).",
            (330,150)))
        # 파서
        N_P=newid()
        widgets=[(b in on_classes) for b in BOOL_ORDER]
        l_img_to_parser=link(N_LOAD,0,N_P,0,"IMAGE"); load_out_links.append(l_img_to_parser)
        parser=node(N_P,"Cozy Human Parser ATR",(base_x,py+170),widgets,
            [inp("image","IMAGE",l_img_to_parser)],
            [out("mask","MASK",[]),out("map","IMAGE",[])],
            f"Parse: {name}",(300,430))
        nodes.append(parser)
        # JoinImageWithAlpha
        N_J=newid()
        l_img_to_join=link(N_LOAD,0,N_J,0,"IMAGE"); load_out_links.append(l_img_to_join)
        l_mask=link(N_P,0,N_J,1,"MASK")
        parser["outputs"][0]["links"].append(l_mask)
        join=node(N_J,"JoinImageWithAlpha",(base_x+360,py+170),[],
            [inp("image","IMAGE",l_img_to_join),inp("alpha","MASK",l_mask)],
            [out("IMAGE","IMAGE",[])],f"RGBA: {name}",(280,100))
        nodes.append(join)
        # SaveImage
        N_S=newid()
        l_save=link(N_J,0,N_S,0,"IMAGE")
        join["outputs"][0]["links"].append(l_save)
        nodes.append(node(N_S,"SaveImage",(base_x+660,py+170),[prefix],
            [inp("images","IMAGE",l_save)],[],f"Save: {name}",(320,300)))
        # 그룹 박스
        groups.append({"title":f"요소: {name}","bounding":[base_x-20,py-10,1020,620],
                       "color":"#3f789e","font_size":24,"flags":{}})
        lane_y+=660

    # LoadImage outputs[0] 링크 채우기
    load_node["outputs"][0]["links"]=load_out_links

    wf={"last_node_id":nid[0],"last_link_id":_lid[0],
        "nodes":nodes,"links":links,"groups":groups,
        "config":{},"extra":{},"version":0.4}

    here=os.path.dirname(os.path.abspath(__file__))
    path=os.path.join(here,"decompose_all.json")
    with open(path,"w",encoding="utf-8") as f:
        json.dump(wf,f,ensure_ascii=False,indent=2)
    print("wrote decompose_all.json | nodes",len(nodes),"| links",len(links),"| groups",len(groups))

if __name__=="__main__":
    main()
