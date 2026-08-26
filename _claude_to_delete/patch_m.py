# -*- coding: utf-8 -*-
# Editor에서도 InputField 포커스가 잡히게 한다.
# #if !UNITY_ANDROID 는 빌드 타겟이 Android면 Editor에서도 코드를 빼버린다.
# 이 프로젝트의 MR 분기 관례는 '#if UNITY_ANDROID || UNITY_EDITOR' 이므로 그 대칭으로 맞춘다.
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
"""        // InputField 포커스 설정 (Android 제외 - 렉 방지)
#if !UNITY_ANDROID
        inputField.Select();
        inputField.ActivateInputField();
#endif""",
"""        // InputField 포커스 설정 (실기 Android 제외 - 렉 방지, Kickoff Guide 4-16)
        // Editor는 제외하지 않는다: 빌드 타겟이 Android면 '#if !UNITY_ANDROID'가
        // Editor + Quest Link에서도 이 코드를 빼버려 포커스가 아예 안 잡힌다.
        // 실기에서만 막으면 되므로 UNITY_EDITOR를 예외로 둔다.
#if !UNITY_ANDROID || UNITY_EDITOR
        inputField.Select();
        inputField.ActivateInputField();
        Debug.Log($"[MRInput/진단] PrepareChatBalloon 포커스 시도 | isFocused={inputField.isFocused} interactable={inputField.interactable}");
#endif""",
"M1. PrepareChatBalloon 포커스 Editor 예외")

patch("ChatBalloonManager.cs",
"""        Debug.Log($"[ChatBalloonManager] STT 텍스트 추가됨: {sttText}");

        // InputField 포커스 (Android 제외)
#if !UNITY_ANDROID
        inputField.Select();
        inputField.ActivateInputField();
#endif""",
"""        Debug.Log($"[ChatBalloonManager] STT 텍스트 추가됨: {sttText}");

        // InputField 포커스 (실기 Android 제외). Editor는 위와 같은 이유로 예외.
#if !UNITY_ANDROID || UNITY_EDITOR
        inputField.Select();
        inputField.ActivateInputField();
#endif""",
"M2. AppendSTTText 포커스 Editor 예외")

sys.exit(0 if ok else 1)
