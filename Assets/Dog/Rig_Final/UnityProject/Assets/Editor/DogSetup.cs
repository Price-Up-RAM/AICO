using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// cookie_all.fbx 임포트 설정 + Animator Controller + 데모 씬을 코드로 만든다.
/// 손으로 클릭해서 세팅한 값은 재현이 안 되므로 전부 스크립트로 고정해 둔 것이다.
/// AICO 로 이식할 때도 이 스크립트를 그대로 실행하면 동일한 결과가 나온다.
/// </summary>
public static class DogSetup
{
    const string FBX   = "Assets/Dog/cookie_all.fbx";
    const string CTRL  = "Assets/Dog/Cookie_AnimCtrl.controller";
    const string SCENE = "Assets/DogDemo.unity";
    const float  YAW   = 100f;

    // Unity 임포터 기본값은 정점당 본 4개로 잘라내고 재정규화한다.
    // 웨이트 스무딩으로 영향 본이 늘어난 상태에서 4개로 자르면 변형이 어긋나 스파이크가 생긴다.
    // 실측: 4본 -> Sit_02 6.72x / Walk 8.84x,  8본 -> Sit_02 4.53x / Walk 4.81x
    const int MAX_BONES_PER_VERTEX = 8;

    // 루프 재생할 클립
    static readonly string[] LOOPING = { "cookie_Idle_01", "cookie_Idle_02",
                                         "cookie_Walk", "cookie_WalkCycle" };

    // AICO 의 DogAnimationController 와 동일한 파라미터 이름 (bWalk2 만 신규)
    static readonly string[] TRIGGERS = { "tDefault", "tJump", "tSit1", "tSit2",
                                          "tUnique1", "tUnique2", "tUnique3",
                                          "tUnique4", "tUnique5" };

    // 트리거 -> 스테이트
    static readonly Dictionary<string, string> ONESHOT = new Dictionary<string, string> {
        { "Jump", "tJump" }, { "Sit_01", "tSit1" }, { "Sit_02", "tSit2" },
        { "Unique_01", "tUnique1" }, { "Unique_04", "tUnique4" }, { "Unique_05", "tUnique5" },
    };

    static void Log(string s) { Debug.Log("[DogSetup] " + s); }
    static void Warn(string s) { Debug.LogWarning("[DogSetup] " + s); }

