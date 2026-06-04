using UnityEngine;

public class CharacterEffectSpawnerPlane : MonoBehaviour
{
    [Header("Roots")]
    public Transform effectRoot;   // 보통 캐릭터A(루트) 또는 그 아래 Effects
    public Transform patRoot;      // 요구사항: PatRoot 기준

    [Header("Prefabs")]
    public GameObject starPrefab1;
    public GameObject starPrefab2;
    public GameObject heartPrefab;

    [Header("생성 개수 (랜덤)")]
    [Range(1, 50)] public int minStars = 1;
    [Range(1, 50)] public int maxStars = 3;

    [Header("발사 각도 (위쪽 기준, 좌우)")]
    [Range(0f, 90f)] public float launchAngle = 45f;

    [Header("발사 속도 (랜덤)")]
    public float minSpeed = 0.001f;
    public float maxSpeed = 0.003f;

    [Header("크기 (랜덤)")]
    public float minScale = 0.2f;
    public float maxScale = 1f;

    [Header("Hearts")]
    public float heartSpawnInterval = 0.3f;  // 시작 간격
    public float heartIntervalMin = 0.2f;    // 랜덤 최소
    public float heartIntervalMax = 0.5f;    // 랜덤 최대

    [Header("Smash Bonus")]
    public int smashBonusStars = 10; // 10회부터 계속 추가 발사

    private bool _patActive;
    private float _heartTimer;
    private float _nextHeartInterval;


    private void Awake()
    {
        if (effectRoot == null) effectRoot = transform;
        _nextHeartInterval = heartSpawnInterval;
    }

    private void Update()
    {
        if (!_patActive) return;
        if (heartPrefab == null || patRoot == null) return;

        _heartTimer += Time.deltaTime;
        if (_heartTimer >= _nextHeartInterval)
        {
            SpawnHeart();
            _heartTimer = 0f;
            _nextHeartInterval = Random.Range(heartIntervalMin, heartIntervalMax);
        }
    }

    public void SetPatActive(bool active)
    {
        _patActive = active;
        _heartTimer = 0f;
        _nextHeartInterval = heartSpawnInterval;
    }

    public void SpawnSmashStars(int smashHitCount)
    {
        if (starPrefab1 == null || starPrefab2 == null) return;
        if (patRoot == null) return;

        int bonus = (smashHitCount >= 10) ? smashBonusStars : 0;


        // PatRoot의 로컬 위치를 기준으로 생성
        Vector3 anchorLocal = effectRoot.InverseTransformPoint(patRoot.position);

        int numberOfStars = Random.Range(minStars, maxStars + 1) + bonus;

        for (int i = 0; i < numberOfStars; i++)
        {
            // [수정] * size 부분 전부 제거 (부모 스케일을 자동으로 따라감)
            Vector2 spawnOffset = new Vector2(Random.Range(-1f, 1f), Random.Range(0.5f, 1f));
            Vector3 localPos = anchorLocal + new Vector3(spawnOffset.x, spawnOffset.y, 0f);

            GameObject prefab = (Random.Range(0, 2) == 0) ? starPrefab1 : starPrefab2;
            GameObject star = Instantiate(prefab, effectRoot);
            star.transform.localPosition = localPos;
            star.transform.localRotation = Quaternion.identity;

            float scale = Random.Range(minScale, maxScale);
            star.transform.localScale = new Vector3(scale, scale, 1f);

            float angle = Random.Range(-launchAngle, launchAngle);
            Vector2 dir = (Quaternion.Euler(0, 0, angle) * Vector2.up).normalized;
            float speed = Random.Range(minSpeed, maxSpeed);

            var p = star.GetComponent<PlaneStarParticle>();
            if (p != null) p.Init(dir * speed);
        }
    }

    private void SpawnHeart()
    {
        // [수정] * size 부분 전부 제거
        Vector3 anchorLocal = effectRoot.InverseTransformPoint(patRoot.position) + new Vector3(0f, -1.5f, 0f);
        Vector2 spawnOffset = new Vector2(Random.Range(-2f, 2f), Random.Range(2.5f, 3f));
        Vector3 localPos = anchorLocal + new Vector3(spawnOffset.x, spawnOffset.y, 0f);

        GameObject heart = Instantiate(heartPrefab, effectRoot);
        heart.transform.localPosition = localPos;
        heart.transform.localRotation = Quaternion.identity;

        float scale = Random.Range(minScale, maxScale);
        heart.transform.localScale = new Vector3(scale, scale, 1f);

        var p = heart.GetComponent<PlaneHeartParticle>();
        // 파티클 내부 로직에 넘겨주던 size도 1f로 고정하거나 아예 빼버립니다.
        if (p != null) p.Init(1f);
    }
}
