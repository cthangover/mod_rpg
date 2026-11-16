using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Rpg.Validation
{
    /// <summary>Shares rule explanations between map, object and part tools; native translation is used only inside the game.</summary>
    public static class RpgDataMessages
    {
        /// <summary>Returns an explanation independently of severity, resource identity and source attribution.</summary>
        public static string Explain(string code)
        {
            if (!GameEnvironment.IsRuntime)
            {
                return code;
            }

            return code switch
            {
                "quad_count" => TranslationServer.Translate("rpg/data_check/quad_count"),
                "quad_number" => TranslationServer.Translate("rpg/data_check/quad_number"),
                "quad_area" => TranslationServer.Translate("rpg/data_check/quad_area"),
                "quad_affine" => TranslationServer.Translate("rpg/data_check/quad_affine"),
                "vector" => TranslationServer.Translate("rpg/data_check/vector"),
                "size" => TranslationServer.Translate("rpg/data_check/size"),
                "scale" => TranslationServer.Translate("rpg/data_check/scale"),
                "crop" => TranslationServer.Translate("rpg/data_check/crop"),
                "material" => TranslationServer.Translate("rpg/data_check/material"),
                "image" => TranslationServer.Translate("rpg/data_check/image"),
                "sheet" => TranslationServer.Translate("rpg/data_check/sheet"),
                "part" => TranslationServer.Translate("rpg/data_check/part"),
                "duplicate_part" => TranslationServer.Translate("rpg/data_check/duplicate_part"),
                "enum" => TranslationServer.Translate("rpg/data_check/enum"),
                "projection" => TranslationServer.Translate("rpg/data_check/projection"),
                "map_size" => TranslationServer.Translate("rpg/data_check/map_size"),
                "map_rows" => TranslationServer.Translate("rpg/data_check/map_rows"),
                "reference" => TranslationServer.Translate("rpg/data_check/reference"),
                "layer_form" => TranslationServer.Translate("rpg/data_check/layer_form"),
                "json" => TranslationServer.Translate("rpg/data_check/json"),
                "budget" => TranslationServer.Translate("rpg/data_check/budget"),
                _ => TranslationServer.Translate("rpg/data_check/unknown"),
            };
        }
    }
}
