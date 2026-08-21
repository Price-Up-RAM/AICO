using System.Collections.Generic;
using UnityEngine;

public enum HologramStyle
{
    Original,       // 원본 텍스처 (불투명)
    Transparent,    // 반투명 (원본 텍스처 그대로 투명도만 50%)
    SolidBlue,      // 반투명 + 하늘색 단색 (텍스처 무시)
    MixedBlue       // 절반 단색 + 절반 원본 (하늘색 틴트 + 반투명)
}

[DefaultExecutionOrder(1000)]
public class MRHologramPortrait : MonoBehaviour
{
    [Header("스타일 설정")]
    [Tooltip("에디터에서 런타임 중에 실시간으로 스타일을 변경해 볼 수 있습니다.")]
    public HologramStyle currentStyle = HologramStyle.MixedBlue;
    private HologramStyle _lastStyle = HologramStyle.Original;

    [Header("설정")]
    [Tooltip("홀로그램 렌더러에 일괄 적용할 머티리얼. 비워두면 자동 생성합니다.")]
    public Material hologramMaterial;
    
    [Tooltip("홀로그램의 크기 비율 (원본 캐릭터의 월드 스케일 대비, 기본 10%)")]
    public float scaleMultiplier = 0.1f;

    [Tooltip("손목 위 오프셋")]
    public Vector3 localOffset = new Vector3(0, 0.1f, 0);

    private GameObject _originalCharacter;
    private GameObject _cloneCharacter;
    
    // 원본 뼈대 -> 복제 뼈대 매핑
    private Dictionary<Transform, Transform> _boneMap = new Dictionary<Transform, Transform>();
    
    // 블렌드셰이프 동기화를 위한 렌더러 매핑
    private Dictionary<Renderer, Renderer> _rendererMap = new Dictionary<Renderer, Renderer>();

    public void SetCharacter(GameObject original)
    {
        if (_originalCharacter == original) return;

        ClearHologram();
        _originalCharacter = original;

        if (_originalCharacter != null)
        {
            CreateHologram();
        }
    }

    private void ClearHologram()
    {
        if (_cloneCharacter != null)
        {
            Destroy(_cloneCharacter);
            _cloneCharacter = null;
        }
        _boneMap.Clear();
        _rendererMap.Clear();
    }

    private void CreateHologram()
    {
        // 1. 원본 복제
        _cloneCharacter = Instantiate(_originalCharacter, transform);
        _cloneCharacter.name = _originalCharacter.name + "_Hologram";
        
        // 2. 위치 및 크기 초기화
        _cloneCharacter.transform.localPosition = localOffset;
        _cloneCharacter.transform.localRotation = Quaternion.identity;
        // 크기는 LateUpdate에서 원본의 lossyScale을 기준으로 실시간 계산합니다.

        // 3. 불필요한 컴포넌트 제거 (물리, 애니메이터, 스크립트 등)
        // DestroyImmediate를 사용해야 복제 직후 원본 캐릭터의 스크립트가 실행되어 위치나 크기를 망가뜨리는 것을 방지할 수 있습니다.
        Component[] allComponents = _cloneCharacter.GetComponentsInChildren<Component>(true);
        foreach (var comp in allComponents)
        {
            if (comp is Transform || comp is SkinnedMeshRenderer || comp is MeshRenderer || comp is MeshFilter)
                continue;
            
            DestroyImmediate(comp);
        }

        // 4. 뼈대 매핑 (이름 기준)
        Transform[] originalBones = _originalCharacter.GetComponentsInChildren<Transform>(true);
        Transform[] cloneBones = _cloneCharacter.GetComponentsInChildren<Transform>(true);

        Dictionary<string, Transform> cloneBoneDict = new Dictionary<string, Transform>();
        foreach (var cb in cloneBones)
        {
            if (!cloneBoneDict.ContainsKey(cb.name))
                cloneBoneDict.Add(cb.name, cb);
        }

        foreach (var ob in originalBones)
        {
            if (ob == _originalCharacter.transform) continue;

            if (cloneBoneDict.TryGetValue(ob.name, out Transform cb))
            {
                if (cb == _cloneCharacter.transform) continue;
                _boneMap[ob] = cb;
            }
        }

        // 5. 렌더러 매핑 (MeshRenderer 포함)
        Renderer[] originalSmrs = _originalCharacter.GetComponentsInChildren<Renderer>(true);
        Renderer[] cloneSmrs = _cloneCharacter.GetComponentsInChildren<Renderer>(true);

        foreach (var oSmr in originalSmrs)
        {
            foreach (var cSmr in cloneSmrs)
            {
                if (oSmr.name == cSmr.name)
                {
                    _rendererMap[oSmr] = cSmr;
                    // cSmr.gameObject.layer = gameObject.layer; // 제거: 원본 레이어를 유지해야 조명을 받음
                    break;
                }
            }
        }
        
        // 충돌체 추가
        BoxCollider bc = _cloneCharacter.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.0f, 0); 
        bc.size = new Vector3(0.6f, 2.0f, 0.6f); 

