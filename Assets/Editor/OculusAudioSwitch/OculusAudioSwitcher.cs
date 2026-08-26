using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class OculusAudioSwitcher
{
    private const string OculusScript = @"C:\Tools\SoundVolumeView\set_oculus_default.bat";
    private const string SpeakerScript = @"C:\Tools\SoundVolumeView\set_speaker_default.bat";
    private const string TargetSceneName = "SampleSceneKAI-MP";

    static OculusAudioSwitcher()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (EditorSceneManager.GetActiveScene().name == TargetSceneName)
            {
                RunScript(OculusScript);
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            RunScript(SpeakerScript);
        }
    }

    private static void RunScript(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
    }
}
