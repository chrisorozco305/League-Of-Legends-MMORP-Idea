using UnityEngine;

/// <summary>
/// Fire-and-forget positional sound effects.
///
/// Spawns a throwaway AudioSource at a world position and cleans it up when the
/// clip finishes. This exists because the obvious approach - an AudioSource on
/// the thing making the noise - cuts the sound off when that object dies. A
/// fireball lives about 0.4s in flight; its cast sound is 3s. Parent the audio
/// to the projectile and you hear the first eighth of it.
///
/// Static class - never attached to anything.
/// </summary>
public static class Sfx
{
    /// <summary>
    /// Plays <paramref name="clip"/> at a world position, detached from whatever
    /// triggered it. Safe to call with a null clip.
    /// </summary>
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f,
                              float pitchJitter = 0.06f, float spatialBlend = 0.7f,
                              float minDistance = 6f, float maxDistance = 45f)
    {
        if (clip == null) return;

        var go = new GameObject("SFX: " + clip.name);
        go.transform.position = position;

        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;

        // A sample replayed at exactly one pitch reads as a machine gun of the
        // same click. A few percent of variation is enough to break that up.
        src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

        // Not fully 3D: a top-down MOBA camera sits far from the action, so a
        // pure 3D sound is close to inaudible at the edges of the screen.
        // Blending keeps a sense of direction without losing the effect.
        src.spatialBlend = spatialBlend;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;

        src.Play();

        // Pitch changes playback duration - a clip at 1.06x finishes early, at
        // 0.94x it runs long. Destroying on raw clip.length would cut the tail.
        Object.Destroy(go, clip.length / Mathf.Max(0.01f, src.pitch) + 0.1f);
    }
}
