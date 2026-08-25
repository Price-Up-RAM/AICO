using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DogAnimPathDiagnostic
{
    public static void RunFromCLI()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/DogAnimationTestScene.unity");
        Diagnose();
    }

    [MenuItem("Tools/Dog/Diagnose Anim Paths")]
    public static void Diagnose()
    {
        string clipPath = "Assets/Dog/Woongjin/Models/Pet/CookieAnim_Idle01_origin2.fbx";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath)
            ?? AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().FirstOrDefault();
        if (clip == null) { Debug.LogError("Clip not found."); return; }

        var root = GameObject.Find("Pet_Dog_Cookie");
        if (root == null) { Debug.LogWarning("Pet_Dog_Cookie not found in open scene."); return; }

        var cookieOrigin = root.transform.Find("Cookie Origin");
        var pelvis = root.transform.Find("Cookie Origin/Bip001 Pelvis");

        Debug.Log("=== SUMMARY ===");
        Debug.Log($"Pet_Dog_Cookie: localScale={root.transform.localScale} lossyScale={root.transform.lossyScale} localPos={root.transform.localPosition}");
        if (cookieOrigin != null)
            Debug.Log($"Cookie Origin: localScale={cookieOrigin.localScale} lossyScale={cookieOrigin.lossyScale} localRot={cookieOrigin.localRotation.eulerAngles} localPos={cookieOrigin.localPosition}");
        if (pelvis != null)
            Debug.Log($"Bip001 Pelvis (scene, rest): localScale={pelvis.localScale} lossyScale={pelvis.lossyScale} localPos={pelvis.localPosition}");

        if (cookieOrigin != null)
        {
            Debug.Log($"Cookie Origin direct children count: {cookieOrigin.childCount}");
            foreach (Transform child in cookieOrigin)
                Debug.Log($"  Cookie Origin child: '{child.name}'");
        }

        // find ALL SkinnedMeshRenderers under Pet_Dog_Cookie
        var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Debug.Log($"SkinnedMeshRenderers under Pet_Dog_Cookie: {renderers.Length}");
        foreach (var r in renderers)
        {
            Debug.Log($"  Renderer '{r.name}' on GO path: {GetPath(r.transform)} rootBone={(r.rootBone != null ? r.rootBone.name : "NULL")} bounds={r.bounds}");
        }

        var latteRoot = GameObject.Find("Pet_Dog_Latte_01");
        if (latteRoot != null)
        {
            Debug.Log($"Pet_Dog_Latte_01: localScale={latteRoot.transform.localScale} lossyScale={latteRoot.transform.lossyScale} localPos={latteRoot.transform.localPosition}");
            foreach (Transform child in latteRoot.transform)
                Debug.Log($"  Latte child '{child.name}': localScale={child.localScale} lossyScale={child.lossyScale} localRot={child.localRotation.eulerAngles} localPos={child.localPosition}");
        }
        else
        {
            Debug.Log("Pet_Dog_Latte_01 not found in scene.");
        }

        // Compare: does Latte's own Idle_01 clip have a Position curve on Pelvis?
        var latteClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Dog/Woongjin/Models/Pet/Pet_Dog_Latte_01@Idle_01.FBX")
            ?? AssetDatabase.LoadAllAssetsAtPath("Assets/Dog/Woongjin/Models/Pet/Pet_Dog_Latte_01@Idle_01.FBX").OfType<AnimationClip>().FirstOrDefault();
        if (latteClip != null)
        {
            var latteBindings = AnimationUtility.GetCurveBindings(latteClip);
            var pelvisPathCandidates = latteBindings.Select(b => b.path).Distinct().Where(p => p.Contains("Pelvis")).ToList();
            Debug.Log($"Latte clip '{latteClip.name}': paths containing 'Pelvis': {string.Join(", ", pelvisPathCandidates)}");
            foreach (var pp in pelvisPathCandidates)
            {
                var pb = latteBindings.Where(b => b.path == pp).ToList();
                foreach (var b in pb)
                {
                    var curve = AnimationUtility.GetEditorCurve(latteClip, b);
                    var firstVal = curve != null && curve.length > 0 ? curve.keys[0].value : 0f;
                    Debug.Log($"  Latte Pelvis prop='{b.propertyName}' firstKeyValue={firstVal}");
                }
            }
        }
        else
        {
            Debug.Log("Latte Idle_01 clip not found.");
        }

        // Also: cookie clip's Pelvis position curve - does it vary across frames, or is it constant?
        var cookiePelvisPosBindings = AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.path == "Cookie Origin/Bip001 Pelvis" && b.propertyName.StartsWith("m_LocalPosition")).ToList();
        foreach (var b in cookiePelvisPosBindings)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, b);
            if (curve != null && curve.length > 0)
            {
                var vals = curve.keys.Select(k => k.value).ToList();
                Debug.Log($"Cookie Pelvis {b.propertyName}: min={vals.Min()} max={vals.Max()} keyCount={vals.Count}");
            }
        }
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
