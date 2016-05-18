using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Q42.HueApi;
using Q42.HueApi.Interfaces;

namespace zecil.AmbiHueTv
{
    public class Hue
    {

        public static string AppName = "AmbiHueTv";
        public static string DeviceName = "pi";

        private ObservableCollection<string> _bridgeIps;
        private IEnumerable<Light> _allLights;
        private ObservableCollection<Light> _filteredLights;
        private ILocalHueClient _client;


        public async Task<string> FindFirstBridge()
        {

            _bridgeIps = new ObservableCollection<string>();
            IBridgeLocator locator = new HttpBridgeLocator();
            IEnumerable<string> bridgeIPs = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5));

            _bridgeIps.Clear();
            foreach (var ip in bridgeIPs)
            {
                _bridgeIps.Add(ip);
            }

            if (_bridgeIps.Count > 0)
            {
                return _bridgeIps[0];
            }

            return string.Empty;

        }

        public async Task RegisterIfNeeded()
        {
            _client = new LocalHueClient(Settings.DefaultBridgeIp);

            if (string.IsNullOrEmpty(Settings.TheAppKey))
            {
                Settings.TheAppKey = await _client.RegisterAsync(AppName, DeviceName);
            }
        }

        public async Task Initialize()
        {
            _client.Initialize(Settings.TheAppKey);
            _allLights = await _client.GetLightsAsync();

        }

        public void FilterLightsByNameContaining(string searchPhrase)
        {

            _filteredLights = new ObservableCollection<Light>();
            _filteredLights.Clear();
            foreach (var light in _allLights)
            {
                if (light.Name.Contains(searchPhrase))
                    _filteredLights.Add(light);
            }

        }

        public async Task TurnOnFilteredLights()
        {
            var modifiedLights = new List<string>(_filteredLights.Select(a => a.Id));
            var command = new LightCommand();

            command.TurnOn();
            command.Brightness = 200;
            await _client.SendCommandAsync(command, modifiedLights);

        }


        public async Task TurnOffFilteredLights()
        {
            var modifiedLights = new List<string>(_filteredLights.Select(a => a.Id));
            var command = new LightCommand();

            command.TurnOff();
            await _client.SendCommandAsync(command, modifiedLights);

        }

        public async Task<bool> ChangeFilteredLightsColor(byte red, byte green, byte blue)
        {
            if (IsSignificantChange(red, green, blue))
            {
                var modifiedLights = new List<string>(_filteredLights.Select(a => a.Id));
                var command = new LightCommand
                {
                    TransitionTime = TimeSpan.FromMilliseconds(50)
                };
                command.SetColor(red, green, blue);
                await _client.SendCommandAsync(command, modifiedLights);
                return true;
            }
            else
            {
                return false;
            }
        }

        private byte _lastRed;
        private byte _lastGreen;
        private byte _lastBlue;

        public bool IsSignificantChange(byte red, byte green, byte blue)
        {
            const int deltaThreshold = 30;

            if (_lastBlue == 0 && _lastGreen == 0 && _lastBlue == 0)
            {
                _lastRed = red;
                _lastGreen = green;
                _lastBlue = blue;
                return true;
            }

            if (Math.Abs((red - _lastRed) + (green - _lastGreen) + (blue - _lastBlue)) > deltaThreshold)
            {
                _lastRed = red;
                _lastGreen = green;
                _lastBlue = blue;
                return true;
            }
            else
            {
                return false; 
            }
        }
    }
}
