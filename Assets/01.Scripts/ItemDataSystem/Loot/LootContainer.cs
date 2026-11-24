using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드에 놓인 적재함. 지금은 <b>부서진 적의 잔해</b>가 이걸 들고 있다(<see cref="WreckLoot"/>). 상자도 같은 것을 쓰면 된다.
///
/// <b>비지 않은 동안은 남는다.</b> 다 꺼내면 자기가 붙은 오브젝트(잔해)째 사라진다. 새 판이 오면 전부 사라진다(<see cref="ClearAll"/>).
///
/// <b>탐색</b> — 처음 연 사람에게는 안의 아이템이 가려져 있다가 하나씩 드러난다(등급이 높을수록 오래).
/// 진행은 <b>보는 사람마다</b> 따로다(멀티에서 남이 뒤진 걸 내가 공짜로 보지 않는다). 상태는 여기(월드 오브젝트)에 둔다 — 싱글턴에 두지 않는다(RULES).
/// 창을 닫았다 다시 열면 이어서 뒤진다. 사람이 칸에 넣은 아이템은 가려지지 않는다.
///
/// 여는 일은 직접 UI를 부르지 않고 <see cref="OnOpenRequestedEvent"/>로 알린다 — 인벤토리 패널이 듣는다.
/// </summary>
public class LootContainer : MonoBehaviour, IInteractable
{
    /// <summary>누가 이 적재함을 열려고 한다. 그 사람의 화면이 듣고 연다.</summary>
    public static event Action<LootContainer, Agent> OnOpenRequestedEvent;

    /// <summary>다 비었다. 곧 사라진다.</summary>
    public event Action OnEmptiedEvent;

    private static readonly List<LootContainer> s_all = new();

    [SerializeField, Min(1)] private int _capacity = 12;

    [Header("탐색")]
    [Tooltip("한 칸이 드러나는 데 걸리는 기본 시간(초).")]
    [SerializeField, Min(0f)] private float _searchBase = 0.5f;

    [Tooltip("등급 하나마다 더 걸리는 시간(초).")]
    [SerializeField, Min(0f)] private float _searchPerGrade = 0.35f;

    [SerializeField] private string _labelKey = "ui.interact.search";
    [SerializeField] private string _labelFallback = "탐색";

    private readonly Dictionary<Agent, SearchState> _search = new();
    private bool _emptied;

    private class SearchState
    {
        public readonly List<int> hidden = new();
        public float progress;
    }

    public Inventory Inventory { get; private set; }

    public static IReadOnlyList<LootContainer> All => s_all;

    public Vector2 InteractPosition => transform.position;
    public string InteractLabel => Localization.Get(_labelKey, _labelFallback);

    private void Awake()
    {
        Inventory = new Inventory(_capacity);
        Inventory.OnSlotChangedEvent += HandleSlotChanged;
    }

    private void OnEnable()
    {
        s_all.Add(this);
        InteractableRegistry.Register(this);
    }

    private void OnDisable()
    {
        s_all.Remove(this);
        InteractableRegistry.Unregister(this);
    }

    /// <summary>내용을 채운다. 다 못 넣은 건 버린다(로그). 비어 있으면 바로 사라진다.</summary>
    public void Fill(IReadOnlyList<ItemStack> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].IsEmpty) continue;

            bool added = items[i].HasInstance
                ? Inventory.AddStack(items[i])
                : Inventory.Add(items[i].itemId, items[i].count) == 0;

            if (!added)
                Debug.LogWarning($"[Loot] {name}에 자리가 없어 {items[i].itemId}를 넣지 못했습니다. 칸 수({_capacity})를 늘릴 것.", this);
        }

        CheckEmpty();
    }

    #region 상호작용

    public bool CanInteract(Agent who) => who != null && !_emptied;

    public void Interact(Agent who) => OnOpenRequestedEvent?.Invoke(this, who);

    #endregion

    #region 탐색

    /// <summary>이 사람이 처음 열었다. 지금 들어 있는 것을 전부 가린다. 이미 시작했으면 그대로 이어간다.</summary>
    public void BeginSearch(Agent viewer)
    {
        if (viewer == null || _search.ContainsKey(viewer)) return;

        SearchState state = new();
        for (int i = 0; i < Inventory.Capacity; i++)
        {
            if (!Inventory[i].IsEmpty) state.hidden.Add(i);
        }

        _search.Add(viewer, state);
    }

    /// <summary>창이 열려 있는 동안 매 프레임. 맨 앞의 가려진 칸부터 하나씩 드러낸다.</summary>
    public void TickSearch(Agent viewer, float deltaTime)
    {
        if (viewer == null || !_search.TryGetValue(viewer, out SearchState state) || state.hidden.Count == 0) return;

        state.progress += deltaTime;

        float needed = SearchTimeOf(state.hidden[0]);
        if (state.progress < needed) return;

        state.progress = 0f;
        state.hidden.RemoveAt(0);
    }

    public bool IsConcealed(Agent viewer, int slot)
        => viewer != null && _search.TryGetValue(viewer, out SearchState state) && state.hidden.Contains(slot);

    /// <summary>지금 뒤지고 있는 칸과 그 진행(0~1). 없으면 -1.</summary>
    public int GetSearching(Agent viewer, out float progress01)
    {
        progress01 = 0f;
        if (viewer == null || !_search.TryGetValue(viewer, out SearchState state) || state.hidden.Count == 0) return -1;

        int slot = state.hidden[0];
        float needed = SearchTimeOf(slot);
        progress01 = needed > 0f ? Mathf.Clamp01(state.progress / needed) : 1f;
        return slot;
    }

    public bool IsFullySearched(Agent viewer)
        => viewer != null && _search.TryGetValue(viewer, out SearchState state) && state.hidden.Count == 0;

    private float SearchTimeOf(int slot)
    {
        ItemDataSO data = Inventory[slot].Resolve();
        int grade = data != null ? (int)data.ItemGrade : 0;
        return _searchBase + _searchPerGrade * grade;
    }

    #endregion

    private void HandleSlotChanged(int slot)
    {
        // 비워진 칸은 더 가릴 게 없다. 누가 칸에 넣은 것은 넣은 사람이 이미 안다 — 가리지 않는다.
        if (Inventory[slot].IsEmpty)
        {
            foreach (SearchState state in _search.Values)
                state.hidden.Remove(slot);
        }

        CheckEmpty();
    }

    private void CheckEmpty()
    {
        if (_emptied) return;

        for (int i = 0; i < Inventory.Capacity; i++)
        {
            if (!Inventory[i].IsEmpty) return;
        }

        _emptied = true;
        OnEmptiedEvent?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>새 판. 남은 적재함(잔해)을 전부 치운다.</summary>
    public static void ClearAll()
    {
        for (int i = s_all.Count - 1; i >= 0; i--)
        {
            if (s_all[i] != null) Destroy(s_all[i].gameObject);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        s_all.Clear();
        OnOpenRequestedEvent = null;
    }
}
