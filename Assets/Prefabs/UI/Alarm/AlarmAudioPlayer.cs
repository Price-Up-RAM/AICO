using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AlarmClipEntry
{
    public string id;
    public AudioClip clip;
}

public class AlarmAudioPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip defaultClip;
    [SerializeField] private bool loopAlarmClip = true;
    [SerializeField] private List<AlarmClipEntry> clipEntries = new List<AlarmClipEntry>();

    private AudioSource audioSource;
    private string currentAlarmId;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }

    // Play the alarm AudioClip.
    public void PlayAlarmClip(AlarmItem alarm)
    {
        if (alarm == null)
        {
            return;
        }

        AudioClip clip = ResolveClip(alarm.audioClipId);
        if (clip == null)
        {
            Debug.LogWarning("[AlarmAudioPlayer] Alarm clip not found: " + alarm.audioClipId);
            return;
        }

        StopAlarmClip();
        currentAlarmId = alarm.id;
        audioSource.clip = clip;
        audioSource.loop = loopAlarmClip;
        audioSource.Play();
    }

    // Stop the current alarm AudioClip.
    public void StopAlarmClip()
    {
        if (audioSource == null)
        {
            return;
        }

        if (audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        currentAlarmId = string.Empty;
    }

    // Stop all alarm AudioClips.
    public void StopAllAlarmClips()
    {
        StopAlarmClip();
    }

    // Return the currently playing alarm id.
    public string GetCurrentAlarmId()
    {
        return currentAlarmId;
    }

    // Resolve an AudioClip by id or Resources path.
    private AudioClip ResolveClip(string audioClipId)
    {
        if (!string.IsNullOrEmpty(audioClipId))
        {
            for (int i = 0; i < clipEntries.Count; i++)
            {
                AlarmClipEntry entry = clipEntries[i];
                if (entry != null && entry.id == audioClipId && entry.clip != null)
                {
                    return entry.clip;
                }
            }

            AudioClip resourceClip = Resources.Load<AudioClip>(audioClipId);
            if (resourceClip != null)
            {
                return resourceClip;
            }
        }

        return defaultClip;
    }
}
