// Services/HintRockSystem.cs
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WhispersOfTheLamp.Services
{
    public sealed class HintRockSystem
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly string _locationName;

        private readonly HashSet<Vector2> _hintTiles = new HashSet<Vector2>();
        private const string PropName = "HintRock";

        private const string HintMessage = "De l’aube jusqu’à la nuit, suis le ciel dans sa course, et le passage s’ouvrira. ";

        public HintRockSystem(IModHelper helper, IMonitor monitor, string locationName)
        {
            _helper = helper;
            _monitor = monitor;
            _locationName = locationName;
        }

        public void Enable()
        {
            _helper.Events.Player.Warped += OnWarped_ScanHints;
            _helper.Events.Input.ButtonPressed += OnButtonPressed_ShowHint;
            _helper.Events.Display.RenderedWorld += OnRenderedWorld_ChangeCursor;
        }

        private void OnWarped_ScanHints(object? sender, WarpedEventArgs e)
        {
            if (e.NewLocation.Name != _locationName) return;
            _hintTiles.Clear();

            foreach (var layerName in new[] { "Back", "Buildings", "Front" })
            {
                var layer = e.NewLocation.Map.GetLayer(layerName);
                if (layer is null) continue;

                for (var y = 0; y < layer.LayerHeight; y++)
                for (var x = 0; x < layer.LayerWidth; x++)
                {
                    var t = layer.Tiles[x, y];
                    if (t == null) continue;

                    var hasProp =
                        (t.Properties?.ContainsKey(PropName) == true) ||
                        (t.TileIndexProperties?.ContainsKey(PropName) == true);

                    if (hasProp)
                        _hintTiles.Add(new Vector2(x, y));
                }
            }

            _monitor.Log($"Found {_hintTiles.Count} hint rocks.", LogLevel.Debug);
        }

        private void OnButtonPressed_ShowHint(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation?.Name != _locationName) return;
            if (e.Button != SButton.MouseRight && e.Button != SButton.ControllerA) return;

            var tile = _helper.Input.GetCursorPosition().GrabTile;
            if (!_hintTiles.Contains(tile)) return;
            Game1.playSound("stoneCrack");
            Game1.drawLetterMessage(HintMessage);
        }

        private void OnRenderedWorld_ChangeCursor(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation?.Name != _locationName)
                return;

            var tile = _helper.Input.GetCursorPosition().Tile;
            if (_hintTiles.Contains(tile))
                Game1.mouseCursor = Game1.cursor_talk;
        }
    }
}