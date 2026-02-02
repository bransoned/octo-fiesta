using System.Text;
using System.Text.Json;
using octo_fiesta.Models.Domain;
using octo_fiesta.Models.Settings;
using octo_fiesta.Models.Download;
using octo_fiesta.Models.Search;
using octo_fiesta.Models.Subsonic;
using octo_fiesta.Services.Local;
using octo_fiesta.Services.Common;
using Microsoft.Extensions.Options;
using IOFile = System.IO.File;
using Microsoft.Extensions.Logging;

namespace octo_fiesta.Services.SquidWTF;

/// <summary>
/// Handles track downloading from tidal.squid.wtf (no encryption, no auth required)
/// Downloads are direct from Tidal's CDN via the squid.wtf proxy
/// </summary>
public class SquidWTFDownloadService : BaseDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly string? _preferredQuality;
    private readonly SquidWTFSettings _squidwtfSettings;
	
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly int _minRequestIntervalMs = 200;
    
	private const string SquidWTFApiBase = "https://triton.squid.wtf";

    protected override string ProviderName => "squidwtf";

    public SquidWTFDownloadService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILocalLibraryService localLibraryService,
        IMusicMetadataService metadataService,
        IOptions<SubsonicSettings> subsonicSettings,
        IOptions<SquidWTFSettings> SquidWTFSettings,
		IServiceProvider serviceProvider,
        ILogger<SquidWTFDownloadService> logger)
        : base(configuration, localLibraryService, metadataService, subsonicSettings.Value, serviceProvider, logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _squidwtfSettings = SquidWTFSettings.Value;
    }

    #region BaseDownloadService Implementation

    public override async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Test connectivity to triton.squid.wtf
            var response = await _httpClient.GetAsync("https://tidal-api.binimum.org/");
			Console.WriteLine($"Response code from is available async: {response.IsSuccessStatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "SquidWTF service not available");
            return false;
        }
	}

    protected override string? ExtractExternalIdFromAlbumId(string albumId)
    {
        const string prefix = "ext-squidwtf-album-";
        if (albumId.StartsWith(prefix))
        {
			Console.WriteLine(albumId[prefix.Length..]);
            return albumId[prefix.Length..];
        }
        return null;
    }

    protected override async Task<string> DownloadTrackAsync(string trackId, Song song, CancellationToken cancellationToken)
    {
        var downloadInfo = await GetTrackDownloadInfoAsync(trackId, cancellationToken);
        
		Logger.LogInformation("Track token obtained: {Url}", downloadInfo.DownloadUrl);
        Logger.LogInformation("Using format: {Format}", downloadInfo.MimeType);

        // Determine extension from MIME type
        var extension = downloadInfo.MimeType?.ToLower() switch
        {
            "audio/flac" => ".flac",
            "audio/mpeg" => ".mp3",
            "audio/mp4" => ".m4a",
            _ => ".flac" // Default to FLAC
        };
		
        // Build organized folder structure: Artist/Album/Track using AlbumArtist (fallback to Artist for singles)
        var artistForPath = song.AlbumArtist ?? song.Artist;
        var outputPath = PathHelper.BuildTrackPath(DownloadPath, artistForPath, song.Album, song.Title, song.Track, extension);
        
        // Create directories if they don't exist
        var albumFolder = Path.GetDirectoryName(outputPath)!;
        EnsureDirectoryExists(albumFolder);
        
        // Resolve unique path if file already exists
        outputPath = PathHelper.ResolveUniquePath(outputPath);

        // Download from Tidal CDN (no authentication needed, token is in URL)
        var response = await QueueRequestAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadInfo.DownloadUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0");
            request.Headers.Add("Accept", "*/*");
            
            return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        });

        response.EnsureSuccessStatusCode();
		
        // Download directly (no decryption needed - squid.wtf handles everything)
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var outputFile = IOFile.Create(outputPath);
        
		await responseStream.CopyToAsync(outputFile, cancellationToken);
        
        // Close file before writing metadata
        await outputFile.DisposeAsync();
        
        // Write metadata and cover art
        await WriteMetadataAsync(outputPath, song, cancellationToken);

        return outputPath;
    }

    #endregion	
	
	#region SquidWTF API Methods
	
	private async Task<DownloadResult> GetTrackDownloadInfoAsync(string trackId, CancellationToken cancellationToken)
    {
        return await QueueRequestAsync(async () =>
        {
            // Map quality settings to Tidal's quality levels
            var quality = _squidwtfSettings.Quality?.ToUpperInvariant() switch
            {
                "FLAC" => "LOSSLESS",
                "HI_RES" => "HI_RES_LOSSLESS",
                "LOSSLESS" => "LOSSLESS",
                "HIGH" => "HIGH",
                "LOW" => "LOW",
                _ => "LOSSLESS" // Default to lossless
            };
            
            // Use the triton.squid.wtf endpoint to get track download info
            var url = $"https://tidal-api.binimum.org/track/?id={trackId}&quality={quality}";

            Console.WriteLine($"%%%%%%%%%%%%%%%%%%% URL For downloads??: {url}");

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            
            if (!doc.RootElement.TryGetProperty("data", out var data))
            {
                throw new Exception("Invalid response from https://tidal-api.binimum.org");
            }
            
            // Get the manifest (base64 encoded JSON containing the actual CDN URL)
            var manifestBase64 = data.GetProperty("manifest").GetString()
                ?? throw new Exception("No manifest in response");
            
            // Decode the manifest
            var manifestJson = Encoding.UTF8.GetString(Convert.FromBase64String(manifestBase64));
            var manifest = JsonDocument.Parse(manifestJson);
            
            // Extract the download URL from the manifest
            if (!manifest.RootElement.TryGetProperty("urls", out var urls) || urls.GetArrayLength() == 0)
            {
                throw new Exception("No download URLs in manifest");
            }
            
            var downloadUrl = urls[0].GetString()
                ?? throw new Exception("Download URL is null");
            
            var mimeType = manifest.RootElement.TryGetProperty("mimeType", out var mimeTypeEl)
                ? mimeTypeEl.GetString()
                : "audio/flac";
            
            var audioQuality = data.TryGetProperty("audioQuality", out var audioQualityEl)
                ? audioQualityEl.GetString()
                : "LOSSLESS";
            
            Logger.LogDebug("Decoded manifest - URL: {Url}, MIME: {MimeType}, Quality: {Quality}", 
                downloadUrl, mimeType, audioQuality);
            
            return new DownloadResult
            {
                DownloadUrl = downloadUrl,
                MimeType = mimeType ?? "audio/flac",
                AudioQuality = audioQuality ?? "LOSSLESS"
            };
        });
    }
	
	#endregion
	
    #region Utility Methods

    private async Task<T> QueueRequestAsync<T>(Func<Task<T>> action)
    {
        await _requestLock.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            var timeSinceLastRequest = (now - _lastRequestTime).TotalMilliseconds;
            
            if (timeSinceLastRequest < _minRequestIntervalMs)
            {
                await Task.Delay((int)(_minRequestIntervalMs - timeSinceLastRequest));
            }

            _lastRequestTime = DateTime.UtcNow;
            return await action();
        }
        finally
        {
            _requestLock.Release();
        }
    }

    #endregion

    private class DownloadResult
    {
        public string DownloadUrl { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public string AudioQuality { get; set; } = string.Empty;
    }
}	
