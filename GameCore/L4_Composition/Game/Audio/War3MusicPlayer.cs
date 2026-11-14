using Godot;
using OpenWC3.Game;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 对战背景音乐。曲目来自 UI\war3skins.txt 的 `[种族] Music_V{版本}`（分号分隔），
/// 文件在 Sound\Music\mp3Music\ 下；随机起播后按表顺序推进，曲间留 20 秒，
/// 音量按客户端的软件混音曲线换算，停止/整组音量走淡变。
/// </summary>
public sealed class War3MusicPlayer
{
    private const float InterTrackGap = 20f;
    private const float FadeOutSeconds = 1f;

    private readonly AudioStreamPlayer _player;
    private readonly List<string> _tracks = new();
    private readonly Random _random = new(4242);
    private int _index;
    private bool _active;
    private bool _gapPending;
    private float _gapRemaining;
    private float _userVolume = War3Volume.DefaultMusicVolume;
    private float _gain = 1f;
    private float _fadeTarget = 1f;
    private float _fadeRate;
    private bool _stopAfterFade;

    public War3MusicPlayer(Node parent)
    {
        _player = new AudioStreamPlayer { Name = "Music" };
        parent.AddChild(_player);
        _player.Finished += OnTrackFinished;
        ApplyVolume();
    }

    public string CurrentTrack { get; private set; } = "";

    /// <summary>开始播放某族的对战音乐；找不到就退回 Default / Human。</summary>
    public void Start(string race, int version)
    {
        _tracks.Clear();
        _tracks.AddRange(Resolve(race, $"Music_V{version}"));
        if (_tracks.Count == 0) _tracks.AddRange(Resolve(race, "Music_V1"));
        if (_tracks.Count == 0) _tracks.AddRange(Resolve("Default", "Music_V1"));
        if (_tracks.Count == 0)
        {
            GD.Print("[MUSIC] 没有可用曲目");
            return;
        }

        // 原版随机起播后按表顺序推进
        _index = _random.Next(_tracks.Count);
        _active = true;
        _gapPending = false;
        GD.Print($"[MUSIC] {race} 曲目 {_tracks.Count} 首：{string.Join(", ", _tracks)}");
        Play(_tracks[_index], _tracks.Count == 1);
    }

    /// <summary>胜负音乐：VictoryMusic_V{n} / DefeatMusic_V{n}。</summary>
    public void PlayEnding(string race, int version, bool victory)
    {
        string key = (victory ? "VictoryMusic_V" : "DefeatMusic_V") + version;
        List<string> tracks = Resolve(race, key);
        if (tracks.Count == 0) tracks = Resolve("Default", key);
        if (tracks.Count == 0) return;

        _active = false;
        _gapPending = false;
        Play(tracks[0]);
    }

    public void Stop(bool fade = false)
    {
        _active = false;
        _gapPending = false;
        if (!_player.Playing)
        {
            ResetGain();
            return;
        }
        if (fade) FadeTo(0f, FadeOutSeconds, stopAfterFade: true);
        else
        {
            _player.Stop();
            ResetGain();
        }
    }

    public void SetUserVolume(float volume)
    {
        _userVolume = Mathf.Clamp(volume, 0f, 1f);
        ApplyVolume();
    }

    public void Update(float delta)
    {
        if (_gapPending)
        {
            _gapRemaining -= delta;
            if (_gapRemaining <= 0f)
            {
                _gapPending = false;
                if (_tracks.Count > 0) Play(_tracks[_index]);
            }
        }

        if (_fadeRate != 0f)
        {
            _gain += _fadeRate * delta;
            if (_fadeRate > 0f ? _gain >= _fadeTarget : _gain <= _fadeTarget)
            {
                _gain = _fadeTarget;
                _fadeRate = 0f;
                if (_stopAfterFade)
                {
                    _stopAfterFade = false;
                    _player.Stop();
                    ResetGain();
                }
            }
            ApplyVolume();
        }
    }

