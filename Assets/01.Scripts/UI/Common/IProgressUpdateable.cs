using UnityEngine;

public interface IProgressUpdateable
{
    public void UpdateProgress(int current, int max);
    public void UpdateProgress(float progressRatio);
}