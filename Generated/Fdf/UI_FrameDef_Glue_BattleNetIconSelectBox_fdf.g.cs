namespace OpenWC3.Game.Generated.Fdf;

internal static class UI_FrameDef_Glue_BattleNetIconSelectBox_fdf
{
    internal const string Text =
@"// -- INCLUDE FILES ---------------------------------------------

IncludeFile ""UI\FrameDef\Glue\BattleNetTemplates.fdf"",

// -- LOCAL TEMPLATES -------------------------------------------

// -- FRAMES ----------------------------------------------------

Frame ""CONTROL"" ""BattleNetIconSelectBox"" {
    ControlBackdrop ""MapListBoxBackdrop"",
    Frame ""BACKDROP"" ""MapListBoxBackdrop"" INHERITS ""StandardEditBoxBackdropTemplate"" {
    }

    Frame ""SCROLLBAR"" ""IconSelectScrollBar"" INHERITS WITHCHILDREN ""BattleNetScrollBarTemplate"" {
        SetPoint BOTTOMRIGHT, ""BattleNetIconSelectBox"", BOTTOMRIGHT, -0.009375, 0.009375,
    }
}
";
}
