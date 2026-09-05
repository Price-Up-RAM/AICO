# -*- coding: utf-8 -*-
# MRSceneStripper 전용 패치 — 파일의 실제 개행을 자동 판별해 사용
import sys, os

P = os.path.expanduser("~/mnt/AICO/Assets/Scripts/MR/MRSceneStripper.cs")
ok = True

data = open(P, "rb").read()
crlf = data.count(b"\r\n")
lf_only = data.count(b"\n") - crlf
if crlf > lf_only:
    EOL = "\r\n"
else:
    EOL = "\n"
print("개행 판별: CRLF=%d LF단독=%d → 사용=%s" % (crlf, lf_only, repr(EOL)))


def patch(old, new, label):
    global data, ok
    o = old.replace("\n", EOL).encode("utf-8")
    n = new.replace("\n", EOL).encode("utf-8")
    c = data.count(o)
    if c != 1:
        print("FAIL %s : 앵커 %d회 매치" % (label, c))
        ok = False
        return
    data = data.replace(o, n)
    print("OK   %s" % label)


patch(
"""        typeof(ApiVlPlannerManager),
        typeof(ApiVlRouterManager),
        typeof(ApiVlRouterResponseManager),
        typeof(ApiAgentFunctionManager),""",
"""        typeof(ApiVlPlannerManager),
        // ApiVlRouterManager / ApiVlRouterResponseManager / ApiAgentFunctionManager는
        // 2026-08-24 Phase 5에서 제외를 해제했다 (ReviewedKeepTypes로 이동).
        // 이름의 Vl 접두사와 달리 화면 조작과 무관하고, 춤·일정·음원 스킬의 실행기다.""",
"B1. DesktopOnlyTypes에서 3종 제거")

patch(
"""        typeof(ChatModeManager), typeof(ChatHandler), typeof(ServerManager),""",
"""        typeof(ChatModeManager), typeof(ChatHandler), typeof(ServerManager),
        // 라우터 + 스킬 (2026-08-24 Phase 5에서 제외 해제).
        // ApiVlRouterManager = /router/job/run 호출, ApiVlRouterResponseManager = 툴콜 디스패치,
        // ApiAgentFunctionManager = character_dance / todo_* / play_sfx 실행기.
        // 화면 캡처는 intent_image 가드 안에만 있고, MR에서는 손 프레임 주입분을 쓴다.
        typeof(ApiVlRouterManager), typeof(ApiVlRouterResponseManager), typeof(ApiAgentFunctionManager),""",
"B2. ReviewedKeepTypes에 3종 추가")

if ok:
    open(P, "wb").write(data)
    print("저장 완료")
sys.exit(0 if ok else 1)
