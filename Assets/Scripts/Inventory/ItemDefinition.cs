using UnityEngine;

/// <summary>
/// 一种物品的静态定义（不会随游戏变化）。
///
/// 放置位置：Assets/Scripts/Inventory/ItemDefinition.cs
/// 创建方式：Project 窗口右键 → Create → Inventory → Item Definition
///
/// 说明：这是 ScriptableObject 资源，改数值不用改代码。
///       每种物品建一个资源文件，例如 Assets/Resources/Items/Item_Wood.asset
/// </summary>
[CreateAssetMenu(fileName = "Item_", menuName = "Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("标识")]
    [Tooltip("唯一 id，代码里用它匹配，全小写无空格，例如 wood / gold。建好后别改")]
    public string itemId;

    [Tooltip("显示名称，例如 木材")]
    public string displayName;

    [Header("外观")]
    [Tooltip("背包格子里显示的图标（32×32）")]
    public Sprite icon;

    [Tooltip("掉落在地上的外观。留空则自动用 icon")]
    public Sprite worldSprite;

    [Header("堆叠")]
    [Min(1)]
    [Tooltip("单个格子最多堆叠多少个")]
    public int maxStack = 99;

    [Header("可选")]
    [TextArea(2, 4)]
    public string description;

    /// <summary>
    /// 取得地面显示用的 Sprite（worldSprite 为空时回退到 icon）。
    /// </summary>
    public Sprite WorldSprite => worldSprite != null ? worldSprite : icon;

    /// <summary>
    /// 编辑器里改完值时自动纠正：itemId 去空格转小写。
    /// </summary>
    void OnValidate()
    {
        if (!string.IsNullOrEmpty(itemId))
            itemId = itemId.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(itemId))
            displayName = itemId;

        if (maxStack < 1) maxStack = 1;
    }
}
