using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// 背包系统一键配置工具（大幅减少手动编辑器操作）。
///
/// 放置位置：Assets/Editor/InventorySetupTool.cs   （★ 必须在 Editor 文件夹下）
///
/// 菜单项：
///   Tools → Inventory → 1. 生成物品定义(ItemDefinition)
///   Tools → Inventory → 2. 一键搭建背包 UI
///   Tools → Inventory → 3. 检查素材导入设置
///   Tools → Inventory → 4. 在角色附近撒测试物品
/// </summary>
public static class InventorySetupTool
{
    const string ITEM_DIR    = "Assets/Resources/Items";
    const string SPRITE_DIR  = "Assets/Sprites/Inventory";

    // (itemId, 显示名, 图标文件名, maxStack)
    static readonly (string id, string name, string file, int max)[] ITEMS =
    {
        ("gold",   "金币", "item_gold",   999),
        ("wood",   "木材", "item_wood",   99),
        ("stone",  "石头", "item_stone",  99),
        ("apple",  "苹果", "item_apple",  99),
        ("potion", "药水", "item_potion", 20),
        ("seed",   "种子", "item_seed",   999),
        ("key",    "钥匙", "item_key",    1),
        ("gem",    "宝石", "item_gem",    99),
    };

    // ==================================================================
    // 1. 生成物品定义
    // ==================================================================

    [MenuItem("Tools/Inventory/1. 生成物品定义(ItemDefinition)", priority = 10)]
    public static void GenerateItemDefinitions()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Items");

        var created = new List<string>();

        foreach (var (id, name, file, max) in ITEMS)
        {
            string path = $"{ITEM_DIR}/Item_{Capitalize(id)}.asset";

            // 已存在就跳过（保留用户手改过的数值）
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            bool isNew = existing == null;

            var def = isNew
                ? ScriptableObject.CreateInstance<ItemDefinition>()
                : existing;

            def.itemId = id;
            def.displayName = name;
            def.maxStack = max;

            // 找图标
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_DIR}/{file}.png");
            if (icon != null)
            {
                def.icon = icon;
                def.worldSprite = icon;
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(def, path);
                created.Add(path);
                Debug.Log($"[InventorySetup] 新建 {path}（{name} x{max}）");
            }
            else
            {
                EditorUtility.SetDirty(def);
                Debug.Log($"[InventorySetup] 更新 {path}（保留原有数值，只补 icon）");
            }

