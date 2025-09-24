// Services/InputHandler.cs
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace WhispersOfTheLamp.Services
{
    public class InputHandler
    {
        private readonly IMonitor Monitor;

        public InputHandler(IMonitor monitor)
        {
            Monitor = monitor;
        }

        public void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (e.Button != SButton.MouseRight && e.Button != SButton.ControllerA) return;
            if (Game1.currentLocation?.Name != "Desert") return;

            var held = Game1.player.ActiveObject;
            if (held is not { QualifiedItemId: ModConfig.LampQualifiedId }) return;

            var farmerTile = new Point(
                (int)(Game1.player.Position.X / 64f),
                (int)(Game1.player.Position.Y / 64f)
            );

            if (!ModConfig.PillarsBox.Contains(farmerTile)) return;

            Game1.playSound("wand");
            Game1.addHUDMessage(HUDMessage.ForCornerTextbox("The Lamp hums with ancient power..."));

            Game1.warpFarmer(ModConfig.CavernName, 8, 8, flip: false);

            Monitor.Log("Teleported via lamp ritual", LogLevel.Info);
        }
    }
}