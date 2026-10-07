using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 拾取飘字提示 —— 屏幕上冒出 "木材 +1" 并淡出。
///
/// 放置位置：Assets/Scripts/Inventory/PickupToast.cs
/// 挂载位置：Canvas 下的一个空物体 "ToastLayer" 上
///
/// 新手提示：这是一个可选的锦上添花功能。
///           如果暂时不想做，可以直接删掉这个脚本，
///           ItemPickup 里已做了 null 判断，不会报错。
/// </summary>
public class PickupToast : MonoBehaviour
{
    public static PickupToast Instance { get; private set; }

    [Header("预制体（可选）")]
    [Tooltip("飘字预制体，含一个 Text。留空则运行时用代码动态创建")]
    public GameObject toastPrefab;

    [Header("样式")]
    public Font font;
    public int fontSize = 20;
    public Color normalColor = new Color(1f, 1f, 1f, 1f);
    public Color warnColor = new Color(1f, 0.5f, 0.35f, 1f);

    [Header("动画")]
    public float lifetime = 1.2f;
    public float riseDistance = 50f;

    [Header("上限")]
    [Tooltip("同时最多显示几条，超出的挤掉最旧的")]
    public int maxToasts = 5;

    readonly List<GameObject> active = new List<GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ==================== 对外接口 ====================

    /// <summary>显示 "物品名 +N"。</summary>
    public void ShowItem(string displayName, int count, string suffix = "")
    {
        string text = $"{displayName} +{count}{suffix}";
        Show(text, suffix.Contains("满") ? warnColor : normalColor);
    }

    /// <summary>显示任意提示文字（如"背包已满！"）。</summary>
    public void ShowMessage(string text)
    {
        Show(text, warnColor);
    }

    // ==================== 内部 ====================

    void Show(string text, Color color)
    {
        // 超上限就删最旧的
        while (active.Count >= maxToasts)
        {
            if (active[0] != null) Destroy(active[0]);
            active.RemoveAt(0);
        }

        var go = CreateToast(text, color);
        active.Add(go);
        StartCoroutine(Animate(go));
    }

    GameObject CreateToast(string text, Color color)
    {
        GameObject go;

        if (toastPrefab != null)
        {
            go = Instantiate(toastPrefab, transform);
            var t = go.GetComponentInChildren<Text>();
            if (t != null) { t.text = text; t.color = color; }
        }
        else
        {
            // 动态创建
            go = new GameObject("Toast", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);

            var t = go.GetComponent<Text>();
            t.text = text;
            t.color = color;
            t.fontSize = fontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            else t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 描边，保证在任何背景上都看得清
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.08f, 0.06f, 1f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260f, 36f);
            rt.anchoredPosition = new Vector2(0f, 60f);
        }

        return go;
    }

    IEnumerator Animate(GameObject go)
    {
        if (go == null) yield break;

        var rt = go.GetComponent<RectTransform>();
        var txt = go.GetComponentInChildren<Text>();
        Vector2 startPos = rt != null ? rt.anchoredPosition : Vector2.zero;
        // 新的一条往上顶一点，避免重叠
        startPos.y += active.Count * 8f + Random.Range(-6f, 6f);
        if (rt != null) rt.anchoredPosition = startPos;

        Color baseColor = txt != null ? txt.color : normalColor;
        float t = 0f;
        const float fadeStart = 0.6f;   // 后 40% 时间开始淡出

        while (t < lifetime)
        {
            t += Time.deltaTime;
            float k = t / lifetime;

            if (rt != null)
                rt.anchoredPosition = startPos + new Vector2(0f, riseDistance * k);

            if (txt != null && k > fadeStart)
            {
                float a = Mathf.InverseLerp(1f, fadeStart, k);
                txt.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            }

            yield return null;
        }

        if (go != null) Destroy(go);
        active.Remove(go);
    }
}
