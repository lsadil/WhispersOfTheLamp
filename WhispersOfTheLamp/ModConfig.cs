using Microsoft.Xna.Framework;

namespace WhispersOfTheLamp
{
    public static class ModConfig
    {
        public const string MailId = "Gunther_Lamp";
        public const string LampQualifiedId = "(O)Adil.WhispersOfTheLamp.Items_Old_Lamp";
        public static readonly Rectangle PillarsBox = new Rectangle(26, 139, 9, 7);

        public const string CavernAsset = "Maps/Whispers_DesertCavern";
        public const string CavernName = "Whispers_DesertCavern";

        public const string SpawnTag = "Adil.WOTL:SpawnedLampStone";

        public static readonly string[] MainOreIds =
        {
            "(O)LampStone_Topaz",
            "(O)LampStone_Aquamarine",
            "(O)LampStone_Ruby",
            "(O)LampStone_Obsidian"
        };

        public const string FallbackRockId = "(O)670";
    }
}