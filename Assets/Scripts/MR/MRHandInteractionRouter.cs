using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction.Input;

/// <summary>
/// Meta Interaction SDK (Interaction Rig, Real Hands)를 사용하여
/// 손의 3D 위치로 캐릭터 콜라이더(Ball/Pat/Tickle)와 직접 충돌 감지 후
/// MRSpineCharacterController에 전달합니다.
/// </summary>
public class MRHandInteractionRouter : MonoBehaviour
{
    [Header("Interaction SDK Hand References")]
    [Tooltip("Interaction Rig 하위의 Hand 컴포넌트(Left/Right 등)를 연결하세요.")]
    [SerializeField] private Hand[] interactionHands;

    [Header("Interaction Settings")]
    [Tooltip("손가락 끝과 콜라이더 사이의 최대 감지 거리 (OverlapSphere 반경)")]
    [SerializeField] private float proximityRadius = 0.03f;

    [Tooltip("Fist 감지: 모든 손가락 curl이 이 값 이상이면 주먹으로 판정")]
    [SerializeField] private float fistCurlThreshold = 0.6f;

    [Tooltip("Smash 감지: 주먹의 하강 속도가 이 값 이상이면 꿀밤 트리거 (m/s)")]
    [SerializeField] private float smashVelocityThreshold = 0.8f;

    [Header("Tickle Tap (Menu)")]
    [Tooltip("Tickle 영역 진입 후 이 시간 내에 나가면 탭으로 인식 (초)")]
    [SerializeField] private float tickleTapMaxDuration = 0.3f;

    [Tooltip("탭 판정 시 최대 이동 거리 (m)")]
    [SerializeField] private float tickleTapMaxMovement = 0.02f;

    [Header("Tag Settings")]
    [SerializeField] private string ballTag = "Ball";
    [SerializeField] private string patTag = "Pat";
    [SerializeField] private string tickleTag = "Tickle";

    [Header("Raycast / Physics")]
    [SerializeField] private LayerMask interactionMask = ~0;


    // ===== Per-hand state =====
    public class HandState
    {
        public MRSpineCharacterController lockedCharacter;
        public string lockedTag;
        public bool isInteracting;

        // Smash velocity tracking
        public Vector3 lastFistPos;
        public bool hasFistPosHistory;
        public float fistVelocityY;

        // Tickle tap detection
        public float tickleEnterTime;
        public Vector3 tickleEnterPos;
        public bool tickleTapArmed;

        public string lastLogGesture = "";
        
        public bool wasPinching;
        public Vector3 indexTip;
        public Vector3 palmCenter;

        public void Reset()
        {
            lockedCharacter = null;
            lockedTag = null;
            isInteracting = false;
            hasFistPosHistory = false;
            fistVelocityY = 0f;
            tickleTapArmed = false;
        }
    }

    public HandState[] handStates;
    private Collider[] _overlapResults;

    private void Awake()
    {
        int handCount = (interactionHands != null) ? interactionHands.Length : 0;
        handStates = new HandState[handCount];
        for (int i = 0; i < handCount; i++)
            handStates[i] = new HandState();

        _overlapResults = new Collider[32];
    }

    private void Update()
    {

        if (interactionHands == null) return;

        for (int i = 0; i < interactionHands.Length; i++)
        {
            if (interactionHands[i] == null) continue;
            ProcessHand(i, interactionHands[i]);
        }
    }

