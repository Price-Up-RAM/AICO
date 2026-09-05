using UnityEngine;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using Meta.XR;

public enum FrameState { IDLE, ARMED, CAPTURE }

public class MRHandFrameGesture : MonoBehaviour
{
    [Header("파라미터 (실측 후 갱신)")]
    [Tooltip("사각형이 이만큼 유지돼야 ARMED")]
    [SerializeField] private float armEnterHold = 0.25f;

    [Tooltip("사각형이 잠깐 깨져도 봐주는 시간")]
    [SerializeField] private float jitterGrace = 0.20f;

    [Tooltip("셔터 시점에서 이만큼 과거의 사각형을 쓴다")]
    [SerializeField] private float latchBackoff = 0.12f;

    [Tooltip("링버퍼 길이")]
    [SerializeField] private float latchBufferSeconds = 0.40f;

    [Tooltip("이 아래면 엄지 접힘 (실측값 0.65~0.80 반영)")]
    [SerializeField] private float thumbFoldRatio = 0.85f;

    [Tooltip("검지 폄 판정")]
    [SerializeField] private float indexExtendRatio = 1.25f;

    [Tooltip("ARMED 시점 대비 손목 이동 허용")]
    [SerializeField] private float handMoveTolerance = 0.08f;

    [Tooltip("촬영 후 재무장까지 쿨다운")]
    [SerializeField] private float cooldown = 1.0f;

    [Header("판정 허용 오차")]
    [Tooltip("직각 및 평행 판별 허용 오차 (0.0=엄격, 1.0=느슨) - 손목 꺾임 방지용으로 0.5까지 완화")]
    [SerializeField] private float angleTolerance = 0.50f;

    [Tooltip("손가락 연장선상에서의 최대 허용범위")]
    [SerializeField] private float intersectionForwardLimit = 15.0f;

    [Tooltip("손가락 아래(손목 방향)로의 교차점 허용범위")]
    [SerializeField] private float intersectionBackwardLimit = -1.0f;

    [Header("진단")]
    [Tooltip("매 프레임 로그를 남길지 여부 (기본 false)")]
    [SerializeField] private bool logEveryFrame = false;

    private FrameState _state = FrameState.IDLE;
    private float _stateTimer = 0f;
    private float _graceTimer = 0f;

    private Hand _leftHand;
    private Hand _rightHand;

    private struct LatchItem
    {
        public float time;
        public Vector3[] quad;
        public float diagRatio;
        public Pose camPose;
    }
    private List<LatchItem> _latchBuffer = new List<LatchItem>();

    private Vector3 _armedLeftWristPos;
    private Vector3 _armedRightWristPos;
    private bool _wasAnyFolded = false;
    
    // 로그 스팸 방지용
    private string _lastLogMsg = "";

    private LineRenderer _frameLine;

    // 프리뷰 및 플래시 관련
    private GameObject _previewObj;
    private MeshFilter _previewMeshFilter;
    private MeshRenderer _previewMeshRenderer;
    private Material _previewMat;
    private Mesh _previewMesh;

    private GameObject _flashObj;
    private MeshFilter _flashMeshFilter;
    private MeshRenderer _flashRenderer;
    private Material _flashMat;
    private float _flashTimer = -1f;

