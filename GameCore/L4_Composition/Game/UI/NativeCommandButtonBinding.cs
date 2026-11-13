using System.Numerics;

namespace OpenWC3.Game.UI;

internal static class NativeCommandButtonBinding
{
    internal static Vector4 Color(uint packed)=>new(
        (packed&255)/255f,((packed>>8)&255)/255f,((packed>>16)&255)/255f,((packed>>24)&255)/255f);
    internal static uint BoundColor(int state,uint stored)=>state==1?stored:uint.MaxValue;
    internal static string DisabledArt(string icon,string directory)
    {
        string normalized=(icon??"").Replace('/','\\');
        string name=normalized[(normalized.LastIndexOf('\\')+1)..];
        return (directory??@"ReplaceableTextures\CommandButtonsDisabled\")+"DIS"+name;
    }
}
