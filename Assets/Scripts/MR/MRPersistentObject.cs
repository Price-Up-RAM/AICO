using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Oculus.Interaction; // Meta Interaction SDK 활용

/// <summary>
/// 자동 위치 저장 및 물리/잡기 연동 스크립트 (Auto-Link 버전)
/// </summary>
public class MRPersistentObject : MonoBehaviour
{
    [Header("Persistence Settings")]
    public string objectID = "PersistentObject";
    public float spawnDistance = 1.0f;
    public float spawnHeightOffset = -0.5f;

    [Header("Physics Settings")]
    public bool enableGravityOnRelease = true;
    
    private Rigidbody _rb;
    private OVRSpatialAnchor _currentAnchor;
    private Coroutine _saveCoroutine;

    // Meta Interaction SDK의 잡기 컴포넌트
    private Grabbable _grabbable;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        // [중요] 게임 시작 직후 바닥 메쉬가 생성되기 전에 떨어져서 바닥을 뚫는 현상 방지
        if (_rb != null)
        {
            _rb.isKinematic = true; 
        }

        // Grabbable 컴포넌트 자동 찾기 및 이벤트 연결 (수동 연결 불필요)
        _grabbable = GetComponent<Grabbable>();
        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    private void OnDestroy()
    {
        if (_grabbable != null)
        {
            _grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
    }

    private async void Start()
    {
        await Task.Delay(1500); // MR 공간 로딩 대기
        LoadSavedPosition();
    }

    // 잡기(Grab) 이벤트 자동 감지
    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select) 
        {
            // 잡았을 때
            OnGrabbed();
        }
        else if (evt.Type == PointerEventType.Unselect) 
        {
            // 놓았을 때
            OnReleased();
        }
    }

    private async void LoadSavedPosition()
    {
        string uuidStr = PlayerPrefs.GetString("MRPersistent_" + objectID, "");

        if (!string.IsNullOrEmpty(uuidStr) && Guid.TryParse(uuidStr, out Guid savedGuid))
        {
            var uuids = new List<Guid>() { savedGuid };
            var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
            var loadResult = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(uuids, unboundAnchors);
            
            if (loadResult.Success && unboundAnchors.Count > 0)
            {
                var unbound = unboundAnchors[0];
                if (await unbound.LocalizeAsync())
                {
                    _currentAnchor = gameObject.AddComponent<OVRSpatialAnchor>();
                    unbound.BindTo(_currentAnchor);
                    Debug.Log($"[{objectID}] 위치 복원 완료!");
                    return;
                }
            }
        }

        SpawnInFrontOfCamera();
    }

    private void SpawnInFrontOfCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0; forward.Normalize();
            Vector3 spawnPos = cam.transform.position + forward * spawnDistance;
            spawnPos.y += spawnHeightOffset;
            transform.position = spawnPos;
            
            Vector3 lookPos = cam.transform.position; lookPos.y = transform.position.y;
            transform.LookAt(lookPos); transform.Rotate(0, 180, 0);

            SaveCurrentPosition();
        }
    }

    public void OnGrabbed()
    {
        if (_saveCoroutine != null)
        {
            StopCoroutine(_saveCoroutine);
            _saveCoroutine = null;
        }

        if (_rb != null) _rb.isKinematic = true;

        if (_currentAnchor != null)
        {
            if (_currentAnchor.Created) _currentAnchor.EraseAnchorAsync();
            Destroy(_currentAnchor);
            _currentAnchor = null;
        }
        Debug.Log($"[{objectID}] 잡기 자동 감지 완료. 앵커 해제.");
    }

    public void OnReleased()
    {
        if (_rb != null && enableGravityOnRelease)
        {
            _rb.isKinematic = false; // 놓는 순간에만 중력 활성화!
        }
        _saveCoroutine = StartCoroutine(AutoSaveAfterSettle());
    }

    private IEnumerator AutoSaveAfterSettle()
    {
        yield return new WaitForSeconds(1.0f);

        if (_rb != null)
        {
            while (_rb.linearVelocity.sqrMagnitude > 0.01f || _rb.angularVelocity.sqrMagnitude > 0.01f)
            {
                yield return new WaitForSeconds(0.5f);
            }
            _rb.isKinematic = true; // 안착하면 다시 물리 계산 끄기 (최적화 & 바닥 뚫음 방지)
        }
        else
        {
            yield return new WaitForSeconds(1.0f);
        }

        SaveCurrentPosition();
    }

    public async void SaveCurrentPosition()
    {
        if (_currentAnchor != null) Destroy(_currentAnchor);
        await Task.Yield();

        _currentAnchor = gameObject.AddComponent<OVRSpatialAnchor>();
        if (await _currentAnchor.WhenCreatedAsync())
        {
            var saveResult = await _currentAnchor.SaveAnchorAsync();
            if (saveResult.Success)
            {
                PlayerPrefs.SetString("MRPersistent_" + objectID, _currentAnchor.Uuid.ToString());
                PlayerPrefs.Save();
                Debug.Log($"[{objectID}] 자동 위치 저장 성공!");
            }
        }
    }
}
