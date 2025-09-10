using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework;
using xTile;
using xTile.Dimensions;
using xTile.Tiles;
using Rectangle = Microsoft.Xna.Framework.Rectangle;


namespace WhispersOfTheLamp;

public sealed class ModEntry : Mod
{
    private const string MailId = "Gunther_Lamp";
    private const string LampQualifiedId = "(O)Adil.WhispersOfTheLamp.Items_Old_Lamp";
    private static Rectangle _pillarsBox = new Rectangle(26, 139, 9, 7);

    private const string CavernAsset = "Maps/Whispers_DesertCavern";
    private const string CavernName = "Whispers_DesertCavern";

    public override void Entry(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded_AddLocation;
    }

    private void OnSaveLoaded_AddLocation(object? sender, SaveLoadedEventArgs e)
    {
        if (Game1.locations.Any(l => l.Name.Equals(CavernName)))
            return;

        // create location directly from map asset path
        var loc = new GameLocation(CavernAsset, CavernName);

        Game1.locations.Add(loc);
        Monitor.Log($"Added custom location '{CavernName}'.", LogLevel.Info);
    }

    private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
    {
        if (!Context.IsWorldReady) return;
        if (e.Button != SButton.MouseRight && e.Button != SButton.ControllerA) return;
        if (Game1.currentLocation?.Name != "Desert") return;

        var held = Game1.player.ActiveObject;
        if (held is not { QualifiedItemId: LampQualifiedId }) return;

        var farmerTile = new Point(
            (int)(Game1.player.Position.X / 64f),
            (int)(Game1.player.Position.Y / 64f)
        );

        if (!_pillarsBox.Contains(farmerTile)) return;

        Game1.playSound("wand");
        Game1.addHUDMessage(HUDMessage.ForCornerTextbox("The Lamp hums with ancient power..."));

        Game1.warpFarmer(CavernName, 8, 8, flip: false);

        Monitor.Log("Teleported via lamp ritual", LogLevel.Info);
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
            e.Edit(EditMail);
    }

    private static void EditMail(IAssetData asset)
    {
        var data = asset.AsDictionary<string, string>().Data;

        data[MailId] =
            "Dear @,^^While going through my late father's belongings, I stumbled upon something unusual.^" +
            "It's an old lamp... rusted, heavy, and clearly very, very old.^^I have no record of it in our archives, " +
            "nor do I know what purpose it once served.^^Still, something about it feels... different, as though it " +
            "carries a story waiting to be uncovered.^Since you've been such a dedicated friend to the museum, I thought " +
            "you might enjoy having it.^^Perhaps you'll uncover what I cannot.^Consider it a small token of my gratitude " +
            "for all the wonders you've already shared with Pelican Town.^^Take care of it, @. Whatever secrets this lamp holds, " +
            "they now belong to you.^^— Gunther " +
            $"%item id {LampQualifiedId} 1 %%";
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (Game1.player.mailReceived.Contains(MailId) ||
            Game1.player.mailbox.Contains(MailId)) return;
        Game1.addMail(MailId, false, true);
        Monitor.Log("Queued Gunther_Lamp letter for today's mailbox.", LogLevel.Info);
    }
}