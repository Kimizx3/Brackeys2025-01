using System;
using UnityEngine;

public enum FadeCommand { Show, Hide }

[Serializable]
public class FadeAction
{
    //把Fadable挂在要淡入/淡出的对象或其根上
    public Fadable target;
    public FadeCommand command = FadeCommand.Show;
    //淡入/淡出时长（秒）
    public float duration = 0.2f;

    public void Execute()
    {
        if (!target) return;
        if (command == FadeCommand.Show) target.Show(duration);
        else target.Hide(duration);
    }
}