using Makaretu.Dns;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace WakeVUp
{
    class HueBridge
    {
        private string bridgeIP;
        private string HUE_API => $"http://{bridgeIP}/api/<api_key>";

        private int bedroomID;

        private CancellationTokenSource wakeupCancellationTokenSource;

        public HueBridge()
        {
            bedroomID = -1;

            FindHueBridge();
        }

        private async void FindHueBridge()
		{
            using (var client = new HttpClient())
			{
                try
                {
                    var response = await client.GetAsync(new Uri("https://discovery.meethue.com/"));
                    if (response.IsSuccessStatusCode)
                    {
                        var bridges = JsonConvert.DeserializeObject<Dictionary<string, string>[]>(await response.Content.ReadAsStringAsync());
                        bridgeIP = bridges[0]["internalipaddress"];

                        FindBedroomID();
                    }
                }
                catch (Exception) { }
			}
        }

        private async void FindBedroomID()
        {
            using(var client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(new Uri($"{HUE_API}/groups"));
                    if (response.IsSuccessStatusCode)
                    {
                        var groups = JsonConvert.DeserializeObject<Dictionary<int, Group>>(await response.Content.ReadAsStringAsync());
                        bedroomID = groups.Where(pair => pair.Value.name == "Bedroom").FirstOrDefault().Key;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    Debug.WriteLine(e.StackTrace);
                }
            }
        }

        public async void BedroomDoorOpened()
        {
            if(wakeupCancellationTokenSource != null)
            {
                wakeupCancellationTokenSource.Cancel();
                wakeupCancellationTokenSource = null;
                return;
            }

            var action = new Action { on = true };
            if(DateTime.Now.Hour >= 22 || DateTime.Now.Hour < 6)
            {
                action.ct = 500;
                action.bri = 56;
            }
            else if(DateTime.Now.Hour >= 18)
            {
                action.ct = 475;
                action.bri = 100;
            }
            else
            {
                action.ct = 366;
                action.bri = 153;
            }

            using (var client = new HttpClient())
            {
                var response = await client.PutAsync(new Uri($"{HUE_API}/groups/{bedroomID}/action"), new HttpStringContent(JsonConvert.SerializeObject(action)));
            }
        }

        public async void BedroomDoorClosed()
        {
            if (DateTime.Now.Hour >= 22 || DateTime.Now.Hour < 6) return;

            var action = new Action { on = false };

            using (var client = new HttpClient())
            {
                var response = await client.PutAsync(new Uri($"{HUE_API}/groups/{bedroomID}/action"), new HttpStringContent(JsonConvert.SerializeObject(action)));
            }
        }

        public async void StartWakeUp()
        {
            wakeupCancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(9.0), wakeupCancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            byte brightness = 0;

            var action = new Action { on = true, bri = brightness, ct = 250 };

            while (brightness < 64)
            {
                action.bri = ++brightness;

                using (var client = new HttpClient())
                {
                    var response = await client.PutAsync(new Uri($"{HUE_API}/groups/{bedroomID}/action"), new HttpStringContent(JsonConvert.SerializeObject(action)));
                }

                if (wakeupCancellationTokenSource == null) break;

                try
                {
                    wakeupCancellationTokenSource = new CancellationTokenSource();

                    await Task.Delay(TimeSpan.FromSeconds(10.0), wakeupCancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
            
        }
    }
}