    private void ProcessHand(int handIndex, Hand hand)
    {
        var state = handStates[handIndex];

        // 손이 추적되지 않거나 데이터 포즈가 유효하지 않으면 상호작용 종료
        if (!hand.IsTrackedDataValid)
        {
            if (state.isInteracting) EndHandInteraction(state);
            state.hasFistPosHistory = false;
            return;
        }

        // 핵심 위치들 추출
        hand.GetJointPose(HandJointId.HandIndexTip, out Pose indexPose);
        Vector3 indexTipPos = indexPose.position;
        state.indexTip = indexTipPos;

        hand.GetJointPose(HandJointId.HandThumbTip, out Pose thumbTipPose);
        Vector3 thumbTipPos = thumbTipPose.position;

        hand.GetJointPose(HandJointId.HandWristRoot, out Pose wristPose);
        Vector3 wristPos = wristPose.position;

        hand.GetJointPose(HandJointId.HandMiddle1, out Pose middle1Pose);
        Vector3 palmCenter = (wristPos + middle1Pose.position) * 0.5f;
        state.palmCenter = palmCenter;

        hand.GetJointPose(HandJointId.HandMiddleTip, out Pose middleTipPose);
        Vector3 middleTipPos = middleTipPose.position;

        // 제스처 판별
        bool isNativePinching = hand.GetFingerIsPinching(HandFinger.Index);
        bool isDistPinching = Vector3.Distance(thumbTipPos, indexTipPos) < 0.035f; // 엄지-검지 끝 거리가 3.5cm 이내면 핀치로 간주
        bool isPinching = isNativePinching || isDistPinching;

        bool justPinched = isPinching && !state.wasPinching;
        state.wasPinching = isPinching;
        state.indexTip = indexTipPos;
        state.palmCenter = palmCenter;

        bool isFist = IsFist(hand);
        bool isFlat = IsFlatHand(hand);

        // 제스처 로그 출력 (바뀔 때만 출력하여 ADB 도배 방지)
        // OK 사인을 만들었을 때 Flat이 아니라 Pinch로 출력되도록 Pinch 우선순위 상향
        string currentGesture = isPinching ? "Pinch" : (isFist ? "Fist" : (isFlat ? "Flat" : "None"));
        if (state.lastLogGesture != currentGesture)
        {
            string handSide = handIndex == 0 ? "Left" : "Right";
            Debug.Log($"[MRHand: {handSide}] Gesture: {currentGesture}");
            state.lastLogGesture = currentGesture;
        }

        // === Smash 처리 (주먹 -> 빠른 하강) ===
        if (isFist)
        {
            ProcessSmash(state, palmCenter, wristPos);
        }
        else
        {
            state.hasFistPosHistory = false;
        }

        // === 상호작용 진행 업데이트 및 종료 ===
        if (state.isInteracting)
        {
            bool shouldEnd = false;

            if (state.lockedCharacter == null || !state.lockedCharacter.IsInteractionLocked)
            {
                shouldEnd = true;
            }
            else if (state.lockedTag == ballTag)
            {
                // 볼 당기기는 핀치 유지시에만 
                if (isPinching)
                    state.lockedCharacter.UpdateInteraction(indexTipPos);
                else
                    shouldEnd = true;
            }
            else if (state.lockedTag == patTag)
            {
                // 쓰다듬기는 계속 손이 쫙 펴진 상태여야 함 (Flat)
                if (isFlat)
                {
                    // 손목부터 중지 끝까지의 캡슐(유지 반경 10cm)로 충돌 검사
                    bool stillInRange = IsHandNearCollider(wristPos, middleTipPos, 0.1f, state.lockedCharacter, patTag);
                    if (stillInRange)
                    {
                        state.lockedCharacter.UpdateInteraction(middleTipPos);
                        state.lockedCharacter.ResetPatMiss();
                    }
                    else
                    {
                        state.lockedCharacter.AccumulatePatMiss(Time.deltaTime);
                    }
                }
                else
                {
                    shouldEnd = true;
                }
            }
            else if (state.lockedTag == tickleTag)
            {
                // 간지럽히기 유지: 주먹(Fist)만 아니면 실수로 핀치가 되더라도 튕기지 않고 유지 허용
                if (!isFist)
                {
                    Vector3 trackPoint = indexTipPos;
                    bool stillInRange = IsHandNearCollider(indexTipPos, indexTipPos, 0.08f, state.lockedCharacter, tickleTag);
                    
                    if (!stillInRange && isFlat)
                    {
                        // 손바닥 전체로 비비는 중이라면 손목~중지끝 캡슐 적용
                        stillInRange = IsHandNearCollider(wristPos, middleTipPos, 0.1f, state.lockedCharacter, tickleTag);
                        trackPoint = middleTipPos;
                    }

                    if (stillInRange)
                    {
                        state.lockedCharacter.UpdateInteraction(trackPoint);
                        state.lockedCharacter.ResetTickleMiss();
                    }
                    else
                    {
                        if (state.tickleTapArmed)
                        {
                            float duration = Time.time - state.tickleEnterTime;
                            float movement = Vector3.Distance(trackPoint, state.tickleEnterPos);

                            if (duration <= tickleTapMaxDuration && movement <= tickleTapMaxMovement)
                            {
                                state.tickleTapArmed = false;
                                shouldEnd = true;
                            }
                            else
                            {
                                state.lockedCharacter.AccumulateTickleMiss(Time.deltaTime);
                            }
                        }
                        else
                        {
                            state.lockedCharacter.AccumulateTickleMiss(Time.deltaTime);
                        }
                    }
                }
                else
                {
                    shouldEnd = true;
                }
            }

            if (shouldEnd)
            {
                EndHandInteraction(state);
            }
            
            return;
        }

        // === 새로운 상호작용 시작 ===
        if (!state.isInteracting)
        {
            // 1. 볼 당기기: 엄지와 검지가 열린 상태에서 볼을 사이에 두고 '핀치로 닫히는 순간(justPinched)' 발동
            if (isPinching)
            {
                if (justPinched)
                {
                    // 엄지 끝과 검지 끝을 잇는 아주 얇은 캡슐(반경 2cm) 선분이 볼과 겹쳐있는지 깐깐하게 검사
                    var (character, tag) = FindClosestCharacterCollider(indexTipPos, thumbTipPos, 0.02f, ballTag);
                    if (character != null)
                    {
                        StartInteraction(state, character, tag, indexTipPos);
                        return;
                    }
                }
                
                // 핀치된 상태(OK 사인 등)에서는 쓰다듬기나 간지럽히기 등 다른 상호작용이 켜지지 않게 최우선 차단
                return;
            }

            // 2. 쓰다듬기 (손을 쫙 편 상태에서 전체 손바닥 면적이 머리에 대면 허용)
            if (isFlat)
            {
                // 손목에서 중지 끝까지를 잇는 캡슐(두께 5cm)로 면적 검사
                var (character, tag) = FindClosestCharacterCollider(wristPos, middleTipPos, 0.05f, patTag);
                if (character != null)
                {
                    StartInteraction(state, character, tag, middleTipPos);
                    return;
                }
            }

            // 3. 간지럽히기 (주먹이나 핀치가 아닐 때 검지 끝이나 손 면적으로 배를 건드리면 허용)
            if (!isFist && !isPinching)
            {
                var (character, tag) = FindClosestCharacterCollider(indexTipPos, indexTipPos, 0.06f, tickleTag);
                Vector3 startPoint = indexTipPos;

                if (character == null && isFlat)
                {
                    var (char2, tag2) = FindClosestCharacterCollider(wristPos, middleTipPos, 0.05f, tickleTag);
                    if (char2 != null)
                    {
                        character = char2;
                        tag = tag2;
                        startPoint = middleTipPos;
                    }
                }

                if (character != null)
                {
                    StartInteraction(state, character, tag, startPoint);
                    
                    state.tickleTapArmed = true;
                    state.tickleEnterTime = Time.time;
                    state.tickleEnterPos = startPoint;
                    return;
                }
            }
        }
    }

