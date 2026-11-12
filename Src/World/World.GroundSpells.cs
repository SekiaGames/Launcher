using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>不需要施法者站桩的地面法术（烈焰风暴的余烬、蝗群的陆续释放）。</summary>
public interface IGroundSpell
{
    bool Step(World world, float delta);
}

public sealed partial class World
{
    private readonly List<IGroundSpell> _groundSpells = new();

    public int GroundSpellCount => _groundSpells.Count;

    public void AddGroundSpell(IGroundSpell spell)
    {
        if (spell != null) _groundSpells.Add(spell);
    }

    public void TickGroundSpells()
    {
        for (int index = _groundSpells.Count - 1; index >= 0; index--)
        {
            if (_groundSpells[index].Step(this, Tick)) continue;
            _groundSpells.RemoveAt(index);
        }
    }
}