using UnityEngine;

/// <summary>
/// 俯视角 2D 角色控制器。
/// 放置位置：Assets/Scripts/HeroController.cs
/// 依赖组件：Rigidbody2D、Animator（需挂 Character.controller）
///
/// Animator 需要以下参数：
///   Speed (float)  —— 0 = 待机, >0 = 行走
///   DirX  (float)  —— -1 / 0 / 1
///   DirY  (float)  —— -1 / 0 / 1
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class HeroController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 4f;

    [Header("调试")]
    [Tooltip("勾选后在 Console 打印当前方向，排查动画问题用")]
    public bool debugDir = false;

    Rigidbody2D rb;
    Animator anim;

    Vector2 moveInput;
    Vector2 lastDir = Vector2.down;   // 默认朝下

    // 缓动速度，避免 Speed 突变导致动画抖动
    float smoothSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // 俯视角：无重力、不旋转
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        // 1. 读取输入
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        // 2. 更新方向（关键：只保留 4 个正交方向，避免混合树斜向糊图）
        if (moveInput != Vector2.zero)
        {
            if (Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
                lastDir = new Vector2(Mathf.Sign(moveInput.x), 0f);   // 左 / 右
            else
                lastDir = new Vector2(0f, Mathf.Sign(moveInput.y));   // 上 / 下

            if (debugDir)
                Debug.Log($"方向: {lastDir}");
        }

        // 3. 喂给 Animator（Speed 做一点平滑，动画过渡更自然）
        smoothSpeed = Mathf.Lerp(smoothSpeed, moveInput.magnitude, Time.deltaTime * 12f);
        if (Mathf.Abs(smoothSpeed - moveInput.magnitude) < 0.01f)
            smoothSpeed = moveInput.magnitude;

        anim.SetFloat("Speed", smoothSpeed);
        anim.SetFloat("DirX", lastDir.x);
        anim.SetFloat("DirY", lastDir.y);
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }
}
