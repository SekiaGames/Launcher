using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenWC3;

/// <summary>
/// Spell Book（Aspb）运行时容器。DataA 是内部技能码，DataB 共享冷却，
/// DataC/DataD 是最小/最大技能数，DataE 是基础命令。
/// </summary>
public sealed class RuntimeSpellBookAbility : Ability
{
    readonly List<Ability> _createdChildren = new();
    bool _pendingSelection;

    public IReadOnlyList<Ability> Children => _createdChildren;

    public IReadOnlyList<string> SpellRawcodes { get; private set; } = Array.Empty<string>();
    public bool SharedCooldown { get; private set; }
    public int MinimumSpells { get; private set; }
    public int MaximumSpells { get; private set; }
    public string BaseOrderId { get; private set; } = "spellbook";

    public override bool IsPassive => false;
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;

    public override void OnAdd()
    {
        RefreshSpellBookData();
        SynchronizeChildren();
    }

    public override void OnLevelUp()
    {
        RefreshSpellBookData();
        SynchronizeChildren();
    }

    public override void OnLevelDown()
    {
        RefreshSpellBookData();
        SynchronizeChildren();
    }

    public override void OnSpawn()
    {
        if (_pendingSelection) SynchronizeChildren();
    }

    public override void OnRemove()
    {
        _pendingSelection = false;
        RemoveCreatedChildren();
    }

    public override void OnOwnerAbilityCast(Ability ability)
    {
        if (!SharedCooldown || ability == null
            || !ReferenceEquals(ability.ContainerAbility, this)) return;
        foreach (Ability child in _createdChildren)
        {
            if (ReferenceEquals(child, ability)) continue;
            child.CooldownRemaining = Math.Max(child.CooldownRemaining, ability.CooldownRemaining);
        }
    }

    /// <summary>物品装备时把书挂到主人技能表上，子技能随书一起创建。</summary>
    public static RuntimeSpellBookAbility AttachToItem(Unit owner, Item item, AbilityType type)
    {
        if (owner == null || item == null || type == null) return null;

        foreach (Ability existing in owner.Abilities)
        {
            if (existing is RuntimeSpellBookAbility attached && ReferenceEquals(attached.SourceItem, item))
                return attached;
        }

        var book = new RuntimeSpellBookAbility { Owner = owner, Type = type };
        book.BindObjectData(type.Id, new AbilityTypeObjectData(type), 1);
        book.Level = 1;
        book.SourceItem = item;
        owner.Abilities.Add(book);
        book.OnAdd();
        book.OnLevelUp();
        if (book.Learned) book.OnLearned();
        return book;
    }

    public void RefreshSpellBookData()
    {
        int level = Math.Max(1, Level);
        SpellRawcodes = ParseRawcodes(Type?.DataTextAt(level, 'A'));
        SharedCooldown = ReadInt(level, 'B', 0) != 0;
        MinimumSpells = Math.Max(0, ReadInt(level, 'C', SpellRawcodes.Count));
        MaximumSpells = Math.Clamp(ReadInt(level, 'D', SpellRawcodes.Count), 0, 11);

        string order = (Type?.DataTextAt(level, 'E') ?? "").Trim();
        BaseOrderId = string.IsNullOrWhiteSpace(order) ? "spellbook" : order;
        OrderId = BaseOrderId;
    }

    // Game.dll 6f4e3ce8 / 6f4e3d80: dice count then swap-with-tail draws.
    public static int[] SelectSpellIndices(int minimum, int maximum, int candidates, Func<uint> next)
    {
        int max = Math.Clamp(maximum, 0, Math.Min(11, Math.Max(0, candidates)));
        int min = Math.Clamp(minimum, 0, max);
        if (min == max) return Sequence(max);

        int count = min + 1 + (int)(((ulong)next() * (uint)(max - min)) >> 32);
        int[] pool = Sequence(Math.Min(11, candidates));
        var result = new int[count];
        int remaining = pool.Length;
        for (int i = 0; i < count; i++)
        {
            int index = (int)(((ulong)next() * (uint)remaining) >> 32);
            result[i] = pool[index];
            pool[index] = pool[--remaining];
        }
        return result;
    }

    static int[] Sequence(int count)
    {
        var values = new int[Math.Max(0, count)];
        for (int i = 0; i < values.Length; i++) values[i] = i;
        return values;
    }

    void SynchronizeChildren()
    {
        if (Owner == null) return;
        RemoveCreatedChildren();

        var candidates = new List<string>(Math.Min(11, SpellRawcodes.Count));
        foreach (string id in SpellRawcodes)
        {
            if (candidates.Count >= 11) break;
            if (AbilityCatalog.Get(id) != null) candidates.Add(id);
        }

        int max = Math.Min(MaximumSpells, candidates.Count);
        _pendingSelection = Owner.World == null && Math.Min(MinimumSpells, max) != max;
        if (_pendingSelection) return;

        int[] indices = SelectSpellIndices(MinimumSpells, MaximumSpells, candidates.Count,
            () => Owner.World != null ? (uint)Owner.World.Rng.Next() : 0u);
        foreach (int index in indices)
        {
            string rawcode = candidates[index];
            if (IsAncestor(rawcode)) continue;

            AbilityType type = AbilityCatalog.Get(rawcode);
            if (type == null) continue;
            Ability child = RuntimeAbilityFactory.Create(type);
            if (child == null) continue;

            child.Owner = Owner;
            child.BindObjectData(type.Id, new AbilityTypeObjectData(type), 1);
            child.Level = 1;
            child.ContainerRawcode = Id;
            child.ContainerAbility = this;
            child.SourceItem = SourceItem;
            Owner.Abilities.Add(child);
            child.OnAdd();
            child.OnLevelUp();
            if (child.Learned) child.OnLearned();
            _createdChildren.Add(child);
        }
    }

    bool IsAncestor(string rawcode)
    {
        for (Ability parent = this; parent != null; parent = parent.ContainerAbility)
        {
            if (string.Equals(parent.Rawcode, rawcode, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    void RemoveCreatedChildren()
    {
        if (Owner == null)
        {
            _createdChildren.Clear();
            return;
        }
        foreach (Ability child in _createdChildren)
        {
            child.OnRemove();
            Owner.Abilities.Remove(child);
        }
        _createdChildren.Clear();
    }

    int ReadInt(int level, char slot, int fallback)
    {
        string raw = Type?.DataTextAt(level, slot) ?? "";
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }

    static IReadOnlyList<string> ParseRawcodes(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();

        var result = new List<string>();
        foreach (string part in value.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length < 4 || part == "----") continue;
            result.Add(part[..4]);
        }
        return result;
    }
}