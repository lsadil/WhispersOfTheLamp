// Services/DisplaySpotSystem.cs
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using xTile.Layers;
using xTile.Tiles;

namespace WhispersOfTheLamp.Services
{
    /// <summary>
    /// Lets the player place/take items on tiles marked with a TileData/Tile property (DisplaySpot = T).
    /// Persists items in GameLocation.modData and renders them in-world.
    /// </summary>
    public sealed class DisplaySpotSystem : IDisposable
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly string _locationName;

        private const string SpotProp = "DisplaySpot";
        private const string DataKeyPrefix = "Adil.WOTL:display";

        private readonly HashSet<Vector2> _displaySpots = new HashSet<Vector2>();
        private readonly Dictionary<Vector2, Item> _displayed = new Dictionary<Vector2, Item>();

        private bool _enabled;

        public DisplaySpotSystem(IModHelper helper, IMonitor monitor, string locationName)
        {
            _helper = helper;
            _monitor = monitor;
            _locationName = locationName;
        }

        public void Enable()
        {
            if (_enabled) return;
            _enabled = true;

            _helper.Events.Player.Warped += OnWarped_RebuildSpotsAndLoad;
            _helper.Events.Input.ButtonPressed += OnButtonPressed_TryPlaceOrTake;
            _helper.Events.Display.RenderedWorld += OnRenderedWorld_DrawPlacedItems;
        }

        public void Dispose()
        {
            if (!_enabled) return;
            _enabled = false;

            _helper.Events.Player.Warped -= OnWarped_RebuildSpotsAndLoad;
            _helper.Events.Input.ButtonPressed -= OnButtonPressed_TryPlaceOrTake;
            _helper.Events.Display.RenderedWorld -= OnRenderedWorld_DrawPlacedItems;

            _displaySpots.Clear();
            _displayed.Clear();
        }

        // -- events --

        private void OnWarped_RebuildSpotsAndLoad(object? sender, WarpedEventArgs e)
        {
            if (e.NewLocation?.Name != _locationName)
                return;

            _displaySpots.Clear();
            _displayed.Clear();

            // scan common layers for DisplaySpot = T
            foreach (var layerName in new[] { "Back", "Buildings", "Front" })
            {
                var layer = e.NewLocation.Map.GetLayer(layerName);
                if (layer is null) continue;

                for (var y = 0; y < layer.LayerHeight; y++)
                for (var x = 0; x < layer.LayerWidth; x++)
                {
                    var t = layer.Tiles[x, y];
                    if (t is null) continue;

                    var hasSpot =
                        (t.Properties?.TryGetValue(SpotProp, out var pv1) == true && !string.IsNullOrEmpty(pv1?.ToString())) ||
                        (t.TileIndexProperties?.TryGetValue(SpotProp, out var pv2) == true && !string.IsNullOrEmpty(pv2?.ToString()));

                    if (hasSpot)
                        _displaySpots.Add(new Vector2(x, y));
                }
            }

            // restore persisted items
            foreach (var spot in _displaySpots)
            {
                var key = $"{DataKeyPrefix}:{(int)spot.X},{(int)spot.Y}";
                if (!e.NewLocation.modData.TryGetValue(key, out string qid) || string.IsNullOrWhiteSpace(qid)) continue;
                var item = ItemRegistry.Create(qid, 1, allowNull: true);
                if (item != null)
                    _displayed[spot] = item;
            }

            _monitor.Log($"Display spots: {_displaySpots.Count}; restored items: {_displayed.Count}.", LogLevel.Trace);
        }

        private void OnButtonPressed_TryPlaceOrTake(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation?.Name != _locationName)
                return;

            if (e.Button != SButton.MouseRight && e.Button != SButton.ControllerA)
                return;

            var tile = _helper.Input.GetCursorPosition().GrabTile;
            if (!_displaySpots.Contains(tile))
                return;

            var loc = Game1.currentLocation;
            var key = $"{DataKeyPrefix}:{(int)tile.X},{(int)tile.Y}";
            var held = Game1.player.ActiveItem;

            // take back existing (only with empty hand)
            if (_displayed.TryGetValue(tile, out var existing))
            {
                if (held != null)
                {
                    Game1.showRedMessage("Hands full.");
                    Game1.playSound("cancel");
                    return;
                }

                if (!Game1.player.addItemToInventoryBool(existing.getOne()))
                {
                    Game1.showRedMessage("No inventory space.");
                    Game1.playSound("cancel");
                    return;
                }

                _displayed.Remove(tile);
                loc.modData.Remove(key);
                Game1.playSound("coin");
                return;
            }

            // place new (must be holding an item)
            if (held is null)
            {
                Game1.playSound("cancel");
                return;
            }

            // (optional) filter what can be placed
            // if (!CanPlace(held)) { Game1.showRedMessage("Can't place that here."); return; }

            var toPlace = held.getOne();
            _displayed[tile] = toPlace;
            loc.modData[key] = toPlace.QualifiedItemId;

            held.Stack--;
            if (held.Stack <= 0)
                Game1.player.removeItemFromInventory(held);

            Game1.playSound("stoneStep");
            Game1.addHUDMessage(new HUDMessage($"Placed {toPlace.DisplayName}", 2));
        }

        private void OnRenderedWorld_DrawPlacedItems(object? sender, RenderedWorldEventArgs e)
        {
            if (Game1.currentLocation?.Name != _locationName || _displayed.Count == 0)
                return;

            SpriteBatch b = e.SpriteBatch;

            foreach (var (tile, item) in _displayed)
            {
                var world = new Vector2(tile.X * 64f, tile.Y * 64f);
                var screen = Game1.GlobalToLocal(Game1.viewport, world + new Vector2(32f, 24f));
                item.drawInMenu(b, screen, 0.75f, 1f, 0.9f, StackDrawType.Hide, Color.White, false);
            }
        }

        // If you ever want to restrict placeable items, implement this:
        // private bool CanPlace(Item i) => i is StardewValley.Object;
    }
}