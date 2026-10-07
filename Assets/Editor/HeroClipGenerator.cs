using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 一键把 8 张 hero_*.png 生成为 AnimationClip。
/// 放置位置：Assets/Editor/HeroClipGenerator.cs
/// 使用：菜单栏 Tools → Generate Hero Clips
///
/// 前置条件：
///   1. 图片已放在 Assets/Sprites/Hero/ 下
///   2. 每张图已在 Sprite Editor 切成 48x48 的多个 Sprite
///   3. 切片命名形如 hero_walk_down_0 / _1 / _2 ...（Unity Grid 切片默认就是这个格式）
/// </summary>
public static class HeroClipGenerator
{
    const string SPRITE_DIR = "Assets/Sprites/Hero";
    const string OUT_DIR    = "Assets/Animations";
    const int    FPS        = 10;    // 采样帧率，行走/待机 8~12 都行

    [MenuItem("Tools/Generate Hero Clips")]
    public static void Generate()
    {
        var configs = new (string file, string clip)[]
        {
            ("hero_idle_down",  "Hero_Idle_Down"),
            ("hero_idle_up",    "Hero_Idle_Up"),
            ("hero_idle_left",  "Hero_Idle_Left"),
            ("hero_idle_right", "Hero_Idle_Right"),
            ("hero_walk_down",  "Hero_Walk_Down"),
            ("hero_walk_up",    "Hero_Walk_Up"),
            ("hero_walk_left",  "Hero_Walk_Left"),
            ("hero_walk_right", "Hero_Walk_Right"),
        };

        // 确保输出目录存在
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");

        int ok = 0;
        foreach (var (file, clipName) in configs)
        {
            string path = $"{SPRITE_DIR}/{file}.png";

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => ParseIndex(s.name))
                .ToArray();

            if (sprites.Length == 0)
            {
                Debug.LogWarning($"[HeroClipGenerator] 在 {path} 没找到 Sprite。" +
                                 "请确认图片已放入该路径，且 Sprite Mode 为 Multiple 并完成切片。");
                continue;
            }

            // 创建 clip
            var clip = new AnimationClip { frameRate = FPS };

            // 绑定到 SpriteRenderer.m_Sprite 属性
            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",                    // 空 = 当前 GameObject 自身
                propertyName = "m_Sprite"
            };

            var keys = new List<ObjectReferenceKeyframe>();
            for (int i = 0; i < sprites.Length; i++)
            {
                keys.Add(new ObjectReferenceKeyframe
                {
                    time  = i / (float)FPS,
                    value = sprites[i]
                });
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

            // 开启循环
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // 若同名 clip 已存在则覆盖
            string outPath = $"{OUT_DIR}/{clipName}.anim";
            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(clip, outPath);

            Debug.Log($"[HeroClipGenerator] 生成 {clipName}（{sprites.Length} 帧）");
            ok++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[HeroClipGenerator] 完成，共生成 {ok} / 8 个 AnimationClip。");
    }

    /// <summary>从 "hero_walk_down_3" 里解析末尾数字 3，用于排序。</summary>
    static int ParseIndex(string name)
    {
        int i = name.LastIndexOf('_');
        if (i < 0) return 0;
        return int.TryParse(name.Substring(i + 1), out int v) ? v : 0;
    }
}
