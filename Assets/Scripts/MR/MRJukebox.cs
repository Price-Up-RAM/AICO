using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MR Jukebox 제어 스크립트.
/// 재생/일시정지, 이전/다음 곡 이동 기능을 제공합니다.
/// PokeInteractable 등의 Unity Event와 연결하여 사용합니다.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MRJukebox : MonoBehaviour
{
    [Header("Playlist")]
    [Tooltip("재생할 오디오 클립 목록")]
    [SerializeField] private List<AudioClip> playlist = new List<AudioClip>();
    
    [Tooltip("앱 시작 시 자동으로 재생할지 여부")]
    [SerializeField] private bool playOnAwake = false;

    private AudioSource _audioSource;
    private int _currentIndex = 0;
    
    // 현재 일시정지 상태인지 추적
    private bool _isPaused = false;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            Debug.LogError("[MRJukebox] AudioSource 컴포넌트가 없습니다!");
            return;
        }

        // 초기화 시 AudioSource의 playOnAwake를 Jukebox 세팅과 맞춤
        _audioSource.playOnAwake = playOnAwake;
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
            PlayTrack(_currentIndex);
        }
    }

    /// <summary>
    /// 재생 중이면 일시정지, 일시정지 상태면 이어서 재생, 
    /// 정지 상태면 처음부터 재생을 토글합니다.
    /// (UI 버튼의 onClick 이나 Interactable Unity Event Wrapper에 연결)
    /// </summary>
    public void PlayPauseToggle()
    {
        if (playlist.Count == 0) return;

        if (_audioSource.isPlaying)
        {
            // 재생 중 -> 일시정지
            _audioSource.Pause();
            _isPaused = true;
            Debug.Log($"[MRJukebox] 일시정지: {playlist[_currentIndex].name}");
        }
        else if (_isPaused)
        {
            // 일시정지 -> 이어서 재생
            _audioSource.UnPause();
            _isPaused = false;
            Debug.Log($"[MRJukebox] 재생 재개: {playlist[_currentIndex].name}");
        }
        else
        {
            // 완전 정지 상태 -> 현재 곡 재생
            PlayTrack(_currentIndex);
        }
    }

    /// <summary>
    /// 다음 곡을 재생합니다. (리스트 끝이면 처음으로 루프)
    /// </summary>
    public void PlayNext()
    {
        if (playlist.Count <= 1) return;

        _currentIndex++;
        if (_currentIndex >= playlist.Count)
        {
            _currentIndex = 0; // 끝에 도달하면 처음으로 루프
        }

        PlayTrack(_currentIndex);
    }

    /// <summary>
    /// 이전 곡을 재생합니다. (리스트 처음이면 끝으로 루프)
    /// </summary>
    public void PlayPrevious()
    {
        if (playlist.Count <= 1) return;

        _currentIndex--;
        if (_currentIndex < 0)
        {
            _currentIndex = playlist.Count - 1; // 처음에 도달하면 끝으로 루프
        }

        PlayTrack(_currentIndex);
    }

    /// <summary>
    /// 특정 인덱스의 곡을 재생합니다.
    /// </summary>
    private void PlayTrack(int index)
    {
        if (index < 0 || index >= playlist.Count) return;

        _audioSource.clip = playlist[index];
        _audioSource.Play();
        _isPaused = false;
        
        Debug.Log($"[MRJukebox] 재생 중: {_audioSource.clip.name} ({index + 1}/{playlist.Count})");
    }

    // (선택) 곡이 끝났을 때 자동으로 다음 곡으로 넘어가게 하려면 아래 Update 활용
    private void Update()
    {
        if (playlist.Count == 0 || _isPaused) return;

        // 클립이 배정되어 있고, 플레이중이 아니면서, 오디오 포지션이 끝까지 갔다면 다음 곡으로
        if (_audioSource.clip != null && !_audioSource.isPlaying && _audioSource.time == 0)
        {
            // Unity AudioSource 특성상 재생이 끝난 직후 time이 0으로 초기화됨
            // 짧은 프레임 안에 PlayNext를 여러 번 부르지 않도록 주의
            if (!_audioSource.loop) // 단일 클립 무한루프 모드가 아닐 때만 다음 곡으로
            {
                // PlayNext();
                // 위 코드를 활성화하면 곡이 끝날 때마다 자동으로 넘어갑니다.
            }
        }
    }
}
