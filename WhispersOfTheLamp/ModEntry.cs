using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WhispersOfTheLamp;

public sealed class ModEntry : Mod
{
    private const string MailId = "Gunther_Lamp";

    // Replace this with the real qualified ID from `list_items Old Lamp`
    private const string LampQualifiedId = "(O)Adil.WhispersOfTheLamp.Items_Old_Lamp";

    public override void Entry(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
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
            "for all the wonders you've already shared with Pelican Town.^Take care of it, @. Whatever secrets this lamp holds, " +
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
