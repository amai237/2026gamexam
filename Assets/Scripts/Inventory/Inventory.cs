using System;
using UnityEngine;

/// <summary>
/// 背包数据核心 —— 20 个格子的数组 + 全部增删改查 + 堆叠 + 交换。
///
/// 放置位置：Assets/Scripts/Inventory/Inventory.cs
///
/// 设计要点：
///   - 纯 C# 类，用 new Inventory(20) 创建，不需要挂到 GameObject。
///   - 只存数据、只发事件，完全不碰 UI。
///   - 所有写入都必须经过 SetSlot() 这个唯一出口，
///     这样 UI 才能通过 OnSlotChanged 事件正确刷新。
/// </summary>
public class Inventory
{
    // ==================== 事件（数据变化的唯一出口） ====================

    /// <summary>某个格子内容变化，参数 = 格子下标。UI 订阅它刷新单格。</summary>
    public event Action<int> OnSlotChanged;

    /// <summary>整体变化 / 批量操作，UI 订阅它做全量刷新。</summary>
    public event Action OnInventoryChanged;

    // ==================== 数据 ====================

    readonly ItemStack[] slots;

    public int Capacity => slots.Length;

    public Inventory(int capacity = 20)
    {
        if (capacity <= 0) capacity = 1;
        slots = new ItemStack[capacity];
    }

    // ==================== 查询 ====================

