using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlaneHeartParticle : MonoBehaviour
{
    public float minLifetime = 1f;
    public float maxLifetime = 2f;

    public float popInDuration = 0.4f;

    public float waveFrequency = 2f;
    public float waveAmplitude = 0.15f;

    public float fadeOutDuration = 1f;

    [Header("Spawn Stretch")]
    public float startYStretch = 1.2f;   // 세로로 쭉 늘어나는 비율(원하면 1.5 등으로)

    private SpriteRenderer _sr;
    private Vector3 _finalScale;

    private float _initialLocalX;
    private float _timeOffset;

    private Vector2 _vel;
    public float _damping=2f;
    public float _upGravity=3f;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();

        // ✅ 여기서는 finalScale을 잡지 않는다 (스포너가 랜덤 스케일 세팅하기 전이기 때문)
        _initialLocalX = transform.localPosition.x;
        _timeOffset = Random.Range(0f, 10f);
    }

    public void Init(float characterSize)
    {
        // ✅ 스포너가 랜덤 스케일을 세팅한 "그 값"이 최종 크기
        _finalScale = transform.localScale;

        // ✅ 시작은 “전체가 큰 상태”가 아니라 “세로로 늘어난 상태”
        transform.localScale = new Vector3(_finalScale.x, _finalScale.y * startYStretch, _finalScale.z);

        // _upGravity = 0.003f * characterSize;
        // _damping = 2f * characterSize;
        _vel = Vector2.zero;

        StopAllCoroutines();
        StartCoroutine(StretchBackToFinal());   // ✅ 늘어난 세로가 원래 비율로 돌아옴
        StartCoroutine(FadeOutAndDestroy());
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        _vel.y += _upGravity * dt;

        float damp = 1f / (1f + _damping * dt);
        _vel *= damp;

        Vector3 lp = transform.localPosition;
        lp += new Vector3(_vel.x, _vel.y, 0f) * dt;

        float xOffset = Mathf.Sin((Time.time + _timeOffset) * waveFrequency) * waveAmplitude;
        lp.x = _initialLocalX + xOffset;

        transform.localPosition = lp;
    }

    private IEnumerator StretchBackToFinal()
    {
        float timer = 0f;
        Vector3 startScale = transform.localScale;

        while (timer < popInDuration)
        {
            float t = timer / popInDuration;
            float eased = 1 - Mathf.Pow(1 - t, 3);
            transform.localScale = Vector3.Lerp(startScale, _finalScale, eased);

            timer += Time.deltaTime;
            yield return null;
        }

        transform.localScale = _finalScale;
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float lifetime = Random.Range(minLifetime, maxLifetime);
        yield return new WaitForSeconds(lifetime);

        float timer = 0f;
        Color start = _sr.color;

        while (timer < fadeOutDuration)
        {
            float a = Mathf.Lerp(start.a, 0f, timer / fadeOutDuration);
            _sr.color = new Color(start.r, start.g, start.b, a);

            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
