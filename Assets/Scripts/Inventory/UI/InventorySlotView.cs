using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个背包格子的视图 —— 负责显示图标和数量。
///
/// 放置位置：Assets/Scripts/Inventory/UI/InventorySlotView.cs
/// 挂载位置：Slot 预制体的根物体上（与 ItemDragHandler 同物体）
///
/// 只负责"显示"，不处理拖拽（拖拽在 ItemDragHandler 里）。
/// </summary>
public class InventorySlotView : MonoBehaviour
{
    [Header("子物体引用（在预制体里拖好）")]
    [Tooltip("物品图标 Image")]
    public Image iconImage;

    [Tooltip("右下角数量 Text")]
    public Text countText;

    [Tooltip("拖拽悬停时显示的高亮边框（可选）")]
    public GameObject highlight;

    /// <summary>本格在背包中的下标。由 InventoryUI 在生成时注入。</summary>
    public int SlotIndex { get; private set; } = -1;

    /// <summary>背包数据引用。由 InventoryUI 注入。</summary>
    public Inventory Inv { get; private set; }

    /// <summary>格子是否已初始化。</summary>
    public bool IsReady => Inv != null && SlotIndex >= 0;

    /// <summary>当前格子的数据（空格返回 null）。拖拽脚本会读它。</summary>
    public ItemStack Current => Inv != null ? Inv.GetSlot(SlotIndex) : null;

    // ==================== 初始化 ====================

    /// <summary>由 InventoryUI 调用，注入下标和数据引用。</summary>
    public void Init(int index, Inventory inventory)
    {
        SlotIndex = index;
        Inv = inventory;

        // 自动补引用（没在 Inspector 里拖时，按名字找）
        if (iconImage == null)
        {
            var t = transform.Find("Icon");
            if (t != null) iconImage = t.GetComponent<Image>();
        }
        if (countText == null)
        {
            var t = transform.Find("Count");
            if (t != null) countText = t.GetComponent<Text>();
        }
        if (highlight == null)
        {
            var t = transform.Find("Highlight");
            if (t != null) highlight = t.gameObject;
        }

        SetHighlight(false);
        Refresh();
    }

    // ==================== 刷新 ====================

    /// <summary>
    /// 从数据层读取当前格内容，更新图标与数量显示。
    /// 数据变化时由 InventoryUI 调用。
    /// </summary>
    public void Refresh()
    {
        if (!IsReady) return;

        var stack = Inv.GetSlot(SlotIndex);

        if (stack == null || stack.count <= 0)
        {
            // 空格：隐藏图标和数量
            if (iconImage != null)
            {
                iconImage.enabled = false;
                iconImage.sprite = null;
            }
            if (countText != null) countText.enabled = false;
            return;
        }

        // 有物品：显示图标
        var def = stack.Def;
        if (iconImage != null)
        {
            if (def != null && def.icon != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = def.icon;
                iconImage.preserveAspect = true;
            }
            else
            {
                // 找不到定义或图标 —— 显示为空白，但保留可选中状态
                iconImage.enabled = false;
                iconImage.sprite = null;
                if (def == null)
                    Debug.LogWarning($"[InventorySlotView] 格子{SlotIndex}：" +
                                     $"物品 '{stack.itemId}' 未在 ItemDatabase 注册。");
            }
        }

        // 数量：只在 >1 时显示（1 个不用标数字，视觉更干净）
        if (countText != null)
        {
            if (stack.count > 1)
            {
                countText.enabled = true;
                countText.text = stack.count.ToString();
            }
            else
            {
                countText.enabled = false;
            }
        }
    }

    // ==================== 高亮 ====================

    /// <summary>拖拽悬停时切换高亮边框。</summary>
    public void SetHighlight(bool on)
    {
        if (highlight != null) highlight.SetActive(on);
    }

    /// <summary>
    /// 拖拽中把图标设为半透明（表示"正在被拖走"）。
    /// </summary>
    public void SetIconAlpha(float a)
    {
        if (iconImage == null) return;
        var c = iconImage.color;
        c.a = a;
        iconImage.color = c;
    }
}
