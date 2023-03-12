using SpotifyLyrics;

var SpotifyAPI = new Spotify();
string currentTrackId = "";
while (true)
{
    var currentTrack = await SpotifyAPI.GetCurrentlyPlayingTrackAsync();
    if ((currentTrack.track_id != currentTrackId) && (currentTrack.track_id != null))
    {
        currentTrackId = currentTrack.track_id;
        Console.WriteLine(currentTrackId);
        var lyrics = await SpotifyAPI.GetLyricsAsync(currentTrackId);
        SpotifyAPI.DisplayLyrics(lyrics, currentTrack.progress_ms);
    }
    else
    {
        Console.Clear();
        Thread.Sleep(2000);
        Console.WriteLine("No track playing probably, sleeping for 2 seconds...");
    }
}