    private void StartInteraction(HandState state, MRSpineCharacterController character, string tag, Vector3 point)
    {
        state.lockedCharacter = character;
        state.lockedTag = tag;
        state.isInteracting = true;
        character.BeginInteraction(point, tag);
    }

    // =========================================================
    // Smash Processing
    // =========================================================
    private void ProcessSmash(HandState state, Vector3 handPos, Vector3 wristPos)
    {
        if (state.hasFistPosHistory)
        {
            float deltaY = handPos.y - state.lastFistPos.y;
            state.fistVelocityY = deltaY / Time.deltaTime;

            if (state.fistVelocityY < -smashVelocityThreshold * 0.5f)
            {
                var (character, tag) = FindClosestCharacterCollider(wristPos, handPos, 0.06f, patTag);
                if (character != null)
                {
                    character.ProcessFistFrame(handPos);
                }
            }
        }

        state.lastFistPos = handPos;
        state.hasFistPosHistory = true;

        if (state.isInteracting)
            EndHandInteraction(state);
    }

    // =========================================================
    // 신뢰도 높은 제스처 판정 로직 (거리 기반)
    // =========================================================
    private bool IsFist(Hand hand)
    {
        hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist);
        hand.GetJointPose(HandJointId.HandIndexTip, out Pose index);
        hand.GetJointPose(HandJointId.HandMiddleTip, out Pose middle);
        hand.GetJointPose(HandJointId.HandRingTip, out Pose ring);
        hand.GetJointPose(HandJointId.HandPinkyTip, out Pose pinky);

