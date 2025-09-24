// Services/LocationManager.cs

using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Linq;

namespace WhispersOfTheLamp.Services
{
    public class LocationManager
    {
        private readonly IMonitor Monitor;

        public LocationManager(IMonitor monitor)
        {
            Monitor = monitor;
        }

        public void OnSaveLoaded_AddLocation(object? sender, SaveLoadedEventArgs e)
        {
            if (Game1.locations.Any(l => l.Name.Equals(ModConfig.CavernName)))
                return;

            var loc = new GameLocation(ModConfig.CavernAsset, ModConfig.CavernName);
            Game1.locations.Add(loc);
            Monitor.Log($"Added custom location '{ModConfig.CavernName}'.", LogLevel.Info);

            loc = new GameLocation("Maps/Whispers_SecondCavern", "Whispers_SecondCavern");
            Game1.locations.Add(loc);
            Monitor.Log("Added custom location 'Second Cavern'.", LogLevel.Info);
        }
    }
}