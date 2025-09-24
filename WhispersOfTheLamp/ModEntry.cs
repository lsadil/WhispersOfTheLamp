// ModEntry.cs

using StardewModdingAPI;
using WhispersOfTheLamp.Services;

namespace WhispersOfTheLamp
{
    public sealed class ModEntry : Mod
    {
        private SpawnManager _spawnManager = null!;
        private LocationManager _locationManager = null!;
        private InputHandler _inputHandler = null!;
        private MailManager _mailManager = null!;
        private DisplaySpotSystem? _displaySpots;

        public override void Entry(IModHelper helper)
        {

            _spawnManager = new SpawnManager(Monitor);
            _locationManager = new LocationManager(Monitor);
            _inputHandler = new InputHandler(Monitor);
            _mailManager = new MailManager(Monitor);

            helper.Events.Player.Warped += _spawnManager.OnWarped_SpawnFromTileMarkers;
            helper.Events.GameLoop.SaveLoaded += _locationManager.OnSaveLoaded_AddLocation;
            helper.Events.Input.ButtonReleased += _inputHandler.OnButtonReleased;
            helper.Events.Content.AssetRequested += _mailManager.OnAssetRequested;
            helper.Events.GameLoop.DayStarted += _mailManager.OnDayStarted;

            _displaySpots = new DisplaySpotSystem(helper, Monitor, ModConfig.CavernName);
            _displaySpots.Enable();
            helper.Events.GameLoop.ReturnedToTitle += (_, __) => _displaySpots?.Dispose();
        }
    }
}