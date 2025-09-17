using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework;
using xTile.Layers;
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

    private const string SpawnTag = "Adil.WOTL:SpawnedLampStone";

    private static readonly string[] MainOreIds =
    {
        "(O)LampStone_Topaz",
        "(O)LampStone_Aquamarine",
        "(O)LampStone_Ruby",
        "(O)LampStone_Obsidian"
    };

    private const string FallbackRockId = "(O)670";

    public override void Entry(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += OnAssetRequested;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded_AddLocation;
        helper.Events.Player.Warped += OnWarped_SpawnFromTileMarkers;
    }

    private void OnWarped_SpawnFromTileMarkers(object? sender, WarpedEventArgs e)
    {
        if (e.NewLocation?.Name != CavernName) return;
        SpawnFromTileMarkers(e.NewLocation);
    }

    private void SpawnFromTileMarkers(GameLocation loc)
    {
        // 0) clean previous spawns
        var toRemove = (from p in loc.objects.Pairs where p.Value?.modData?.ContainsKey(SpawnTag) == true select p.Key)
            .ToList();
        foreach (var t in toRemove)
            loc.objects.Remove(t);

        // 1) gather all marker tiles (property name **T** on Back/Buildings/Front)
        var markers = new List<Vector2>();
        foreach (var layerName in new[] { "Back", "Buildings", "Front" })
        {
            var layer = loc.Map.GetLayer(layerName);
            if (layer is null) continue;

            for (var y = 0; y < layer.LayerHeight; y++)
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                var tile = layer.Tiles[x, y];
                if (tile is null) continue;

                // presence of property "PuzzleRock" on either property collection counts
                var hasT =
                    (tile.Properties?.TryGetValue("PuzzleRock", out var pv1) == true &&
                     !string.IsNullOrEmpty(pv1?.ToString())) ||
                    (tile.TileIndexProperties?.TryGetValue("PuzzleRock", out var pv2) == true &&
                     !string.IsNullOrEmpty(pv2?.ToString()));

                if (!hasT) continue;

                var pos = new Vector2(x, y);
                if (!loc.objects.ContainsKey(pos)) // don't overwrite existing objects
                    markers.Add(pos);
            }
        }

        if (markers.Count == 0)
        {
            Monitor.Log("No TileData markers with property 'PuzzleRock' found.", LogLevel.Info);
            return;
        }

        // 2) shuffle the marker list
        Shuffle(markers);

        var placed = 0;

        // 3) first up to 4 tiles → place the four main ores in random order
        var oreOrder = new List<string>(MainOreIds);
        Shuffle(oreOrder);

        var mainCount = Math.Min(4, Math.Min(markers.Count, oreOrder.Count));
        for (var i = 0; i < mainCount; i++)
        {
            if (CreateAndPlace(loc, oreOrder[i], markers[i]))
                placed++;
        }

        // 4) remaining tiles → place fallback rock (670)
        for (var i = mainCount; i < markers.Count; i++)
        {
            if (CreateAndPlace(loc, FallbackRockId, markers[i]))
                placed++;
        }

        Monitor.Log($"Spawned {placed} nodes from TileData markers in '{loc.Name}'.", LogLevel.Info);
    }

    private static bool CreateAndPlace(GameLocation loc, string id, Vector2 tile)
    {
        var item = ItemRegistry.Create(id, 1, 0, allowNull: true);
        if (item is not StardewValley.Object obj) return false;

        obj.CanBeGrabbed = false; // must be mined
        obj.modData[SpawnTag] = "1"; // mark for cleanup next time
        loc.objects[tile] = obj;
        return true;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Game1.random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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