// Services/PedestalPuzzleSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using xTile.ObjectModel;
using xTile.Tiles;

namespace WhispersOfTheLamp.Services
{
    /// <summary>
    /// 4-pedestal gem puzzle:
    ///  • PedestalIndex = 0..3 markers (left→right order to validate).
    ///  • Required order: Topaz → Aquamarine → Ruby → Obsidian.
    ///  • Draws placed items centered/raised (override per tile with DrawOffsetX/Y).
    ///  • When solved: shows a warp circle sprite, plays departure FX on use, and teleports via a vanilla tile warp.
    ///
    /// Map markers:
    ///  • Four pedestals: TileData with PedestalIndex 0..3 (optional DrawOffsetX/Y).
    ///  • Warp pad tile: TileData (or tile property) with WarpPad = T on a visible layer (Back/Buildings/Front).
    ///    Optional: WarpTarget, WarpX, WarpY (defaults to "Whispers_SecondCavern" @ 8,8).
    /// </summary>
    public sealed class PedestalPuzzleSystem : IDisposable
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private readonly string _locationName;

        // ======= CONFIG =======
        private static readonly string[] RequiredOrder =
        {
            "(O)68", // Topaz
            "(O)62", // Aquamarine
            "(O)64", // Ruby
            "(O)575", // Obsidian
        };

        private static readonly Vector2 DefaultDrawOffset = new Vector2(0f, -72f);

        // Warp circle PNG (provided via CP; see instructions)
        private const string WarpCircleAsset = "Adil.WOTL/warp_circle";

        // Unique id for the temporary sprite (TemporaryAnimatedSprite.id is int)
        private const int WarpSpriteId = 7004001;

        // ======= STATE =======
        private bool _enabled;
        private bool _solved;

        private Pedestal[] _slots = Array.Empty<Pedestal>();
        private WarpSpot? _warpSpot; // logical warp tile + destination

        // Departure FX state
        private bool _isWarping; // guards re-entrancy within the departure flow
        private string? _lastWarpSource; // to restore vanilla warp on the source map after manual warp
        // edge-trigger for departure sound
        private bool _wasOnPadLastTick = false;


        // persistence
        private const string SaveKeyPrefix = "Adil.WOTL:Pedestal:"; // + index -> QualifiedId
        private const string SolvedKey = "Adil.WOTL:Pedestal:Solved";

        private sealed record Pedestal(Vector2 Tile, int Index, Vector2 DrawOffset);

        private sealed record WarpSpot(Vector2 Tile, string Target, int X, int Y);

        public PedestalPuzzleSystem(IModHelper helper, IMonitor monitor, string locationName)
        {
            _helper = helper;
            _monitor = monitor;
            _locationName = locationName;
        }

        public void Enable()
        {
            if (_enabled) return;
            _enabled = true;

            _helper.Events.Player.Warped += OnWarped_Load;
            _helper.Events.Input.ButtonPressed += OnButtonPressed_InteractPedestal;
            _helper.Events.Display.RenderedWorld += OnRenderedWorld_DrawPlaced;

            // for departure FX (sound/sparkles BEFORE warp)
            _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked_DepartureFx;
        }

        public void Dispose()
        {
            if (!_enabled) return;
            _enabled = false;

            _helper.Events.Player.Warped -= OnWarped_Load;
            _helper.Events.Input.ButtonPressed -= OnButtonPressed_InteractPedestal;
            _helper.Events.Display.RenderedWorld -= OnRenderedWorld_DrawPlaced;
            _helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked_DepartureFx;

            _slots = Array.Empty<Pedestal>();
            _warpSpot = null;
            _solved = false;
            _isWarping = false;
            _lastWarpSource = null;
        }

