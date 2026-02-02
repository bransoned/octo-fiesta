using octo_fiesta.Models.Domain;
using octo_fiesta.Models.Settings;
using octo_fiesta.Models.Download;
using octo_fiesta.Models.Search;
using octo_fiesta.Models.Subsonic;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
//using Microsoft.Extensions.Logging;

namespace octo_fiesta.Services.SquidWTF;

/// <summary>
/// Metadata service implementation using the SquidWTF API (free, no key required)
/// </summary>

public class SquidWTFMetadataService : IMusicMetadataService
{
	private readonly HttpClient _httpClient;
    private readonly SubsonicSettings _settings;
	private readonly ILogger<SquidWTFMetadataService> _logger;
    private const string BaseUrl = "https://tidal-api.binimum.org";

	public SquidWTFMetadataService(IHttpClientFactory httpClientFactory, 
	IOptions<SubsonicSettings> settings,
	IOptions<SquidWTFSettings> squidwtfSettings,
	ILogger<SquidWTFMetadataService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _settings = settings.Value;
		_logger = logger;
		
		// Set up default headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
			"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:83.0) Gecko/20100101 Firefox/83.0");

    }

    public async Task<List<Song>> SearchSongsAsync(string query, int limit = 20)
    {
        try
        {
            var url = $"{BaseUrl}/search/?s={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetAsync(url);
            			
            if (!response.IsSuccessStatusCode) return new List<Song>();
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);
            
            var songs = new List<Song>();
            if (result.RootElement.TryGetProperty("data", out var data) &&
				data.TryGetProperty("items", out var items))
            {
				int count = 0;
                foreach (var track in items.EnumerateArray())
                {
					if (count >= limit) break;
                    var song = ParseTidalTrack(track);
                    songs.Add(song);
					count++;

                }
            }
            return songs;
        }
        catch (Exception ex)
        {    
			return new List<Song>();
        }
	}
	
    public async Task<List<Album>> SearchAlbumsAsync(string query, int limit = 20)
    {
        try
        {
            var url = $"{BaseUrl}/search/?al={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode) return new List<Album>();
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);
            
            var albums = new List<Album>();
            if (result.RootElement.TryGetProperty("data", out var data) &&
				data.TryGetProperty("albums", out var albumsObj) &&
				albumsObj.TryGetProperty("items", out var items))
            {
				int count = 0;
                foreach (var album in items.EnumerateArray())
                {
					if (count >= limit) break;
                    albums.Add(ParseTidalAlbum(album));
					count++;
                }
            }
			
            return albums;
        }
        catch
        {
            return new List<Album>();
        }
    }

    public async Task<List<Artist>> SearchArtistsAsync(string query, int limit = 20)
    {
        try
        {
            var url = $"{BaseUrl}/search/?a={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<Artist>();
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);
            
            var artists = new List<Artist>();
            if (result.RootElement.TryGetProperty("data", out var data) &&
				data.TryGetProperty("artists", out var artistsObj) &&
				artistsObj.TryGetProperty("items", out var items))
            {
				int count = 0;
                foreach (var artist in items.EnumerateArray())
                {
					if (count >= limit) break;
                    artists.Add(ParseTidalArtist(artist));
					count++;
                }
            }

            return artists;
        }
        catch
        {
            return new List<Artist>();
        }
    }
	
	public async Task<List<ExternalPlaylist>> SearchPlaylistsAsync(string query, int limit = 20)
	{
		try
		{
			var url = $"{BaseUrl}/search/?p={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<ExternalPlaylist>();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);

            var playlists = new List<ExternalPlaylist>();
			if (result.RootElement.TryGetProperty("data", out var data) &&
				data.TryGetProperty("playlists", out var playlistObj) &&
				playlistObj.TryGetProperty("items", out var items))
			{
				foreach(var playlist in items.EnumerateArray())
				{
					playlists.Add(ParseTidalPlaylist(playlist));
				}
			}
			return playlists;
		}
		catch
		{
			return new List<ExternalPlaylist>();
		}
		
		
	}

    public async Task<SearchResult> SearchAllAsync(string query, int songLimit = 20, int albumLimit = 20, int artistLimit = 20)
    {
        // Execute searches in parallel
        var songsTask = SearchSongsAsync(query, songLimit);
        var albumsTask = SearchAlbumsAsync(query, albumLimit);
        var artistsTask = SearchArtistsAsync(query, artistLimit);
        
        await Task.WhenAll(songsTask, albumsTask, artistsTask);
		
		var temp = new SearchResult
        {			
            Songs = await songsTask,
            Albums = await albumsTask,
            Artists = await artistsTask
        };

		return temp;
    }

    public async Task<Song?> GetSongAsync(string externalProvider, string externalId)
    {
        if (externalProvider != "squidwtf") return null;
        
        try
        {
            // Use the /info endpoint for full track metadata
            var url = $"{BaseUrl}/info/?id={externalId}";
						
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);
            
			if (!result.RootElement.TryGetProperty("data", out var track))
				return null;

			return ParseTidalTrackFull(track);
        }
        catch (Exception ex)
        {
			_logger.LogWarning(ex, "GetSongAsync Exception");
            return null;
        }
    }

    public async Task<Album?> GetAlbumAsync(string externalProvider, string externalId)
    {
        if (externalProvider != "squidwtf") return null;
        
        try
        {
            // Use the /info endpoint for full track metadata
            var url = $"{BaseUrl}/album/?id={externalId}";
			
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);
            
			
			if (!result.RootElement.TryGetProperty("data", out var albumElement))
				return null;

			var album = ParseTidalAlbum(albumElement);
	
			// Get album tracks
			if (albumElement.TryGetProperty("items", out var tracks))
			{
				foreach (var trackWrapper in tracks.EnumerateArray())
				{
					if (trackWrapper.TryGetProperty("item", out var track))
					{
						var song = ParseTidalTrack(track);
						if (ShouldIncludeSong(song))
						{
							album.Songs.Add(song);
						}
					}
				}
			}
			return album;	
		}
        catch (Exception ex)
        {
			_logger.LogWarning(ex, "GetAlbumAsync Exception");
            return null;
        }
    }
	
    public async Task<Artist?> GetArtistAsync(string externalProvider, string externalId)
    {
        if (externalProvider != "squidwtf") return null;
  
        try
        {
            // Use the /info endpoint for full track metadata
            var url = $"{BaseUrl}/artist/?f={externalId}"; 

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonDocument.Parse(json);
            
			JsonElement? artistSource = null;
			int albumCount = 0;
			
			// Think this can maybe switch to something using ParseTidalAlbum
            if (result.RootElement.TryGetProperty("albums", out var albums) &&
				albums.TryGetProperty("items", out var albumItems) &&
				albumItems.GetArrayLength() > 0)
			{
				albumCount = albumItems.GetArrayLength();
				artistSource = albumItems[0].GetProperty("artist");
            }
			
			// Think this can maybe switch to something using ParseTidalTrack
			else if (result.RootElement.TryGetProperty("tracks", out var tracks) &&
					 tracks.GetArrayLength() > 0 &&
					 tracks[0].TryGetProperty("artists", out var artists) &&
					 artists.GetArrayLength() > 0)
			{
				artistSource = artists[0];
			}

			if (artistSource == null) return null;

			var artistElement = artistSource.Value;
			var normalizedArtist = new JsonObject
			{
				["id"] = artistElement.GetProperty("id").GetInt64(),
				["name"] = artistElement.GetProperty("name").GetString(),
				["albums_count"] = albumCount,
				["picture"] = artistElement.GetProperty("picture").GetString()
			};

			using var doc = JsonDocument.Parse(normalizedArtist.ToJsonString());
			return ParseTidalArtist(doc.RootElement);

        }
        catch (Exception ex)
        {
			_logger.LogWarning(ex, "GetArtistAsync Exception.");
            return null;
        }
    }

    public async Task<List<Album>> GetArtistAlbumsAsync(string externalProvider, string externalId)
    {

		try
		{
			if (externalProvider != "squidwtf") return new List<Album>();
			
			var url = $"{BaseUrl}/artist/?f={externalId}";
			var response = await _httpClient.GetAsync(url);
			
			if (!response.IsSuccessStatusCode) return new List<Album>();
			
			var json = await response.Content.ReadAsStringAsync();
			var result = JsonDocument.Parse(json);
			
			var albums = new List<Album>();
			
			if (result.RootElement.TryGetProperty("albums", out var albumsObj) &&
				albumsObj.TryGetProperty("items", out var items))
			{
				foreach (var album in items.EnumerateArray())
				{
					albums.Add(ParseTidalAlbum(album));
				}
			}
			
			return albums;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get SquidWTF artist albums for {ExternalId}");
			return new List<Album>();
		}
	}

    public async Task<ExternalPlaylist?> GetPlaylistAsync(string externalProvider, string externalId)
	{
		if (externalProvider != "squidwtf") return null;
		
		try
		{
			var url = $"{BaseUrl}/playlist/?id={externalId}";
			var response = await _httpClient.GetAsync(url);
			if (!response.IsSuccessStatusCode) return null;
			
            var json = await response.Content.ReadAsStringAsync();
            var playlistElement = JsonDocument.Parse(json).RootElement;
			
            if (playlistElement.TryGetProperty("error", out _)) return null;
            
			return ParseTidalPlaylist(playlistElement);
		}
		catch
		{
			return null;
		}
		
	}
	
    public async Task<List<Song>> GetPlaylistTracksAsync(string externalProvider, string externalId)
	{
		if (externalProvider != "squidwtf") return new List<Song>();
		
		try
		{
			var url = $"{BaseUrl}/playlist/?id={externalId}";
			var response = await _httpClient.GetAsync(url);
			if (!response.IsSuccessStatusCode) return new List<Song>();
			
			var json = await response.Content.ReadAsStringAsync();
            var playlistElement = JsonDocument.Parse(json).RootElement;
            
			if (playlistElement.TryGetProperty("error", out _)) return new List<Song>();
			
			JsonElement? playlist = null;
			JsonElement? tracks = null;

			if (playlistElement.TryGetProperty("playlist", out var playlistEl))
			{
				playlist = playlistEl;
			}

			if (playlistElement.TryGetProperty("items", out var tracksEl))
			{
				tracks = tracksEl;
			}			
			
			var songs = new List<Song>();
			
			// Get playlist name for album field
			var playlistName = playlist.Value.TryGetProperty("title", out var titleEl)
				? titleEl.GetString() ?? "Unknown Playlist"
				: "Unknown Playlist";

			if (tracks != null)
			{
				int trackIndex = 1;
				foreach (var entry in tracks.Value.EnumerateArray())
				{
					if (!entry.TryGetProperty("item", out var track))
						continue;
					
					// For playlists, use the track's own artist (not a single album artist)
					var song = ParseTidalTrack(track, trackIndex);
				
					// Override album name to be the playlist name
					song.Album = playlistName;
					
					if (ShouldIncludeSong(song))
					{
						songs.Add(song);
					}
					trackIndex++;
				}
			}
			return songs;
		}
		catch
		{
			return new List<Song>();
		}
		
	}

	// --- Parser functions start here ---

    private Song ParseTidalTrack(JsonElement track, int? fallbackTrackNumber = null)
    {
        var externalId = track.GetProperty("id").GetInt64().ToString();

		// Explicit content lyrics value - idk if this will work
		int? explicitContentLyrics =
			track.TryGetProperty("explicit", out var ecl) && ecl.ValueKind == JsonValueKind.True
				? 1
				: 0;
        
        int? trackNumber = track.TryGetProperty("trackNumber", out var trackNum) 
            ? trackNum.GetInt32() 
            : fallbackTrackNumber;
        
        int? discNumber = track.TryGetProperty("volumeNumber", out var volNum)
            ? volNum.GetInt32()
            : null;
        
        // Get artist name - handle both single artist and artists array
        string artistName = "";
        if (track.TryGetProperty("artist", out var artist))
        {
            artistName = artist.GetProperty("name").GetString() ?? "";
        }
        else if (track.TryGetProperty("artists", out var artists) && artists.GetArrayLength() > 0)
        {
            artistName = artists[0].GetProperty("name").GetString() ?? "";
        }
        
        // Get artist ID
        string? artistId = null;
        if (track.TryGetProperty("artist", out var artistForId))
        {
            artistId = $"ext-squidwtf-artist-{artistForId.GetProperty("id").GetInt64()}";
        }
        else if (track.TryGetProperty("artists", out var artistsForId) && artistsForId.GetArrayLength() > 0)
        {
            artistId = $"ext-squidwtf-artist-{artistsForId[0].GetProperty("id").GetInt64()}";
        }
        
        // Get album info
        string albumTitle = "";
        string? albumId = null;
        string? coverArt = null;
        
        if (track.TryGetProperty("album", out var album))
        {
            albumTitle = album.GetProperty("title").GetString() ?? "";
            albumId = $"ext-squidwtf-album-{album.GetProperty("id").GetInt64()}";
            
            if (album.TryGetProperty("cover", out var cover))
            {
                var coverGuid = cover.GetString()?.Replace("-", "/");
                coverArt = $"https://resources.tidal.com/images/{coverGuid}/320x320.jpg";
            }
        }
        
        return new Song
        {
            Id = $"ext-squidwtf-song-{externalId}",
            Title = track.GetProperty("title").GetString() ?? "",
            Artist = artistName,
            ArtistId = artistId,
            Album = albumTitle,
            AlbumId = albumId,
            Duration = track.TryGetProperty("duration", out var duration) 
                ? duration.GetInt32() 
                : null,
            Track = trackNumber,
            DiscNumber = discNumber,
            CoverArtUrl = coverArt,
            IsLocal = false,
            ExternalProvider = "squidwtf",
            ExternalId = externalId,
            ExplicitContentLyrics = explicitContentLyrics
        };
    }

    private Song ParseTidalTrackFull(JsonElement track)
    {
        var externalId = track.GetProperty("id").GetInt64().ToString();

		// Explicit content lyrics value - idk if this will work
		int? explicitContentLyrics =
			track.TryGetProperty("explicit", out var ecl) && ecl.ValueKind == JsonValueKind.True
				? 1
				: 0;

		
        int? trackNumber = track.TryGetProperty("trackNumber", out var trackNum) 
            ? trackNum.GetInt32() 
            : null;
        
        int? discNumber = track.TryGetProperty("volumeNumber", out var volNum)
            ? volNum.GetInt32()
            : null;
        
        int? bpm = track.TryGetProperty("bpm", out var bpmVal) && bpmVal.ValueKind == JsonValueKind.Number
            ? bpmVal.GetInt32() 
            : null;
        
        string? isrc = track.TryGetProperty("isrc", out var isrcVal) 
            ? isrcVal.GetString() 
            : null;
        
        int? year = null;
        if (track.TryGetProperty("streamStartDate", out var streamDate))
        {
            var dateStr = streamDate.GetString();
            if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4)
            {
                if (int.TryParse(dateStr.Substring(0, 4), out var y))
                    year = y;
            }
        }
        
        // Get artist info
        string artistName = track.GetProperty("artist").GetProperty("name").GetString() ?? "";
        long artistIdNum = track.GetProperty("artist").GetProperty("id").GetInt64();
        
        // Album artist - same as main artist for Tidal tracks
        string? albumArtist = artistName;
        
        // Get album info
        var album = track.GetProperty("album");
        string albumTitle = album.GetProperty("title").GetString() ?? "";
        long albumIdNum = album.GetProperty("id").GetInt64();
        
        // Cover art URLs
        string? coverArt = null;
        string? coverArtLarge = null;
        if (album.TryGetProperty("cover", out var cover))
        {
            var coverGuid = cover.GetString()?.Replace("-", "/");
            coverArt = $"https://resources.tidal.com/images/{coverGuid}/320x320.jpg";
            coverArtLarge = $"https://resources.tidal.com/images/{coverGuid}/1280x1280.jpg";
        }
        
        // Copyright
        string? copyright = track.TryGetProperty("copyright", out var copyrightVal)
            ? copyrightVal.GetString()
            : null;
        
        // Explicit content
        bool isExplicit = track.TryGetProperty("explicit", out var explicitVal) && explicitVal.GetBoolean();
        
        return new Song
        {
            Id = $"ext-squidwtf-song-{externalId}",
            Title = track.GetProperty("title").GetString() ?? "",
            Artist = artistName,
            ArtistId = $"ext-squidwtf-artist-{artistIdNum}",
            Album = albumTitle,
            AlbumId = $"ext-squidwtf-album-{albumIdNum}",
            AlbumArtist = albumArtist,
            Duration = track.TryGetProperty("duration", out var duration) 
                ? duration.GetInt32() 
                : null,
            Track = trackNumber,
            DiscNumber = discNumber,
            Year = year,
            Bpm = bpm,
            Isrc = isrc,
            CoverArtUrl = coverArt,
            CoverArtUrlLarge = coverArtLarge,
            Label = copyright, // Store copyright in label field
            IsLocal = false,
            ExternalProvider = "squidwtf",
            ExternalId = externalId,
            ExplicitContentLyrics = explicitContentLyrics
        };
    }

    private Album ParseTidalAlbum(JsonElement album)
    {
        var externalId = album.GetProperty("id").GetInt64().ToString();
        
        int? year = null;
        if (album.TryGetProperty("releaseDate", out var releaseDate))
        {
            var dateStr = releaseDate.GetString();
            if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4)
            {
                if (int.TryParse(dateStr.Substring(0, 4), out var y))
                    year = y;
            }
        }
        
        string? coverArt = null;
        if (album.TryGetProperty("cover", out var cover))
        {
            var coverGuid = cover.GetString()?.Replace("-", "/");
            coverArt = $"https://resources.tidal.com/images/{coverGuid}/320x320.jpg";
        }
        
        // Get artist name
        string artistName = "";
        string? artistId = null;
        if (album.TryGetProperty("artist", out var artist))
        {
            artistName = artist.GetProperty("name").GetString() ?? "";
            artistId = $"ext-squidwtf-artist-{artist.GetProperty("id").GetInt64()}";
        }
        else if (album.TryGetProperty("artists", out var artists) && artists.GetArrayLength() > 0)
        {
            artistName = artists[0].GetProperty("name").GetString() ?? "";
            artistId = $"ext-squidwtf-artist-{artists[0].GetProperty("id").GetInt64()}";
        }
        
        return new Album
        {
            Id = $"ext-squidwtf-album-{externalId}",
            Title = album.GetProperty("title").GetString() ?? "",
            Artist = artistName,
            ArtistId = artistId,
            Year = year,
            SongCount = album.TryGetProperty("numberOfTracks", out var trackCount) 
                ? trackCount.GetInt32() 
                : null,
            CoverArtUrl = coverArt,
            IsLocal = false,
            ExternalProvider = "squidwtf",
            ExternalId = externalId
        };
    }

	// TODO: Think of a way to implement album count when this function is called by search function
	// 		 as the API endpoint in search does not include this data
    private Artist ParseTidalArtist(JsonElement artist)
    {
        var externalId = artist.GetProperty("id").GetInt64().ToString();
        
        string? imageUrl = null;
        if (artist.TryGetProperty("picture", out var picture))
        {
            var pictureGuid = picture.GetString()?.Replace("-", "/");
            imageUrl = $"https://resources.tidal.com/images/{pictureGuid}/320x320.jpg";
        }
		
        return new Artist
        {
            Id = $"ext-squidwtf-artist-{externalId}",
            Name = artist.GetProperty("name").GetString() ?? "",
            ImageUrl = imageUrl,
			AlbumCount = artist.TryGetProperty("albums_count", out var albumsCount)
				? albumsCount.GetInt32()
				: null,
            IsLocal = false,
            ExternalProvider = "squidwtf",
            ExternalId = externalId
        };
    }
	
    private ExternalPlaylist ParseTidalPlaylist(JsonElement playlistElement)
	{
		JsonElement? playlist = null;
		JsonElement? tracks = null;

		if (playlistElement.TryGetProperty("playlist", out var playlistEl))
		{
			playlist = playlistEl;
		}
		
		if (playlistElement.TryGetProperty("items", out var tracksEl))
		{
			tracks = tracksEl;
		}
		
		var externalId = playlist.Value.GetProperty("uuid").GetString()!;
		
        // Get curator/creator name
        string? curatorName = null;
        if (playlist.Value.TryGetProperty("creator", out var creator) &&
            creator.TryGetProperty("id", out var id))
        {
            curatorName = id.GetString();
        }
		
		// Get creation date
        DateTime? createdDate = null;
        if (playlist.Value.TryGetProperty("created", out var creationDateEl))
        {
            var dateStr = creationDateEl.GetString();
            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
            {
                createdDate = date;
            }
        }
		
		// Get playlist image URL
		string? imageUrl = null;
        if (playlist.Value.TryGetProperty("squareImage", out var picture))
        {
            var pictureGuid = picture.GetString()?.Replace("-", "/");
            imageUrl = $"https://resources.tidal.com/images/{pictureGuid}/1080x1080.jpg";
			// Maybe later add support for potentential fallbacks if this size isn't available
        }

		return new ExternalPlaylist
        {
            Id = Common.PlaylistIdHelper.CreatePlaylistId("squidwtf", externalId),
            Name = playlist.Value.GetProperty("title").GetString() ?? "",
            Description = playlist.Value.TryGetProperty("description", out var desc) 
                ? desc.GetString() 
                : null,
            CuratorName = curatorName,
            Provider = "squidwtf",
            ExternalId = externalId,
            TrackCount = playlist.Value.TryGetProperty("numberOfTracks", out var nbTracks) 
                ? nbTracks.GetInt32() 
                : 0,
            Duration = playlist.Value.TryGetProperty("duration", out var duration) 
                ? duration.GetInt32() 
                : 0,
            CoverUrl = imageUrl,
            CreatedDate = createdDate
        };
		
	}

    /// <summary>
    /// Determines whether a song should be included based on the explicit content filter setting
    /// </summary>
    /// <param name="song">The song to check</param>
    /// <returns>True if the song should be included, false otherwise</returns>
    private bool ShouldIncludeSong(Song song)
    {
        // If no explicit content info, include the song
        if (song.ExplicitContentLyrics == null)
            return true;
        
        return _settings.ExplicitFilter switch
        {
            // All: No filtering, include everything
            ExplicitFilter.All => true,
            
            // ExplicitOnly: Exclude clean/edited versions (value 3)
            // Include: 0 (naturally clean), 1 (explicit), 2 (not applicable), 6/7 (unknown)
            ExplicitFilter.ExplicitOnly => song.ExplicitContentLyrics != 3,
            
            // CleanOnly: Only show clean content
            // Include: 0 (naturally clean), 3 (clean/edited version)
            // Exclude: 1 (explicit)
            ExplicitFilter.CleanOnly => song.ExplicitContentLyrics != 1,
            
            _ => true
        };
    }

}
