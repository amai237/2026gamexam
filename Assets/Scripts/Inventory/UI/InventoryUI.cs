using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包 UI 总控 —— 自动生成 20 个格子、订阅数据事件、Tab 开关面板。
///
/// 放置位置：Assets/Scripts/Inventory/UI/InventoryUI.cs
/// 挂载位置：Canvas 下的 "InventoryPanel" 上
///
/// 依赖：
///   - gridParent：挂着 GridLayoutGroup 的容器（格子会生成到它下面）
///   - slotPrefab：Slot 预制体
///   - panelRoot：整个面板根物体（用于显示/隐藏）
///
/// 优点：不用手动摆 20 个格子、不用设 20 个锚点，BuildSlots() 全自动。
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("挂着 GridLayoutGroup 的容器，格子会生成到它下面")]
    public Transform gridParent;

    [Tooltip("格子预制体（含 InventorySlotView + ItemDragHandler）")]
    public InventorySlotView slotPrefab;

    [Tooltip("整个面板的根物体，用于显示/隐藏。留空则用自己")]
    public GameObject panelRoot;

    [Header("开关")]
    public KeyCode toggleKey = KeyCode.B;

    [Tooltip("游戏开始时是否默认隐藏背包")]
    public bool startHidden = true;

    [Header("调试")]
    public bool debugLog = false;

    Inventory inv;
    readonly List<InventorySlotView> views = new List<InventorySlotView>();
    bool isOpen;

    // ==================== 生命周期 ====================

    void Start()
    {
        if (panelRoot == null) panelRoot = gameObject;

        // 1. 等 InventoryManager 就绪（它在 Awake 初始化，这里 Start 一定已就绪）
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[InventoryUI] 场景里找不到 InventoryManager！" +
                           "请创建空物体挂上 InventoryManager 组件。UI 无法工作。");
            return;
        }
        inv = InventoryManager.Instance.Inventory;

        // 2. 生成格子
        BuildSlots();

        // 3. 订阅数据变化事件（必须在生成格子之后）
        inv.OnSlotChanged += HandleSlotChanged;
        inv.OnInventoryChanged += RefreshAll;

        // 4. 首次全量刷新
        RefreshAll();

        // 5. 初始显隐
        isOpen = !startHidden;
        panelRoot.SetActive(isOpen);

        Log($"初始化完成，共 {views.Count} 个格子，背包容量 {inv.Capacity}。");
    }

    void OnDestroy()
    {
        // 退订，避免悬空引用
        if (inv != null)
        {
            inv.OnSlotChanged -= HandleSlotChanged;
            inv.OnInventoryChanged -= RefreshAll;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    // ==================== 构建格子 ====================

    /// <summary>
    /// 按背包容量自动生成格子。新手不用手动摆 20 次。
    /// </summary>
    void BuildSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogError("[InventoryUI] slotPrefab 为空！请在 Inspector 里给 slotPrefab 赋值。");
            return;
        }
        if (gridParent == null)
        {
            Debug.LogError("[InventoryUI] gridParent 为空！请把挂着 GridLayoutGroup 的容器拖进来。");
            return;
        }

        // 清掉旧格子（防止重复生成）
        foreach (var v in views)
            if (v != null) Destroy(v.gameObject);
        views.Clear();

        for (int i = 0; i < inv.Capacity; i++)
        {
            var view = Instantiate(slotPrefab, gridParent);
            view.name = $"Slot_{i:00}";
            view.Init(i, inv);
            views.Add(view);
        }
    }

    // ==================== 事件响应 ====================

    /// <summary>某个格子数据变化 → 只刷新那一格。</summary>
    void HandleSlotChanged(int index)
    {
        if (index < 0 || index >= views.Count) return;
        var v = views[index];
        if (v != null) v.Refresh();
    }

    /// <summary>全量刷新。</summary>
    public void RefreshAll()
    {
        foreach (var v in views)
            if (v != null) v.Refresh();
    }

    // ==================== 开关面板 ====================

    public void Toggle()
    {
        isOpen = !isOpen;
        panelRoot.SetActive(isOpen);

        // 关闭时取消正在进行的拖拽，避免 ghost 残留
        if (!isOpen)
            ItemDragHandler.CancelDrag();

        Log(isOpen ? "打开背包" : "关闭背包");
    }

    public void Open()
    {
        isOpen = true;
        panelRoot.SetActive(true);
    }

    public void Close()
    {
        isOpen = false;
        panelRoot.SetActive(false);
        ItemDragHandler.CancelDrag();
    }

    public bool IsOpen => isOpen;

    void Log(string msg)
    {
        if (debugLog) Debug.Log($"[InventoryUI] {msg}");
    }
}
