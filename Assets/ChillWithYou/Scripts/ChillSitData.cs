using System.Collections.Generic;
using UnityEngine;

// 칠윗유 모드에서 캐릭터별 착석 위치/스케일 오프셋 데이터
[CreateAssetMenu(fileName = "ChillSitData", menuName = "AICO/Chill With You/Chill Sit Data")]
public class ChillSitData : ScriptableObject
{
    [System.Serializable]
    public class CharacterSitOffset
    {
        public string charcode;  // ch0001 같은 캐릭터 코드
        public Vector3 positionOffset;  // 의자 기준 로컬 위치 오프셋
        public Vector3 rotationOffset;  // 의자 기준 로컬 회전 오프셋 (오일러 각)
        public float scaleMultiplier = 1f;  // 원본 스케일(initLocalScale) 대비 배율
        public Vector3 chairLocalPosition;  // 의자(chairRoot) 절대 로컬 위치 (캐릭터별 다리 길이 등 보정용)
        public Vector3 chairLocalRotation;  // 의자(chairRoot) 절대 로컬 회전 (오일러 각)
    }

    public CharacterSitOffset defaultOffset;  // charcode 매칭 실패 시 사용
    public List<CharacterSitOffset> perCharacterOffsets = new List<CharacterSitOffset>();

    [Header("책상 공통 배치 (캐릭터 무관 — ChillModeManager가 진입 시 로드)")]
    // 기본값은 기존 ChillModeManager 프리팹 튜닝값과 동일 (기존 에셋 역직렬화 시 동작 불변)
    public Vector3 deskPositionOffset = new Vector3(703.3f, -493f, -355.4f);
    public Vector3 deskRotationOffset = new Vector3(0f, 40.423f, 0f);
    public float deskScaleMultiplier = 250f;

    // charcode에 해당하는 오프셋 반환, 없으면 defaultOffset 반환
    public CharacterSitOffset GetOffset(string charcode)
    {
        foreach (CharacterSitOffset offset in perCharacterOffsets)
        {
            if (offset.charcode == charcode)
            {
                return offset;
            }
        }
        return defaultOffset;
    }

    // charcode 전용 오프셋을 반환, 없으면 defaultOffset을 복사해 새로 만들어 등록 (다른 캐릭터/defaultOffset을 건드리지 않고 개별 튜닝하기 위함)
    public CharacterSitOffset GetOrCreateOffset(string charcode)
    {
        foreach (CharacterSitOffset offset in perCharacterOffsets)
        {
            if (offset.charcode == charcode)
            {
                return offset;
            }
        }

        CharacterSitOffset newOffset = new CharacterSitOffset();
        newOffset.charcode = charcode;
        newOffset.positionOffset = defaultOffset.positionOffset;
        newOffset.rotationOffset = defaultOffset.rotationOffset;
        newOffset.scaleMultiplier = defaultOffset.scaleMultiplier;
        newOffset.chairLocalPosition = defaultOffset.chairLocalPosition;
        newOffset.chairLocalRotation = defaultOffset.chairLocalRotation;
        perCharacterOffsets.Add(newOffset);
        return newOffset;
    }
}