    private void Start()
    {
        _state = FrameState.IDLE;
        
        _frameLine = gameObject.AddComponent<LineRenderer>();
        _frameLine.positionCount = 4;
        _frameLine.loop = true;
        _frameLine.startWidth = 0.005f;
        _frameLine.endWidth = 0.005f;
        _frameLine.useWorldSpace = true;
        _frameLine.enabled = false;
        
        Material lineMat = new Material(Shader.Find("Hidden/Internal-Colored"));
        lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        _frameLine.material = lineMat;

        // 프리뷰 생성
        _previewObj = new GameObject("MRHandFramePreview");
        _previewObj.transform.SetParent(transform, false);
        _previewMeshFilter = _previewObj.AddComponent<MeshFilter>();
        _previewMeshRenderer = _previewObj.AddComponent<MeshRenderer>();
        _previewMat = new Material(Shader.Find("Unlit/Texture"));
        _previewMeshRenderer.material = _previewMat;
        _previewMesh = new Mesh();
        _previewMesh.MarkDynamic();
        _previewMeshFilter.mesh = _previewMesh;
        _previewObj.SetActive(false);

        // 플래시 생성
        _flashObj = new GameObject("MRHandFrameFlash");
        _flashObj.transform.SetParent(transform, false);
        _flashMeshFilter = _flashObj.AddComponent<MeshFilter>();
        _flashRenderer = _flashObj.AddComponent<MeshRenderer>();
        _flashMat = new Material(Shader.Find("Sprites/Default"));
        _flashMat.color = new Color(1f, 1f, 1f, 0f);
        _flashRenderer.material = _flashMat;
        _flashObj.SetActive(false);
        
        Mesh flashMesh = new Mesh();
        flashMesh.vertices = new Vector3[] { 
            new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0)
        };
        flashMesh.uv = new Vector2[] {
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1)
        };
        flashMesh.triangles = new int[] { 0, 3, 2, 0, 2, 1 };
        _flashMeshFilter.mesh = flashMesh;
    }

    private void Update()
    {
        float curTime = Time.time;
        bool hasSquare = false;
        Vector3[] quad = null;
        
        if (_state == FrameState.CAPTURE) {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= cooldown) {
                TransitionTo(FrameState.IDLE);
                _previewObj.SetActive(false);
            }
            // 캡처 중에는 제스처 판단 생략
        }
        else 
        {
            _latchBuffer.RemoveAll(item => curTime - item.time > latchBufferSeconds);

        ResolveHands();
        if (_leftHand == null || !_leftHand.IsTrackedDataValid || 
            _rightHand == null || !_rightHand.IsTrackedDataValid)
        {
            if (logEveryFrame) {
                string lStat = _leftHand == null ? "null" : _leftHand.IsTrackedDataValid.ToString();
                string rStat = _rightHand == null ? "null" : _rightHand.IsTrackedDataValid.ToString();
                LogThrottled($"[MRHandFrame] 손 인식 대기 중... Left:{lStat}, Right:{rStat}");
            }
            if (_state == FrameState.ARMED) {
                _graceTimer += Time.deltaTime;
                if (_graceTimer > jitterGrace) TransitionTo(FrameState.IDLE);
            }
            // skip visualizer update and return here? No, if we return, visualizer and flash won't run.
            // Actually, if we return early, we MUST run visualizer and flash first.
            // Instead of return, just jump to visualizer.
            goto VisualizerUpdate;
        }

        hasSquare = TryDetectSquare(out quad, out float diagRatio, out string debugMsg);
        
        bool lThumbFolded, lIndexExtended;
        float lThumbRatio = GetThumbFoldRatio(_leftHand, out lThumbFolded, out lIndexExtended, out float lIndexRatio);
        
        bool rThumbFolded, rIndexExtended;
        float rThumbRatio = GetThumbFoldRatio(_rightHand, out rThumbFolded, out rIndexExtended, out float rIndexRatio);

        if (logEveryFrame) {
            if (hasSquare) {
                LogThrottled($"[MRHandFrame] 사각형 감지 중... 대각비: {diagRatio:F3}");
            } else {
                LogThrottled($"[MRHandFrame] 감지 실패: {debugMsg}");
            }
        }

        if (_state == FrameState.IDLE) {
            if (hasSquare) {
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= armEnterHold) {
                    TransitionTo(FrameState.ARMED);
                    _armedLeftWristPos = GetWristPos(_leftHand);
                    _armedRightWristPos = GetWristPos(_rightHand);
                }
            } else {
                _stateTimer = 0f;
            }
        }
        else if (_state == FrameState.ARMED) {
            if (hasSquare) {
                _graceTimer = 0f;
                UpdateLatch(curTime, quad, diagRatio);
            } else {
                _graceTimer += Time.deltaTime;
                if (_graceTimer > jitterGrace) {
                    Debug.Log($"[MRHandFrame] 셔터 인식 실패! (엄지 임계값이 너무 낮을 수 있습니다)\n" + 
                              $"현재 엄지접힘비: 좌={lThumbRatio:F2}, 우={rThumbRatio:F2} (접힘 임계={thumbFoldRatio})\n" +
                              $"현재 검지폄비: 좌={lIndexRatio:F2}, 우={rIndexRatio:F2} (폄 임계={indexExtendRatio})");
                    TransitionTo(FrameState.IDLE);
                    goto VisualizerUpdate; 
                }
            }

            bool anyFolded = lThumbFolded || rThumbFolded;
            
            if (anyFolded && !_wasAnyFolded) 
            {
                bool isLeft = lThumbFolded;
                string sideStr = isLeft ? "좌" : "우";
                float tRatio = isLeft ? lThumbRatio : rThumbRatio;
                bool idxExt = isLeft ? lIndexExtended : rIndexExtended;
                float idxRatio = isLeft ? lIndexRatio : rIndexRatio;
                
                float lMove = Vector3.Distance(GetWristPos(_leftHand), _armedLeftWristPos);
                float rMove = Vector3.Distance(GetWristPos(_rightHand), _armedRightWristPos);
                float maxMove = Mathf.Max(lMove, rMove);
                bool moveOk = maxMove <= handMoveTolerance;

                bool isdkPinch = isLeft ? _leftHand.GetFingerIsPinching(HandFinger.Index) : _rightHand.GetFingerIsPinching(HandFinger.Index);

                string logStr = $"[MRHandFrame/{sideStr}] 엄지접힘비 {tRatio:F2} (임계 {thumbFoldRatio}) → 접힘 | ";
                
                if (idxExt) logStr += $"검지폄 {idxRatio:F2} (임계 {indexExtendRatio}) ✓ | ";
                else logStr += $"검지폄 {idxRatio:F2} (임계 {indexExtendRatio}) ❌ | ";

                if (moveOk) logStr += $"손목이동 {maxMove:F3}m (허용 {handMoveTolerance}) ✓ | ";
                else logStr += $"손목이동 {maxMove:F3}m (허용 {handMoveTolerance}) ❌ | ";

                logStr += $"ISDK핀치 {isdkPinch} | ARMED {_stateTimer:F1}s";

                if (idxExt && moveOk) {
                    LatchItem latched = GetLatchedItem(curTime - latchBackoff);
                    logStr += $" | 래치 -{latchBackoff}s 사각형 대각비 {latched.diagRatio:F3} (온전) → 촬영";
                    Debug.Log(logStr);
                    
                    if (_frameLine != null) {
                        for (int i = 0; i < 4; i++) _frameLine.SetPosition(i, latched.quad[i]);
                    }
                    
                    StartCoroutine(DoCapture(latched));
                    TransitionTo(FrameState.CAPTURE);
                    
                    // 프리뷰 고정 및 플래시 발동
                    UpdatePreviewQuad(latched.quad);
                    _flashTimer = 0.3f;
                    _flashObj.SetActive(true);
                    _flashMat.color = new Color(1f, 1f, 1f, 1f);
                    Mesh fm = _flashMeshFilter.mesh;
                    if (fm != null) {
                        fm.vertices = latched.quad;
                        fm.RecalculateBounds();
                    }
                } else {
                    logStr += " → 조건 미달로 촬영 실패";
                    Debug.Log(logStr);
                }
            }
            _wasAnyFolded = anyFolded;

            if (_state == FrameState.ARMED) {
                _stateTimer += Time.deltaTime;
            }
        }

        } // end of else (not CAPTURE)
        
        VisualizerUpdate:
        // --- Visualizer Update ---
        if (_frameLine != null) {
            if (_state == FrameState.CAPTURE) {
                _frameLine.enabled = true;
                _frameLine.startColor = Color.gray;
                _frameLine.endColor = Color.gray;
                // keep the old positions (don't update)
            } else if (hasSquare) {
                _frameLine.enabled = true;
                for (int i = 0; i < 4; i++) _frameLine.SetPosition(i, quad[i]);
                
                if (_state == FrameState.IDLE) {
                    _frameLine.startColor = new Color(1f, 1f, 0f, 0.4f); // faint yellow
                    _frameLine.endColor = new Color(1f, 1f, 0f, 0.4f);
                } else if (_state == FrameState.ARMED) {
                    _frameLine.startColor = Color.green; // bright green
                    _frameLine.endColor = Color.green;
                }
                UpdatePreviewQuad(quad);
                _previewObj.SetActive(true);
            } else if (_state == FrameState.ARMED) {
                _frameLine.enabled = true;
                _frameLine.startColor = Color.red; // failing
                _frameLine.endColor = Color.red;
                // positions remain from the last valid frame
            } else {
                _frameLine.enabled = false;
                _previewObj.SetActive(false);
            }
        }
        
        // Flash animation
        if (_flashTimer >= 0f) {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f) {
                _flashObj.SetActive(false);
            } else {
                float a = Mathf.Clamp01(_flashTimer / 0.3f);
                _flashMat.color = new Color(1f, 1f, 1f, a);
            }
        }
    }

    private void UpdatePreviewQuad(Vector3[] quad)
    {
#if UNITY_EDITOR || UNITY_ANDROID
        PassthroughCameraAccess pca = FindObjectOfType<PassthroughCameraAccess>();
        if (pca == null || !pca.IsPlaying) return;

        Texture tex = pca.GetTexture();
        if (tex != null) {
            _previewMat.mainTexture = tex;
        }

        Pose camPose = pca.GetCameraPose();
        
        Vector2[] uvs = new Vector2[4];
        for (int i = 0; i < 4; i++) {
            uvs[i] = pca.WorldToViewportPoint(quad[i], camPose);
        }
        
        _previewMesh.vertices = quad;
        _previewMesh.uv = uvs;
        _previewMesh.triangles = new int[] { 0, 3, 2, 0, 2, 1 };
        _previewMesh.RecalculateBounds();
#endif
    }


    private void UpdateLatch(float time, Vector3[] quad, float diagRatio)
    {
        float threshold = time - latchBufferSeconds;
        _latchBuffer.RemoveAll(x => x.time < threshold);
        
        Pose pose = Pose.identity;
#if UNITY_EDITOR || UNITY_ANDROID
        PassthroughCameraAccess pca = FindObjectOfType<PassthroughCameraAccess>();
        if (pca != null) {
            pose = pca.GetCameraPose();
        } else if (Camera.main != null) {
            pose = new Pose(Camera.main.transform.position, Camera.main.transform.rotation);
        }
#endif
        _latchBuffer.Add(new LatchItem { time = time, quad = quad, diagRatio = diagRatio, camPose = pose });
    }


    private void LogThrottled(string msg) {
        if (_lastLogMsg != msg) {
            Debug.Log(msg);
            _lastLogMsg = msg;
        }
    }

    private LatchItem GetLatchedItem(float targetTime)
    {
        if (_latchBuffer.Count == 0) return new LatchItem { diagRatio = 1f };
        
        LatchItem best = _latchBuffer[0];
        float minDiff = Mathf.Abs(best.time - targetTime);

        for (int i = 1; i < _latchBuffer.Count; i++) {
            float diff = Mathf.Abs(_latchBuffer[i].time - targetTime);
            if (diff < minDiff) {
                minDiff = diff;
                best = _latchBuffer[i];
            }
        }
        return best;
    }

    private void TransitionTo(FrameState nextState)
    {
        Debug.Log($"[MRHandFrame] {_state} → {nextState}");
        _state = nextState;
        _stateTimer = 0f;
        _graceTimer = 0f;
        if (nextState == FrameState.IDLE) {
            _latchBuffer.Clear();
            _wasAnyFolded = false;
        }
    }

    private void ResolveHands()
    {
        if (_leftHand == null || _rightHand == null)
        {
            Hand[] hands = FindObjectsByType<Hand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                if (hand.Handedness == Handedness.Left) _leftHand = hand;
                if (hand.Handedness == Handedness.Right) _rightHand = hand;
            }
        }
    }

    private bool GetJoint(Hand hand, HandJointId jointId, out Pose pose)
    {
        pose = Pose.identity;
        if (hand == null || !hand.IsTrackedDataValid) return false;
        return hand.GetJointPose(jointId, out pose);
    }

    private Vector3 GetWristPos(Hand hand)
    {
        if (GetJoint(hand, HandJointId.HandWristRoot, out Pose pose)) return pose.position;
        return Vector3.zero;
    }

    private float GetThumbFoldRatio(Hand hand, out bool isFolded, out bool isIndexExtended, out float indexRatio)
    {
        isFolded = false;
        isIndexExtended = false;
        indexRatio = 0f;

        if (!GetJoint(hand, HandJointId.HandWristRoot, out Pose wrist) ||
            !GetJoint(hand, HandJointId.HandMiddle1, out Pose middle1) ||
            !GetJoint(hand, HandJointId.HandThumbTip, out Pose thumbTip) ||
            !GetJoint(hand, HandJointId.HandIndex1, out Pose index1) ||
            !GetJoint(hand, HandJointId.HandIndexTip, out Pose indexTip)) return 0f;

        float palmLen = Vector3.Distance(wrist.position, middle1.position);
        float tRatio = 999f;
        if (palmLen > 0.001f) {
            tRatio = Vector3.Distance(thumbTip.position, index1.position) / palmLen;
        }

        float iBaseDist = Vector3.Distance(wrist.position, index1.position);
        if (iBaseDist > 0.001f) {
            indexRatio = Vector3.Distance(wrist.position, indexTip.position) / iBaseDist;
        }

        isFolded = tRatio < thumbFoldRatio;
        isIndexExtended = indexRatio >= indexExtendRatio;
        
        return tRatio;
    }

    private bool IsExtended(Hand hand, HandJointId mcp, HandJointId tip)
    {
        if (!GetJoint(hand, HandJointId.HandWristRoot, out Pose wrist) ||
            !GetJoint(hand, mcp, out Pose baseP) ||
            !GetJoint(hand, tip, out Pose tipP)) return false;
            
        float baseDist = Vector3.Distance(wrist.position, baseP.position);
        if (baseDist < 0.001f) return false;
        return Vector3.Distance(wrist.position, tipP.position) >= baseDist * indexExtendRatio;
    }

    private bool TryDetectSquare(out Vector3[] quad, out float diagRatio, out string debugMsg)
    {
        quad = null;
        diagRatio = 1f;
        debugMsg = "";

        if (!GetJoint(_leftHand, HandJointId.HandThumb2, out Pose lThumb2) ||
            !GetJoint(_leftHand, HandJointId.HandThumbTip, out Pose lThumbTip) ||
            !GetJoint(_leftHand, HandJointId.HandIndex1, out Pose lIndex1) ||
            !GetJoint(_leftHand, HandJointId.HandIndexTip, out Pose lIndexTip)) { debugMsg="좌측 관절 획득 실패"; return false; }

        if (!GetJoint(_rightHand, HandJointId.HandThumb2, out Pose rThumb2) ||
            !GetJoint(_rightHand, HandJointId.HandThumbTip, out Pose rThumbTip) ||
            !GetJoint(_rightHand, HandJointId.HandIndex1, out Pose rIndex1) ||
            !GetJoint(_rightHand, HandJointId.HandIndexTip, out Pose rIndexTip)) { debugMsg="우측 관절 획득 실패"; return false; }

        bool lT = IsExtended(_leftHand, HandJointId.HandThumb2, HandJointId.HandThumbTip);
        bool lI = IsExtended(_leftHand, HandJointId.HandIndex1, HandJointId.HandIndexTip);
        bool rT = IsExtended(_rightHand, HandJointId.HandThumb2, HandJointId.HandThumbTip);
        bool rI = IsExtended(_rightHand, HandJointId.HandIndex1, HandJointId.HandIndexTip);

        if (!lT || !lI || !rT || !rI) {
            debugMsg = $"손가락 폄 부족: 좌엄지({lT}) 좌검지({lI}) 우엄지({rT}) 우검지({rI})";
            return false;
        }

        Camera cam = Camera.main;
        if (cam == null) { debugMsg="Camera.main 없음"; return false; }
        Transform camT = cam.transform;

        // 카메라 평면(XY)으로 투영하여 2D로 계산 (사용자 시점 기준)
        Vector2 lT2 = camT.InverseTransformPoint(lThumb2.position);
        Vector2 lTTip = camT.InverseTransformPoint(lThumbTip.position);
        Vector2 lI1 = camT.InverseTransformPoint(lIndex1.position);
        Vector2 lITip = camT.InverseTransformPoint(lIndexTip.position);

        Vector2 rT2 = camT.InverseTransformPoint(rThumb2.position);
        Vector2 rTTip = camT.InverseTransformPoint(rThumbTip.position);
        Vector2 rI1 = camT.InverseTransformPoint(rIndex1.position);
        Vector2 rITip = camT.InverseTransformPoint(rIndexTip.position);

        Vector2 l_t_dir = (lTTip - lT2).normalized;
        Vector2 l_i_dir = (lITip - lI1).normalized;
        Vector2 r_t_dir = (rTTip - rT2).normalized;
        Vector2 r_i_dir = (rITip - rI1).normalized;

        float dotL = Mathf.Abs(Vector2.Dot(l_t_dir, l_i_dir));
        float dotR = Mathf.Abs(Vector2.Dot(r_t_dir, r_i_dir));
        if (dotL > angleTolerance || dotR > angleTolerance) {
            debugMsg = $"시각적 직각 검사 실패: 좌({dotL:F2}) 우({dotR:F2}) > {angleTolerance}";
            return false;
        }

        float dotThumb = Mathf.Abs(Vector2.Dot(l_t_dir, r_t_dir));
        float dotIndex = Mathf.Abs(Vector2.Dot(l_i_dir, r_i_dir));
        if (dotThumb < 1f - angleTolerance || dotIndex < 1f - angleTolerance) {
            debugMsg = $"시각적 평행 검사 실패: 엄지({dotThumb:F2}) 검지({dotIndex:F2}) < {1f - angleTolerance:F2}";
            return false;
        }

        if (!LineIntersection2D(lT2, lTTip, lI1, lITip, out Vector2 c1, out _, out _)) { debugMsg="교차 실패 C1"; return false; }
        if (!LineIntersection2D(rT2, rTTip, rI1, rITip, out Vector2 c2, out _, out _)) { debugMsg="교차 실패 C2"; return false; }
        
        if (!LineIntersection2D(lT2, lTTip, rI1, rITip, out Vector2 c3, out float t3, out float u3)) { debugMsg="교차 실패 C3"; return false; }
        if (!LineIntersection2D(lI1, lITip, rT2, rTTip, out Vector2 c4, out float t4, out float u4)) { debugMsg="교차 실패 C4"; return false; }

        if (t3 < intersectionBackwardLimit || t3 > intersectionForwardLimit ||
            u3 < intersectionBackwardLimit || u3 > intersectionForwardLimit ||
            t4 < intersectionBackwardLimit || t4 > intersectionForwardLimit ||
            u4 < intersectionBackwardLimit || u4 > intersectionForwardLimit) {
            debugMsg = $"교차점 연장선 이탈: t3({t3:F1}) u3({u3:F1}) t4({t4:F1}) u4({u4:F1})";
            return false;
        }

        float d1 = Vector2.Distance(c1, c2);
        float d2 = Vector2.Distance(c3, c4);
        if (d2 > 0.0001f) {
            diagRatio = d1 / d2;
            if (diagRatio < 1f && diagRatio > 0f) diagRatio = 1f / diagRatio;
        }

        Vector2 center = (c1 + c2 + c3 + c4) / 4f;
        Vector2 avgX = ((c3 - c1) + (c2 - c4)) / 2f;
        Vector2 avgY = ((c4 - c1) + (c2 - c3)) / 2f;

        float rectW = avgX.magnitude;
        float rectH = avgY.magnitude;

        if (rectW < 0.01f || rectH < 0.01f) {
            debugMsg = $"크기 부족: {rectW:F2}x{rectH:F2}";
            return false;
        }

        Vector2 dirX = avgX.normalized;
        Vector2 dirY = new Vector2(-dirX.y, dirX.x);
        if (Vector2.Dot(avgY, dirY) < 0) dirY = -dirY;

        Vector2 halfW = dirX * (rectW / 2f);
        Vector2 halfH = dirY * (rectH / 2f);

        Vector2[] quad2D = new Vector2[] {
            center - halfW - halfH, 
            center + halfW - halfH, 
            center + halfW + halfH, 
            center - halfW + halfH  
        };

        float avgDepth = (camT.InverseTransformPoint(lIndex1.position).z + camT.InverseTransformPoint(rIndex1.position).z) / 2f;
        
        quad = new Vector3[4];
        for (int i = 0; i < 4; i++) {
            quad[i] = camT.TransformPoint(new Vector3(quad2D[i].x, quad2D[i].y, avgDepth));
        }

        return true;
    }

    private bool LineIntersection2D(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, out Vector2 intersection, out float t, out float u)
    {
        intersection = Vector2.zero;
        t = 0; u = 0;
        
        float den = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
        if (Mathf.Abs(den) < 1e-5f) return false;
        
        t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / den;
        u = -((p1.x - p2.x) * (p1.y - p3.y) - (p1.y - p2.y) * (p1.x - p3.x)) / den;
        
        intersection = p1 + t * (p2 - p1);
        return true;
    }

    private System.Collections.IEnumerator DoCapture(LatchItem latched)
    {
#if UNITY_EDITOR || UNITY_ANDROID
        PassthroughCameraAccess pca = FindObjectOfType<PassthroughCameraAccess>();
        if (pca == null || !pca.IsPlaying) {
            Debug.LogWarning("[MRHandFrame] PassthroughCameraAccess 없음 또는 IsPlaying=false");
            yield break;
        }

        Vector2Int res = pca.CurrentResolution;
        if (res.x == 0 || res.y == 0) yield break;

        // GetColors는 블로킹 API이므로 프레임 드랍 발생 가능 (셔터 1회만 호출)
        Unity.Collections.NativeArray<Color32> srcPixels = pca.GetColors();
        if (!srcPixels.IsCreated) yield break;

        Vector2[] uv = new Vector2[4];
        for (int i = 0; i < 4; i++) {
            uv[i] = pca.WorldToViewportPoint(latched.quad[i], latched.camPose);
            uv[i].x *= res.x;
            uv[i].y *= res.y;
        }

        float minX = uv[0].x, maxX = uv[0].x;
        float minY = uv[0].y, maxY = uv[0].y;
        for (int i=1; i<4; i++) {
            if (uv[i].x < minX) minX = uv[i].x;
            if (uv[i].x > maxX) maxX = uv[i].x;
            if (uv[i].y < minY) minY = uv[i].y;
            if (uv[i].y > maxY) maxY = uv[i].y;
        }
        
        int cropW = Mathf.Clamp(Mathf.CeilToInt(maxX - minX), 1, res.x);
        int cropH = Mathf.Clamp(Mathf.CeilToInt(maxY - minY), 1, res.y);
        
        Texture2D tex = new Texture2D(cropW, cropH, TextureFormat.RGBA32, false);
        Color32[] dstPixels = new Color32[cropW * cropH];

        for (int y = 0; y < cropH; y++) {
            float v = (float)y / (cropH - 1);
            Vector2 pL = Vector2.Lerp(uv[0], uv[3], v); 
            Vector2 pR = Vector2.Lerp(uv[1], uv[2], v);
            
            for (int x = 0; x < cropW; x++) {
                float u = (float)x / (cropW - 1);
                Vector2 p = Vector2.Lerp(pL, pR, u);
                
                int px = Mathf.Clamp(Mathf.RoundToInt(p.x), 0, res.x - 1);
                int py = Mathf.Clamp(Mathf.RoundToInt(p.y), 0, res.y - 1);
                
                // GetColors의 인덱싱
                int idx = py * res.x + px;
                if (idx >= 0 && idx < res.x * res.y) {
                    dstPixels[y * cropW + x] = srcPixels[idx];
                }
            }
        }
        
        tex.SetPixels32(dstPixels);
        tex.Apply();
        
        byte[] pngBytes = tex.EncodeToPNG();
        Destroy(tex);
        
        // Debugging: Save to disk so user can verify the cropped image visually
#if UNITY_EDITOR
        string debugPath = System.IO.Path.Combine(Application.dataPath, "..", "mr_crop_test.png");
#else
        string debugPath = System.IO.Path.Combine(Application.persistentDataPath, "mr_crop_test.png");
#endif
        System.IO.File.WriteAllBytes(debugPath, pngBytes);
        
        ScreenshotManager sm = ScreenshotManager.Instance;
        if (sm != null) {
            sm.InjectMRScreenshot(pngBytes);
            Debug.Log($"[MRHandFrame] 캡처 및 주입 완료! {cropW}x{cropH} ({pngBytes.Length} bytes). 테스트용 파일 저장됨: {debugPath}");

            // 찍었으면 곧바로 말풍선을 열고 '첨부됨' 상태로 만든다.
            // 주입만 해 두면 사용자는 사진이 붙었는지 알 수 없고,
            // 말풍선의 이미지 모드가 off면 라우터 가드가 다시 off로 강등해 버린다.
            ChatBalloonManager balloon = ChatBalloonManager.Instance;
            if (balloon == null) {
                Debug.LogWarning("[MRHandFrame] ChatBalloonManager를 찾지 못했다. 사진은 주입됐지만 말풍선에 표시되지 않는다.");
            } else {
                balloon.OnMRScreenshotCaptured(cropW, cropH, pngBytes.Length);
            }
        }
#endif
        yield return null;
    }
}
