# -*- coding: utf-8 -*-
# ShowChatBalloon 진단 + 활성화 보장
import sys, os

ROOT = os.path.expanduser("~/mnt/UnityProject--AICO/Assets/Scripts")
ok = True


def eol_of(p):
    b = open(p, "rb").read()
    crlf = b.count(b"\r\n")
    if crlf > (b.count(b"\n") - crlf):
        return "\r\n"
    return "\n"


def patch(path, old, new, label):
    global ok
    p = os.path.join(ROOT, path)
    e = eol_of(p)
    data = open(p, "rb").read()
    o = old.replace("\n", e).encode("utf-8")
    n = new.replace("\n", e).encode("utf-8")
    c = data.count(o)
    if c != 1:
        print("FAIL %s : 앵커 %d회 매치" % (label, c))
        ok = False
        return
    open(p, "wb").write(data.replace(o, n))
    print("OK   %s" % label)


patch("ChatBalloonManager.cs",
"""    // chatBalloon을 보이고 텍스트를 초기화하는 함수
    public void ShowChatBalloon()
    {
        SetModeTop(true);
    }""",
"""    // chatBalloon을 보이고 텍스트를 초기화하는 함수
    public void ShowChatBalloon()
    {
        // PrepareChatBalloon은 AnswerBalloonManager / AnswerBalloonSimpleManager /
        // StatusManager / ChatModeManager / AnimationManager를 null 가드 없이 부른다.
        // 그 중 하나라도 MR 씬에서 비어 있으면 chatBalloon.SetActive(true)에 도달하기 전에 죽는다.
        // '호출했다'와 '켜졌다'는 다른 사실이므로 결과를 확인한다 (Kickoff Guide 4-58).
        try
        {
            SetModeTop(true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChatBalloon] SetModeTop 실패: {e.GetType().Name} {e.Message}\\n{e.StackTrace}");
        }

        // 폴백 — 위에서 예외가 났어도 말풍선 자체는 켠다.
        if (chatBalloon != null && !chatBalloon.activeSelf)
        {
            Debug.LogWarning("[ChatBalloon] SetModeTop 후에도 꺼져 있어 직접 활성화한다");
            chatBalloon.SetActive(true);
            if (StatusManager.Instance != null)
            {
                StatusManager.Instance.IsChatting = true;
            }
            if (chatBalloonMode == "off")
            {
                chatBalloonMode = "char";
            }
        }

        string active = "(chatBalloon이 null)";
        if (chatBalloon != null)
        {
            active = $"{chatBalloon.activeSelf}/{chatBalloon.activeInHierarchy}";
        }
        Debug.Log($"[ChatBalloon] ShowChatBalloon 결과 | activeSelf/InHierarchy={active} mode={chatBalloonMode} inputField={(inputField == null ? "null" : "있음")}");
    }""",
"L1. ShowChatBalloon 진단 + 폴백")

sys.exit(0 if ok else 1)
