using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// NotoSansKR 정적 SDF 베이크 + 폰트별 Outline 머티리얼 프리셋 생성.
// MY-Little-Jarvis(원본 프로젝트) Text→TMP 전환의 기본 본문 폰트 생산용 — 산출물은
// FontAssets의 SDF/프리셋을 스크립트 저장소로 이관해 사용한다.
// 차셋: Assets/Editor/NotoKR_charset.txt (ASCII + KS X 1001 한글 2350 + 호환자모 + 기호 + 실사용 문자).
// 한글/ASCII/자모가 아틀라스에 못 들어가면(하드 미싱) pointSize를 낮춰 재시도한다.
// LanguageData 유래 일본어 한자 등은 폴백(NotoSansJP→SUIT-Bold) 몫이라 소프트 미싱으로 허용.
public static class NotoKRFontBaker
{
    private const string OtfPath = "Assets/FontAssets/NotoSansKR-Regular.otf";
    private const string CharsetPath = "Assets/Editor/NotoKR_charset.txt";
    private const string OutPath = "Assets/FontAssets/NotoSansKR-Regular SDF.asset";
    private const string JpSdfPath = "Assets/FontAssets/NotoSansJP-Regular SDF.asset";
    private const string SuitPath = "Assets/FontAssets/SUIT-Bold.asset";
    private const string KrOutlineMatPath = "Assets/FontAssets/NotoSansKR-Regular SDF Outline.mat";
    private const string SuitOutlineMatPath = "Assets/FontAssets/SUIT-Bold Outline.mat";

    [MenuItem("Tools/Font/Bake NotoSansKR SDF")]
    public static void Bake()
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(OtfPath);
        if (font == null)
        {
            Debug.LogError("[NotoKRBaker] otf 로드 실패: " + OtfPath);
            return;
        }

        if (File.Exists(CharsetPath) == false)
        {
            Debug.LogError("[NotoKRBaker] charset 파일 없음: " + CharsetPath);
            return;
        }

        string charset = File.ReadAllText(CharsetPath);
        Debug.Log("[NotoKRBaker] charset " + charset.Length + "자 로드");

        // pointSize/padding 사다리 — 4096x4096 단일 아틀라스(멀티 아틀라스 금지)에 하드 문자가 다 들어갈 때까지 축소
        int[] sizes = { 64, 60, 56, 52 };
        int[] pads = { 8, 7, 7, 6 };
        TMP_FontAsset fa = null;
        string missing = null;

        for (int i = 0; i < sizes.Length; i++)
        {
            fa = TMP_FontAsset.CreateFontAsset(font, sizes[i], pads[i], GlyphRenderMode.SDFAA, 4096, 4096,
                AtlasPopulationMode.Dynamic, false);
            if (fa == null)
            {
                Debug.LogError("[NotoKRBaker] CreateFontAsset 실패");
                return;
            }

            fa.TryAddCharacters(charset, out missing, false);

            bool hardMissing = false;
            if (string.IsNullOrEmpty(missing) == false)
            {
                foreach (char c in missing)
                {
                    if ((c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x20 && c <= 0x7E) || (c >= 0x3131 && c <= 0x3163))
                    {
                        hardMissing = true;
                        break;
                    }
                }
            }

            if (hardMissing == false)
            {
                int soft = string.IsNullOrEmpty(missing) ? 0 : missing.Length;
                Debug.Log("[NotoKRBaker] pointSize " + sizes[i] + "/pad " + pads[i] + " 채택 (소프트 미싱 " + soft + "자 — 폴백 몫)");
                break;
            }

            Debug.LogWarning("[NotoKRBaker] pointSize " + sizes[i] + " 하드 미싱 — 축소 재시도");
            Object.DestroyImmediate(fa.material, true);
            Object.DestroyImmediate(fa.atlasTexture, true);
            Object.DestroyImmediate(fa, true);
            fa = null;
        }

        if (fa == null)
        {
            Debug.LogError("[NotoKRBaker] 모든 pointSize에서 하드 미싱 — 아틀라스 확장 필요");
            return;
        }

        // 정적 전환(소스 폰트 참조 절단) 후 서브에셋 구성으로 저장
        fa.atlasPopulationMode = AtlasPopulationMode.Static;
        fa.name = "NotoSansKR-Regular SDF";
        Material mat = fa.material;
        Texture2D tex = fa.atlasTexture;
        mat.name = fa.name + " Material";
        tex.name = fa.name + " Atlas";

        if (File.Exists(OutPath))
        {
            AssetDatabase.DeleteAsset(OutPath);
        }

        AssetDatabase.CreateAsset(fa, OutPath);
        AssetDatabase.AddObjectToAsset(tex, fa);
        AssetDatabase.AddObjectToAsset(mat, fa);

        // 폴백 체인: KR → JP(가나/일본 한자) → SUIT-Bold(최후)
        TMP_FontAsset jp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JpSdfPath);
        TMP_FontAsset suit = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SuitPath);
        fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (jp != null)
        {
            fa.fallbackFontAssetTable.Add(jp);
        }
        if (suit != null)
        {
            fa.fallbackFontAssetTable.Add(suit);
        }
        EditorUtility.SetDirty(fa);

        // Outline 프리셋 (레거시 uGUI Outline dist=1 근사 — 폭 0.12, 검정 50%)
        CreateOutlinePreset(mat, KrOutlineMatPath, "NotoSansKR-Regular SDF Outline");
        if (suit != null)
        {
            CreateOutlinePreset(suit.material, SuitOutlineMatPath, "SUIT-Bold Outline");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NotoKRBaker] DONE chars=" + fa.characterTable.Count + " glyphs=" + fa.glyphTable.Count
            + " atlas=" + fa.atlasWidth + "x" + fa.atlasHeight
            + " guid=" + AssetDatabase.AssetPathToGUID(OutPath));
    }

    // 폰트 머티리얼 복제 후 아웃라인 파라미터만 설정 — 아틀라스 텍스처 바인딩은 복제로 승계
    private static void CreateOutlinePreset(Material baseMat, string path, string name)
    {
        Material m = new Material(baseMat);
        m.name = name;
        if (m.HasProperty("_OutlineWidth"))
        {
            m.SetFloat("_OutlineWidth", 0.12f);
        }
        if (m.HasProperty("_OutlineColor"))
        {
            m.SetColor("_OutlineColor", new Color(0f, 0f, 0f, 0.5f));
        }
        m.EnableKeyword("OUTLINE_ON");

        if (File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.CreateAsset(m, path);
        Debug.Log("[NotoKRBaker] Outline 프리셋: " + path + " guid=" + AssetDatabase.AssetPathToGUID(path));
    }
}
