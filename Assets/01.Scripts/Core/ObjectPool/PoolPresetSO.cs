using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 특정 씬/게임 세션에서 실제로 쓸 PoolType 묶음.
/// PoolManager는 여기에 등록된 것만 미리 만들어 둔다.
/// (예: Common + Stage1 처럼 여러 개를 조합해서 쓴다)
/// </summary>
[CreateAssetMenu(fileName = "PoolPresetSO", menuName = "SO/Core/PoolPreset")]
public class PoolPresetSO : ScriptableObject
{
    [SerializeField, TextArea] private string _description;
    [SerializeField] private List<PoolType> _types = new();

    public string Description => _description;
    public IReadOnlyList<PoolType> Types => _types;
}
