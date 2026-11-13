namespace OpenWC3.Game.Generated.Fdf;

internal static class UI_FrameDef_Glue_CheckListBox_fdf
{
    internal const string Text =
@"// -- INCLUDE FILES ---------------------------------------------

IncludeFile ""UI\FrameDef\Glue\StandardTemplates.fdf"",

// -- LOCAL TEMPLATES -------------------------------------------

// -- FRAMES ----------------------------------------------------

Frame ""CONTROL"" ""CheckListBox"" {

    ControlBackdrop ""CheckListBoxBackdrop"",
    Frame ""BACKDROP"" ""CheckListBoxBackdrop"" INHERITS ""StandardEditBoxBackdropTemplate"" {
    }

    Frame ""SCROLLBAR"" ""CheckListBoxScrollBar"" INHERITS WITHCHILDREN ""StandardScrollBarTemplate"" {
        SetPoint TOPRIGHT, ""CheckListBox"", TOPRIGHT, 0.0, 0.0,
    }
}
";
}
