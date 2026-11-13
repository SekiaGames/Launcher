namespace OpenWC3.Game.Generated.Fdf;

internal static class UI_FrameDef_Glue_MapPreferenceBox_fdf
{
    internal const string Text =
@"// -- INCLUDE FILES ---------------------------------------------

IncludeFile ""UI\FrameDef\Glue\StandardTemplates.fdf"",

// -- LOCAL TEMPLATES -------------------------------------------

// -- FRAMES ----------------------------------------------------

Frame ""CONTROL"" ""MapPreferenceBox"" {

    ControlBackdrop ""MapPreferenceBoxBackdrop"",
    Frame ""BACKDROP"" ""MapPreferenceBoxBackdrop"" INHERITS ""StandardEditBoxBackdropTemplate"" {
    }

    Frame ""SCROLLBAR"" ""MapPreferenceScrollBar"" INHERITS WITHCHILDREN ""StandardScrollBarTemplate"" {
        SetPoint TOPRIGHT, ""MapPreferenceBox"", TOPRIGHT, 0.0, 0.0,
    }
}
";
}
