namespace OpenWC3;

/// <summary>Shared object-data target mask: Game.dll 691240 / 68c7b0 / 68aa30 / 68a650.</summary>
public static class TargetRules
{
    public const TargetFlags Shapes=TargetFlags.Ground|TargetFlags.Air|TargetFlags.Structure
        |TargetFlags.Ward|TargetFlags.Item|TargetFlags.Tree|TargetFlags.Wall
        |TargetFlags.Debris|TargetFlags.Decoration|TargetFlags.Bridge;
    public const TargetFlags Relations=TargetFlags.Self|TargetFlags.Player|TargetFlags.Ally
        |TargetFlags.Neutral|TargetFlags.Enemy;

    // Missing categories allow both sides, except life/vulnerability/shape.
    // These are the native 68c7b0 defaults, not skill-specific assumptions.
    public static TargetFlags Normalize(TargetFlags rules)
    {
        if((rules&TargetFlags.NotSelf)!=0)
            rules|=Relations&~TargetFlags.Self;
        rules=Default(rules,Shapes,TargetFlags.Ground|TargetFlags.Air|TargetFlags.Structure);
        rules=Default(rules,Relations,Relations);
        rules=Default(rules,TargetFlags.Alive|TargetFlags.Dead,TargetFlags.Alive);
        rules=Default(rules,TargetFlags.Vulnerable|TargetFlags.Invulnerable,TargetFlags.Vulnerable);
        rules=Default(rules,TargetFlags.Hero|TargetFlags.NonHero,TargetFlags.Hero|TargetFlags.NonHero);
        rules=Default(rules,TargetFlags.Organic|TargetFlags.Mechanical,TargetFlags.Organic|TargetFlags.Mechanical);
        rules=Default(rules,TargetFlags.Ancient|TargetFlags.NonAncient,TargetFlags.Ancient|TargetFlags.NonAncient);
        return Default(rules,TargetFlags.Sapper|TargetFlags.NonSapper,TargetFlags.Sapper|TargetFlags.NonSapper);
    }

    static TargetFlags Default(TargetFlags rules,TargetFlags category,TargetFlags fallback)
        =>(rules&category)==0?rules|fallback:rules;

    // Native enemy-area helper 56a7f0 replaces relationships before calling
    // the same classifier. Immediate Thunder Clap (56e410) includes neutral.
    public static TargetFlags ForEnemyArea(TargetFlags rules,bool includeNeutral=true)
        =>(rules&~Relations&~TargetFlags.NotSelf)|TargetFlags.Enemy
            |(includeNeutral?TargetFlags.Neutral:0);

    public static TargetFlags ShapeOf(Unit unit)
    {
        TargetFlags shape=unit.TargetType&Shapes;
        if(unit.Snared&&unit.MovementType==MovementType.Fly)
            shape=(shape&~TargetFlags.Air)|TargetFlags.Ground;
        return shape;
    }

    public static TargetFlags RelationOf(Unit source,Unit target)
        =>ReferenceEquals(source,target)?TargetFlags.Self
            :source.PlayerId==target.PlayerId?TargetFlags.Player
            :!(source.World?.AreAllied(source,target)??source.Team==target.Team)?TargetFlags.Enemy
            :(source.World?.SharedSpellsQuery(source,target)??source.Team==target.Team)?TargetFlags.Ally
            :TargetFlags.Neutral;

    public static bool Matches(Unit source,Unit target,TargetFlags rules,
        bool ignoreVisibility=false,bool ignoreLifeState=false,bool useHeroSpellRules=false,
        bool ignoreFaction=false)
    {
        if(source==null||target==null||target.Removed||target.Untargetable
            ||(rules&TargetFlags.NoTargets)!=0)return false;
        if((rules&TargetFlags.NonMagicImmune)!=0&&target.MagicImmune)return false;
        if((rules&TargetFlags.NonEthereal)!=0&&target.Ethereal)return false;
        bool bypassRelation=ignoreFaction&&(rules&TargetFlags.Player)==0;
        rules=Normalize(rules);
        // Native categories are allowed sets: any shape intersection suffices.
        TargetFlags shape=ShapeOf(target);
        if((rules&shape)==0)return false;
        // 68aa30 applies unit class/relationship rules only to unit shapes.
        if((shape&(TargetFlags.Ground|TargetFlags.Air|TargetFlags.Structure|TargetFlags.Ward))!=0)
        {
            if(!bypassRelation&&(rules&RelationOf(source,target))==0)return false;
            if((rules&(target.IsHero?TargetFlags.Hero:TargetFlags.NonHero))==0)return false;
            // Resistant Skin restricts nonhero-only spells; it does not turn a
            // normal unit into an eligible target of hero-only spells.
            if(useHeroSpellRules&&(rules&TargetFlags.Hero)==0&&target.UsesHeroSpellRules)return false;
            if((rules&(target.Is(Classification.Mechanical)?TargetFlags.Mechanical:TargetFlags.Organic))==0)return false;
            if((rules&(target.Is(Classification.Ancient)?TargetFlags.Ancient:TargetFlags.NonAncient))==0)return false;
            if((rules&(target.Is(Classification.Sapper)?TargetFlags.Sapper:TargetFlags.NonSapper))==0)return false;
        }
        if(!ignoreLifeState&&((rules&(target.Alive?TargetFlags.Alive:TargetFlags.Dead))==0
            ||!target.Alive&&!target.Death.IsCorpse))return false;
        if((rules&(target.Invulnerable?TargetFlags.Invulnerable:TargetFlags.Vulnerable))==0)return false;
        if(!ignoreVisibility&&target.Invisible&&!target.IsDetectedByPlayer(source.PlayerId)
            &&(source.World?.AreEnemies(source,target)??source.Team!=target.Team))return false;
        return true;
    }
}