    private void OnTrackFinished()
    {
        if (!_active || _tracks.Count == 0) return;
        if (_tracks.Count == 1)
        {
            Play(_tracks[_index], loop: true);
            return;
        }
        _index = (_index + 1) % _tracks.Count;
        _gapPending = true;
        _gapRemaining = InterTrackGap;
    }

    private void Play(string resPath, bool loop = false)
    {
        AudioStream stream = ResourceLoader.Exists(resPath) ? ResourceLoader.Load<AudioStream>(resPath) : null;
        if (stream == null)
        {
            GD.PushWarning($"[MUSIC] 曲目缺失 {resPath}");
            return;
        }

        if (loop && stream is AudioStreamMP3 mp3)
        {
            var copy = (AudioStreamMP3)mp3.Duplicate();
            copy.Loop = true;
            stream = copy;
        }

        CurrentTrack = resPath;
        _player.Stream = stream;
        ResetGain();
        _player.Play();
        ApplyVolume();
    }

    private void ResetGain()
    {
        _gain = 1f;
        _fadeTarget = 1f;
        _fadeRate = 0f;
    }

    private void FadeTo(float target, float seconds, bool stopAfterFade)
    {
        _fadeTarget = target;
        _stopAfterFade = stopAfterFade;
        _fadeRate = seconds > 0f ? (target - _gain) / seconds : 0f;
        if (seconds <= 0f)
        {
            _gain = target;
            if (_stopAfterFade)
            {
                _stopAfterFade = false;
                _player.Stop();
                ResetGain();
            }
        }
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        _player.VolumeDb = War3Volume.ToDecibels(_userVolume * Mathf.Clamp(_gain, 0f, 1f));
    }

    private static List<string> Resolve(string race, string key)
    {
        var result = new List<string>();
        string value = Lookup(race, key);
        if (string.IsNullOrWhiteSpace(value)) return result;

        foreach (string entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string res = ToRes(entry);
            if (!string.IsNullOrEmpty(res)) result.Add(res);
        }
        return result;
    }

    private static string Lookup(string race, string key)
    {
        // 与客户端一致：[CustomSkin] → [种族] → [Default]，地图皮肤优先
        foreach (string section in new[] { "CustomSkin", race, "Default" })
        {
            string value = ReadKey(section, key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    private static Dictionary<string, Dictionary<string, string>> _sections;

    private static string ReadKey(string section, string key)
    {
        LoadSkin();
        if (_sections == null) return null;
        return _sections.TryGetValue(section ?? "Default", out Dictionary<string, string> fields)
            && fields.TryGetValue(key, out string value) ? value : null;
    }

    private static void LoadSkin()
    {
        if (_sections != null) return;
        _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        Godot.Collections.Dictionary keys = GeneratedTexts.Get("UIwar3skins")["keys"].AsGodotDictionary();
        if (keys.Count == 0)
        {
            GD.PushWarning("[MUSIC] 生成的 war3skins 为空");
            return;
        }

        foreach (KeyValuePair<Variant, Variant> pair in keys)
        {
            string name = pair.Key.AsString();
            int dot = name.IndexOf('.');
            string section = dot > 0 ? name[..dot] : "Default";
            string key = dot > 0 ? name[(dot + 1)..] : name;
            if (!_sections.TryGetValue(section, out Dictionary<string, string> fields))
            {
                fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _sections[section] = fields;
            }
            fields[key] = pair.Value.AsString().Trim('"');
        }
    }

    /// <summary>Sound\Music\mp3Music\X.mp3 → res://extract/Sound/Music/mp3Music/X.mp3。</summary>
    private static string ToRes(string war3Path)
    {
        if (string.IsNullOrWhiteSpace(war3Path)) return null;

        string relative = war3Path.Replace('\\', '/').TrimStart('/');
        string direct = "res://extract/" + relative;
        if (ResourceLoader.Exists(direct)) return direct;

        if (!relative.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) relative += ".mp3";
        direct = "res://extract/" + relative;
        if (ResourceLoader.Exists(direct)) return direct;

        string name = relative[(relative.LastIndexOf('/') + 1)..];
        string guess = "res://extract/Sound/Music/mp3Music/" + name;
        return ResourceLoader.Exists(guess) ? guess : null;
    }
}