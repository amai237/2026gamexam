using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 格子拖拽处理器 —— 实现 UGUI 的拖拽三阶段接口。
///
/// 放置位置：Assets/Scripts/Inventory/UI/ItemDragHandler.cs
/// 挂载位置：Slot 预制体的根物体上（与 InventorySlotView 同物体）
///
/// 依赖：同物体上的 InventorySlotView（提供下标和数据）
///
/// ★ 最容易踩的坑：拖拽用的 ghost 图标必须把 Raycast Target 关掉，
///   否则它会挡在鼠标下面，永远找不到落点，表现为"拖得动但松手没反应"。
/// </summary>
[RequireComponent(typeof(InventorySlotView))]
public class ItemDragHandler : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("拖拽视觉")]
    [Range(0.1f, 1f)]
    [Tooltip("原格子图标在拖拽时的透明度")]
    public float draggingAlpha = 0.35f;

    [Tooltip("跟随鼠标的 ghost 图标大小")]
    public float ghostSize = 64f;

    // ==================== 全局共享状态（一次只能拖一个） ====================

    static GameObject   _ghost;          // 跟随鼠标的临时图标
    static Canvas       _canvas;         // 缓存根 Canvas，用于坐标换算
    static int          _sourceIndex = -1;
    static InventorySlotView _target;    // 当前悬停的目标格子
    static Inventory    _inv;
    static InventorySlotView _sourceView; // 正在被拖的源格子视图（用于恢复透明度）

    InventorySlotView view;

    // ==================== 生命周期 ====================

    void Awake()
    {
        view = GetComponent<InventorySlotView>();
    }

    // ==================== 拖拽三阶段 ====================

    /// <summary>
    /// ① 开始拖拽：创建 ghost，源头半透明。
    /// 注意：UGUI 要鼠标移动超过阈值才触发，单纯点击不触发。
    /// </summary>
    public void OnBeginDrag(PointerEventData e)
    {
        if (view == null || !view.IsReady) return;

        var stack = view.Current;
        if (stack == null || stack.count <= 0) return;   // 空格不能拖

        _inv = view.Inv;
        _sourceIndex = view.SlotIndex;
        _sourceView = view;                              // 记住源格，便于取消时恢复

        // 缓存 Canvas（用于 ghost 坐标换算）
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        CreateGhost(stack);

        // 源头半透明
        view.SetIconAlpha(draggingAlpha);
        ClearHighlight();

        Debug.Log($"[Drag] 开始拖动 格子{_sourceIndex}：{stack}");
    }

    /// <summary>
    /// ② 拖拽中：ghost 跟随鼠标，并高亮落点。
    /// </summary>
    public void OnDrag(PointerEventData e)
    {
        if (_ghost == null) return;

        // ghost 跟随鼠标
        _ghost.transform.position = e.position;

        // 找鼠标下的格子并高亮
        var target = SlotUnderPointer(e);
        if (target != _target)
        {
            ClearHighlight();
            _target = target;
            if (_target != null && _target != view)
                _target.SetHighlight(true);
        }
    }

    /// <summary>
    /// ③ 结束拖拽：判定落点 → 交换 → 清理。
    /// </summary>
    public void OnEndDrag(PointerEventData e)
    {
        CleanupGhost();

        var target = SlotUnderPointer(e);
        ClearHighlight();
        _target = null;

        // 恢复源头透明度
        if (view != null) view.SetIconAlpha(1f);

        // 拖到空白处 或 拖回自己 → 无事发生
        if (target == null || _inv == null || _sourceIndex < 0)
        {
            Debug.Log("[Drag] 落在空白处或无效目标，取消。");
            _sourceIndex = -1;
            _sourceView = null;
            return;
        }

        if (target.SlotIndex == _sourceIndex)
        {
            Debug.Log("[Drag] 拖回原格，无变化。");
            _sourceIndex = -1;
            _sourceView = null;
            return;
        }

        // 执行交换（Inventory 内部会处理：空格移动 / 同种合并 / 纯交换）
        Debug.Log($"[Drag] 交换 格子{_sourceIndex} ↔ 格子{target.SlotIndex}");
        _inv.SwapSlots(_sourceIndex, target.SlotIndex);

        _sourceIndex = -1;
        _sourceView = null;
    }

    // ==================== ghost 管理 ====================

    void CreateGhost(ItemStack stack)
    {
        var def = stack.Def;
        if (def == null || def.icon == null) return;

        _ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
        _ghost.transform.SetParent(_canvas.transform, false);
        _ghost.transform.SetAsLastSibling();       // 显示在最上层

        var img = _ghost.GetComponent<Image>();
        img.sprite = def.icon;
        img.preserveAspect = true;
        // ★★★ 关键：必须关掉射线检测，否则 ghost 会挡住落点判定 ★★★
        img.raycastTarget = false;

        var rt = _ghost.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(ghostSize, ghostSize);

        var c = img.color;
        c.a = 0.85f;
        img.color = c;
    }

    void CleanupGhost()
    {
        if (_ghost != null)
        {
            Destroy(_ghost);
            _ghost = null;
        }
    }

    /// <summary>
    /// 外部强制取消拖拽（关闭背包时调用）。
    /// 会销毁 ghost、恢复源格透明度、清除高亮。
    /// </summary>
    public static void CancelDrag()
    {
        // 销毁 ghost
        if (_ghost != null)
        {
            Object.Destroy(_ghost);
            _ghost = null;
        }

        // 恢复源格透明度
        if (_sourceView != null)
            _sourceView.SetIconAlpha(1f);

        // 清除目标格高亮
        if (_target != null)
            _target.SetHighlight(false);

        _sourceIndex = -1;
        _sourceView = null;
        _target = null;
        _inv = null;
    }

    // ==================== 落点判定 ====================

    /// <summary>
    /// 找出鼠标下方的背包格子。
    /// 用 EventSystem 的 RaycastAll，从所有命中里挑第一个 InventorySlotView。
    /// </summary>
    static InventorySlotView SlotUnderPointer(PointerEventData e)
    {
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(e, results);

        foreach (var r in results)
        {
            if (r.gameObject == null) continue;
            var slot = r.gameObject.GetComponentInParent<InventorySlotView>();
            if (slot != null) return slot;
        }
        return null;
    }

    // ==================== 提示 ====================

    void ClearHighlight()
    {
        if (_target != null)
        {
            _target.SetHighlight(false);
            _target = null;
        }
    }
}