        // ================= load & scan =================
        private void OnWarped_Load(object? sender, WarpedEventArgs e)
        {
            // Reset FX flags every time we enter any location
            _isWarping = false;
            _lastWarpSource = null;

            if (e.NewLocation.Name != _locationName)
                return;

            var pedestals = new List<Pedestal>();
            _warpSpot = null;

            foreach (var layerName in new[] { "Buildings", "Back", "Front" })
            {
                var layer = e.NewLocation.Map.GetLayer(layerName);
                if (layer is null) continue;

                for (var y = 0; y < layer.LayerHeight; y++)
                for (var x = 0; x < layer.LayerWidth; x++)
                {
                    var t = layer.Tiles[x, y];
                    if (t is null) continue;

                    if (TryGetIntProp(t, "PedestalIndex", out var idx) && idx is >= 0 and <= 3)
                    {
                        var off = new Vector2(
                            TryGetIntProp(t, "DrawOffsetX", out var dx) ? dx : (int)DefaultDrawOffset.X,
                            TryGetIntProp(t, "DrawOffsetY", out var dy) ? dy : (int)DefaultDrawOffset.Y
                        );
                        pedestals.Add(new Pedestal(new Vector2(x, y), idx, off));
                    }

                    if (_warpSpot is not null || !HasTruthyProp(t, "WarpPad")) continue;
                    var target = TryGetStringProp(t, "WarpTarget", out var s) ? s! : "Whispers_SecondCavern";
                    var wx = TryGetIntProp(t, "WarpX", out var tx) ? tx : 8;
                    var wy = TryGetIntProp(t, "WarpY", out var ty) ? ty : 8;
                    _warpSpot = new WarpSpot(new Vector2(x, y), target, wx, wy);
                }
            }

            pedestals.Sort((a, b) => a.Index.CompareTo(b.Index));
            _slots = pedestals.ToArray();

            _solved = e.NewLocation.modData.TryGetValue(SolvedKey, out var solvedStr) && solvedStr == "1";

            if (_warpSpot != null)
            {
                if (_solved)
                {
                    EnsureVanillaWarp(e.NewLocation);
                    ShowWarpCircleSprite(e.NewLocation);
                }
                else
                {
                    RemoveVanillaWarp(e.NewLocation);
                    HideWarpCircleSprite(e.NewLocation);
                }
            }

            _monitor.Log($"Pedestals={_slots.Length}, solved={_solved}, warp={(_warpSpot != null)}");
        }

        // ================= interaction (placing gems) =================
        private void OnButtonPressed_InteractPedestal(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.currentLocation?.Name != _locationName)
                return;

            if (e.Button != SButton.MouseRight && e.Button != SButton.ControllerA)
                return;

            var loc = Game1.currentLocation;
            var tile = _helper.Input.GetCursorPosition().GrabTile;

            // pedestals only
            var slotIndex = Array.FindIndex(_slots, s => s.Tile == tile);
            if (slotIndex < 0) return;

            if (_solved)
            {
                Game1.playSound("cancel");
                return;
            }

            var slotKey = SaveKeyPrefix + slotIndex;
            var held = Game1.player.ActiveItem;

            switch (held)
            {
                // take back with empty hand
                case null when loc.modData.TryGetValue(slotKey, out var existingQid) && !string.IsNullOrWhiteSpace(existingQid):
                {
                    var item = ItemRegistry.Create(existingQid, allowNull: true);
                    if (item != null && Game1.player.addItemToInventoryBool(item))
                    {
                        loc.modData.Remove(slotKey);
                        Game1.playSound("coin");
                    }
                    else
                    {
                        Game1.showRedMessage("No inventory space.");
                        Game1.playSound("cancel");
                    }

                    return;
                }
                // empty hand on empty pedestal
                case null:
                    Game1.playSound("cancel");
                    return;
            }

            var qid = held.QualifiedItemId;
            if (Array.IndexOf(RequiredOrder, qid) < 0)
            {
                Game1.showRedMessage("That gem doesn't fit here.");
                Game1.playSound("cancel");
                return;
            }

            loc.modData[slotKey] = qid;

            held.Stack--;
            if (held.Stack <= 0)
                Game1.player.removeItemFromInventory(held);

            Game1.playSound("stoneStep");

            // success?
            if (!IsSolved(loc)) return;
            _solved = true;
            loc.modData[SolvedKey] = "1";
            Game1.playSound("secret1");

