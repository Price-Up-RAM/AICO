// 바닥에 원을 그리는 LineRenderer 공용 헬퍼.
//
// 왜 뽑았나
// --------
// 소비자가 둘이다 — MRAimHighlight(조준한 캐릭터 발밑 링)와
// MRRayDragAdapter(드래그 착지 미리보기 링). 반경 계산은 이미 MRCharacterBounds 한 곳에
// 모여 있는데(§4-47) 그리는 코드까지 둘로 복사하면 링 모양·세그먼트·머티리얼이 따로 놀게 된다.
//
// 외부 자산에 의존하지 않는다 — 메시도 머티리얼도 런타임에 만든다.
//
// ⚠ 링 오브젝트는 **월드 최상위**에 만든다. 캐릭터(픽셀 공간 래퍼 1/120)나 스케일이 걸린
// 부모 밑에 두면 반경과 선 굵기가 그 배율만큼 왜곡된다 (Kickoff Guide §4-1).

using UnityEngine;

public static class MRRingRenderer
{
    // 링 하나를 만든다. 만들어진 직후에는 꺼진 상태다.
    public static LineRenderer Create(string objectName, float lineWidth, Color color)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(null);

        LineRenderer ring = go.AddComponent<LineRenderer>();
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.startWidth = lineWidth;
        ring.endWidth = lineWidth;
        ring.startColor = color;
        ring.endColor = color;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.enabled = false;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader != null)
        {
            ring.material = new Material(shader);
        }

        return ring;
    }

    // 중심·반경으로 원의 정점을 다시 찍는다. XZ 평면 기준이다.
    public static void BuildCircle(LineRenderer ring, Vector3 center, float radius, int segments)
    {
        if (ring == null) return;

        if (segments < 8)
        {
            segments = 8;
        }

        if (ring.positionCount != segments)
        {
            ring.positionCount = segments;
        }

        for (int i = 0; i < segments; i++)
        {
            float t = (float)i / segments * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            ring.SetPosition(i, center + offset);
        }
    }

    public static void SetColor(LineRenderer ring, Color color)
    {
        if (ring == null) return;

        ring.startColor = color;
        ring.endColor = color;
    }

    // 링은 최상위 오브젝트라 소유자가 사라져도 씬에 남는다. 소유자의 OnDestroy에서 부를 것.
    public static void Dispose(LineRenderer ring)
    {
        if (ring == null) return;

        Object.Destroy(ring.gameObject);
    }
}