        // 홀로그램 전용 얼굴 조명 (무서운 그림자 방지)
        GameObject lightObj = new GameObject("HologramFaceLight");
        lightObj.transform.SetParent(_cloneCharacter.transform);
        // 캐릭터 머리 약간 위, 앞쪽에 배치 (로컬 기준 Y: 1.6m, Z: 0.8m)
        lightObj.transform.localPosition = new Vector3(0f, 1.6f, 0.8f);
        Light fillLight = lightObj.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.range = 3.0f; // 10% 축소시 약 0.3m 반경
        fillLight.intensity = 1.0f;
        fillLight.color = new Color(1.0f, 0.98f, 0.95f); // 자연스러운 톤

        // 스타일 최초 적용
        _lastStyle = currentStyle;
        ApplyHologramStyle();
    }

    private void Update()
    {
        if (currentStyle != _lastStyle)
        {
            _lastStyle = currentStyle;
            ApplyHologramStyle();
        }
    }

    private void ApplyHologramStyle()
    {
        if (_rendererMap.Count == 0) return;

        foreach (var kvp in _rendererMap)
        {
            Renderer oSmr = kvp.Key;
            Renderer cSmr = kvp.Value;

            if (currentStyle == HologramStyle.Original)
            {
                cSmr.sharedMaterials = oSmr.sharedMaterials;
                continue;
            }

            Material[] newMats = new Material[oSmr.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                Material oMat = oSmr.sharedMaterials[i];
                Material nMat;

                if (currentStyle == HologramStyle.SolidBlue)
                {
                    nMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    MakeMaterialTransparent(nMat);
                    SetMaterialColor(nMat, new Color(0.2f, 0.6f, 1.0f, 0.7f));
                    nMat.EnableKeyword("_EMISSION");
                    nMat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.8f, 1.0f));
                }
                else if (currentStyle == HologramStyle.Transparent)
                {
                    nMat = new Material(oMat);
                    MakeMaterialTransparent(nMat);
                    SetMaterialAlpha(nMat, 0.5f);
                }
                else // MixedBlue
                {
                    nMat = new Material(oMat);
                    MakeMaterialTransparent(nMat);
                    BlendMaterialColor(nMat, new Color(0.2f, 0.6f, 1.0f, 0.7f), 0.5f);
                    nMat.EnableKeyword("_EMISSION");
                    nMat.SetColor("_EmissionColor", new Color(0.05f, 0.2f, 0.4f, 1.0f));
                }

                newMats[i] = nMat;
            }
            cSmr.sharedMaterials = newMats;
        }
    }

    private void MakeMaterialTransparent(Material mat)
    {
        // MToon (VRM)
        if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 2); 
        // URP / Standard
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0);
        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2); 

        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        string[] colorProps = { "_BaseColor", "_Color", "_Tint" };
        foreach (var prop in colorProps)
        {
            if (mat.HasProperty(prop)) mat.SetColor(prop, color);
        }
    }

    private void SetMaterialAlpha(Material mat, float alpha)
    {
        string[] colorProps = { "_BaseColor", "_Color", "_Tint" };
        foreach (var prop in colorProps)
        {
            if (mat.HasProperty(prop))
            {
                Color c = mat.GetColor(prop);
                c.a = alpha;
                mat.SetColor(prop, c);
            }
        }
    }

    private void BlendMaterialColor(Material mat, Color targetColor, float t)
    {
        string[] colorProps = { "_BaseColor", "_Color", "_Tint" };
        foreach (var prop in colorProps)
        {
            if (mat.HasProperty(prop))
            {
                Color c = mat.GetColor(prop);
                Color blended = Color.Lerp(c, targetColor, t);
                blended.a = targetColor.a; 
                mat.SetColor(prop, blended);
            }
        }
    }

    private float _yawOffset = 0f;

    private void OnEnable()
    {
        // 켜질 때마다 카메라를 바라보도록 오프셋 계산
        if (_originalCharacter != null && Camera.main != null)
        {
            Vector3 camFwd = Camera.main.transform.forward;
            camFwd.y = 0;
            Vector3 charFwd = _originalCharacter.transform.forward;
            charFwd.y = 0;

            if (camFwd.sqrMagnitude > 0.001f && charFwd.sqrMagnitude > 0.001f)
            {
                // 실제 캐릭터가 바라보는 방향과 카메라가 바라보는 방향(의 반대)의 차이
                float camYaw = Quaternion.LookRotation(-camFwd).eulerAngles.y;
                float charYaw = Quaternion.LookRotation(charFwd).eulerAngles.y;
                _yawOffset = camYaw - charYaw;
            }
        }
    }

    private void LateUpdate()
    {
        if (_originalCharacter == null || _cloneCharacter == null) return;

        // 0. 최상위 루트 회전 적용 (카메라를 바라보도록 Yaw 보정)
        _cloneCharacter.transform.rotation = Quaternion.Euler(0, _yawOffset, 0) * _originalCharacter.transform.rotation;

        // 0.1 최상위 루트 스케일 동기화 (원본 캐릭터의 최종 월드 스케일 기준)
        // 프리팹/모델 어느 쪽에 스케일이 들어있든 상관없이, 최종 월드 스케일(lossyScale)의 scaleMultiplier(10%)가 되도록 계산
        Vector3 targetWorldScale = _originalCharacter.transform.lossyScale * scaleMultiplier;
        Vector3 parentLossyScale = _cloneCharacter.transform.parent != null ? _cloneCharacter.transform.parent.lossyScale : Vector3.one;
        _cloneCharacter.transform.localScale = new Vector3(
            targetWorldScale.x / parentLossyScale.x,
            targetWorldScale.y / parentLossyScale.y,
            targetWorldScale.z / parentLossyScale.z
        );

        // 1. 뼈대 동기화 (로컬 회전/위치를 그대로 복사하여 모션 깨짐 방지)
        foreach (var kvp in _boneMap)
        {
            Transform ob = kvp.Key;
            Transform cb = kvp.Value;
            
            if (ob == null || cb == null) continue;

            // 원본의 최상위 트랜스폼 자체는 위에서 맞췄으므로 생략
            if (ob == _originalCharacter.transform || cb == _cloneCharacter.transform) continue;

            // 로컬 회전 및 위치를 원본과 동일하게 1:1 복사
            cb.localPosition = ob.localPosition;
            cb.localRotation = ob.localRotation;
            cb.localScale = ob.localScale;
        }

        // 2. 표정 (BlendShape) 동기화
        foreach (var kvp in _rendererMap)
        {
            SkinnedMeshRenderer oSmr = kvp.Key as SkinnedMeshRenderer;
            SkinnedMeshRenderer cSmr = kvp.Value as SkinnedMeshRenderer;

            if (oSmr == null || cSmr == null || oSmr.sharedMesh == null) continue;

            for (int i = 0; i < oSmr.sharedMesh.blendShapeCount; i++)
            {
                cSmr.SetBlendShapeWeight(i, oSmr.GetBlendShapeWeight(i));
            }
        }
    }
}
