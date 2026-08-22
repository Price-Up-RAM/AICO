using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// GPU 없이 수치로 검증한다. Animator 를 실제로 돌려서(Animator.Update) 스킨 결과를
/// SkinnedMeshRenderer.BakeMesh 로 뽑아 몸 크기를 재면, 렌더링 없이도 메쉬가
/// 찌그러지는지 판정할 수 있다. RDP / 헤드리스 환경에서도 동작한다.
/// 메뉴: Dog > 3. Verify
/// </summary>
public static class DogVerify
{
    const string FBX  = "Assets/Dog/cookie_all.fbx";
    const string CTRL = "Assets/Dog/Cookie_AnimCtrl.controller";
    const float REST_HEIGHT = 0.62f;   // 쿠키 레스트 상태 키 (m)

    static void L(string s) { Debug.Log("[DogVerify] " + s); }

    [MenuItem("Dog/3. Verify", false, 3)]
    public static void Run()
    {
        AssetDatabase.Refresh();

        var mi = (ModelImporter)AssetImporter.GetAtPath(FBX);
        L("importer: animationType=" + mi.animationType + " skinWeights=" + mi.skinWeights
          + " maxBonesPerVertex=" + mi.maxBonesPerVertex);
        L("QualitySettings.skinWeights = " + QualitySettings.skinWeights
          + (QualitySettings.skinWeights == SkinWeights.Unlimited ? "" : "   <== Unlimited 이어야 함"));

        // 정점당 본 개수 (임포터가 잘랐는지 확인)
        var mesh = AssetDatabase.LoadAllAssetsAtPath(FBX).OfType<Mesh>()
                     .OrderByDescending(m => m.vertexCount).First();
        var bpv = mesh.GetBonesPerVertex();
        int over4 = 0, maxN = 0;
        for (int i = 0; i < bpv.Length; i++) { if (bpv[i] > 4) over4++; if (bpv[i] > maxN) maxN = bpv[i]; }
        L("mesh verts=" + mesh.vertexCount + "  정점당 본 최대=" + maxN + "  >4본 정점=" + over4
          + (over4 > 0 ? "  (절단 안 됨, 정상)" : "  <== 4본으로 잘렸다"));

        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL);
        L("controller states: " + string.Join(", ",
            ctrl.layers[0].stateMachine.states.Select(s => s.state.name)));
        L("controller params: " + string.Join(", ", ctrl.parameters.Select(p => p.name)));

        foreach (var c in AssetDatabase.LoadAllAssetsAtPath(FBX).OfType<AnimationClip>()
                            .Where(c => !c.name.StartsWith("__preview")).OrderBy(c => c.name))
        {
            var st = AnimationUtility.GetAnimationClipSettings(c);
            L(string.Format("  clip {0,-30} {1,5:F2}s loop={2}",
                c.name.Replace("Bip001|cookie_", ""), c.length, st.loopTime ? "ON" : "off"));
        }

        // Animator 로 각 스테이트를 실제 재생하며 몸 크기 측정
        var prefab = AssetDatabase.LoadAllAssetsAtPath(FBX).OfType<GameObject>()
                       .First(g => g.transform.parent == null);
        L("--- Animator 재생 검증 (기준 키 " + REST_HEIGHT.ToString("F2") + " m) ---");
        foreach (var state in ctrl.layers[0].stateMachine.states.Select(s => s.state.name).OrderBy(s => s))
            Drive(prefab, ctrl, state);
        L("=== done");
    }

    static void Drive(GameObject prefab, RuntimeAnimatorController ctrl, string state)
    {
        var inst = (GameObject)Object.Instantiate(prefab);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var smr = inst.GetComponentsInChildren<SkinnedMeshRenderer>().First();

        anim.Rebind();
        if (state == "Walk") anim.SetBool("bWalk", true);
        if (state == "Walk2") anim.SetBool("bWalk2", true);
        anim.Play(state, 0, 0f);
        anim.Update(0f);

        var baked = new Mesh();
        float hMin = 99f, hMax = 0f, yLo = 99f;
        for (int i = 0; i < 24; i++)
        {
            anim.Update(3.0f / 24f);
            smr.BakeMesh(baked, true);
            var lv = baked.vertices;
            if (lv.Length == 0) continue;
            float lo = 99f, hi = -99f;
            foreach (var v in lv)
            {
                var w = smr.transform.TransformPoint(v);
                if (w.y < lo) lo = w.y;
                if (w.y > hi) hi = w.y;
            }
            if (hi - lo < hMin) hMin = hi - lo;
            if (hi - lo > hMax) hMax = hi - lo;
            if (lo < yLo) yLo = lo;
        }
        bool bad = hMax > REST_HEIGHT * 1.6f || hMax < REST_HEIGHT * 0.6f;
        L(string.Format("  {0,-12} height {1:F3}..{2:F3} m   minY {3,7:F3}   {4}",
            state, hMin, hMax, yLo, bad ? "<<<< 이상" : "정상"));
        Object.DestroyImmediate(inst);
    }
}
