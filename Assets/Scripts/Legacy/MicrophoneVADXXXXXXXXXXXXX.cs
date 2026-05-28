using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class MicrophoneVADXXXXXXXXXXXXX : MonoBehaviour
{
    private const int SampleWindow = 128; // 샘플 윈도우 크기 (샘플의 개수)
    private const float VoiceThreshold = 0.25f; // 음성을 감지하기 위한 최소 레벨 임계값
    private const float VADTimeout = 1.0f; // 음성 활동이 없을 경우 타임아웃(1초)

    private AudioClip microphoneClip; // 마이크로폰으로부터 받아오는 오디오 클립
    private float lastVoiceDetectedTime; // 마지막 음성 감지 시간

    public ReactiveCommand<byte[]> OnMaxLevelChangeCommand = new(); // 최대 레벨 변경 시 실행될 명령

    private void Start()
    {
        // 현재 마이크 장치 확인
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected. Please connect a microphone.");
            enabled = false; // 스크립트 비활성화
            return;
        }

        // 마이크로폰 시작 (10초 길이, 16000Hz)
        microphoneClip = Microphone.Start(null, true, 10, 16000);
        if (microphoneClip == null)
        {
            Debug.LogError("Failed to start microphone.");
            enabled = false; // 스크립트 비활성화
            return;
        }

        lastVoiceDetectedTime = Time.time; // 초기 시간 설정
        Debug.Log($"Microphone started: {Microphone.devices[0]}");
    }

    private void FixedUpdate()
    {
        // 고정된 시간 간격으로 최대 레벨을 체크
        CheckMaxLevel();

        // 타임아웃 시간이 지난 후 음성이 감지되지 않았다면 데이터를 전송
        if (Time.time - lastVoiceDetectedTime > VADTimeout)
        {
            // 마이크로폰 데이터가 존재하면 명령을 실행
            var microphoneData = GetMicrophoneData();
            Debug.Log(microphoneData);
            if (microphoneData != null)
            {
                // SaveWavFile(microphoneData, "output.wav"); // 데이터를 output.wav로 저장
                Debug.Log("Audio data saved to output.wav");
                OnMaxLevelChangeCommand.Execute(microphoneData); // 명령 실행
            }
            lastVoiceDetectedTime = Time.time; // 명령 실행 후 마지막 음성 감지 시간을 갱신
        }
    }

    // 마이크로폰의 최대 레벨을 체크
    private void CheckMaxLevel()
    {
        float maxLevel = 0f; // 현재 샘플에서 발견된 최대 레벨을 추적
        float[] samples = new float[SampleWindow]; // 샘플 데이터 저장 배열
        int startPosition = Microphone.GetPosition(null) - SampleWindow + 1; // 현재 마이크 위치에서 샘플 윈도우 크기만큼 이전 위치로 설정

        // startPosition이 0보다 클 경우에만 샘플을 가져옴
        if (startPosition > 0)
        {
            microphoneClip.GetData(samples, startPosition); // 마이크로폰 데이터 가져오기

            // 샘플에서 최대값을 찾아냄
            foreach (var sample in samples)
            {
                float absSample = Mathf.Abs(sample); // 샘플의 절댓값을 취하여 음성의 세기 측정
                if (absSample > maxLevel)
                {
                    maxLevel = absSample; // 최대값 갱신
                }
            }

            // 최대값이 임계값을 초과하면 음성이 감지된 것으로 간주
            if (maxLevel > VoiceThreshold)
            {
                Debug.Log("inputing"); // 음성이 감지되었음을 출력
                lastVoiceDetectedTime = Time.time; // 음성 감지 시간 갱신
            }
        }
    }

    // 마이크로폰에서 샘플 데이터를 가져와 byte 배열로 변환
    private byte[] GetMicrophoneData()
    {
        // 마이크 위치가 0이면 데이터가 없으므로 null 반환
        if (Microphone.GetPosition(null) <= 0)
        {
            return null;
        }
        else
        {
            // 마이크로폰 샘플 데이터를 가져올 배열 (채널 수와 샘플 수에 맞춰 크기 설정)
            float[] samples = new float[microphoneClip.samples * microphoneClip.channels];
            microphoneClip.GetData(samples, 0); // 샘플 데이터 가져오기

            byte[] audioData = new byte[samples.Length * 2]; // byte 배열로 변환할 배열 (short 형식으로 2배 크기)
            
            // float 데이터를 16비트 PCM 형식으로 변환
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = (short)(samples[i] * short.MaxValue); // float을 short 값으로 변환 (PCM 16비트)
                byte[] sampleBytes = BitConverter.GetBytes(sample); // short를 byte 배열로 변환
                audioData[i * 2] = sampleBytes[0]; // 낮은 바이트
                audioData[i * 2 + 1] = sampleBytes[1]; // 높은 바이트
            }
            return audioData; // 변환된 audioData 반환
        }
    }

    // byte 데이터를 WAV 파일로 저장
    private void SaveWavFile(byte[] audioData, string fileName)
    {
        using (var fileStream = new FileStream(fileName, FileMode.Create))
        {
            // WAV 헤더 작성
            WriteWavHeader(fileStream, audioData.Length);
            // 오디오 데이터 쓰기
            fileStream.Write(audioData, 0, audioData.Length);
        }
    }

    // WAV 파일 헤더를 작성
    private void WriteWavHeader(Stream stream, int dataLength)
    {
        int sampleRate = 16000; // 샘플 레이트
        int channels = 1; // 채널 수 (모노)
        int byteRate = sampleRate * channels * 2; // 바이트 레이트 (샘플 레이트 * 채널 * 16비트)

        using (var writer = new BinaryWriter(stream))
        {
            writer.Write("RIFF".ToCharArray()); // Chunk ID
            writer.Write(36 + dataLength); // Chunk Size
            writer.Write("WAVE".ToCharArray()); // Format
            writer.Write("fmt ".ToCharArray()); // Subchunk1 ID
            writer.Write(16); // Subchunk1 Size
            writer.Write((short)1); // Audio Format (1 = PCM)
            writer.Write((short)channels); // Num Channels
            writer.Write(sampleRate); // Sample Rate
            writer.Write(byteRate); // Byte Rate
            writer.Write((short)(channels * 2)); // Block Align
            writer.Write((short)16); // Bits Per Sample
            writer.Write("data".ToCharArray()); // Subchunk2 ID
            writer.Write(dataLength); // Subchunk2 Size
        }
    }
}
