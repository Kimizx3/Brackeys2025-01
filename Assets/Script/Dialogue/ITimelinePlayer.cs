using System;

public interface ITimelinePlayer
{
    void Play(string key, Action onComplete);
}