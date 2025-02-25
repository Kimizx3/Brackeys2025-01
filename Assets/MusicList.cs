using UnityEngine;

public class MusicList : MonoBehaviour
{
    public AudioSource[] _audioSources;
    public int _currentIndex = 0;

    void Start()
    {
        // Get all AudioSource components in child GameObjects
        _audioSources = GetComponentsInChildren<AudioSource>();

        if (_audioSources.Length > 0)
        {
            // Start playing the first song
            PlayCurrentSong();
        }
    }

    void Update()
    {
        // Check if the current song has finished playing
        if (!_audioSources[_currentIndex].isPlaying)
        {
            // Move to the next song
            _currentIndex = (_currentIndex + 1) % _audioSources.Length;
            PlayCurrentSong();
        }
    }

    private void PlayCurrentSong()
    {
        // Stop all audio sources
        foreach (var audioSource in _audioSources)
        {
            audioSource.Stop();
        }

        // Play the current song
        _audioSources[_currentIndex].Play();
    }
}
