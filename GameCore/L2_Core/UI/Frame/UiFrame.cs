using System.Numerics;

namespace OpenWC3.UI;

// UI Frame 抽象基类: 对齐 Warsmash AbstractRenderableFrame
// 位置/锚点/可见/启用/命中/渲染; 布局产物 RenderBounds (0.8x0.6 比例坐标)
public abstract class UiFrame
{
    public string Name { get; set; }
    public bool Visible = true;
    public bool Enabled = true;
    public bool MouseTransparent;   // 纯装饰帧, 点击穿透
    public bool ClipChildren;
    public IScrollTarget ScrollTarget;
    public UiFrame Parent { get; private set; }
    public readonly List<UiFrame> Children = new();
    public readonly List<SetPoint> SetPoints = new();
    public float? Width, Height;
    public UiRect RenderBounds;
    public bool LayoutValid;

    protected UiFrame(string name = null) => Name = name;

    public void AddChild(UiFrame child)
    {
        if (child.Parent != null) throw new InvalidOperationException("Frame already has a parent");
        child.Parent = this;
        Children.Add(child);
        InvalidateLayout();
    }

    // 插到指定位置 (渲染正序 = 层级从低到高), 用于让 chrome 垫在最底层
    public void InsertChild(int index, UiFrame child)
    {
        if (child.Parent != null) throw new InvalidOperationException("Frame already has a parent");
        child.Parent = this;
        Children.Insert(Math.Clamp(index, 0, Children.Count), child);
        InvalidateLayout();
    }

    public void RemoveChild(UiFrame child)
    {
        if (child.Parent != this) return;
        child.Parent = null;
        Children.Remove(child);
        InvalidateLayout();
    }

    // SetAllPoints: 对角对齐父四角 (拉伸填满)
    // 对齐 Warsmash setSetAllPoints: 只补未被占用的锚点, 已有 SetPoint 优先
    public void SetAllPoints(UiFrame target = null)
    {
        target ??= Parent;
        if (target == null) return;
        AddIfUnset(FramePoint.TopLeft);
        AddIfUnset(FramePoint.BottomRight);
        InvalidateLayout();

        void AddIfUnset(FramePoint self)
        {
            foreach (var sp in SetPoints) if (sp.Self == self) return;
            SetPoints.Add(new SetPoint(self, target, self, Vector2.Zero));
        }
    }

    public void AddSetPoint(SetPoint point)
    {
        SetPoints.Add(point);
        InvalidateLayout();
    }

    // 单点锚定 (本帧 self 锚点 -> 目标 target 锚点 + 偏移), 配合 Width/Height
    public void SetPoint(FramePoint self, UiFrame target, FramePoint targetPoint, Vector2 offset)
        => AddSetPoint(new SetPoint(self, target, targetPoint, offset));

