using UnityEngine;

/// <summary>
/// 다른 씬으로 넘어가는 문. 주둔지의 <b>출격구</b>가 쓴다 — 탄 기체를 그대로 들고 필드로.
/// E로 쓴다(<see cref="IInteractable"/>). 씬을 바꾸는 일은 <see cref="SceneFlow"/>가 한다.
///
/// 필드에서 돌아오는 길은 탈출 지점(버티기)이다. 이 문의 <see cref="Destination.Garrison"/>은 그 전까지의 <b>임시 귀환</b>이다.
/// </summary>
public class SceneGate : MonoBehaviour, IInteractable
{
    public enum Destination
    {
        Field,
        Garrison,
    }

    [SerializeField] private Destination _destination = Destination.Field;

    [SerializeField] private string _labelKey = "ui.interact.sortie";
    [SerializeField] private string _labelFallback = "출격";

    public Vector2 InteractPosition => transform.position;

    public string InteractLabel => Localization.Get(_labelKey, _labelFallback);

    private void OnEnable() => InteractableRegistry.Register(this);

    private void OnDisable() => InteractableRegistry.Unregister(this);

    // 기체가 있어야 나간다. 내 화면의 기체만 — 멀티에서 남이 문을 열어 내 씬이 바뀌면 안 된다(그건 파티 출격 때 정한다).
    public bool CanInteract(Agent who)
        => !SceneFlow.IsLoading && who is Player mecha && mecha.Pilot == PlayerPilot.Local;

    public void Interact(Agent who)
    {
        if (!CanInteract(who)) return;

        if (_destination == Destination.Field) SceneFlow.Sortie();
        else SceneFlow.ReturnToGarrison(withMecha: true);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = _destination == Destination.Field ? new Color(1f, 0.6f, 0.2f, 0.6f) : new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
#endif
}
