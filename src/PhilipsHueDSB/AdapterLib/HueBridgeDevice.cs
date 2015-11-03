﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AdapterLib
{
    internal class AdapterIcon : BridgeRT.IAdapterIcon
    {
        byte[] _image = null;
        public AdapterIcon(string url)
        {
            Url = url;
        }
        public string MimeType
        {
            get { return "image/png"; }
        }

        public string Url
        {
            get; private set;
        }

        public byte[] GetImage()
        {
            return _image;
        }
    }
    internal class HueBridgeDevice : AdapterDevice
    {
        private Q42.HueApi.HueClient _client;
        private List<HueBulbDevice> _devices = new List<HueBulbDevice>();
        HueBridgeDescription _description;

        public HueBridgeDevice(Q42.HueApi.HueClient client, HueBridgeDescription desc) : base(
            desc.FriendlyName, desc.Manufacturer, desc.ModelName, "", desc.SerialNumber, desc.ModelDescription)
        {
            _client = client;
            _description = desc;

            var EnableJoinMethod = new AdapterMethod("Link", "Puts the adapter into join mode", 0);
            EnableJoinMethod.InvokeAction = Link;
            Methods.Add(EnableJoinMethod);

            var UpdateMethod = new AdapterMethod("Update", "Looks for any removed or added lights", 0);
            UpdateMethod.InvokeAction = UpdateDeviceList;
            Methods.Add(UpdateMethod);

            var container = ApplicationData.Current.LocalSettings.CreateContainer("RegisteredHueBridges", ApplicationDataCreateDisposition.Always);
            if(container.Values.ContainsKey(desc.SerialNumber))
            {
                var key = container.Values[desc.SerialNumber] as string;
                if (key != null)
                {
                    (client as Q42.HueApi.LocalHueClient)?.Initialize(key);
                    UpdateDeviceList();
                }
            }
            if (desc.IconUri != null)
                Icon = new AdapterIcon(desc.IconUri.OriginalString);
        }

        private async void Link()
        {
            try {
                var c = _client as Q42.HueApi.LocalHueClient;
                var applicationKey = await c.RegisterAsync(GetApplicationName(), "minwinpc").ConfigureAwait(false);
                if (applicationKey != null)
                {
                    var container = ApplicationData.Current.LocalSettings.CreateContainer("RegisteredHueBridges", ApplicationDataCreateDisposition.Always);
                    container.Values[_description.SerialNumber] = applicationKey;
                    c.Initialize(applicationKey);
                    UpdateDeviceList();
                }
            }
            catch
            {

            }
        }

        private static string GetApplicationName()
        {
            Windows.ApplicationModel.Package package = Windows.ApplicationModel.Package.Current;
            Windows.ApplicationModel.PackageId packageId = package.Id;
            return package.Id.Name;
        }

        private async void UpdateDeviceList()
        {
            try
            {
                var lights = (await _client.GetLightsAsync()).ToList();
                //Report all lost lights
                foreach (var device in _devices.ToArray())
                {
                    if (!lights.Where(l => l.UniqueId == device.Light.UniqueId).Any())
                    {
                        //Light no longer available
                        _devices.Remove(device);
                        NotifyDeviceRemoval?.Invoke(this, device);
                    }
                }
                //Report all newly found lights
                foreach (var light in lights)
                {
                    if (!_devices.Where(l => l.Light.UniqueId == light.UniqueId).Any())
                    {
                        var device = new HueBulbDevice(_client, light);
                        _devices.Add(device);
                        NotifyDeviceArrival?.Invoke(this, device);
                    }
                }
            }
            catch { }
        }

        public event EventHandler<HueBulbDevice> NotifyDeviceArrival;
        public event EventHandler<HueBulbDevice> NotifyDeviceRemoval;
    }
}