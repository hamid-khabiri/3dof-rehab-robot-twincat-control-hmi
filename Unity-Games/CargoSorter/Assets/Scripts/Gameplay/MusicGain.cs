using UnityEngine;

// Amplifies the AudioSource on this GameObject beyond 1.0 (AudioSource.volume cannot go higher).
// Added at runtime by GameAudio on the music-only child object.
[RequireComponent(typeof(AudioSource))]
public sealed class MusicGain : MonoBehaviour
{
    private volatile float gain = 1f;

    public float Gain { get => gain; set => gain = Mathf.Max(0f, value); }

    // Runs on the audio thread.
    private void OnAudioFilterRead(float[] data, int channels)
    {
        float currentGain = gain;
        if (Mathf.Approximately(currentGain, 1f)) return;
        for (int index = 0; index < data.Length; index++)
            data[index] = Mathf.Clamp(data[index] * currentGain, -1f, 1f);
    }
}
