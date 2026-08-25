# -*- coding: utf-8 -*-
# unity_functions_list 실제 전송값 계측 — "보냈다고 믿는 것"과 "나간 것"을 가른다 (4-58)
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


patch("ApiVlRouterManager.cs",
"""        string unityFunctionsList = ApiAgentFunctionManager.Instance.GetFunctionsList();
        string unityFunctionsDetailList = ApiAgentFunctionManager.Instance.GetFunctionsDetailList();""",
"""        string unityFunctionsList = ApiAgentFunctionManager.Instance.GetFunctionsList();
        string unityFunctionsDetailList = ApiAgentFunctionManager.Instance.GetFunctionsDetailList();

        // 계측: 서버로 '실제로' 나가는 함수 목록. 필터가 반영됐는지, 서버가 이걸 쓰는지 대조용.
        // 목록을 만들었다는 것과 그 목록이 전송된다는 것은 다른 사실이다 (Kickoff Guide 4-58).
        Debug.Log($"[AgentFunc/전송] unity_functions_list ({unityFunctionsList.Length}자) = {unityFunctionsList}");
        Debug.Log($"[AgentFunc/전송] unity_functions_detail_list ({unityFunctionsDetailList.Length}자)");""",
"G1. 전송 목록 계측")

patch("ApiVlRouterManager.cs",
"""            WriteFormField(writer, boundary, "unity_functions_list", unityFunctionsList ?? "");
            WriteFormField(writer, boundary, "unity_functions_detail_list", unityFunctionsDetailList ?? "");""",
"""            // 계측: 폼 필드에 실제로 실린 길이. 위 [AgentFunc/전송] 값과 일치해야 한다.
            // 0이면 목록은 만들어졌는데 전송 직전에 사라졌다는 뜻이다.
            string fieldList = unityFunctionsList ?? "";
            string fieldDetail = unityFunctionsDetailList ?? "";
            Debug.Log($"[AgentFunc/전송] 폼 필드 적재: list={fieldList.Length}자 detail={fieldDetail.Length}자");
            WriteFormField(writer, boundary, "unity_functions_list", fieldList);
            WriteFormField(writer, boundary, "unity_functions_detail_list", fieldDetail);""",
"G2. 폼 필드 적재 계측")

sys.exit(0 if ok else 1)
