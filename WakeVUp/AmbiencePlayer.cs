using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace WakeVUp
{
    class AmbiencePlayer
    {
        private class YoutubeResponse
        {
            public class StreamingData
            {
                public class Format
                {
                    public string url { get; set; }
                    public bool IsAudio => url.Contains("mime=audio");
                    public int itag { get; set; }
                }

                public Format[] formats { get; set; }
                public Format[] adaptiveFormats { get; set; }
            }

            public StreamingData streamingData { get; set; }
        }

        private static readonly Random random = new Random();

        private MediaPlayer audioPlayer;

        public AmbiencePlayer()
        {
            
        }

        public async Task StartAmbience(string videoUrl)
        {
            var streamUrl = await GetAudioStreamURL(videoUrl);

            if(!string.IsNullOrEmpty(streamUrl))
            {
                audioPlayer = new MediaPlayer();
                audioPlayer.Source = MediaSource.CreateFromUri(new Uri(streamUrl));
                audioPlayer.Volume = 1.0f;
                audioPlayer.IsLoopingEnabled = false;
                audioPlayer.Play();
            }
        }

        public void StopAmbience()
        {
            audioPlayer?.Pause();
        }

        private async Task<string> GetAudioStreamURL(string videoUrl)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Referrer = new Uri("http://localhost");
                var result = await client.GetAsync($"https://images{~~random.Next(0, 33)}-focus-opensocial.googleusercontent.com/gadgets/proxy?container=none&url={HttpUtility.UrlEncode($"https://www.youtube.com/watch?hl=en&v={videoUrl}")}");
                if(result.IsSuccessStatusCode)
                {
                    var response = await result.Content.ReadAsStringAsync();
                    response = response.Split("window.getPageData")[0].Replace("ytInitialPlayerResponse = null", "").Replace("ytInitialPlayerResponse=window.ytInitialPlayerResponse", "").Replace("ytplayer.config={args:{raw_player_response:ytInitialPlayerResponse}};", "");

                    var regex = new Regex("(?:ytplayer\\.config\\s*=\\s*|ytInitialPlayerResponse\\s?=\\s?)(.+?)(?:;var|;\\(function|\\)?;\\s*if|;\\s*if|;\\s*ytplayer\\.|;\\s*<\\/script)", RegexOptions.ECMAScript | RegexOptions.Multiline);
                    var matches = regex.Matches(response);
                    if(matches.Count > 0)
                    {
                        var data = matches[0].Value.Replace("ytInitialPlayerResponse = ", "").Replace(";</script", "");

                        var streamingData = JsonConvert.DeserializeObject<YoutubeResponse>(data).streamingData;
                        var streams = new List<YoutubeResponse.StreamingData.Format>();

                        if(streamingData.adaptiveFormats != null)
                        {
                            streams.AddRange(streamingData.adaptiveFormats);
                        }
                        if(streamingData.formats != null)
                        {
                            streams.AddRange(streamingData.formats);
                        }

                        var format = streams.FirstOrDefault(s => s.itag == 141) ?? streams.FirstOrDefault(s => s.itag == 140) ?? streams.FirstOrDefault(s => s.itag == 139);
                        if(format != null)
                        {
                            return format.url;
                        }
                    }
                }
            }

            return "";
        }
    }
}
