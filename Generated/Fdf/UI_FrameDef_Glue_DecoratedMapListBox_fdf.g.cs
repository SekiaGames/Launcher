namespace OpenWC3.Game.Generated.Fdf;

internal static class UI_FrameDef_Glue_DecoratedMapListBox_fdf
{
    internal const string Text =
@"// -- INCLUDE FILES ---------------------------------------------

IncludeFile ""UI\FrameDef\UI\EscMenuTemplates.fdf"",

// -- LOCAL TEMPLATES -------------------------------------------

// -- FRAMES ----------------------------------------------------

Frame ""CONTROL"" ""DecoratedMapListBox"" {

    ControlBackdrop ""MapListBoxBackdrop"",
    Frame ""BACKDROP"" ""MapListBoxBackdrop"" INHERITS ""EscMenuEditBoxBackdropTemplate"" {
    }

    Frame ""SCROLLBAR"" ""MapListScrollBar"" INHERITS WITHCHILDREN ""EscMenuScrollBarTemplate"" {
        SetPoint TOPRIGHT, ""DecoratedMapListBox"", TOPRIGHT, 0.0, 0.0,
    }
}
";
}
