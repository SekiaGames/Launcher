namespace OpenWC3.Game.Generated.Fdf;

internal static class UI_FrameDef_Glue_BattleNetNewsBox_fdf
{
    internal const string Text =
@"// -- INCLUDE FILES ---------------------------------------------

IncludeFile ""UI\FrameDef\Glue\StandardTemplates.fdf"",

// -- LOCAL TEMPLATES -------------------------------------------

// -- FRAMES ----------------------------------------------------

Frame ""CONTROL"" ""BattleNetNewsBox"" {
    Frame ""SCROLLBAR"" ""BattleNetNewsBoxScrollBar"" INHERITS WITHCHILDREN ""StandardScrollBarTemplate"" {
        SetPoint TOPRIGHT, ""BattleNetNewsBox"", TOPRIGHT, 0.0, 0.0,
    }
}
";
}
