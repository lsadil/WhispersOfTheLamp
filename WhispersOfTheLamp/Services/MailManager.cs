// Services/MailManager.cs

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WhispersOfTheLamp.Services
{
    public class MailManager
    {
        private readonly IMonitor Monitor;

        public MailManager(IMonitor monitor)
        {
            Monitor = monitor;
        }

        public void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
                e.Edit(EditMail);
        }

        private static void EditMail(IAssetData asset)
        {
            var data = asset.AsDictionary<string, string>().Data;

            data[ModConfig.MailId] =
                "Dear @,^^While going through my late father's belongings, I stumbled upon something unusual.^" +
                "It's an old lamp... rusted, heavy, and clearly very, very old.^^I have no record of it in our archives, " +
                "nor do I know what purpose it once served.^^Still, something about it feels... different, as though it " +
                "carries a story waiting to be uncovered.^Since you've been such a dedicated friend to the museum, I thought " +
                "you might enjoy having it.^^Perhaps you'll uncover what I cannot.^Consider it a small token of my gratitude " +
                "for all the wonders you've already shared with Pelican Town.^^Take care of it, @. Whatever secrets this lamp holds, " +
                "they now belong to you.^^— Gunther " +
                $"%item id {ModConfig.LampQualifiedId} 1 %%";
        }

        public void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (Game1.player.mailReceived.Contains(ModConfig.MailId) ||
                Game1.player.mailbox.Contains(ModConfig.MailId)) return;
            Game1.addMail(ModConfig.MailId, false, true);
            Monitor.Log("Queued Gunther_Lamp letter for today's mailbox.", LogLevel.Info);
        }
    }
}