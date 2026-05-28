using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assistant
{
    [Serializable]
    public struct VoiceData
    {
        public AudioClip Clip; // 대사 오디오 클립
        public string Text;    // 대사 텍스트
    }
    
    // ScriptableObject를 사용하여 ActorData 생성 메뉴를 유니티 에디터에 추가
    [CreateAssetMenu(fileName = "ActorData", menuName = "ActorData" )]
    public class ActorData : ScriptableObject
    {
        public string actorName; // 캐릭터 이름
        public VoiceData[] LoginVoice; // 로그인 시 사용할 대사 목록
        public VoiceData[] TouchVoice; // 터치 시 사용할 대사 목록

        private int _lastTouchIndex = -1; // 마지막으로 재생된 터치 대사의 인덱스
        
        // 무작위로 로그인 대사 가져오기
        public VoiceData GetLoginVoice()
        {
            // 로그인 대사 중 하나를 무작위로 반환
            return LoginVoice[Random.Range(0, LoginVoice.Length)];
        }
        
        // 무작위로 터치 대사 가져오기
        public VoiceData GetTouchVoice()
        {
            // 터치 대사 중 하나를 무작위로 선택
            int index = Random.Range(0, TouchVoice.Length);
            
            // 터치 대사가 여러 개 있을 때, 이전과 다른 대사를 선택할 때까지 반복
            while (TouchVoice.Length > 1 && index == _lastTouchIndex)
            {
                index = Random.Range(0, TouchVoice.Length);
            }
            
            _lastTouchIndex = index; // 마지막 재생된 대사 인덱스 업데이트
            return TouchVoice[index]; // 선택된 터치 대사 반환
        }
    }
}
