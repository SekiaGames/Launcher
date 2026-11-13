using Godot;

namespace OpenWC3.Game;

internal sealed class War3MissileAnimation
{
    private bool _birthComplete;
    private string _current;

    internal void Start(AnimationPlayer player)
    {
        if (player == null) return;
        _birthComplete = false;
        _current = null;
        if (player.HasAnimation("Birth"))
        {
            Play(player, "Birth", 0f, loop: false);
            return;
        }
        _birthComplete = true;
        Play(player, "Stand", 0f, loop: true);
    }

    internal bool Advance(AnimationPlayer player, float flightAge, float? deathAge)
    {
        if (player == null) return deathAge.HasValue;
        if (!_birthComplete)
        {
            if (player.HasAnimation("Birth"))
            {
                float birthLength = Length(player, "Birth");
                if (!deathAge.HasValue && flightAge < birthLength)
                {
                    Play(player, "Birth", flightAge, loop: false);
                    return false;
                }
            }
            _birthComplete = true;
        }

        if (deathAge.HasValue)
        {
            if (!player.HasAnimation("Death")) return true;
            float deathLength = Length(player, "Death");
            Play(player, "Death", deathAge.Value, loop: false);
            return deathAge.Value >= deathLength;
        }

        string sequence = player.HasAnimation("Stand") ? "Stand" : null;
        if (sequence == null) return false;
        float birthSpan = player.HasAnimation("Birth") ? Length(player, "Birth") : 0f;
        float standLength = Mathf.Max(0.001f, Length(player, "Stand"));
        Play(player, sequence, (flightAge - birthSpan) % standLength, loop: true);
        return false;
    }

    private void Play(AnimationPlayer player, string name, float seconds, bool loop)
    {
        Animation animation = player.GetAnimation(name);
        if (animation == null) return;
        animation.LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
        if (_current != name)
        {
            player.Play(name);
            _current = name;
        }
        player.Seek(Mathf.Max(0f, seconds), true);
    }

    private static float Length(AnimationPlayer player, string name)
    {
        Animation animation = player.GetAnimation(name);
        return animation == null ? 0f : (float)animation.Length;
    }
}