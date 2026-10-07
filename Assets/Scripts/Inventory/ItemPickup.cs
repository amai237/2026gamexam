using System.Collections;
using UnityEngine;

/// <summary>
/// 地面上的可拾取物品 —— 角色碰到就自动捡起。
///
/// 放置位置：Assets/Scripts/Inventory/ItemPickup.cs
/// 挂载位置：地面物品物体上（需要一个 Collider2D，勾上 Is Trigger）
///
/// ★ 触发前提（三个都要满足）：
///   1. 本物体的 Collider2D 勾了 Is Trigger
///   2. 角色身上有 Rigidbody2D（你的 Hero 已经满足）
///   3. 角色打了 Tag = Player
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour
{
    [Header("物品")]
    [Tooltip("物品 id，必须与某个 ItemDefinition.itemId 一致")]
    public string itemId = "wood";

    [Tooltip("这一堆有几个")]
    [Min(1)] public int amount = 1;

    [Header("外观")]
    [Tooltip("显示用的 Sprite。留空则自动从 ItemDefinition.worldSprite 取")]
    public SpriteRenderer spriteRenderer;

    [Tooltip("玩家靠近时的上下漂浮，0 = 不飘")]
    public float bobAmplitude = 0.06f;

    public float bobSpeed = 2f;

    [Tooltip("自动旋转（可选）")]
    public bool spin = false;

    public float spinSpeed = 40f;

    [Header("拾取表现")]
    [Tooltip("拾取成功后的消失动画时长，0 = 立即消失")]
    public float vanishDuration = 0.15f;

    [Tooltip("拾取音效（可选）")]
    public AudioClip pickupSfx;

    [Header("调试")]
    public bool debugLog = false;

    Vector3 basePos;
    bool picked;

    // ==================== 生命周期 ====================

    void Start()
    {
        basePos = transform.position;

        // 自动补 SpriteRenderer 引用
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // 从物品定义自动取外观
        if (spriteRenderer != null && spriteRenderer.sprite == null)
        {
            var def = ItemDatabase.Get(itemId);
            if (def != null && def.WorldSprite != null)
                spriteRenderer.sprite = def.WorldSprite;
            else if (def == null)
                Debug.LogWarning($"[ItemPickup] 物品 '{itemId}' 未注册，" +
                                 "地上这个物品会显示不出来。检查 InventoryManager 的 allItems。");
        }

        // 确保 Collider 是 Trigger
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[ItemPickup] {name} 的 Collider2D 没勾 Is Trigger，" +
                             "拾取不会触发。已在运行时自动修正。");
            col.isTrigger = true;
        }
    }

    void Update()
    {
        if (picked) return;

        // 上下漂浮
        if (bobAmplitude > 0f)
        {
            float y = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = basePos + new Vector3(0f, y, 0f);
        }

        // 自转
        if (spin)
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }

    // ==================== 拾取 ====================

    /// <summary>
    /// 角色进入触发器 → 尝试拾取。
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        if (picked) return;
        if (!other.CompareTag("Player"))
        {
            if (debugLog)
                Debug.Log($"[ItemPickup] 碰到 {other.name}，但它不是 Player Tag，忽略。");
            return;
        }

        TryPickup();
    }

    /// <summary>
    /// 角色停留在范围内时也尝试（处理"当时背包满了，后来腾出空间"的情况）。
    /// </summary>
    void OnTriggerStay2D(Collider2D other)
    {
        if (picked) return;
        if (!other.CompareTag("Player")) return;
        if (CanPickupNow()) TryPickup();
    }

    /// <summary>背包现在放得下吗。</summary>
    bool CanPickupNow()
    {
        if (InventoryManager.Instance == null) return false;
        var inv = InventoryManager.Instance.Inventory;

        // 有空位 → 能放
        if (!inv.IsFull) return true;

        // 没空位，但已有同种未满堆叠 → 也能放
        for (int i = 0; i < inv.Capacity; i++)
        {
            var s = inv.GetSlot(i);
            if (s != null && s.itemId == itemId && !s.IsFull) return true;
        }
        return false;
    }

    /// <summary>
    /// 核心拾取逻辑。
    /// </summary>
    void TryPickup()
    {
        // remain = 没能放进去的数量
        int remain = InventoryManager.GrantItem(itemId, amount);
        int taken = amount - remain;

        if (taken <= 0)
        {
            // 一个都没放进去 → 背包满
            if (debugLog) Debug.Log($"[ItemPickup] 背包已满，无法拾取 {itemId}。");
            if (PickupToast.Instance != null)
                PickupToast.Instance.ShowMessage("背包已满！");
            return;   // ★ 物品留在原地，不销毁
        }

        // 播放音效
        if (pickupSfx != null)
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position);

        // 显示飘字
        if (PickupToast.Instance != null)
        {
            var def = ItemDatabase.Get(itemId);
            string name = def != null ? def.displayName : itemId;
            string suffix = remain > 0 ? $"（背包满，剩 {remain}）" : "";
            PickupToast.Instance.ShowItem(name, taken, suffix);
        }

        if (remain > 0)
        {
            // 部分拾取：地上还剩一些
            amount = remain;
            if (debugLog)
                Debug.Log($"[ItemPickup] 部分拾取 {taken} 个，地上还剩 {remain} 个。");

            // 刷新地面显示的数量（如果有文字子物体可扩展）
            return;   // 不销毁
        }

        // 全部拾取 → 消失
        picked = true;
        StartCoroutine(VanishAndDestroy());
    }

    /// <summary>
    /// 消失动画：缩小 + 变淡，然后销毁。
    /// </summary>
    IEnumerator VanishAndDestroy()
    {
        // 关掉碰撞，避免消失过程中再次触发
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (vanishDuration > 0f && spriteRenderer != null)
        {
            Vector3 s0 = transform.localScale;
            Color c0 = spriteRenderer.color;
            float t = 0f;

            while (t < vanishDuration)
            {
                t += Time.deltaTime;
                float k = t / vanishDuration;
                transform.localScale = Vector3.Lerp(s0, s0 * 0.2f, k);
                spriteRenderer.color = new Color(c0.r, c0.g, c0.b, Mathf.Lerp(c0.a, 0f, k));
                yield return null;
            }
        }

        Destroy(gameObject);
    }
}
