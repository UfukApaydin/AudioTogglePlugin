using BarRaider.SdTools;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace AudioTogglePlugin
{
    // 1. Move this OUTSIDE the AudioAction class to ensure it sends correctly
    public class DevicePayload
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    [PluginActionId("com.yourname.audiotoggle.action")]
    public class AudioAction : KeypadBase
    {
        private class PluginSettings
        {
            [JsonProperty(PropertyName = "deviceA")]
            public string DeviceA { get; set; }

            [JsonProperty(PropertyName = "deviceB")]
            public string DeviceB { get; set; }
        }

        private PluginSettings settings;
        private CoreAudioController audioController;
        private string lastKnownID = "";
        private int tickCounter = 0;

        public AudioAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            audioController = new CoreAudioController();

            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = new PluginSettings();
                SaveSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            CheckCurrentState();
        }

        public override void Dispose()
        {
            audioController.Dispose();
        }

        public override void KeyPressed(KeyPayload payload)
        {
            ToggleAudio();
        }

        public override void KeyReleased(KeyPayload payload) { }
        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override async void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
            lastKnownID = "";
            CheckCurrentState();

            // Trigger list update when settings change
            await SendDeviceListToPropertyInspector();
        }

        public override async void OnTick()
        {
            tickCounter++;

            // Check audio state
            if (tickCounter % 20 == 0)
            {
                CheckCurrentState();
            }

            // FORCE UPDATE: Send the list every 1 second (approx 10 ticks)
            // This ensures the UI gets it eventually.
            if (tickCounter % 10 == 0)
            {
                await SendDeviceListToPropertyInspector();
            }
        }

        private async Task SendDeviceListToPropertyInspector()
        {
            try
            {
                var devices = await audioController.GetPlaybackDevicesAsync(DeviceState.Active);

                var simpleList = new List<DevicePayload>();
                foreach (var d in devices)
                {
                    simpleList.Add(new DevicePayload { Id = d.Id.ToString(), Name = d.FullName });
                }

                await Connection.SendToPropertyInspectorAsync(JObject.FromObject(new
                {
                    event_name = "GetDeviceList",
                    items = simpleList
                }));
            }
            catch { }
        }

        private async void CheckCurrentState()
        {
            try
            {
                var currentDefault = audioController.DefaultPlaybackDevice;
                if (currentDefault == null) return;
                if (currentDefault.Id.ToString() == lastKnownID) return;

                lastKnownID = currentDefault.Id.ToString();

                if (lastKnownID == settings.DeviceA) await Connection.SetStateAsync(0);
                else if (lastKnownID == settings.DeviceB) await Connection.SetStateAsync(1);
            }
            catch { }
        }

        private void SaveSettings()
        {
            Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private async void ToggleAudio()
        {
            if (string.IsNullOrEmpty(settings.DeviceA) || string.IsNullOrEmpty(settings.DeviceB))
            {
                await Connection.ShowAlert();
                return;
            }

            Guid guidA;
            Guid guidB;

            if (!Guid.TryParse(settings.DeviceA, out guidA) || !Guid.TryParse(settings.DeviceB, out guidB))
            {
                await Connection.ShowAlert();
                return;
            }

            try
            {
                var devA = await audioController.GetDeviceAsync(guidA);
                var devB = await audioController.GetDeviceAsync(guidB);

                if (devA == null || devB == null)
                {
                    await Connection.ShowAlert();
                    return;
                }

                if (devA.IsDefaultDevice)
                {
                    await devB.SetAsDefaultAsync();
                    await devB.SetAsDefaultCommunicationsAsync();
                }
                else
                {
                    await devA.SetAsDefaultAsync();
                    await devA.SetAsDefaultCommunicationsAsync();
                }

                await Connection.ShowOk();
            }
            catch
            {
                await Connection.ShowAlert();
            }
        }
    }
}