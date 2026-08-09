using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class CharacterPomodoroVoicePlayback
{
    private static int playbackRequestId;

    public static bool TryPlayRandomDialogue(MonoBehaviour coroutineHost)
    {
        if (coroutineHost == null ||
            ChatModeManager.Instance == null ||
            !ChatModeManager.Instance.IsPomodoroMode())
        {
            return false;
        }

        string characterName = GetCurrentCharacterName();
        CharacterPomodoroVoiceCatalog catalog =
            CharacterPomodoroVoiceCatalog.LoadDefault();
        PomodoroVoiceSituation situation =
            PomodoroTimer.Instance != null
                ? PomodoroTimer.Instance.CurrentVoiceSituation
                : PomodoroVoiceSituation.Ready;
        bool timerRunning =
            PomodoroTimer.Instance != null &&
            PomodoroTimer.Instance.IsTimerRunning;
        List<CharacterPomodoroPlaybackCandidate> candidates =
            CharacterPomodoroVoiceRepository.GetPlayableCandidates(
                characterName,
                catalog,
                situation);

        if (candidates.Count == 0)
        {
            return false;
        }

        CharacterPomodoroPlaybackCandidate selected =
            SelectCandidate(candidates, situation, timerRunning);

        if (TTSManager.Instance != null)
        {
            TTSManager.Instance.CancelTtsSession();
        }

        int requestId = ++playbackRequestId;
        coroutineHost.StartCoroutine(PlayDialogue(selected, requestId));
        return true;
    }

    private static IEnumerator PlayDialogue(
        CharacterPomodoroPlaybackCandidate selected,
        int requestId)
    {
        if (ChatModeManager.Instance == null ||
            !ChatModeManager.Instance.IsPomodoroMode())
        {
            yield break;
        }

        AudioClip clip = selected.audioClip;

        if (clip == null &&
            !string.IsNullOrWhiteSpace(selected.audioFilePath))
        {
            string fileUri;
            try
            {
                fileUri = new Uri(selected.audioFilePath).AbsoluteUri;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[CharacterPomodoroVoice] Invalid WAV path. " +
                    $"path={selected.audioFilePath}, error={exception.Message}");
                yield break;
            }

            using (UnityWebRequest request =
                   UnityWebRequestMultimedia.GetAudioClip(
                       fileUri,
                       AudioType.WAV))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(
                        $"[CharacterPomodoroVoice] Generated WAV load failed. " +
                        $"path={selected.audioFilePath}, error={request.error}");
                    yield break;
                }

                clip = DownloadHandlerAudioClip.GetContent(request);
            }
        }

        if (ChatModeManager.Instance == null ||
            !ChatModeManager.Instance.IsPomodoroMode() ||
            requestId != playbackRequestId ||
            clip == null ||
            string.IsNullOrWhiteSpace(selected.message))
        {
            yield break;
        }

        PlayVoice(clip);

        if (CaptionBalloonManager.Instance != null)
        {
            CaptionBalloonManager.Instance.ShowForSeconds(
                selected.message,
                clip.length + 0.5f);
        }
        else if (AnswerBalloonSimpleManager.Instance != null)
        {
            AnswerBalloonSimpleManager.Instance
                .ShowAnswerBalloonSimpleForSeconds(
                    selected.message,
                    clip.length + 0.5f);
        }
    }

    private static CharacterPomodoroPlaybackCandidate SelectCandidate(
        List<CharacterPomodoroPlaybackCandidate> candidates,
        PomodoroVoiceSituation situation,
        bool timerRunning)
    {
        // Repository에서 현재 상황과 Anytime 후보만 남긴 뒤 무작위 선택한다.
        Debug.Log(
            $"[CharacterPomodoroVoice] Random selection. " +
            $"situation={situation}, timerRunning={timerRunning}, " +
            $"candidateCount={candidates.Count}");
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private static string GetCurrentCharacterName()
    {
        if (CharManager.Instance == null)
        {
            return string.Empty;
        }

        GameObject currentCharacter =
            CharManager.Instance.GetCurrentCharacter();
        return currentCharacter != null
            ? CharManager.Instance.GetNickname(currentCharacter)
            : string.Empty;
    }

    private static void PlayVoice(AudioClip clip)
    {
        VoiceManager voiceManager = VoiceManager.Instance;
        if (voiceManager == null)
        {
            Debug.LogWarning("[CharacterPomodoroVoice] VoiceManager is unavailable.");
            return;
        }

        if (voiceManager.audioSource == null)
        {
            voiceManager.audioSource =
                voiceManager.GetComponent<AudioSource>();
            if (voiceManager.audioSource == null)
            {
                voiceManager.audioSource =
                    voiceManager.gameObject.AddComponent<AudioSource>();
            }
        }

        voiceManager.StopAudio();
        voiceManager.audioSource.playOnAwake = false;
        voiceManager.audioSource.loop = false;
        voiceManager.audioSource.volume = GetMasterVolume();
        voiceManager.AddToQueue(clip);
    }

    private static float GetMasterVolume()
    {
        if (SettingManager.Instance == null ||
            SettingManager.Instance.settings == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(
            SettingManager.Instance.settings.sound_volumeMaster / 100f);
    }
}
