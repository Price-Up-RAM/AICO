using System.Collections;
using UnityEngine;

public class PlaneStarParticle : MonoBehaviour
{
    [Header("수명 설정")]
    public float lifetime = 0.1f;
    public float lifetimePlus = 0.5f;

    [Header("사라짐")]
    public float shrinkDuration = 0.5f;

    private Vector2 _vel;
    public Vector2 _gravity=new Vector2(0f, -0.003f); // 로컬 Y 기준 중력(포물선)

    public void Init(Vector2 initialVelocity)
    {
        _vel = initialVelocity;

        // 추가 변수 없이: 속도 기반으로 적당히 "아래로" 휘게
        float g = -Mathf.Max(0.0005f, initialVelocity.magnitude * 8f);
        // _gravity = new Vector2(0f, g);

        StartCoroutine(ShrinkAndDestroy());
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _vel += _gravity * dt;
        transform.localPosition += new Vector3(_vel.x, _vel.y, 0f) * dt;
    }

    private IEnumerator ShrinkAndDestroy()
    {
        yield return new WaitForSeconds(Random.Range(lifetime, lifetime + lifetimePlus));

        Vector3 initialScale = transform.localScale;
        float timer = 0f;

        while (timer < shrinkDuration)
        {
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, timer / shrinkDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