    /// <summary>取某格数据；越界或空格返回 null。</summary>
    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return null;
        return slots[index];
    }

    /// <summary>该格是否为空。</summary>
    public bool IsEmpty(int index)
    {
        return GetSlot(index) == null;
    }

    /// <summary>格子下标是否合法。</summary>
    public bool IsValidIndex(int index)
    {
        return index >= 0 && index < slots.Length;
    }

    /// <summary>全背包某种物品的总数量。</summary>
    public int CountOf(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        int total = 0;
        foreach (var s in slots)
            if (s != null && s.itemId == itemId) total += s.count;
        return total;
    }

    /// <summary>数量是否足够（用于扣除前的判断）。</summary>
    public bool Contains(string itemId, int amount = 1)
    {
        return CountOf(itemId) >= amount;
    }

    /// <summary>背包里还有几个空格。</summary>
    public int EmptySlotCount
    {
        get
        {
            int n = 0;
            foreach (var s in slots) if (s == null) n++;
            return n;
        }
    }

    /// <summary>背包是否已满（没有空格）。</summary>
    public bool IsFull => EmptySlotCount == 0;

    // ==================== 增加（带自动堆叠） ====================

    /// <summary>
    /// 尝试放入最多 amount 个物品。
    /// 返回【实际放进去的数量】；放不下时返回值 &lt; amount。
    ///
    /// 两阶段策略：
    ///   阶段1：先往已有的同类堆叠里塞（可跨多个格子）
    ///   阶段2：还有剩余就占用空格
    /// </summary>
    public int Add(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return 0;
        if (ItemDatabase.Get(itemId) == null)
        {
            Debug.LogWarning($"[Inventory] 未知物品 id：'{itemId}'，请确认已在 ItemDatabase 注册。");
            return 0;
        }

        int remaining = amount;
        var dirty = new System.Collections.Generic.List<int>();

        // ---- 阶段 1：填充已有堆叠 ----
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            var s = slots[i];
            if (s == null || s.itemId != itemId || s.IsFull) continue;

            int put = Mathf.Min(remaining, s.Space);
            s.count += put;
            remaining -= put;
            dirty.Add(i);
        }

        // ---- 阶段 2：占用空格 ----
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i] != null) continue;

            int put = Mathf.Min(remaining, GetMaxStack(itemId));
            slots[i] = new ItemStack(itemId, put);
            remaining -= put;
            dirty.Add(i);
        }

        // ---- 统一通知 ----
        foreach (var i in dirty) RaiseSlotChanged(i);
        if (dirty.Count > 0) OnInventoryChanged?.Invoke();

        return amount - remaining;
    }

    // ==================== 减少 ====================

    /// <summary>
    /// 从背包扣除指定数量的物品（从后往前扣，先动空格后面的）。
    /// 数量不足时【不做任何修改】并返回 false（保证原子性）。
    /// </summary>
    public bool Remove(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
        if (!Contains(itemId, amount)) return false;

        int remaining = amount;
        for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
        {
            var s = slots[i];
            if (s == null || s.itemId != itemId) continue;

            int take = Mathf.Min(remaining, s.count);
            s.count -= take;
            remaining -= take;

            if (s.count <= 0) slots[i] = null;
            RaiseSlotChanged(i);
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>从指定格子扣除一定数量。</summary>
    public bool RemoveAt(int index, int amount = 1)
    {
        if (!IsValidIndex(index) || amount <= 0) return false;
        var s = slots[index];
        if (s == null) return false;

        if (amount >= s.count)
        {
            slots[index] = null;
        }
        else
        {
            s.count -= amount;
        }

        RaiseSlotChanged(index);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>清空某一格。</summary>
    public void ClearSlot(int index)
    {
        if (!IsValidIndex(index)) return;
        slots[index] = null;
        RaiseSlotChanged(index);
        OnInventoryChanged?.Invoke();
    }

    /// <summary>清空整个背包。</summary>
    public void ClearAll()
    {
        for (int i = 0; i < slots.Length; i++) slots[i] = null;
        OnInventoryChanged?.Invoke();
    }

    // ==================== 交换 / 移动（拖拽的核心） ====================

    /// <summary>
    /// 交换两个格子。内置"同种物品优先合并"规则。
    ///
    /// 处理顺序：
    ///   情况0  a == b／下标非法        → 不做任何事
    ///   情况1  两边都空                → 不做任何事
    ///   情况2  b 是空格                → a 移到 b
    ///   情况3  a 是空格（防御性）       → b 移到 a
    ///   情况4  同种物品                → 合并数量，装不下再降级为交换
    ///   情况5  其余                    → 纯交换
    /// </summary>
    public void SwapSlots(int a, int b)
    {
        // 情况0
        if (a == b || !IsValidIndex(a) || !IsValidIndex(b)) return;

        var A = slots[a];
        var B = slots[b];

        // 情况1
        if (A == null && B == null) return;

        // 情况2：拖到空格
        if (B == null)
        {
            slots[b] = A;
            slots[a] = null;
            RaiseSlotChanged(a);
            RaiseSlotChanged(b);
            OnInventoryChanged?.Invoke();
            return;
        }

        // 情况3：从空格往有物品的地方（拖拽不会走到，防御性处理）
        if (A == null)
        {
            slots[a] = B;
            slots[b] = null;
            RaiseSlotChanged(a);
            RaiseSlotChanged(b);
            OnInventoryChanged?.Invoke();
            return;
        }

        // 情况4：同种物品 → 尝试合并
        if (A.itemId == B.itemId)
        {
            int move = Mathf.Min(A.count, B.Space);
            if (move > 0)
            {
                B.count += move;
                A.count -= move;
                if (A.count <= 0) slots[a] = null;

                RaiseSlotChanged(a);
                RaiseSlotChanged(b);
                OnInventoryChanged?.Invoke();
                return;   // 合并成功，结束
            }
            // B 已满 → 继续走到情况5 做纯交换
        }

        // 情况5：纯交换
        slots[a] = B;
        slots[b] = A;
        RaiseSlotChanged(a);
        RaiseSlotChanged(b);
        OnInventoryChanged?.Invoke();
    }

    // ==================== 内部 ====================

    int GetMaxStack(string itemId)
    {
        var def = ItemDatabase.Get(itemId);
        return def != null ? Mathf.Max(1, def.maxStack) : 1;
    }

    /// <summary>
    /// 【唯一写入通知出口】。
    /// 任何格子数据变化都必须调用它，否则 UI 不会刷新。
    /// </summary>
    void RaiseSlotChanged(int index)
    {
        OnSlotChanged?.Invoke(index);
    }

    /// <summary>
    /// 调试用：把整个背包内容打印成一行文本。
    /// </summary>
    public string Dump()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[Inventory {Capacity}格] ");
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                sb.Append($"{i}:{slots[i]}  ");
        }
        if (EmptySlotCount == Capacity) sb.Append("(空)");
        return sb.ToString();
    }
}