            if (icon == null)
                Debug.LogWarning($"[InventorySetup] 找不到图标 {SPRITE_DIR}/{file}.png，" +
                                 $"{name} 的 icon 为空。请先把素材导入该目录。");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[InventorySetup] 物品定义完成，新建 {created.Count} 个，" +
                  $"共 {ITEMS.Length} 种。位置：{ITEM_DIR}");
        Debug.Log("[InventorySetup] 下一步：把这些资源拖到 InventoryManager 的 allItems 数组里。");
    }

    // ==================================================================
    // 2. 一键搭建背包 UI
    // ==================================================================

    [MenuItem("Tools/Inventory/2. 一键搭建背包 UI", priority = 20)]
    public static void SetupInventoryUI()
    {
        // ---- 检查必要素材 ----
        var slotNormal = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_DIR}/ui_slot_normal.png");
        var slotHigh   = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_DIR}/ui_slot_highlight.png");
        var panelSpr   = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_DIR}/ui_panel.png");

        if (slotNormal == null)
        {
            EditorUtility.DisplayDialog("缺少素材",
                $"找不到 {SPRITE_DIR}/ui_slot_normal.png\n\n" +
                "请先把背包素材导入该目录。", "知道了");
            return;
        }

        // ---- 查找或创建 Canvas ----
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler),
                                    typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            Debug.Log("[InventorySetup] 新建了 Canvas。");
        }

        // ---- 让 Canvas 支持 EventSystem（拖拽必需）----
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            Debug.Log("[InventorySetup] 新建了 EventSystem（没有它拖拽完全不可用）。");
        }

        // ---- 配置 CanvasScaler ----
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640, 360);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 32f;   // ★ 关键：像素风不对齐的元凶

        // ---- 创建面板 ----
        var panel = CreateUIObject("InventoryPanel", canvas.transform);
        SetAnchoredRect(panel, new Vector2(400, 300), Vector2.zero);

        var panelImg = panel.AddComponent<Image>();
        panelImg.sprite = panelSpr;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = panelSpr != null ? Color.white : new Color(0.16f, 0.13f, 0.11f, 0.95f);

        // ---- 标题 ----
        var title = CreateUIObject("Title", panel.transform);
        SetAnchoredRect(title, new Vector2(200, 24), new Vector2(0, 120));
        var titleText = title.AddComponent<Text>();
        titleText.text = "背包";
        titleText.fontSize = 18;
        titleText.color = new Color(0.94f, 0.86f, 0.7f);
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.raycastTarget = false;

        // ---- 格子容器 ----
        var grid = CreateUIObject("Grid", panel.transform);
        SetAnchoredRect(grid, new Vector2(360, 250), new Vector2(0, -10));

        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(64, 64);
        glg.spacing = new Vector2(8, 8);
        glg.padding = new RectOffset(10, 10, 10, 10);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 5;
        glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
        glg.startAxis = GridLayoutGroup.Axis.Horizontal;
        glg.childAlignment = TextAnchor.UpperCenter;

        // ---- 创建格子预制体 ----
        var slotPrefab = CreateSlotPrefab(slotNormal, slotHigh);

        // ---- 挂 InventoryUI 并接线 ----
        var ui = panel.GetComponent<InventoryUI>();
        if (ui == null) ui = panel.AddComponent<InventoryUI>();

        ui.gridParent = grid.transform;
        ui.slotPrefab = slotPrefab.GetComponent<InventorySlotView>();
        ui.panelRoot = panel;

        EditorUtility.SetDirty(ui);
        EditorSceneMarkDirty();

        Debug.Log("[InventorySetup] 背包 UI 搭建完成！");
        Debug.Log($"  Canvas Scaler：640×360，Reference PPU = 32");
        Debug.Log($"  GridLayoutGroup：Cell 64×64，Spacing 8，固定 5 列（自动排成 5×4）");
        Debug.Log($"  格子预制体：{AssetDatabase.GetAssetPath(slotPrefab)}");
        Debug.Log("  下一步：把 InventoryManager 挂到场景空物体上，并拖入 8 个 ItemDefinition。");

        Selection.activeGameObject = panel;
    }

    static GameObject CreateSlotPrefab(Sprite normal, Sprite highlight)
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Inventory");
        string prefabPath = "Assets/Prefabs/Inventory/InventorySlot.prefab";

        // 根物体
        var root = CreateUIObject("InventorySlot", null);
        SetAnchoredRect(root, new Vector2(64, 64), Vector2.zero);
        var bg = root.AddComponent<Image>();
        bg.sprite = normal;
        bg.type = Image.Type.Simple;

        // 图标
        var icon = CreateUIObject("Icon", root.transform);
        SetAnchoredRect(icon, new Vector2(48, 48), Vector2.zero);
        var iconImg = icon.AddComponent<Image>();
        iconImg.raycastTarget = false;      // 图标不挡射线
        iconImg.preserveAspect = true;
        iconImg.enabled = false;

        // 数量文本
        var count = CreateUIObject("Count", root.transform);
        SetAnchoredRect(count, new Vector2(56, 20), new Vector2(2, -22));
        var countText = count.AddComponent<Text>();
        countText.text = "99";
        countText.fontSize = 14;
        countText.alignment = TextAnchor.LowerRight;
        countText.color = Color.white;
        countText.raycastTarget = false;     // 文本也不挡射线
        countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var outline = count.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.08f, 0.06f, 1f);
        outline.effectDistance = new Vector2(1, -1);

        // 高亮
        var hl = CreateUIObject("Highlight", root.transform);
        SetAnchoredRect(hl, new Vector2(64, 64), Vector2.zero);
        var hlImg = hl.AddComponent<Image>();
        hlImg.sprite = highlight;
        hlImg.raycastTarget = false;
        hl.SetActive(false);

        // 组件
        var view = root.AddComponent<InventorySlotView>();
        view.iconImage = iconImg;
        view.countText = countText;
        view.highlight = hl;

        var drag = root.AddComponent<ItemDragHandler>();

        // 存为预制体
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[InventorySetup] 已生成格子预制体：{prefabPath}");
        return prefab;
    }

    // ==================================================================
    // 3. 检查素材导入设置
    // ==================================================================

    [MenuItem("Tools/Inventory/3. 检查素材导入设置", priority = 30)]
    public static void CheckAssets()
    {
        Debug.Log("========== 背包素材诊断 ==========");

        var files = ITEMS.Select(i => $"item_{i.id}").ToList();
        files.Add("ui_panel");
        files.Add("ui_slot_normal");
        files.Add("ui_slot_highlight");
        files.Add("item_shadow");

        int pass = 0, fail = 0;

        foreach (var name in files)
        {
            string path = $"{SPRITE_DIR}/{name}.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
            {
                Debug.LogError($"✗ 找不到 {path}");
                fail++;
                continue;
            }

            var issues = new List<string>();
            if (importer.textureType != TextureImporterType.Sprite)
                issues.Add($"Texture Type = {importer.textureType}（应为 Sprite）");
            if (importer.filterMode != FilterMode.Point)
                issues.Add($"Filter Mode = {importer.filterMode}（应为 Point）");
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                issues.Add($"Compression = {importer.textureCompression}（应为 None）");
            if (Mathf.Abs(importer.spritePixelsPerUnit - 32f) > 0.01f)
                issues.Add($"Pixels Per Unit = {importer.spritePixelsPerUnit}（应为 32）");

            if (issues.Count == 0) { Debug.Log($"✓ {name}.png 正常"); pass++; }
            else
            {
                Debug.LogWarning($"⚠ {name}.png 有问题：\n    - " + string.Join("\n    - ", issues));
                fail++;
            }
        }

        Debug.Log($"========== 诊断结束：{pass} 正常 / {fail} 有问题 ==========");
        if (fail > 0)
            Debug.Log("修复方法：选中这些图 → Inspector 改成上表的值 → Apply。" +
                      "或框选全部后右键 Copy/Paste Settings 批量应用。");
    }

    // ==================================================================
    // 4. 撒测试物品
    // ==================================================================

    [MenuItem("Tools/Inventory/4. 在角色附近撒测试物品", priority = 40)]
    public static void PlaceTestPickups()
    {
        // 找玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("找不到角色",
                "场景里没有 Tag = Player 的物体。\n\n" +
                "请选中你的角色 → Inspector 顶部 Tag 下拉 → Add Tag → 选 Player。", "知道了");
            return;
        }

        Vector3 center = player.transform.position;
        var created = new List<string>();

        for (int i = 0; i < ITEMS.Length; i++)
        {
            var (id, name, file, max) = ITEMS[i];

            // 摆成一圈
            float angle = i * Mathf.PI * 2f / ITEMS.Length;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * 3f, Mathf.Sin(angle) * 3f, 0f);

            var go = new GameObject($"Pickup_{id}");
            go.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(go, "Create Pickup");

            // Sprite
            var sr = go.AddComponent<SpriteRenderer>();
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_DIR}/{file}.png");
            sr.sprite = spr;
            sr.sortingOrder = 5;

            // Collider
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            // 脚本
            var pickup = go.AddComponent<ItemPickup>();
            pickup.itemId = id;
            pickup.amount = (id == "gold") ? 25 : (id == "wood" ? 15 : 1);

            created.Add(go.name);
        }

        Debug.Log($"[InventorySetup] 已围绕角色撒了 {created.Count} 个测试物品：\n  " +
                  string.Join(", ", created));
        Debug.Log("[InventorySetup] 提示：gold 是 25 个，wood 是 15 个，方便验证数量堆叠显示。");
    }

    // ==================================================================
    // 工具方法
    // ==================================================================

    static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static void SetAnchoredRect(GameObject go, Vector2 size, Vector2 anchoredPos)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }

    static void EditorSceneMarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
#endif
    }
}
