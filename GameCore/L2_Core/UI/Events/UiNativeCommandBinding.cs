namespace OpenWC3.UI;

// Immutable descriptor snapshot. Reference objects are retained without
// resolving or substituting them; their owner validates lifetime at dispatch.
public sealed record UiNativeCommandBinding(int Source,int Action,int Flags,
    int PairedAction,int PairedFlags,object PrimaryReference=null,object SecondaryReference=null)
{
    public UiNativeCommandAction? SelectAction(int eventKind)
    {
        int slot=SelectSlot(Flags,PairedFlags,eventKind);
        return slot<0?null:new(Source,slot==0?Action:PairedAction,slot==0?Flags:PairedFlags,
            slot==0?PrimaryReference:SecondaryReference,slot);
    }

    public static int SelectSlot(int firstFlags,int secondFlags,int eventKind)
    {
        for(int slot=0;slot<2;slot++)
        {
            int flags=slot==0?firstFlags:secondFlags;
            if(flags==0||(flags&0x40)!=0)continue;
            if(eventKind==1&&(flags&0x2000)==0||eventKind==4&&(flags&0x2000)!=0)
                return slot;
        }
        return -1;
    }
}

public readonly record struct UiNativeCommandAction(int Source,int Action,int Flags,object Reference,int Slot);