            EnsureVanillaWarp(loc);
            ShowWarpCircleSprite(loc);
        }

        private bool IsSolved(GameLocation loc)
        {
            if (_slots.Length < 4) return false;
            for (var i = 0; i < 4; i++)
            {
                var key = SaveKeyPrefix + i;
                if (!loc.modData.TryGetValue(key, out var qid) || qid != RequiredOrder[i])
                    return false;
            }

            return true;
        }

        // ================= draw placed items (gem icons on pedestals) =================
        private void OnRenderedWorld_DrawPlaced(object? sender, RenderedWorldEventArgs e)
        {
            if (Game1.currentLocation?.Name != _locationName || _slots.Length == 0)
                return;

            var loc = Game1.currentLocation;
            var b = e.SpriteBatch;

            for (var i = 0; i < _slots.Length; i++)
            {
                var key = SaveKeyPrefix + i;
                if (!loc.modData.TryGetValue(key, out var qid) || string.IsNullOrWhiteSpace(qid))
                    continue;

                var item = ItemRegistry.Create(qid, allowNull: true);
                if (item is null) continue;

                var s = _slots[i];
                var world = s.Tile * 64f + s.DrawOffset; // tweak offsets via DrawOffsetX/Y in Tiled
                var screen = Game1.GlobalToLocal(Game1.viewport, world);

                item.drawInMenu(b, screen, 0.8f, 1f, 0.9f, StackDrawType.Hide, Color.White, false);
            }
        }

        // ================= vanilla tile warp management =================
        private void EnsureVanillaWarp(GameLocation loc)
        {
            if (_warpSpot is null) return;

            if (loc.warps.Any(w => w.X == (int)_warpSpot.Tile.X && w.Y == (int)_warpSpot.Tile.Y))
                return; // present

            var warp = new StardewValley.Warp(
                (int)_warpSpot.Tile.X,
                (int)_warpSpot.Tile.Y,
                _warpSpot.Target,
                _warpSpot.X,
                _warpSpot.Y,
                false
            );
            loc.warps.Add(warp);
        }

        private void RemoveVanillaWarp(GameLocation loc)
        {
            if (_warpSpot is null) return;
            for (var i = loc.warps.Count - 1; i >= 0; i--)
            {
                var w = loc.warps[i];
                if (w.X == (int)_warpSpot.Tile.X && w.Y == (int)_warpSpot.Tile.Y)
                    loc.warps.RemoveAt(i);
            }
        }

        // ================= warp circle PNG sprite =================
        private void ShowWarpCircleSprite(GameLocation loc)
        {
            if (_warpSpot is null) return;
            HideWarpCircleSprite(loc);

            var tex = Game1.content.Load<Texture2D>(WarpCircleAsset);

            // Warp pad tile’s top-left corner
            var basePos = new Vector2(_warpSpot.Tile.X * 64f, _warpSpot.Tile.Y * 64f);

            // You said the art should be 4×4 tiles (256 px), so center it on the pad:
            float scale = (64f * 4) / tex.Width; // 8f for a 32px PNG

            var pos = basePos + new Vector2(-64f, -64f); // shift so the pad is roughly the center of the 4×4 art

            var sprite = new TemporaryAnimatedSprite
            {
                texture = tex,
                sourceRect = new Rectangle(0, 0, tex.Width, tex.Height),
                interval = 9999f,
                animationLength = 1,
                totalNumberOfLoops = 0,
                position = pos,
                scale = scale,
                layerDepth = (basePos.Y + 64f) / 10000f + 0.0001f,
                id = WarpSpriteId
            };

            loc.temporarySprites.Add(sprite);
        }


        private static void HideWarpCircleSprite(GameLocation loc)
        {
            var list = loc.temporarySprites;
            if (list is null || list.Count == 0) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] != null && list[i].id == WarpSpriteId)
                    list.RemoveAt(i);
        }

        // ================= departure FX BEFORE warp =================
        private void OnUpdateTicked_DepartureFx(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !_solved || _warpSpot is null) return;
            if (Game1.currentLocation?.Name != _locationName) return;

            // Are we on the warp tile this tick?
            var farmer = Game1.player;
            int tx = (int)(farmer.Position.X / 64f);
            int ty = (int)(farmer.Position.Y / 64f);
            bool onPadNow = (tx == (int)_warpSpot.Tile.X && ty == (int)_warpSpot.Tile.Y);

            // Play the sound only on the rising edge (we just stepped onto it)
            if (onPadNow && !_wasOnPadLastTick)
                Game1.playSound("wand");

            _wasOnPadLastTick = onPadNow;
        }


        // ================= property helpers =================
        private static bool TryGetIntProp(Tile t, string name, out int value)
        {
            value = 0;
            if (t.Properties?.TryGetValue(name, out var pv) == true && int.TryParse(pv?.ToString(), out value)) return true;
            return t.TileIndexProperties?.TryGetValue(name, out pv) == true && int.TryParse(pv?.ToString(), out value);
        }

        private static bool TryGetStringProp(Tile t, string name, out string? value)
        {
            value = null;
            if (t.Properties?.TryGetValue(name, out var pv) != true &&
                t.TileIndexProperties?.TryGetValue(name, out pv) != true) return false;
            value = pv?.ToString();
            return true;
        }

        private static bool HasTruthyProp(Tile t, string name)
        {
            if (t.Properties?.TryGetValue(name, out var pv) == true || t.TileIndexProperties?.TryGetValue(name, out pv) == true)
                return !string.IsNullOrWhiteSpace(pv?.ToString());
            return false;
        }
    }
}