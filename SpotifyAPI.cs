using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace SpotifyLyrics
{
    public class Spotify
    {
        readonly private string token_url = "https://open.spotify.com/get_access_token?reason=transport&productType=web_player";
        readonly private string lyrics_url = "https://spclient.wg.spotify.com/color-lyrics/v2/track/";

        public class Lyrics
        {
            public List<(string text, int startTimeMs)> Lines { get; set; }
        }

        private async Task<string> GetTokenAsync()
        {
            var sp_dc = "";
            if (File.Exists("spotify.txt"))
            {
                sp_dc = File.ReadAllText("spotify.txt");
            }
            else
            {
                throw new Exception("Please set SP_DC in spotify.txt file.");
            }

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(600);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/101.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("App-platform", "WebPlayer");
                client.DefaultRequestHeaders.Add("cookie", "sp_dc=" + sp_dc);

                using (var response = await client.GetAsync(token_url))
                {
                    response.EnsureSuccessStatusCode();
                    var result = await response.Content.ReadAsStringAsync();
                    var token_json = JsonConvert.DeserializeObject<dynamic>(result);
                    if (token_json == null || (bool)token_json.isAnonymous.Value)
                    {
                        throw new Exception("The SP_DC set seems to be invalid, please correct it!");
                    }
                    using (var token_file = new StreamWriter("config.json"))
                    {
                        token_file.Write(result);
                    }
                    return result;
                }
            }
        }

        private async Task CheckIfExpireAsync()
        {
            if (File.Exists("config.json"))
            {
                var json = File.ReadAllText("config.json");
                var timeleft = JsonConvert.DeserializeObject<dynamic>(json)["accessTokenExpirationTimestampMs"];
                var timenow = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds);
                if (timeleft < timenow)
                {
                    await GetTokenAsync();
                }
            }
            else
            {
                await GetTokenAsync();
            }
        }

        public async Task<Lyrics> GetLyricsAsync(string track_id)
        {
            await CheckIfExpireAsync();
            var json = File.ReadAllText("config.json");
            var token = JsonConvert.DeserializeObject<dynamic>(json)["accessToken"];
            var formatted_url = lyrics_url + track_id + "?format=json&market=from_token";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/101.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("App-platform", "WebPlayer");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)token);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using (var response = await client.GetAsync(formatted_url))
                {
                    response.EnsureSuccessStatusCode();
                    var result = await response.Content.ReadAsStringAsync();
                    dynamic jsonObject = JsonConvert.DeserializeObject(result);
                    dynamic lyricsObject = jsonObject.lyrics;

                    Lyrics lyrics = new Lyrics();
                    lyrics.Lines = new List<(string, int)>();

                    foreach (dynamic lineObject in lyricsObject.lines)
                    {
                        int startTimeMs = lineObject.startTimeMs;
                        string text = lineObject.words;
                        lyrics.Lines.Add((text, startTimeMs));
                    }

                    return lyrics;
                }
            }
        }

        public async Task<(string? track_id, int progress_ms)> GetCurrentlyPlayingTrackAsync()
        {
            // Retrieve an access token for the Spotify Web API
            var accessToken = await GetTokenAsync();

            // If we couldn't retrieve an access token, return null, but it shouldn't happen
            if (string.IsNullOrEmpty(accessToken))
            {
                return (null, 0);
            }

            // Set up a new HTTP client to make requests to the Spotify Web API
            var json = File.ReadAllText("config.json");
            var token = JsonConvert.DeserializeObject<dynamic>(json)["accessToken"];

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)token);

            // Make a GET request to the "Get the User's Currently Playing Track" endpoint
            var response = await client.GetAsync("https://api.spotify.com/v1/me/player/currently-playing");

            // If the request was successful, parse the response JSON and return the currently playing track
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                if (responseJson != null)
                {
                    dynamic track = JsonConvert.DeserializeObject(responseJson);
                    string trackId = track.item.id;
                    int progressMs = track.progress_ms;
                    string trackName = track.item.name;
                    Console.Title = trackName;
                    return (trackId, progressMs);
                }
                return (null, 0);
            }

            // If the request was not successful, return null
            return (null, 0);
        }


        public void DisplayLyrics(Lyrics lyrics, int progress_ms)
        {
            string trackName = Console.Title;
            int delayMs = 0;
            int currentIndex = 0;
            int previousIndex = -1;
            string previousText = "";

            //fast forward to current line
            for (int i = 0; i < lyrics.Lines.Count; i++)
            {
                if (lyrics.Lines[i].startTimeMs > progress_ms)
                {
                    currentIndex = i;
                    break;
                }
            }
            var line = lyrics.Lines[currentIndex];
            var startTimeMs = line.startTimeMs;

            // Calculate the time difference between the start time of the line and the current playback time
            var timeDiffMs = startTimeMs - progress_ms - 2000; //allow us to have buffor, probably to adjust
            if (progress_ms > 5000)
            {
                timeDiffMs -= 2500; //if we start when track is already playing there's a risk we will start displaying when track is mid-line and we will be behind by few seconds
            }


            // If the time difference is positive, sleep for that amount of time before displaying the line
            if (timeDiffMs > 0)
            {
                Thread.Sleep(timeDiffMs);
            }

            while (currentIndex < lyrics.Lines.Count)
            {
                int currentMs = progress_ms + lyrics.Lines[currentIndex].startTimeMs;

                if (previousIndex >= 0 && currentIndex > previousIndex)
                {
                    int previousMs = progress_ms + lyrics.Lines[previousIndex].startTimeMs;
                    delayMs = currentMs - previousMs;
                    Thread.Sleep(delayMs); //delay before showing next line using startTimeMs
                }

                Console.Clear();
                int emptyLines = (Console.WindowHeight - 1) / 3;

                // Write empty lines to center the text vertically
                for (int i = 0; i < emptyLines; i++)
                {
                    Console.WriteLine();
                }

                string text = lyrics.Lines[currentIndex].text;
                if (text.Contains("♪")) //console can't display those emojis so [instrumental] will suit better
                {
                    text = text.Replace("♪", "[INSTRUMENTAL]");
                }

                //you need to set console font size to 72p to make it all readable

                Console.WriteLine(previousText); //write current and previous line to compensate for delays between us and currently playing track progress
                Console.WriteLine(text); //maybe we could use some sync mid-play

                int ms = lyrics.Lines[currentIndex].startTimeMs;
                TimeSpan ts = TimeSpan.FromMilliseconds(ms);
                string minutesSeconds = $"{ts.Minutes}:{ts.Seconds}";
                Console.Title = trackName + " " + minutesSeconds;

                previousText = text;
                previousIndex = currentIndex;
                currentIndex++;
            }
        }

    }
}