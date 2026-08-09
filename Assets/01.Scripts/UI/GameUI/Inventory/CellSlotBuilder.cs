using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 칸 UI를 필요한 개수만큼 확보한다.
///
/// 인스펙터에 미리 등록해두는 방식은 등록을 빠뜨리면 조용히 동작하지 않는다.
/// 이미 만들어 둔 칸이 있으면 그걸 먼저 쓰고, 모자란 만큼만 프리팹으로 찍어낸다.
///
/// 남는 칸은 파괴하지 않고 꺼둔다. 가방을 뺐다 꼈다 하면 개수가 오르내리기 때문이다.
/// </summary>
public class CellSlotBuilder
{
    private readonly CellInventorySlot _prefab;
    private readonly RectTransform _root;
    private readonly List<CellInventorySlot> _cells = new();

    public CellSlotBuilder(CellInventorySlot prefab, RectTransform root, IReadOnlyList<CellInventorySlot> preset)
    {
        _prefab = prefab;
        _root = root;

        Collect(preset);
    }

    public IReadOnlyList<CellInventorySlot> Cells => _cells;
    public int Count => _cells.Count;

    public CellInventorySlot this[int index] => index >= 0 && index < _cells.Count ? _cells[index] : null;

    /// <summary>
    /// 인스펙터에 등록된 것 → 루트 아래 이미 있는 것 순으로 모은다.
    /// 등록 순서를 존중하되, 등록을 빠뜨려도 자식에 있으면 잡힌다.
    /// </summary>
    private void Collect(IReadOnlyList<CellInventorySlot> preset)
    {
        if (preset != null)
        {
            for (int i = 0; i < preset.Count; i++)
            {
                if (preset[i] == null || _cells.Contains(preset[i])) continue;

                _cells.Add(preset[i]);
            }
        }

        if (_root == null) return;

        CellInventorySlot[] children = _root.GetComponentsInChildren<CellInventorySlot>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (_cells.Contains(children[i])) continue;

            _cells.Add(children[i]);
        }
    }

    /// <summary>필요한 개수만큼 확보하고, 남는 칸은 끈다.</summary>
    public bool Ensure(int count)
    {
        count = Mathf.Max(0, count);

        while (_cells.Count < count)
        {
            CellInventorySlot cell = Create();
            if (cell == null) return false; // 프리팹이 없으면 더 만들 수 없다

            _cells.Add(cell);
        }

        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i] == null) continue;

            _cells[i].gameObject.SetActive(i < count);
        }

        return true;
    }

    private CellInventorySlot Create()
    {
        if (_prefab == null || _root == null) return null;

        CellInventorySlot cell = Object.Instantiate(_prefab, _root);
        cell.transform.SetAsLastSibling();

        return cell;
    }
}
