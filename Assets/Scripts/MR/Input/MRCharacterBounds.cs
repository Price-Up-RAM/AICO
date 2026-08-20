// 캐릭터의 **판정 부피**를 재는 공용 헬퍼 (MR_Phase4A_Input_Plan.md §8-5)
//
// 무엇을 재는가 — 콜라이더다, 렌더러가 아니다
// ----------------------------------------
// 캐릭터에는 `Char`(레이어 3) 위의 `CapsuleCollider`가 이미 있다.
// 조준 판정도, 조준 표시(발밑 링)도 **이것 하나만** 본다.
//
// 처음에는 렌더러 경계로 재려 했다. 이유는 "캐릭터에 콜라이더가 없을지도 모른다"는
// 가정뿐이었는데, 그 가정은 이미 반증돼 있었다(`[MRRay] ①`이 캐릭터를 맞히고 있었다).
// 판정은 콜라이더로 하면서 표시만 렌더러로 하면 **두 소스가 어긋난다** —
// Kickoff Guide §4-47이 말한 "A는 되는데 B만 안 되는 비대칭"을 새로 만드는 셈이다.
//
// 그래서 규칙: **보이는 곳 = 잡히는 곳.** 링이 몸과 안 맞아 보이면 그건 링의 버그가 아니라
// **콜라이더가 몸과 안 맞는다는 신고**다. 고칠 대상은 캡슐이지 표시가 아니다.
//
// 왜 transform.position을 쓰지 않는가
// --------------------------------
// VRM 피벗이 발밑인지 허리인지 알 수 없다. 콜라이더 경계로 재면 그 가정 자체가 사라진다.
//
// 비용: `GetComponentsInChildren<Collider>()`만 주기적으로 캐시하고, bounds 합산은
// 호출할 때마다 한다. 콜라이더 한두 개 수준이라 매 프레임 불러도 부담이 없다.

using UnityEngine;

public static class MRCharacterBounds
{
    private static GameObject _cachedCharacter;
    private static Collider[] _colliders;
    private static float _nextRefreshTime;

    private const float RefreshInterval = 0.5f;

    /// <summary>현재 캐릭터의 콜라이더 월드 경계. 캐릭터나 콜라이더가 없으면 false.</summary>
    public static bool TryGet(MRCharacterWorldRoot root, out Bounds bounds)
    {
        bounds = new Bounds();

        if (root == null) return false;

        GameObject character = root.CurrentCharacter;
        if (character == null) return false;

        RefreshColliders(character);
        if (_colliders == null) return false;

        bool first = true;

        for (int i = 0; i < _colliders.Length; i++)
        {
            // 캐릭터 교체·의상 변경으로 파괴된 콜라이더가 목록에 남아 있을 수 있다.
            if (_colliders[i] == null) continue;
            if (!_colliders[i].enabled) continue;

            if (first)
            {
                bounds = _colliders[i].bounds;
                first = false;
                continue;
            }

            bounds.Encapsulate(_colliders[i].bounds);
        }

        return !first;
    }

    /// <summary>가로 방향 반경(m). 발밑 링의 반경에 쓴다.</summary>
    public static float GetHorizontalRadius(Bounds bounds)
    {
        return Mathf.Max(bounds.extents.x, bounds.extents.z);
    }

    /// <summary>인스펙터에서 레이어를 좁히지 않았을 때 쓸 기본 마스크.
    /// `Nothing`이면 아무것도 못 맞히고, `Everything`이면 방 메시·바닥·패널 `GrabPlate`가
    /// 캐릭터보다 앞에서 레이를 가로챈다. 둘 다 조용히 망가지는 값이라 여기서 막는다.</summary>
    public static LayerMask ResolveCharacterMask(LayerMask current, Object context)
    {
        if (current.value != 0 && current.value != ~0) return current;

        int charMask = LayerMask.GetMask("Char");
        if (charMask == 0)
        {
            Debug.LogWarning("[MRCharacterBounds] 'Char' 레이어를 찾지 못했습니다. 레이어 마스크를 인스펙터에서 지정하세요.", context);
            return current;
        }

        Debug.Log("[MRCharacterBounds] 캐릭터 레이어 마스크를 'Char'로 좁혔습니다 (인스펙터 값이 Nothing/Everything이었음).", context);
        return charMask;
    }

    private static void RefreshColliders(GameObject character)
    {
        bool changed = _cachedCharacter != character;

        if (!changed && _colliders != null && Time.unscaledTime < _nextRefreshTime) return;

        _cachedCharacter = character;
        _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        _colliders = character.GetComponentsInChildren<Collider>(false);
    }
}
