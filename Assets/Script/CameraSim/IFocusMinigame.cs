using System;

public interface IFocusMinigame
{
    void StartGame(Action onSuccess);
    void StopGame();
}