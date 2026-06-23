using UnityEngine;

/// <summary>
/// Jukebox 데모. 씬 시작 시 주크박스를 열고 지정한 트랙을 자동 재생한다.
/// (StreamingAssets/Jukebox/BGM/&lt;trackId&gt;.ogg 가 있어야 소리가 난다)
/// </summary>
public class JukeboxDemo : MonoBehaviour
{
    [SerializeField] private JukeboxView jukebox;
    [Tooltip("자동 재생할 트랙 이름 (MRJukebox playlist의 trackName, 예: Lofi1, rain)")]
    [SerializeField] private string trackId = "Lofi1";
    [SerializeField] private bool playOnStart = true;

    private void Start()
    {
        if (jukebox == null)
        {
            jukebox = FindObjectOfType<JukeboxView>();
        }
        if (jukebox == null)
        {
            Debug.LogWarning("[JukeboxDemo] JukeboxView를 찾지 못했습니다.");
            return;
        }

        jukebox.Show();
        if (playOnStart && !string.IsNullOrEmpty(trackId))
        {
            jukebox.PlayTrack(trackId, true);
        }
    }

#if UNITY_EDITOR
    public void EditorSet(JukeboxView view, string id)
    {
        jukebox = view;
        trackId = id;
    }
#endif
}
