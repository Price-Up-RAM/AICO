using UnityEngine;
using System.Collections.Generic;

// BoneFollower가 찾아야 할 Bone 이름 후보 목록을 저장하는 컴포넌트입니다.
// 이 스크립트는 BoneFollower 컴포넌트와 같은 게임오브젝트에 추가해주세요.
public class BoneFollowerAlias : MonoBehaviour
{
    [Tooltip("이 BoneFollower가 찾아야 할 Bone 이름 후보 목록입니다. 위에서부터 순서대로 찾습니다.")]
    public List<string> PossibleBoneNames = new List<string>();
}