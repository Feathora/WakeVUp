using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace WakeVUpCompanion
{
    public partial class MainPage : ContentPage
    {
        private readonly string API_URL = "<api_url>";


		public MainPage()
        {
            InitializeComponent();
        }

        private async void StartAmbience_Clicked(object sender, EventArgs e)
        {
            using (HttpClient client = new HttpClient())
            {
                var data = new { CMD = "StartAmbience", VideoUrl = "jjtEP2y3Mxg" };
                string body = JsonConvert.SerializeObject(data);

                var result = await client.PostAsync(API_URL, new StringContent(body));
                if(result.IsSuccessStatusCode)
                {
                    DependencyService.Get<IToast>().ShowToast("Ambience started");
                }
                else
                {
                    DependencyService.Get<IToast>().ShowToast("Failed to start ambience... try again?");
                }
            }
        }

        private async void StopAmbience_Clicked(object sender, EventArgs e)
        {
            using (HttpClient client = new HttpClient())
            {
                var data = new { CMD = "StopAmbience" };
                string body = JsonConvert.SerializeObject(data);

                var result = await client.PostAsync(API_URL, new StringContent(body));
                if (result.IsSuccessStatusCode)
                {
                    DependencyService.Get<IToast>().ShowToast("Ambience stopped");
                }
                else
                {
                    DependencyService.Get<IToast>().ShowToast("Failed to stop ambience... try again?");
                }
            }
        }

        private async void SetAlarm_Clicked(object sender, EventArgs e)
        {
            using(HttpClient client = new HttpClient())
            {
                var data = new { CMD = "SetAlarm", AlarmTime = AlarmTimePicker.Time };
                string body = JsonConvert.SerializeObject(data);

                var result = await client.PostAsync(API_URL, new StringContent(body));
                if(result.IsSuccessStatusCode)
                {
                    DependencyService.Get<IToast>().ShowToast($"Alarm set for {AlarmTimePicker.Time}");
                }
                else
                {
                    DependencyService.Get<IToast>().ShowToast("Failed to set alarm");
                }
            }
        }
    }
}
