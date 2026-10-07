using UnityEngine;

/// <summary>
/// 背包的全局入口（单例）。挂在场景里的空物体 "GameManager" 上。
///
/// 放置位置：Assets/Scripts/Inventory/InventoryManager.cs
///
/// 依赖：Inspector 里把 8 个 ItemDefinition 资源拖进 allItems 数组，
///       它们会在 Awake 时注册到 ItemDatabase。
///
/// 用法：
///   InventoryManager.Instance.Inventory          // 拿背包数据
///   InventoryManager.GrantItem("wood", 3)        // 便捷静态方法，加物品
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("物品定义（把 8 个 ItemDefinition 资源拖进来）")]
    public ItemDefinition[] allItems;

    [Header("背包容量")]
    [Min(1)] public int capacity = 20;

    [Header("调试")]
    [Tooltip("勾上后 Console 打印每次增删和背包内容")]
    public bool debugLog = false;

    [Tooltip("勾上后在开局赠送一些测试物品（仅用于验证 UI，正式版请取消）")]
    public bool giveTestItems = false;

    /// <summary>背包数据。</summary>
    public Inventory Inventory { get; private set; }

    // ==================== 生命周期 ====================

    void Awake()
    {
        // 单例防重复
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 1. 先注册物品定义（必须在创建 Inventory 之前）
        ItemDatabase.Clear();
        if (allItems != null && allItems.Length > 0)
        {
            ItemDatabase.RegisterAll(allItems);
            Log($"已注册 {ItemDatabase.Count} 种物品定义。");
        }
        else
        {
            Debug.LogWarning("[InventoryManager] allItems 为空！" +
                             "请在 Inspector 里把 ItemDefinition 资源拖进来，否则物品无法显示。");
        }

        // 2. 创建背包
        Inventory = new Inventory(capacity);
        Log($"背包已创建，容量 {capacity} 格。");

        // 3. 测试物品
        if (giveTestItems) GrantTestItems();
    }

    // ==================== 对外接口 ====================

    /// <summary>
    /// 便捷静态方法：往背包里加物品。
    /// 返回【没能放进去的数量】（0 = 全部放成功）。
    /// 背包不存在时返回原数量并警告。
    /// </summary>
    public static int GrantItem(string itemId, int amount = 1)
    {
        if (Instance == null || Instance.Inventory == null)
        {
            Debug.LogWarning("[InventoryManager] 场景里没有 InventoryManager，物品无法拾取。" +
                             "请创建空物体挂上该组件。");
            return amount;
        }

        var inv = Instance.Inventory;
        int before = inv.CountOf(itemId);
        int added = inv.Add(itemId, amount);
        int remain = amount - added;

        if (Instance.debugLog)
        {
            Instance.Log($"拾取 {itemId} x{amount} → 实际放入 {added}，" +
                         $"剩余 {remain}。总数 {before}→{before + added}。");
            Instance.Log(inv.Dump());
        }

        return remain;
    }

    /// <summary>便捷静态方法：扣除物品。</summary>
    public static bool ConsumeItem(string itemId, int amount = 1)
    {
        if (Instance == null || Instance.Inventory == null) return false;
        return Instance.Inventory.Remove(itemId, amount);
    }

    // ==================== 内部 ====================

    void GrantTestItems()
    {
        // 演示堆叠：wood 30 + 80 = 110，会占 2 格（99 + 11）
        Inventory.Add("wood", 30);
        Inventory.Add("wood", 80);
        Inventory.Add("gold", 25);
        Inventory.Add("stone", 12);
        Inventory.Add("apple", 5);

        Log("已发放测试物品：");
        Log(Inventory.Dump());
    }

    void Log(string msg)
    {
        if (debugLog) Debug.Log($"[Inventory] {msg}");
    }

    // ==================== 调试菜单（Inspector 右键组件） ====================

    [ContextMenu("打印背包内容")]
    void CMD_Dump()
    {
        if (Inventory == null) { Debug.Log("背包尚未初始化。"); return; }
        Debug.Log(Inventory.Dump());
    }

    [ContextMenu("测试：加 10 个木材")]
    void CMD_AddWood()
    {
        int remain = GrantItem("wood", 10);
        Debug.Log($"加了 10 个木材，剩余未放入 {remain} 个。");
    }
}
