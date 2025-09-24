// Services/SpawnManager.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using xTile.Layers;
using xTile.Tiles;

namespace WhispersOfTheLamp.Services
{
    public class SpawnManager
    {
        private readonly IMonitor Monitor;

        public SpawnManager(IMonitor monitor)
        {
            Monitor = monitor;
        }

        public void OnWarped_SpawnFromTileMarkers(object? sender, WarpedEventArgs e)
        {
            if (e.NewLocation?.Name != ModConfig.CavernName) return;
            SpawnFromTileMarkers(e.NewLocation);
        }

        public void SpawnFromTileMarkers(GameLocation loc)
        {
            // remove existing
            var toRemove = (from p in loc.objects.Pairs
                    where p.Value?.modData?.ContainsKey(ModConfig.SpawnTag) == true
                    select p.Key)
                .ToList();
            foreach (var t in toRemove)
                loc.objects.Remove(t);

            // find markers
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

                    var hasT =
                        (tile.Properties?.TryGetValue("PuzzleRock", out var pv1) == true &&
                         !string.IsNullOrEmpty(pv1?.ToString())) ||
                        (tile.TileIndexProperties?.TryGetValue("PuzzleRock", out var pv2) == true &&
                         !string.IsNullOrEmpty(pv2?.ToString()));

                    if (!hasT) continue;

                    var pos = new Vector2(x, y);
                    if (!loc.objects.ContainsKey(pos))
                        markers.Add(pos);
                }
            }

            if (markers.Count == 0)
            {
                Monitor.Log("No TileData markers with property 'PuzzleRock' found.", LogLevel.Info);
                return;
            }

            Utils.ShuffleUtils.Shuffle(markers);

            var placed = 0;

            var oreOrder = new List<string>(ModConfig.MainOreIds);
            Utils.ShuffleUtils.Shuffle(oreOrder);

            var mainCount = Math.Min(4, System.Math.Min(markers.Count, oreOrder.Count));
            for (var i = 0; i < mainCount; i++)
            {
                if (CreateAndPlace(loc, oreOrder[i], markers[i]))
                    placed++;
            }

            for (var i = mainCount; i < markers.Count; i++)
            {
                if (CreateAndPlace(loc, ModConfig.FallbackRockId, markers[i]))
                    placed++;
            }

            Monitor.Log($"Spawned {placed} nodes from TileData markers in '{loc.Name}'.", LogLevel.Info);
        }

        private static bool CreateAndPlace(GameLocation loc, string id, Vector2 tile)
        {
            var item = ItemRegistry.Create(id, 1, 0, allowNull: true);
            if (item is not StardewValley.Object obj) return false;

            obj.CanBeGrabbed = false; // must be mined
            obj.modData[ModConfig.SpawnTag] = "1"; // mark for cleanup next time
            loc.objects[tile] = obj;
            return true;
        }
    }
}