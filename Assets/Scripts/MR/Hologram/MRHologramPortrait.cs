using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class MRHologramPortrait : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("홀로그램 렌더러에 일괄 적용할 머티리얼. 비워두면 자동 생성합니다.")]
    public Material hologramMaterial;
    
    [Tooltip("홀로그램의 크기 비율 (원본 대비)")]
    public float scaleMultiplier = 0.1f;

    [Tooltip("손목 위 오프셋")]
    public Vector3 localOffset = new Vector3(0, 0.1f, 0);

    private GameObject _originalCharacter;
    private GameObject _cloneCharacter;
    
    // 원본 뼈대 -> 복제 뼈대 매핑
    private Dictionary<Transform, Transform> _boneMap = new Dictionary<Transform, Transform>();
    
    // 블렌드셰이프 동기화를 위한 렌더러 매핑
    private Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer> _rendererMap = new Dictionary<SkinnedMeshRenderer, SkinnedMeshRenderer>();

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
        _cloneCharacter.transform.localScale = Vector3.one * scaleMultiplier;

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

        // 5. 렌더러 매핑 (단색 머티리얼 덮어쓰기 기능 제외)
        SkinnedMeshRenderer[] originalSmrs = _originalCharacter.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer[] cloneSmrs = _cloneCharacter.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        // 머티리얼 덮어쓰기 주석 처리
        /*
        Material matToApply = hologramMaterial != null ? hologramMaterial : CreateDefaultHologramMaterial();
        foreach (var cSmr in cloneSmrs)
        {
            Material[] newMats = new Material[cSmr.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = matToApply;
            }
            cSmr.sharedMaterials = newMats;
            cSmr.gameObject.layer = gameObject.layer;
        }
        */

        // 렌더러 이름 기준으로 매핑 (블렌드셰이프 동기화 용도)
        foreach (var oSmr in originalSmrs)
        {
            foreach (var cSmr in cloneSmrs)
            {
                if (oSmr.name == cSmr.name)
                {
                    _rendererMap[oSmr] = cSmr;
                    break;
                }
            }
        }
        
        // 충돌체 추가 (Ray 상호작용 용도)
        BoxCollider bc = _cloneCharacter.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.0f, 0); 
        bc.size = new Vector3(0.6f, 2.0f, 0.6f); 
    }

    private Material CreateDefaultHologramMaterial()
    {
        // URP 기본 쉐이더로 형광 푸른색 반투명 머티리얼 생성
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null) urpShader = Shader.Find("Standard");

        Material mat = new Material(urpShader);
        mat.name = "Hologram_Auto";
        
        // 반투명 설정 (URP)
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0); // Alpha
        mat.SetColor("_BaseColor", new Color(0.2f, 0.6f, 1.0f, 0.7f));
        
        // 발광(Emission) 설정
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.1f, 0.3f, 0.8f, 1.0f));

        return mat;
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

        // 0. 최상위 루트 회전 및 스케일 적용 (카메라를 바라보도록 Yaw 보정)
        _cloneCharacter.transform.rotation = Quaternion.Euler(0, _yawOffset, 0) * _originalCharacter.transform.rotation;

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
            SkinnedMeshRenderer oSmr = kvp.Key;
            SkinnedMeshRenderer cSmr = kvp.Value;

            if (oSmr == null || cSmr == null || oSmr.sharedMesh == null) continue;

            for (int i = 0; i < oSmr.sharedMesh.blendShapeCount; i++)
            {
                cSmr.SetBlendShapeWeight(i, oSmr.GetBlendShapeWeight(i));
            }
        }
    }
}
