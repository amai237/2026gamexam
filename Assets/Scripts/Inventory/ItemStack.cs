/// <summary>
/// 一个格子里的堆叠数据 —— "什么物品 × 多少个"。
///
/// 放置位置：Assets/Scripts/Inventory/ItemStack.cs
///
/// 说明：
///   - 纯 C# 类，不继承 MonoBehaviour，可以用 new 创建，方便测试。
///   - 用可变类（count 可改）而不是 struct，因为拖拽交换 / 合并数量
///     需要频繁修改 count，引用类型最直观，也避免值拷贝的坑。
/// </summary>
[System.Serializable]
public class ItemStack
{
    /// <summary>指向 ItemDefinition.itemId</summary>
    public string itemId;

    /// <summary>当前数量，永远 >= 1（减到 0 时应由 Inventory 把格子置为 null）</summary>
    public int count;

    public ItemStack(string itemId, int count)
    {
        this.itemId = itemId;
        this.count = count;
    }

    /// <summary>取静态定义。可能为 null（itemId 没注册），调用方需判空。</summary>
    public ItemDefinition Def => ItemDatabase.Get(itemId);

    /// <summary>该物品是否已堆满。</summary>
    public bool IsFull => Def != null && count >= Def.maxStack;

    /// <summary>这个格子还能再放多少个（已满返回 0）。</summary>
    public int Space => Def == null ? 0 : Def.maxStack - count;

    /// <summary>克隆一份（需要独立副本时用，例如分堆）。</summary>
    public ItemStack Clone() => new ItemStack(itemId, count);

    public override string ToString()
    {
        return $"{itemId} x{count}";
    }
}
