using System.Numerics;
using OpenWC3.RHI;

namespace OpenWC3.UI;

// 横向设置滑条。绑定原版 EscMenuSliderTemplate (EscMenuTemplates.fdf:157) 的三件套:
//   ControlBackdrop: 平铺 EscMenuSliderBackground + 八等分边文件 EscMenuSliderBorder
//                    (CornerSize/BackgroundSize 0.006, Insets 0.0025)
//   SliderThumbButtonFrame: 0.016 方钮, 贴 EscMenuSliderThumbButton
// 未提供官方贴图时 (例如脚本 CreateFrame("SLIDER")) 仍走旧的纯色自绘, 保持兼容。
// Pointer capture is provided by UiEventRouter, so dragging remains active after
// the finger leaves the knob.
public sealed class SliderFrame : UiFrame, IClickable, IDraggable
{
    // EscMenuSliderTemplate: Height 0.012 / thumb 0.016 (FDF 比例 -> 设计单位)
    public const float OfficialTrackHeight = 0.012f * FdfScale.ToDesignUnits;
    public const float OfficialThumbSize = 0.016f * FdfScale.ToDesignUnits;

    readonly BackdropFrame _track;
    readonly TextureFrame _thumb;
    float _value=1f;
    bool _pressed;

    public ITexture BackgroundTexture;
    public ITexture BorderTexture;
    public ITexture ThumbTexture;
    public ITexture DisabledBorderTexture;
    public ITexture DisabledThumbTexture;
    /// <summary>置灰态: 使用 EscMenuSliderDisabledBorder / EscMenuSliderDisabledThumbButton。</summary>
    public bool Disabled;
    /// <summary>轨道视觉高度; 点击判定仍用整个 RenderBounds。</summary>
    public float TrackHeight = OfficialTrackHeight;
    /// <summary>拇指视觉尺寸; 原版 0.016。</summary>
    public float ThumbSize = OfficialThumbSize;

    public float Minimum=.5f;
    public float Maximum=2f;
    public float Value
    {
        get=>_value;
        set=>SetValue(value,notify:false);
    }
    public event Action<float> ValueChanged;

    /// <summary>是否已绑定原版三件套贴图 (否则使用兼容的自绘样式)。</summary>
    public bool HasOfficialArt =>
        BackgroundTexture != null || BorderTexture != null || ThumbTexture != null;

    public SliderFrame(string name=null):base(name)
    {
        MouseTransparent=false;
        // 轨道直接复用原版九宫格背板实现: slider-border 就是 64x8 的八等分边图集。
        _track = new BackdropFrame(name==null?null:name+"Track")
        {
            MouseTransparent = true,
            TileBackground = true,
            CornerSize = FdfScale.S(0.006f),
            BackgroundSize = FdfScale.S(0.006f),
            Insets = new UiRect(FdfScale.S(0.0025f),FdfScale.S(0.0025f),
                FdfScale.S(0.0025f),FdfScale.S(0.0025f)),
        };
        _thumb = new TextureFrame(name==null?null:name+"Thumb") { KeepSquare = true };
        AddChild(_track);
        AddChild(_thumb);
    }

    public override void Render(IUiRenderDevice device)
    {
        if(!Visible)return;
        if(HasOfficialArt)
        {
            LayoutOfficialParts();
            base.Render(device);
            return;
        }
        UiRect bounds=RenderBounds;
        float trackHeight=MathF.Max(4f,bounds.Height*.18f);
        var track=new UiRect(bounds.X,bounds.Y+(bounds.Height-trackHeight)*.5f,
            bounds.Width,trackHeight);
        device.DrawTexturedQuad(track,device.WhiteTexture,new Vector4(.04f,.04f,.04f,.9f));
        float ratio=Ratio;
        device.DrawTexturedQuad(new UiRect(track.X,track.Y,track.Width*ratio,track.Height),
            device.WhiteTexture,new Vector4(.92f,.7f,.12f,1f));
        float knob=MathF.Max(14f,bounds.Height*.72f);
        float center=track.X+track.Width*ratio;
        device.DrawTexturedQuad(new UiRect(center-knob*.5f,
            bounds.Y+(bounds.Height-knob)*.5f,knob,knob),device.WhiteTexture,
            _pressed?new Vector4(1f,.9f,.35f,1f):new Vector4(.82f,.82f,.78f,1f));
        base.Render(device);
    }

    // 原版没有"已填充进度"这一层: 深色平铺轨道 + 金属边 + 宝珠旋钮。
    void LayoutOfficialParts()
    {
        UiRect bounds=RenderBounds;
        if(bounds.Width<=0||bounds.Height<=0)
        {
            _track.Visible=false;
            _thumb.Visible=false;
            return;
        }
        bool disabled=Disabled||!Enabled;
        float trackHeight=MathF.Min(MathF.Max(1f,TrackHeight),bounds.Height);
        _track.BackgroundTexture=BackgroundTexture;
        _track.EdgeTexture=disabled?DisabledBorderTexture??BorderTexture:BorderTexture;
        _track.Visible=_track.BackgroundTexture!=null||_track.EdgeTexture!=null;
        _track.RenderBounds=new UiRect(bounds.X,
            bounds.Y+(bounds.Height-trackHeight)*.5f,bounds.Width,trackHeight);

        float thumb=MathF.Min(MathF.Max(1f,ThumbSize),bounds.Height);
        _thumb.Texture=disabled?DisabledThumbTexture??ThumbTexture:ThumbTexture;
        _thumb.Visible=_thumb.Texture!=null;
        float travel=MathF.Max(0f,bounds.Width-thumb);
        _thumb.RenderBounds=new UiRect(bounds.X+travel*Ratio,
            bounds.Y+(bounds.Height-thumb)*.5f,thumb,thumb);
    }

    bool IClickable.Enabled=>Enabled;
    public void OnPointerDown(Vector2 pos){_pressed=true;SetFromPointer(pos.X);}
    public void OnPointerUp(Vector2 pos){SetFromPointer(pos.X);_pressed=false;}
    public void OnClick(int button){_pressed=false;}
    public void OnPointerDrag(Vector2 pos){if(_pressed)SetFromPointer(pos.X);}

    void SetFromPointer(float x)
    {
        float ratio=RenderBounds.Width<=0?0:(x-RenderBounds.X)/RenderBounds.Width;
        SetValue(Minimum+Math.Clamp(ratio,0f,1f)*(Maximum-Minimum),notify:true);
    }

    void SetValue(float value,bool notify)
    {
        float next=Math.Clamp(float.IsFinite(value)?value:1f,Minimum,Maximum);
        if(MathF.Abs(next-_value)<.001f)return;
        _value=next;
        if(notify)ValueChanged?.Invoke(next);
    }

    float Ratio=>Maximum<=Minimum?0:Math.Clamp((_value-Minimum)/(Maximum-Minimum),0f,1f);
}
