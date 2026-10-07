using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所有 ItemDefinition 的注册表，按 id 快速查询。
///
/// 放置位置：Assets/Scripts/Inventory/ItemDatabase.cs
/// 用法：ItemDatabase.Get("wood")
///
/// 说明：这是静态类，注册一次全局可用。
///       注册入口在 InventoryManager.Awake（把 8 个 ItemDefinition 拖进 Inspector）。
/// </summary>
public static class ItemDatabase
{
    static Dictionary<string, ItemDefinition> _map;

    static Dictionary<string, ItemDefinition> Map
    {
        get
        {
            if (_map == null) _map = new Dictionary<string, ItemDefinition>();
            return _map;
        }
    }

    /// <summary>已注册的物品数量（调试用）。</summary>
    public static int Count => Map.Count;

    /// <summary>注册单个物品定义。</summary>
    public static void Register(ItemDefinition def)
    {
        if (def == null) return;
        if (string.IsNullOrEmpty(def.itemId))
        {
            Debug.LogWarning($"[ItemDatabase] 物品 '{def.name}' 的 itemId 为空，已跳过。");
            return;
        }

        if (Map.ContainsKey(def.itemId))
            Debug.LogWarning($"[ItemDatabase] 重复的 itemId：'{def.itemId}'，" +
                             $"用新定义覆盖（{def.name}）。");
        Map[def.itemId] = def;
    }

    /// <summary>批量注册。</summary>
    public static void RegisterAll(IEnumerable<ItemDefinition> defs)
    {
        if (defs == null) return;
        foreach (var d in defs) Register(d);
    }

    /// <summary>
    /// 按 id 取定义；找不到返回 null（调用方必须判空）。
    /// </summary>
    public static ItemDefinition Get(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        return Map.TryGetValue(itemId, out var def) ? def : null;
    }

    /// <summary>是否存在该 id。</summary>
    public static bool Contains(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && Map.ContainsKey(itemId);
    }

    /// <summary>
    /// 清空注册表。
    /// 用途：Editor 下第二次 Play 时避免残留旧引用。
    /// </summary>
    public static void Clear()
    {
        Map.Clear();
    }

    /// <summary>
    /// 进入 Play 模式时自动清空一次，防止 Editor 中反复 Play 造成的数据残留。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnPlay()
    {
        _map = null;
    }
}
