using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class JukeboxTrack
{
    [Tooltip("재생할 오디오 클립")]
    public AudioClip clip;
    
    [Tooltip("곡 이름 (UI 표시용)")]
    public string trackName;
    
    [Tooltip("AI 검색용 태그 (예: rainy, quiet, jazz)")]
    public List<string> tags = new List<string>();
}

public enum JukeboxPlayMode
{
    Sequential, // 순차 재생 후 끝에서 정지
    LoopAll,    // 전체 반복 재생
    LoopOne,    // 현재 곡만 반복 재생
    Random      // 랜덤 재생
}

/// <summary>
/// MR Jukebox 제어 스크립트.
/// 재생/일시정지, 이전/다음 곡 이동, 다양한 재생 모드 및 AI 태그 검색 기능을 제공합니다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MRJukebox : MonoBehaviour
{
    [Header("Playlist")]
    [SerializeField] private List<JukeboxTrack> playlist = new List<JukeboxTrack>();
    
    [Header("Settings")]
    [Tooltip("앱 시작 시 자동으로 재생할지 여부")]
    [SerializeField] private bool playOnAwake = false;
    
    [Tooltip("기본 재생 모드")]
    [SerializeField] private JukeboxPlayMode playMode = JukeboxPlayMode.LoopAll;

    private AudioSource _audioSource;
    private int _currentIndex = -1; // -1은 아직 선택된 곡이 없음을 의미
    private bool _isPaused = false;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            Debug.LogError("[MRJukebox] AudioSource 컴포넌트가 없습니다!");
            return;
        }

        // 유니티 AudioSource의 자체 PlayOnAwake는 끄고 스크립트에서 제어합니다
        _audioSource.playOnAwake = false; 
    }

    private void Start()
    {
        if (playlist == null || playlist.Count == 0)
        {
            Debug.LogWarning("[MRJukebox] 재생 목록(playlist)이 비어있습니다.");
            return;
        }

        if (playOnAwake)
        {
            if (playMode == JukeboxPlayMode.Random)
                PlayRandomTrack();
            else
                PlayTrack(0);
        }
    }

    private void Update()
    {
        if (playlist.Count == 0 || _isPaused) return;

        // 곡 재생이 끝났는지 체크 (클립이 있고, 재생 중이 아니며, 재생 위치가 0일 때)
        if (_audioSource.clip != null && !_audioSource.isPlaying && _audioSource.time == 0)
        {
            HandleTrackEnd();
        }
    }

    private void HandleTrackEnd()
    {
        switch (playMode)
        {
            case JukeboxPlayMode.LoopOne:
                // 동일 곡 다시 재생
                PlayTrack(_currentIndex);
                break;
                
            case JukeboxPlayMode.LoopAll:
                PlayNext();
                break;
                
            case JukeboxPlayMode.Sequential:
                // 리스트의 마지막 곡이면 정지, 아니면 다음 곡
                if (_currentIndex >= playlist.Count - 1)
                {
                    _audioSource.Stop();
                    _currentIndex = -1;
                }
                else
                {
                    PlayNext();
                }
                break;
                
            case JukeboxPlayMode.Random:
                PlayRandomTrack();
                break;
        }
    }

    /// <summary>
    /// 재생 중이면 일시정지, 일시정지면 이어서 재생, 정지 상태면 재생
    /// </summary>
    public void PlayPauseToggle()
    {
        if (playlist.Count == 0) return;

        if (_audioSource.isPlaying)
        {
            _audioSource.Pause();
            _isPaused = true;
            Debug.Log($"[MRJukebox] 일시정지: {playlist[_currentIndex].trackName}");
        }
        else if (_isPaused)
        {
            _audioSource.UnPause();
            _isPaused = false;
            Debug.Log($"[MRJukebox] 재생 재개: {playlist[_currentIndex].trackName}");
        }
        else
        {
            int indexToPlay = (_currentIndex == -1) ? 0 : _currentIndex;
            if (playMode == JukeboxPlayMode.Random && _currentIndex == -1)
            {
                PlayRandomTrack();
            }
            else
            {
                PlayTrack(indexToPlay);
            }
        }
    }

    public void PlayNext()
    {
        if (playlist.Count == 0) return;

        if (playMode == JukeboxPlayMode.Random)
        {
            PlayRandomTrack();
            return;
        }

        _currentIndex++;
        if (_currentIndex >= playlist.Count)
        {
            _currentIndex = 0;
        }

        PlayTrack(_currentIndex);
    }

    public void PlayPrevious()
    {
        if (playlist.Count == 0) return;

        if (playMode == JukeboxPlayMode.Random)
        {
            PlayRandomTrack();
            return;
        }

        _currentIndex--;
        if (_currentIndex < 0)
        {
            _currentIndex = playlist.Count - 1;
        }

        PlayTrack(_currentIndex);
    }

    private void PlayRandomTrack()
    {
        if (playlist.Count <= 1)
        {
            PlayTrack(0);
            return;
        }

        int newIndex = _currentIndex;
        // 현재 곡과 다른 곡을 랜덤하게 선택
        while (newIndex == _currentIndex)
        {
            newIndex = Random.Range(0, playlist.Count);
        }

        PlayTrack(newIndex);
    }

    private void PlayTrack(int index)
    {
        if (index < 0 || index >= playlist.Count) return;

        _currentIndex = index;
        var track = playlist[index];
        
        if (track.clip == null)
        {
            Debug.LogWarning($"[MRJukebox] 트랙 '{(string.IsNullOrEmpty(track.trackName) ? index.ToString() : track.trackName)}'의 오디오 클립이 비어있습니다.");
            return;
        }

        _audioSource.clip = track.clip;
        _audioSource.Play();
        _isPaused = false;
        
        string tName = string.IsNullOrEmpty(track.trackName) ? track.clip.name : track.trackName;
        Debug.Log($"[MRJukebox] 재생 중: {tName} ({index + 1}/{playlist.Count})");
    }

    // ==========================================
    // 상태 제어 기능 (MR UI 버튼 연동용)
    // ==========================================

    public void SetPlayModeSequential() => playMode = JukeboxPlayMode.Sequential;
    public void SetPlayModeLoopAll() => playMode = JukeboxPlayMode.LoopAll;
    public void SetPlayModeLoopOne() => playMode = JukeboxPlayMode.LoopOne;
    public void SetPlayModeRandom() => playMode = JukeboxPlayMode.Random;

    /// <summary>
    /// 순차 -> 전체반복 -> 한곡반복 -> 랜덤 순으로 모드 토글
    /// </summary>
    public void TogglePlayMode()
    {
        playMode = (JukeboxPlayMode)(((int)playMode + 1) % 4);
        Debug.Log($"[MRJukebox] 재생 모드 변경: {playMode}");
    }

    // ==========================================
    // AI 호출용 기능
    // ==========================================

    /// <summary>
    /// 주어진 태그를 포함하는 곡을 검색하여 재생합니다.
    /// AI 시스템이 이 함수에 태그 문자열을 전달하여 실행할 수 있습니다.
    /// </summary>
    public void PlayByTag(string tag)
    {
        if (playlist.Count == 0 || string.IsNullOrEmpty(tag)) return;
        
        string lowerTag = tag.ToLower();
        List<int> matchedIndices = new List<int>();

        for (int i = 0; i < playlist.Count; i++)
        {
            if (playlist[i].tags == null) continue;

            foreach (var t in playlist[i].tags)
            {
                if (!string.IsNullOrEmpty(t) && t.ToLower().Contains(lowerTag))
                {
                    matchedIndices.Add(i);
                    break;
                }
            }
        }

        if (matchedIndices.Count > 0)
        {
            // 매칭된 곡 중 랜덤으로 하나 재생
            int targetIndex = matchedIndices[Random.Range(0, matchedIndices.Count)];
            PlayTrack(targetIndex);
            Debug.Log($"[MRJukebox] AI 요청: 태그 '{tag}' 매칭 곡 재생 -> {playlist[targetIndex].trackName}");
        }
        else
        {
            Debug.LogWarning($"[MRJukebox] 태그 '{tag}'에 해당하는 곡을 찾을 수 없습니다.");
        }
    }
}
