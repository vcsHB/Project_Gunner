using UnityEngine;

public interface ICastEffector
{
    public void Initialize();
    public void Cast(Collider2D target);
}