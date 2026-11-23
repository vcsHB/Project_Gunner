using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 걸려 있는 상태이상을 셀로 나열한다. 셀은 만들어두고 재사용한다.
/// </summary>
public class ConditionDisplayGroup : MonoBehaviour
{
    [SerializeField] private RectTransform _cellHolder;
    [SerializeField] private CellConditionDisplay _cellPrefab;
    [SerializeField] private ConditionVisualTableSO _visualTable;

    [Tooltip("미리 만들어 둘 셀 개수. 모자라면 그때 더 만든다.")]
    [SerializeField, Min(0)] private int _prewarmCount = 4;

    private readonly Dictionary<ConditionType, CellConditionDisplay> _active = new();
    private readonly Stack<CellConditionDisplay> _idle = new();

    private AgentConditionEffector _effector;

    private void Awake()
    {
        if (_cellHolder == null)
            _cellHolder = transform as RectTransform;

        for (int i = 0; i < _prewarmCount; i++)
            _idle.Push(CreateCell());
    }

    public void Bind(AgentConditionEffector effector)
    {
        if (_effector == effector) return;

        Unbind();
        _effector = effector;

        if (_effector == null) return;

        _effector.OnConditionAddedEvent += HandleAdded;
        _effector.OnConditionRemovedEvent += HandleRemoved;

        // 붙는 시점에 이미 걸려 있는 것들이 있을 수 있다.
        IReadOnlyList<ConditionBase> conditions = _effector.Conditions;
        for (int i = 0; i < conditions.Count; i++)
            HandleAdded(conditions[i]);
    }

    public void Unbind()
    {
        if (_effector != null)
        {
            _effector.OnConditionAddedEvent -= HandleAdded;
            _effector.OnConditionRemovedEvent -= HandleRemoved;
            _effector = null;
        }

        foreach (CellConditionDisplay cell in _active.Values)
            Release(cell);

        _active.Clear();
    }

    private void OnDestroy() => Unbind();

    private void Update()
    {
        foreach (CellConditionDisplay cell in _active.Values)
            cell.Refresh();
    }

    private void HandleAdded(ConditionBase condition)
    {
        // 같은 타입이 갱신된 경우 셀을 새로 만들지 않는다.
        if (_active.TryGetValue(condition.Type, out CellConditionDisplay exist))
        {
            exist.SetData(condition, _visualTable);
            return;
        }

        CellConditionDisplay cell = _idle.Count > 0 ? _idle.Pop() : CreateCell();

        cell.gameObject.SetActive(true);
        cell.transform.SetAsLastSibling();
        cell.SetData(condition, _visualTable);

        _active.Add(condition.Type, cell);
    }

    private void HandleRemoved(ConditionBase condition)
    {
        if (!_active.Remove(condition.Type, out CellConditionDisplay cell)) return;

        Release(cell);
    }

    private CellConditionDisplay CreateCell()
    {
        if (_cellPrefab == null)
        {
            Debug.LogError("[ConditionDisplay] 셀 프리팹이 지정되지 않았습니다.", this);
            return null;
        }

        CellConditionDisplay cell = Instantiate(_cellPrefab, _cellHolder);
        cell.gameObject.SetActive(false);
        return cell;
    }

    private void Release(CellConditionDisplay cell)
    {
        if (cell == null) return;

        cell.Clear();
        cell.gameObject.SetActive(false);
        _idle.Push(cell);
    }
}
