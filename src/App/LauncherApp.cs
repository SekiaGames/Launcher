using Godot;
using System;

namespace Launcher;

public sealed partial class LauncherApp : Node
{
    private Node _current;
    private string _shotPath;
    private float _shotDelay = -1f;
    private float _elapsed;
    private bool _shotTaken;
    private int _cameraPreset = -1;
    private Vector2? _focusMapPoint;
    private bool _orderTest;
    private bool _simTest;
    private bool _clickTest;
    private bool _gatherTest;
    private bool _trainTest;
    private Vector2? _moveTo;
    private bool _pathProbe;
    private bool _lumberTest;
    private bool _buildTest;
    private bool _fast;
    private bool _housekeeping;
    private int _startGold = 1000000;
    private int _startLumber = 1000000;
    private float _timeScale = 1f;

    public override void _Ready()
    {
        Assets.Init();

        string playPath = null;
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--import-map="))
            {
                string[] parts = argument["--import-map=".Length..].Split('|');
                int code = parts.Length == 2 ? GmapImporter.Build(parts[0], parts[1]) : 1;
                GetTree().Quit(code);
                return;
            }
            if (argument.StartsWith("--play="))
            {
                playPath = argument["--play=".Length..];
            }
            else if (argument.StartsWith("--shot="))
            {
                string[] parts = argument["--shot=".Length..].Split('@');
                _shotPath = parts[0];
                _shotDelay = parts.Length > 1 && float.TryParse(parts[1], out float seconds) ? seconds : 3f;
            }
            else if (argument == "--order-test")
            {
                _orderTest = true;
            }
            else if (argument == "--sim-test")
            {
                _simTest = true;
            }
            else if (argument == "--click-test")
            {
                _clickTest = true;
            }
            else if (argument == "--gather-test")
            {
                _gatherTest = true;
            }
            else if (argument == "--train-test")
            {
                _trainTest = true;
            }
            else if (argument == "--path-probe")
            {
                _pathProbe = true;
            }
            else if (argument == "--lumber-test")
            {
                _lumberTest = true;
            }
            else if (argument == "--build-test")
            {
                _buildTest = true;
            }
            else if (argument == "--fast")
            {
                _fast = true;
            }
            else if (argument.StartsWith("--time=") && float.TryParse(argument["--time=".Length..], out float scale))
            {
                _timeScale = Mathf.Clamp(scale, 0.02f, 1f);
            }
            else if (argument == "--housekeeping")
            {
                _housekeeping = true;
            }
            else if (argument.StartsWith("--gold=") && int.TryParse(argument["--gold=".Length..], out int gold))
            {
                _startGold = gold;
            }
            else if (argument.StartsWith("--lumber=") && int.TryParse(argument["--lumber=".Length..], out int lumber))
            {
                _startLumber = lumber;
            }
            else if (argument.StartsWith("--move-to="))
            {
                string[] parts = argument["--move-to=".Length..].Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], out float mx) && float.TryParse(parts[1], out float my))
                {
                    _moveTo = new Vector2(mx, my);
                }
            }
            else if (argument.StartsWith("--cam=") && int.TryParse(argument["--cam=".Length..], out int preset))
            {
                _cameraPreset = preset;
            }
            else if (argument.StartsWith("--focus="))
            {
                string[] parts = argument["--focus=".Length..].Split(',');
                if (parts.Length == 2 && float.TryParse(parts[0], out float mx) && float.TryParse(parts[1], out float my))
                {
                    _focusMapPoint = new Vector2(mx, my);
                }
            }
        }

        if (_simTest)
        {
            int code = BattleSimTest.Run(playPath);
            GetTree().Quit(code);
            return;
        }

        if (!string.IsNullOrEmpty(playPath))
        {
            StartGame(playPath);
        }
        else
        {
            ShowMapList();
        }
    }

    public void ShowMapList()
    {
        Clear();

        var screen = new MapListScreen { Name = "MapList" };
        screen.OnPick += name => StartGame(Assets.WorkspaceDir + "/" + name);
        AddChild(screen);
        _current = screen;
    }

    public void StartGame(string gmapPath)
    {
        string full = gmapPath;
        if (!System.IO.Path.IsPathRooted(full))
        {
            full = Assets.WorkspaceDir + "/" + full;
        }

        MapSource map = MapSource.Open(full);
        if (map == null)
        {
            GD.PushError($"[GAME] 打不开 {full}");
            ShowMapList();
            return;
        }

        Clear();

        var game = new GameScreen { Name = "Game" };
        game.TimeScaleHint = _fast ? Mathf.Min(0.15f, _timeScale) : _timeScale;
        game.StartGold = _startGold;
        game.StartLumber = _startLumber;
        game.Setup(map, ShowMapList);
        if (_cameraPreset >= 0) game.SetCameraPreset(_cameraPreset);
        if (_focusMapPoint.HasValue) game.FocusMapPoint(_focusMapPoint.Value.X, _focusMapPoint.Value.Y);
        AddChild(game);
        if (_orderTest) game.ScriptedMoveTest();
        if (_clickTest) game.RunClickTest();
        if (_gatherTest) game.ScriptedGatherTest();
        if (_trainTest) game.ScriptedTrainTest();
        if (_moveTo.HasValue) game.ScriptedMoveTo(_moveTo.Value.X, _moveTo.Value.Y);
        if (_lumberTest) game.ScriptedLumberTest();
        if (_buildTest) game.ScriptedBuildTest();
        if (_housekeeping) game.RunHousekeeping();
        if (_pathProbe)
        {
            game.PathProbe();
            GetTree().Quit();
            return;
        }
        _current = game;
        _elapsed = 0f;
        _shotTaken = false;
    }

    private void Clear()
    {
        if (_current != null)
        {
            _current.QueueFree();
            _current = null;
        }
    }

    public override void _Process(double delta)
    {
        if (string.IsNullOrEmpty(_shotPath) || _shotTaken || _shotDelay < 0f) return;

        _elapsed += (float)delta;
        if (_elapsed < _shotDelay) return;

        _shotTaken = true;
        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(_shotPath);
        GD.Print($"[SHOT] {_shotPath} -> {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}