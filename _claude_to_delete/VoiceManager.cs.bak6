using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class VoiceManager : MonoBehaviour
{
    [SerializeField] public AudioSource audioSource; // 오디오를 재생할 AudioSource
    private static VoiceManager instance; // 싱글톤 인스턴스
    private Queue<AudioClip> clipQueue = new Queue<AudioClip>(); // AudioClip을 저장하는 Queue
    public bool isQueuePlaying = false;  // 현재 재생 여부를 추적하는 플래그
    private int pendingLoadCount = 0;    // 큐 적재용 로드 코루틴 진행 수 (busy 판정용)

    // VoiceManager 인스턴스에 접근하는 함수
    public static VoiceManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<VoiceManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("VoiceManager");
                    instance = go.AddComponent<VoiceManager>();
                }
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        // 싱글톤 패턴을 적용하여 유일한 인스턴스 유지
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴되지 않도록 설정
        }
        else
        {
            // Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 시작하자마자 재생
        // Dialogue greeting = DialogueManager.Instance.GetRandomGreeting();
        // PlayAudioFromPath(greeting.filePath);
        // PlayWavFromPersistentPath();
    }


    private void Update()
    {
        // AudioSource가 재생 중이지 않고, Queue에 클립이 있으면 다음 클립을 재생
        if (clipQueue.Count > 0 && !audioSource.isPlaying)
        {
            PlayNextClip();
        }
        else if (clipQueue.Count == 0 && !audioSource.isPlaying)
        {
            isQueuePlaying = false;
        }
    }

    // 경로로부터 오디오를 로드하고 재생하는 함수
    public void PlayAudioFromPath(string audioPath)
    {
        // audioPath = "Sound/arona/Arona_Attendance_Enter_1.ogg";
        try
        {
            // string fullPath = "file://" + Application.dataPath + audioPath;  // Assets 패키지화 할 경우 사용
            // string fullPath = "file://" + MRDataPath.Root  + audioPath;  // Assets>StreamingAssets 활용시 사용
            string fullPath = Path.Combine(MRDataPath.Root, audioPath);
            StartCoroutine(LoadAudioOGG(fullPath));
        } catch {
            
        }

    }
    
    public void PlayWavAudioFromPath(string audioPath)
    {
        // audioPath = "Sound/arona/Arona_Attendance_Enter_1.ogg";
        try
        {
            // string fullPath = "file://" + Application.dataPath + audioPath;  // Assets 패키지화 할 경우 사용
            // string fullPath = "file://" + MRDataPath.Root  + audioPath;  // Assets>StreamingAssets 활용시 사용
            string fullPath = Path.Combine(MRDataPath.Root, audioPath);
            StartCoroutine(LoadAudioWav(fullPath));
        }
        catch
        {

        }

    }

    public void PlayWavFromPersistentPath()
    {
        string fullPath = Path.Combine(Application.persistentDataPath, "response.wav");  // API wav
        StartCoroutine(LoadAudioWav(fullPath));
    }

    // 오디오 파일을 로드하는 코루틴
    private IEnumerator LoadAudioOGG(string audioPath)
    {
        // Debug.Log("audioPath: " + audioPath);
        using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.OGGVORBIS))
        {
            yield return uwr.SendWebRequest(); // 요청 전송

            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
            {
                // Debug.LogError("오디오 로드 실패: " + uwr.error);
                Debug.LogError("오디오 로드 실패: " + audioPath);
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr); // 오디오 클립 가져오기
                audioSource.clip = clip;
                audioSource.volume = 1f; // 100%
                try
                {
                    audioSource.volume = SettingManager.Instance.settings.sound_volumeMaster / 100;
                }
                catch
                {
                    Debug.Log("ogg volume change error");
                }
                audioSource.Play(); // 오디오 재생
            }
        }
    }

    // 오디오 파일을 로드하는 코루틴. 늘어나면 변수화
    private IEnumerator LoadAudioWav(string audioPath)
    {
        using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.WAV))
        {
            yield return uwr.SendWebRequest(); // 요청 전송

            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("오디오 로드 실패: " + uwr.error);
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr); // 오디오 클립 가져오기
                audioSource.clip = clip;
                audioSource.volume = 1f; // 100%
                try
                {
                    audioSource.volume = SettingManager.Instance.settings.sound_volumeMaster / 100;
                }
                catch
                {
                    Debug.Log("wav volume change error");
                }
                audioSource.Play(); // 오디오 재생

                // 입 움직이기 시작
                isQueuePlaying = true;
            }
        }
    }


    // 오디오 클립을 Queue에 추가하는 함수
    public void AddToQueue(AudioClip clip)
    {
        clipQueue.Enqueue(clip); // 클립을 Queue에 추가

        // 만약 현재 아무것도 재생 중이지 않다면, 바로 재생 시작
        if (!isQueuePlaying)
        {
            PlayNextClip();
        }
    }

    // Queue에 있는 다음 클립을 재생하는 함수
    private void PlayNextClip()
    {
        if (clipQueue.Count > 0)
        {
            isQueuePlaying = true;
            audioSource.clip = clipQueue.Dequeue();  // Queue에서 클립을 가져옴
            audioSource.Play();  // AudioSource로 재생 시작
        }
        else
        {
            isQueuePlaying = false;  // Queue가 비었을 경우 재생 중지
        }
    }

    public void LoadAudioWavToQueue()
    {
        string audioPath = Path.Combine(Application.persistentDataPath, "response.wav");
        LoadAudioWavToQueue(audioPath);
    }

    public void LoadAudioWavToQueue(string audioPath)
    {
        LoadAudioWavToQueue(audioPath, -1);
    }

    // ttsSessionId >= 0 이면 로드 완료 시점에 TTS 세션이 그대로인지 확인 후 큐에 넣는다
    // (세션 리셋 경계에서 늦게 완료된 로드가 옛 클립을 새 큐에 흘리는 것을 차단)
    public void LoadAudioWavToQueue(string audioPath, int ttsSessionId)
    {
        if (string.IsNullOrEmpty(audioPath))
        {
            Debug.LogError("오디오 로드 실패: audioPath가 비어있습니다.");
            return;
        }

        #if UNITY_ANDROID
        // 안드로이드에서 파일 경로가 다를 경우 처리 방식 다르게 적용
        string audioPathAndroid = audioPath.StartsWith("file://") ? audioPath : "file://" + audioPath; // 안드로이드+UnityWebRequest 에서는 "file://" 경로 필요
        StartCoroutine(LoadAudioWavToQueueEnum(audioPathAndroid, ttsSessionId));
        #else
        // 안드로이드가 아닌 플랫폼에서는 일반적인 파일 경로를 사용
        StartCoroutine(LoadAudioWavToQueueEnum(audioPath, ttsSessionId));
        #endif
    }

    // WAV 파일을 로드하고 Queue에 추가하는 코루틴
    private IEnumerator LoadAudioWavToQueueEnum(string audioPath, int ttsSessionId)
    {
        pendingLoadCount++;
        try
        {
            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(audioPath, AudioType.WAV))
            {
                yield return uwr.SendWebRequest(); // 요청 전송

                if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError("오디오 로드 실패: " + uwr.error);
                }
                else if (ttsSessionId >= 0 && TTSManager.Instance != null && ttsSessionId != TTSManager.Instance.GetSessionId())
                {
                    // 로드 도중 세션이 바뀜 → 옛 대화의 클립이므로 폐기
                    Debug.Log($"[TTS] Ignore stale clip (session mismatch: {ttsSessionId} != {TTSManager.Instance.GetSessionId()})");
                }
                else
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(uwr); // 오디오 클립 가져오기
                    AddToQueue(clip); // 클립을 Queue에 추가
                }
            }
        }
        finally
        {
            pendingLoadCount--;
        }
    }

    // 재생 파이프라인이 소비 중인지 (재생 중 / 큐 대기 / 로드 진행 중)
    public bool IsPlaybackBusy()
    {
        return audioSource.isPlaying || clipQueue.Count > 0 || pendingLoadCount > 0;
    }


    // 오디오 정지 함수: 현재 클립 정지 + 대기 큐 폐기
    // (큐를 남기면 Update의 자동재생이 다음 프레임에 멈춘 발화를 즉시 재개한다)
    public void StopAudio()
    {
        clipQueue.Clear();
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        isQueuePlaying = false;
    }

    // 현재 재생(세팅)중인 clip 반환
    public AudioClip GetAudioClip()
    {
        if (audioSource.clip) {
            return audioSource.clip; // 현재 오디오 소스를 반환하거나 적절한 AudioClip 반환
        }

        return null;
    }

    public void ResetAudio()
    {
        // 현재 재생 중인 오디오를 멈춤
        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // 오디오 큐를 비움
        clipQueue.Clear();

        // 재생 플래그를 false로 설정
        isQueuePlaying = false;

        Debug.Log("Audio playback stopped and queue cleared.");
    }
}