    public void InvalidateLayout()
    {
        // Root.Position short-circuits a valid subtree. A changed child must also
        // dirty its ancestors, otherwise opening/updating a dialog is skipped
        // until the viewport changes or an unrelated HUD frame invalidates it.
        for (UiFrame ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            ancestor.LayoutValid = false;
        LayoutValid = false;
        foreach (var child in Children) child.InvalidateLayout();
    }

    // 计算布局: 按 SetPoints 求解 RenderBounds, 递归 children
    public virtual void Position()
    {
        if (LayoutValid) return;
        LayoutValid = true;
        SolveBounds();
        foreach (var child in Children) child.Position();
    }

    // 锚点求解: 逐轴三点模型, 对齐 open-realm UI_SolveAxisPosition (ui_render.c)
    //   mid       -> pos = mid - size/2
    //   min & max -> pos = min, size = max - min   (拉伸)
    //   max only  -> pos = max - size
    //   min only  -> pos = min
    void SolveBounds()
    {
        if (SetPoints.Count == 0) return;
        float? xMin = null, xMid = null, xMax = null;
        float? yMin = null, yMid = null, yMax = null;
        foreach (var sp in SetPoints)
        {
            // FDF permits forward references to siblings declared later in the file.
            if (sp.Target is { LayoutValid: false } target) target.Position();
            var (su, sv) = FramePoints.AnchorOffset(sp.Self);
            var (tu, tv) = FramePoints.AnchorOffset(sp.TargetPoint);
            UiRect t = sp.Target?.RenderBounds ?? default;
            float px = t.X + t.Width * tu + sp.Offset.X;
            float py = t.Y + t.Height * tv + sp.Offset.Y;
            if (su == 0f) xMin = px; else if (su == 1f) xMax = px; else xMid = px;
            if (sv == 0f) yMin = py; else if (sv == 1f) yMax = py; else yMid = py;
        }
        var (x, w) = SolveAxis(xMin, xMid, xMax, Width ?? RenderBounds.Width, RenderBounds.X);
        var (y, h) = SolveAxis(yMin, yMid, yMax, Height ?? RenderBounds.Height, RenderBounds.Y);
        RenderBounds = new UiRect(x, y, w, h);
    }

    // 单锚点时帧仍需有尺寸, 否则会解出 0 宽高 -> 整棵子树贴图/文字全部不可见。
    // 两点若锚在同一侧, span 会是 0 或负, 绝不能当作拉伸尺寸传下去
    // (负宽度会让 DrawTexturedQuad 的顶点翻转, 整块画不出来)。
    static (float Pos, float Size) SolveAxis(float? min, float? mid, float? max, float size, float fallback)
    {
        float safe = MathF.Max(size, 0f);
        if (mid.HasValue) return (mid.Value - safe * 0.5f, safe);
        if (min.HasValue && max.HasValue)
        {
            float span = max.Value - min.Value;
            return span > 0f ? (min.Value, span) : (min.Value, safe);
        }
        if (max.HasValue) return (max.Value - safe, safe);
        if (min.HasValue) return (min.Value, safe);
        return (fallback, safe);
    }

    // 渲染: 正序遍历 children (先 add 的在底层)
    public virtual void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        if (ClipChildren) device.PushScissor(RenderBounds);
        foreach (var child in Children) child.Render(device);
        if (ClipChildren) device.PopScissor();
    }

    // 命中测试: 逆序遍历 children (Z 高的先命中), 命中即停, 不冒泡。
    // MouseTransparent 只让本帧自己不成为命中目标, 子帧照常参与 —— 对齐 open-realm
    // UIFLAG_PASSTHROUGH; 之前连子树一起屏蔽, 导致 MouseTransparent 的 ConsoleUI
    // 底下那排按钮永远收不到鼠标事件。不可见/禁用帧整棵子树都不命中。
    public virtual UiFrame HitTest(Vector2 point)
    {
        if (!Visible || !Enabled) return null;
        if (ClipChildren && !RenderBounds.Contains(point.X, point.Y)) return null;
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            var hit = Children[i].HitTest(point);
            if (hit != null) return hit;
        }
        return !MouseTransparent && RenderBounds.Contains(point.X, point.Y) ? this : null;
    }

    // 按 Name 递归查找子帧 (FDF 数据绑定用)
    public UiFrame FindByName(string name)
    {
        if (!string.IsNullOrEmpty(name) && Name == name) return this;
        foreach (var c in Children)
        {
            var f = c.FindByName(name);
            if (f != null) return f;
        }
        return null;
    }

    public T FindByName<T>(string name) where T : UiFrame => FindByName(name) as T;

    // 按名给文字帧赋值; 帧不存在或不是 StringFrame 时返回 false
    public bool SetText(string name, string text)
    {
        if (FindByName(name) is not StringFrame frame) return false;
        frame.Text = text ?? "";
        return true;
    }

    // 调试用: 打印帧树 (名字/解出的矩形/可见性 + 各类型的关键字段)
    public void DumpTree(string tag, int depth = 0)
    {
        string extra = this switch
        {
            StringFrame s => $" text='{s.Text}' size={s.FontSize:0.##}",
            TextureFrame t => $" tex={(t.Texture != null ? "ok" : "NULL")}",
            BackdropFrame b => $" bg={(b.BackgroundTexture != null ? "ok" : "NULL")}"
                + $" edge={(b.EdgeTexture != null ? "ok" : "NULL")}",
            _ => "",
        };
        RuntimeDiagnostics.WriteLine($"  [{tag}] {new string(' ', depth * 2)}{GetType().Name} " +
            $"\"{Name}\" {RenderBounds.X:0.#},{RenderBounds.Y:0.#} " +
            $"{RenderBounds.Width:0.#}x{RenderBounds.Height:0.#} vis={Visible}{extra}");
        foreach (var child in Children) child.DumpTree(tag, depth + 1);
    }
}