    [MenuItem("Dog/1. Reimport + Rebuild Controller", false, 1)]
    public static void Reimport()
    {
        AssetDatabase.Refresh();
        var mi = AssetImporter.GetAtPath(FBX) as ModelImporter;
        if (mi == null) { Debug.LogError("[DogSetup] 없음: " + FBX); return; }

        // defaultClipAnimations 로 다시 읽어야 FBX 의 테이크가 전부 반영된다.
        // clipAnimations 를 한번 명시 지정하면 Unity 가 그 목록에 고정되어 새 테이크를 무시한다.
        var clips = mi.defaultClipAnimations;
        Log("FBX 테이크 " + clips.Length + "개: " + string.Join(", ", clips.Select(c => c.name)));
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].loopTime = LOOPING.Any(l => clips[i].name.Contains(l));
            clips[i].loop = false;
        }
        mi.clipAnimations = clips;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        mi.materialLocation = ModelImporterMaterialLocation.External;
        mi.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
        mi.skinWeights = ModelImporterSkinWeights.Custom;
        mi.maxBonesPerVertex = MAX_BONES_PER_VERTEX;
        EditorUtility.SetDirty(mi);
        mi.SaveAndReimport();
        Log("루프 ON: " + string.Join(", ", clips.Where(c => c.loopTime).Select(c => c.name)));
        Log("maxBonesPerVertex = " + MAX_BONES_PER_VERTEX);

        if (QualitySettings.skinWeights != SkinWeights.Unlimited)
            Warn("QualitySettings.skinWeights = " + QualitySettings.skinWeights
                 + " -> 런타임에서 다시 잘린다. Project Settings > Quality 에서 Unlimited 로 올려야 한다.");

        BuildController();
    }

    static void BuildController()
    {
        var byName = new Dictionary<string, AnimationClip>();
        foreach (var c in AssetDatabase.LoadAllAssetsAtPath(FBX).OfType<AnimationClip>())
        {
            if (c.name.StartsWith("__preview")) continue;
            int p = c.name.IndexOf("cookie_");
            byName[p >= 0 ? c.name.Substring(p + 7) : c.name] = c;
        }
        Log("클립 " + byName.Count + "개: " + string.Join(", ", byName.Keys.OrderBy(k => k)));
        if (!byName.ContainsKey("WalkCycle")) Warn("cookie_WalkCycle 이 없다 -> bWalk2 생략됨");

        // 에셋을 지우고 다시 만들면 GUID 가 바뀌어 씬/프리팹의 Animator 참조가 끊긴다.
        // 기존 에셋을 유지한 채 내용만 비우고 재구성한다.
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(CTRL);
        else
        {
            while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);
            var sm0 = ctrl.layers[0].stateMachine;
            foreach (var t in sm0.anyStateTransitions.ToArray()) sm0.RemoveAnyStateTransition(t);
            foreach (var cs in sm0.states.ToArray()) sm0.RemoveState(cs.state);
        }

        foreach (var t in TRIGGERS) ctrl.AddParameter(t, AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("bWalk", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("bWalk2", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        var states = new Dictionary<string, AnimatorState>();
        foreach (var kv in byName.OrderBy(k => k.Key))
        {
            var st = sm.AddState(kv.Key == "WalkCycle" ? "Walk2" : kv.Key);
            st.motion = kv.Value;
            states[kv.Key] = st;
        }
        if (!states.ContainsKey("Idle_01")) { Debug.LogError("[DogSetup] Idle_01 없음"); return; }
        var idle = states["Idle_01"];
        sm.defaultState = idle;

        WireLoop(idle, states, "Walk", "bWalk");
        WireLoop(idle, states, "WalkCycle", "bWalk2");

        foreach (var kv in ONESHOT)
        {
            if (!states.ContainsKey(kv.Key)) continue;
            var any = sm.AddAnyStateTransition(states[kv.Key]);
            any.AddCondition(AnimatorConditionMode.If, 0, kv.Value);
            any.hasExitTime = false; any.duration = 0.1f; any.canTransitionToSelf = false;
            var back = states[kv.Key].AddTransition(idle);
            back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.15f;
        }
        var toIdle = sm.AddAnyStateTransition(idle);
        toIdle.AddCondition(AnimatorConditionMode.If, 0, "tDefault");
        toIdle.hasExitTime = false; toIdle.duration = 0.1f; toIdle.canTransitionToSelf = false;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Log("컨트롤러 완료: states=" + sm.states.Length
            + " params=" + string.Join(",", ctrl.parameters.Select(p => p.name)));
    }

    static void WireLoop(AnimatorState idle, Dictionary<string, AnimatorState> states,
                         string key, string param)
    {
        if (!states.ContainsKey(key)) return;
        var st = states[key];
        var go = idle.AddTransition(st);
        go.AddCondition(AnimatorConditionMode.If, 0, param);
        go.hasExitTime = false; go.duration = 0.15f;
        var back = st.AddTransition(idle);
        back.AddCondition(AnimatorConditionMode.IfNot, 0, param);
        back.hasExitTime = false; back.duration = 0.15f;
        Log(st.name + " <-> Idle_01 (" + param + ")");
    }

    [MenuItem("Dog/2. Build Demo Scene", false, 2)]
    public static void BuildScene()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = Vector3.one * 0.6f;

        var prefab = AssetDatabase.LoadAllAssetsAtPath(FBX).OfType<GameObject>()
                       .First(g => g.transform.parent == null);
        var dog = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        dog.name = "Cookie";
        dog.transform.position = Vector3.zero;
        dog.transform.rotation = Quaternion.Euler(0f, YAW, 0f);

        var an = dog.GetComponent<Animator>();
        if (an == null) an = dog.AddComponent<Animator>();
        an.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(CTRL);
        an.applyRootMotion = false;
        an.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var host = new GameObject("DogAnimDemo");
        var demo = host.AddComponent<DogAnimDemo>();
        demo.animator = an;
        demo.yaw = YAW;

        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            cam.transform.position = new Vector3(1.35f, 0.5f, -1.35f);
            cam.transform.LookAt(new Vector3(0f, 0.3f, 0f));
            var c = cam.GetComponent<Camera>();
            if (c != null)
            {
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.18f, 0.20f, 0.24f);
            }
        }
        var light = GameObject.Find("Directional Light");
        if (light != null) light.transform.rotation = Quaternion.Euler(45f, 140f, 0f);

        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, SCENE);
        Log("데모 씬 저장: " + SCENE);
    }

    /// <summary>배치 실행용 (-executeMethod DogSetup.BatchAll)</summary>
    public static void BatchAll()
    {
        QualitySettings.skinWeights = SkinWeights.Unlimited;
        Reimport();
        BuildScene();
    }
}