        float d1 = Vector3.Distance(wrist.position, index.position);
        float d2 = Vector3.Distance(wrist.position, middle.position);
        float d3 = Vector3.Distance(wrist.position, ring.position);
        float d4 = Vector3.Distance(wrist.position, pinky.position);

        // 4개의 손가락 끝이 손목과 매우 가까우면(거리 평균 12cm 이하) 주먹으로 판단
        // (세게 칠 때 손가락이 살짝 펴져도 인정하도록 더 관대하게 0.12f 로 상향)
        return (d1 + d2 + d3 + d4) / 4f < 0.12f;
    }

    private bool IsFlatHand(Hand hand)
    {
        hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist);
        hand.GetJointPose(HandJointId.HandIndexTip, out Pose index);
        hand.GetJointPose(HandJointId.HandMiddleTip, out Pose middle);
        hand.GetJointPose(HandJointId.HandRingTip, out Pose ring);
        hand.GetJointPose(HandJointId.HandPinkyTip, out Pose pinky);

        float d1 = Vector3.Distance(wrist.position, index.position);
        float d2 = Vector3.Distance(wrist.position, middle.position);
        float d3 = Vector3.Distance(wrist.position, ring.position);
        float d4 = Vector3.Distance(wrist.position, pinky.position);

        // 4개의 손가락 끝이 손목과 멀리 있으면(살짝 굽혀도 인정: 평균 9cm 이상) 쫙 펴진 손바닥(Flat)으로 판단
        return (d1 + d2 + d3 + d4) / 4f > 0.09f;
    }

    // =========================================================
    // Collider Detection (3D Capsule Proximity for Hand Physics)
    // =========================================================
    private (MRSpineCharacterController character, string tag) FindClosestCharacterCollider(Vector3 p1, Vector3 p2, float rad, string preferredTag)
    {
        int count = Physics.OverlapCapsuleNonAlloc(p1, p2, rad, _overlapResults, interactionMask, QueryTriggerInteraction.Collide);

        MRSpineCharacterController bestCharacter = null;
        string bestTag = null;
        int bestPriority = 99;
        float bestDistance = float.MaxValue;
        Vector3 center = (p1 + p2) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            var col = _overlapResults[i];
            if (col == null) continue;

            string tag = col.tag;
            int priority = TagPriority(tag);
            if (priority >= 99) continue;

            if (preferredTag != null && tag != preferredTag) continue;

            var character = col.GetComponentInParent<MRSpineCharacterController>();
            if (character == null) continue;

            if (!character.IsAvailableForInteraction()) continue;

            float dist = Vector3.Distance(center, col.ClosestPoint(center));

            if (priority < bestPriority || (priority == bestPriority && dist < bestDistance))
            {
                bestCharacter = character;
                bestTag = tag;
                bestPriority = priority;
                bestDistance = dist;
            }
        }

        return (bestCharacter, bestTag);
    }

    private bool IsHandNearCollider(Vector3 p1, Vector3 p2, float rad, MRSpineCharacterController character, string tag)
    {
        int count = Physics.OverlapCapsuleNonAlloc(p1, p2, rad, _overlapResults, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            var col = _overlapResults[i];
            if (col == null || col.tag != tag) continue;

            var c = col.GetComponentInParent<MRSpineCharacterController>();
            if (c == character) return true;
        }

        return false;
    }

    private int TagPriority(string tag)
    {
        if (tag == ballTag) return 0;
        if (tag == patTag) return 1;
        if (tag == tickleTag) return 2;
        return 99;
    }

    private void EndHandInteraction(HandState state)
    {
        if (state.lockedCharacter != null)
            state.lockedCharacter.EndInteraction();

        state.Reset();
    }


    // 전역에서 현재 손 위치를 얻어오기 위한 유틸
    public static Vector3? GetHandIndexTip(bool isLeft)
    {
        var router = FindObjectOfType<MRHandInteractionRouter>();
        if (router == null || router.handStates == null) return null;

        foreach (var hand in router.interactionHands)
        {
            if (hand != null && hand.IsTrackedDataValid)
            {
                if (isLeft && hand.Handedness == Handedness.Left)
                    return router.handStates[System.Array.IndexOf(router.interactionHands, hand)].indexTip;
                else if (!isLeft && hand.Handedness == Handedness.Right)
                    return router.handStates[System.Array.IndexOf(router.interactionHands, hand)].indexTip;
            }
        }
        return null;
    }
}
