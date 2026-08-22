using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace AICO.MR.EditorTools
{
    [InitializeOnLoad]
    public class MRHandFrameGestureAutoAttach
    {
        static MRHandFrameGestureAutoAttach()
        {
            EditorApplication.delayCall += () => {
                if (Application.isPlaying) return;

                Scene scene = EditorSceneManager.GetActiveScene();
                if (scene.name != "SampleSceneKAI-MR") return;

                GameObject worldUI = GameObject.Find("WorldUI"); 
                // In case Find doesn't work well with paths, let's find it by name.
                if (worldUI == null) {
                    worldUI = GameObject.Find("MR/WorldUI");
                }
                
                if (worldUI != null && worldUI.GetComponent<MRHandFrameGesture>() == null)
                {
                    worldUI.AddComponent<MRHandFrameGesture>();
                    EditorSceneManager.MarkSceneDirty(scene);
                    Debug.Log("[MRHandFrameGestureAutoAttach] MRHandFrameGesture 컴포넌트를 WorldUI에 자동 부착했습니다.");
                }
            };
        }
    }
}
