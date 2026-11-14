using System;
using System.Collections.Generic;
using System.Numerics;
using OpenWC3.UI;

namespace OpenWC3.Game.UI;

// 世界空间漂浮文字: JASS TextTag 与内部飘字统一在此投影 + 绘制。
// 文字用屏幕空间字号, 拉远镜头也不会跟着变小; 一条文字一个池化 StringFrame, 不建节点。
internal sealed class TextTagFrame : UiContainer
{
    public override void Render(IUiRenderDevice device)
    {
        if (!Visible) return;
        RenderLabels(device);
    }

    void RenderLabels(IUiRenderDevice device)
    {
        foreach (UiFrame child in Children)
        {
            if (child is not StringFrame { Visible: true } text) continue;
            UiRect bounds = text.RenderBounds;
            float width = Math.Max(40f, device.MeasureTextLineWidth(text.Text, text.FontSize) + 4f
                + text.FontShadowOffset.X);
            // 重量宽后按对齐方式重锚, 否则 Center/Right 的锚点会被量宽差拉偏。
            float x = text.Alignment switch
            {
                TextAlignment.Center => bounds.X + (bounds.Width - width) * .5f,
                TextAlignment.Right => bounds.Right - width,
                _ => bounds.X,
            };
            text.RenderBounds = new UiRect(x, bounds.Y, width, bounds.Height);
        }
        base.Render(device);
    }

    // 0d0310 用帧缓冲高度换算并四舍五入; 0da630 把逻辑行高夹到至少两个物理像素。
    internal static float FontLineHeight(float normalizedHeight, int framebufferHeight, float scaleY)
        => Math.Max(2f, MathF.Floor(Math.Max(0f, normalizedHeight) * Math.Max(1, framebufferHeight) + .5f))
            / Math.Max(.0001f, scaleY);

    public TextTagFrame() : base("TextTags") => MouseTransparent = true;

    public struct TagView
    {
        public float ScreenX, ScreenY;
        public string Text;
        public float FontSize;
        public Vector4 Color;
        public TextAlignment Alignment;
    }

    public void Update(IReadOnlyList<TagView> tags, float viewportWidth, float viewportHeight)
    {
        Visible = tags != null && tags.Count > 0;
        if (!Visible)
        {
            // 全层清空(切图/飘字全部过期): 放掉最后一次的文本引用, 池化帧留待下帧复用。
            foreach (UiFrame child in Children)
            {
                if (child is not StringFrame stale) continue;
                stale.Visible = false;
                stale.Text = string.Empty;
            }
            return;
        }

        RenderBounds = new UiRect(0f, 0f, viewportWidth, viewportHeight);

        while (Children.Count < tags.Count)
        {
            var frame = new StringFrame($"TextTag{Children.Count}")
            {
                Alignment = TextAlignment.Left,
                MouseTransparent = true,
                UseFontLineHeight = true,
                // 世界层飘字按脚本给定字号排版, 不属于 UI 组件, 不参与「UI字体大小」。
                IgnoreUiFontScale = true,
                FontShadowColor = new Vector4(0f, 0f, 0f, 1f),
                FontShadowOffset = new Vector2(.0015f * 768f, .0015f * 768f),
            };
            AddChild(frame);
        }
        for (int i = tags.Count; i < Children.Count; i++)
        {
            if (Children[i] is not StringFrame extra) continue;
            extra.Visible = false;
            extra.Text = string.Empty;
        }

        for (int i = 0; i < tags.Count; i++)
        {
            TagView tag = tags[i];
            if (Children[i] is not StringFrame frame) continue;
            frame.Visible = true;
            frame.Text = tag.Text ?? string.Empty;
            frame.FontSize = tag.FontSize;
            frame.Color = tag.Color;
            frame.Alignment = tag.Alignment;
            frame.FontShadowColor = new Vector4(0f, 0f, 0f, tag.Color.W);
            float width = Math.Max(40f, (tag.Text?.Length ?? 0) * tag.FontSize * .6f);
            float height = tag.FontSize + 4f;
            float x = tag.Alignment switch
            {
                TextAlignment.Center => tag.ScreenX - width * .5f,
                TextAlignment.Right => tag.ScreenX - width,
                _ => tag.ScreenX,
            };
            frame.RenderBounds = new UiRect(x, tag.ScreenY - tag.FontSize, width, height);
        }
    }
}
