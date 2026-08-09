using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용 아이템 지급/드랍. 씬에 임시로 붙여 쓴다.
/// </summary>
public class DebugItemSpawner : MonoBehaviour
{
#pragma warning disable CS0649 // 인스펙터에서만 채워진다
    [Serializable]
    private struct Entry
    {
        public Key key;
        public ItemDataSO item;
        [Min(1)] public int count;

        [Tooltip("켜면 바닥에 떨구고, 끄면 인벤토리에 바로 넣는다.")]
        public bool dropToWorld;
    }
#pragma warning restore CS0649

    [SerializeField] private List<Entry> _entries = new();
    [SerializeField] private bool _enableHotkeys = true;

    private Player _player;

    private void Start() => _player = FindAnyObjectByType<Player>();

    private void Update()
    {
        if (!_enableHotkeys || Keyboard.current == null) return;

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry entry = _entries[i];
            if (entry.key == Key.None) continue;

            if (Keyboard.current[entry.key].wasPressedThisFrame)
                Execute(entry);
        }
    }

    [ContextMenu("Give All")]
    private void GiveAll()
    {
        for (int i = 0; i < _entries.Count; i++)
            Execute(_entries[i]);
    }

    private void Execute(Entry entry)
    {
        if (entry.item == null) return;

        if (!entry.item.IsRegistered)
        {
            Debug.LogError($"[DebugItem] {entry.item.name}에 Id가 없습니다. ItemDatabase에서 스캔하세요.", entry.item);
            return;
        }

        if (_player == null)
        {
            Debug.LogWarning("[DebugItem] 씬에서 Player를 찾지 못했습니다.", this);
            return;
        }

        // 개체 상태가 필요한 아이템은 한 번에 하나씩 만들어야 인스턴스가 각각 생긴다.
        int times = entry.item.RequiresInstance ? Mathf.Max(1, entry.count) : 1;
        int perTime = entry.item.RequiresInstance ? 1 : Mathf.Max(1, entry.count);

        for (int i = 0; i < times; i++)
        {
            ItemStack stack = ItemInstanceRegistry.CreateStack(entry.item, perTime);
            if (stack.IsEmpty) continue;

            if (entry.dropToWorld)
                WorldItemSpawner.Spawn(stack, _player.transform.position);
            else
                GiveToInventory(stack);
        }
    }

    private void GiveToInventory(ItemStack stack)
    {
        InventoryController controller = _player.GetCompo<InventoryController>();
        if (controller == null)
        {
            Debug.LogWarning("[DebugItem] 플레이어에 InventoryController가 없습니다.", this);
            return;
        }

        bool placed = stack.HasInstance
            ? controller.Inventory.AddStack(stack)
            : controller.Inventory.Add(stack.itemId, stack.count) == 0;

        if (!placed)
            Debug.Log("[DebugItem] 인벤토리가 가득 차서 전부 넣지 못했습니다.", this);
    }
}
