# DISCLAIMER:
Upstream Octo-Fiesta now supports SquidWTF and other [Hifi-API](https://github.com/binimum/hifi-api) instances as a source. You can find a list of these instances [here](https://github.com/monochrome-music/monochrome/blob/main/INSTANCES.md)

Due to this, I will no longer be maintaining this fork as it is now redundant and not needed. If you are content with the current functionality of Octo-Fiestarr, feel free to continue using this project, but if you would like future updates, please move upstream. To help with this move, I have provided my `docker-compose.yaml` and relevant part of my `.env` file to help in this transition:

```docker
  octo-fiesta:
    image: ghcr.io/v1ck3s/octo-fiesta:latest
    container_name: octo-fiesta
    restart: unless-stopped
    ports:
      - "127.0.0.1:5275:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Subsonic__Url=${SUBSONIC_URL:-http://localhost:4533}
      - Subsonic__ExplicitFilter=${EXPLICIT_FILTER:-ExplicitOnly}
      - Subsonic__DownloadMode=${DOWNLOAD_MODE:-Track}
      - Subsonic__MusicService=${MUSIC_SERVICE}
      - Subsonic__EnableExternalPlaylists=${ENABLE_EXTERNAL_PLAYLISTS}
      - Subsonic__PlaylistsDirectory=${PLAYLISTS_DIRECTORY}
      - Subsonic__StorageMode=${STORAGE_MODE}
      - Subsonic__CacheDurationHours=${CACHE_DURATION_HOURS}
      - Library__DownloadPath=/app/downloads
      - SquidWTF__Source=${SQUIDWTF_SOURCE}
      - SquidWTF__Quality=${SQUIDWTF_QUALITY}
    volumes:
      - ${DOWNLOAD_PATH:-./downloads}:/app/downloads
```

```env
## For octo-fiesta
# Navidrome/Subsonic server URL
SUBSONIC_URL=http://navidrome:4533

# Path where downloaded songs will be stored on the host (only applies if STORAGE_MODE=Permanent)
# Make sure Navidrome can reach this path
DOWNLOAD_PATH=./downloads

# Music service to use: SquidWTF, Deezer, or Qobuz (default: SquidWTF)
MUSIC_SERVICE=SquidWTF

SQUIDWTF_SOURCE=Tidal

# ===== SquidWTF CONFIGURATION =====
# Different quality options for SquidWTF. Only FLAC supported right now
SQUIDWTF_QUALITY=HI_RES_LOSSLESS

# ===== GENERAL SETTINGS =====
# External playlists support (optional, default: true)
# When enabled, allows searching and downloading playlists from Deezer/Qobuz
# Starring a playlist triggers automatic download of all tracks and creates an M3U file
ENABLE_EXTERNAL_PLAYLISTS=true

# Playlists directory name (optional, default: playlists)
# M3U playlist files will be created in {DOWNLOAD_PATH}/{PLAYLISTS_DIRECTORY}/
PLAYLISTS_DIRECTORY=playlists

# Explicit content filter (optional, default: All)
# - All: Show all tracks (no filtering)
# - ExplicitOnly: Exclude clean/edited versions, keep original explicit content
# - CleanOnly: Only show clean content (naturally clean or edited versions)
# Note: This only works with Deezer, Qobuz doesn't expose explicit content flags
EXPLICIT_FILTER=All

# Download mode (optional, default: Track)
# - Track: Download only the played track
# - Album: When playing a track, download the entire album in background
#          The played track is downloaded first, remaining tracks are queued
DOWNLOAD_MODE=Track

# Storage mode (optional, default: Permanent)
# - Permanent: Files are saved to the library permanently and registered in Navidrome
# - Cache: Files are stored in /tmp and automatically cleaned up after CACHE_DURATION_HOURS
#          Not registered in Navidrome, ideal for streaming without library bloat
#          Note: On Linux/Docker, you can customize cache location by setting TMPDIR environment variable
STORAGE_MODE=Permanent

# Cache duration in hours (optional, default: 1)
# Files older than this duration will be automatically deleted when STORAGE_MODE=Cache
# Based on last access time (updated each time the file is streamed)
# Cache location: /tmp/octo-fiesta-cache (or $TMPDIR/octo-fiesta-cache if TMPDIR is set)
CACHE_DURATION_HOURS=1
```

Feel free to edit the `.env` file to your liking/preferences, but this should serve as a solid guide to transfer from Octo-Fiestarr to upstream.

# Octo-Fiestarr

A Subsonic API proxy server that transparently integrates multiple music streaming providers as sources. When a song is not available in your local Navidrome library, it is automatically fetched from your configured provider, downloaded, and served to your Subsonic-compatible client. The downloaded song is then added to your library, making it available locally for future listens.

## Why "Octo-Fiestarr"?

This fork was created to focus on integrating the original concept of Octo-Fiesta with music providers that do not require API credentials, such as SquidWTF. This allows for seamless external music discovery without the need for any subscriptions. Thus, I saw it fitting to change the name of the fork to resemble other *arr projects.

## Features

- **Multi-Provider Architecture**: Pluggable music service system supporting multiple streaming providers (Deezer, Qobuz, and more to come)
- **Transparent Proxy**: Acts as a middleware between Subsonic clients (like Aonsoku, Sublime Music, etc.) and your Navidrome server
- **Seamless Integration**: Automatically searches and streams music from your configured provider when not available locally
- **Automatic Downloads**: Songs are downloaded on-the-fly and cached for future use
- **External Playlist Support**: Search, discover, and download playlists from Deezer, Qobuz, and SquidWTF with automatic M3U generation
- **Hi-Res Audio Support**: SquidWTF provider supports up to 24-bit/192kHz FLAC quality
- **Full Metadata Embedding**: Downloaded files include complete ID3 tags (title, artist, album, track number, year, genre, BPM, ISRC, etc.) and embedded cover art
- **Organized Library**: Downloads are saved in a clean `Artist/Album/Track` folder structure
- **Artist Deduplication**: Merges local and streaming provider artists to avoid duplicates in search results
- **Album Enrichment**: Local albums are enriched with missing tracks from streaming providers
- **Cover Art Proxy**: Serves cover art for external content transparently

## Compatible Clients

### PC

- [Aonsoku](https://github.com/victoralvesf/aonsoku)
- [Feishin](https://github.com/jeffvli/feishin)
- [Subplayer](https://github.com/peguerosdc/subplayer)
- [Aurial](https://github.com/shrimpza/aurial)

### Android

- [Tempus](https://github.com/eddyizm/tempus)
- [Substreamer](https://substreamerapp.com/)

### iOS

- [Narjo](https://www.reddit.com/r/NarjoApp/)
- [Arpeggi](https://www.reddit.com/r/arpeggiApp/)

> **Want to improve client compatibility?** Pull requests are welcome!

### Incompatible Clients

These clients are **not compatible** with octo-fiesta due to architectural limitations:

- [Symfonium](https://symfonium.app/) - Uses offline-first architecture and never queries the server for searches, making streaming provider integration impossible. [See details](https://support.symfonium.app/t/suggestions-on-search-function/1121/)

## Supported Music Providers

- **[SquidWTF](https://tidal.squid.wtf/)** - Quality: FLAC (Hi-Res 24-bit/192kHz & CD-Lossless 16-bit/44.1kHz), AAC
- **[Deezer](https://www.deezer.com/)** - Quality: FLAC, MP3_320, MP3_128
- **[Qobuz](https://www.qobuz.com/)** - Quality: FLAC, FLAC_24_HIGH (Hi-Res 24-bit/192kHz), FLAC_24_LOW, FLAC_16, MP3_320

Choose your preferred provider via the `MUSIC_SERVICE` environment variable. Additional providers may be added in future releases.

## Requirements

- A running Subsonic-compatible server (developed and tested with [Navidrome](https://www.navidrome.org/))
- Credentials for at least one music provider (IF NOT USING SQUIDWTF):
  - **Deezer**: ARL token from browser cookies
  - **Qobuz**: User ID + User Auth Token from browser localStorage ([see Wiki guide](https://github.com/V1ck3s/octo-fiesta/wiki/Getting-Qobuz-Credentials-(User-ID-&-Token)))
- Docker and Docker Compose (recommended) **or** [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) for manual installation

## Quick Start (Docker)

The easiest way to run Octo-Fiestarr is with Docker Compose.

1. **Create your environment file**
   ```bash
   cp .env.example .env
   ```

2. **Edit the `.env` file** with your configuration:
   ```bash
	# Navidrome/Subsonic server URL
	SUBSONIC_URL=http://localhost:4533
	
	# Path where downloaded songs will be stored on the host (only applies if STORAGE_MODE=Permanent)
	DOWNLOAD_PATH=./downloads
	
	# Music service to use: SquidWTF, Deezer, or Qobuz (default: SquidWTF)
	MUSIC_SERVICE=SquidWTF
	
	# ===== SquidWTF CONFIGURATION =====
	# Different quality options for SquidWTF. Only FLAC supported right now
	SQUIDWTF_QUALITY=FLAC
	
	# ===== DEEZER CONFIGURATION =====
	# Deezer ARL token (required if using Deezer)
	# See README.md for instructions on how to get this token
	DEEZER_ARL=your-deezer-arl-token
	
	# Fallback ARL token (optional)
	DEEZER_ARL_FALLBACK=
	
	# Preferred audio quality: FLAC, MP3_320, MP3_128 (optional)
	# If not specified, the highest available quality for your account will be used
	DEEZER_QUALITY=
	
	# ===== QOBUZ CONFIGURATION =====
	# Qobuz user authentication token (required if using Qobuz)
	# Get this from your browser after logging into play.qobuz.com
	# See README.md for detailed instructions
	QOBUZ_USER_AUTH_TOKEN=
	
	# Qobuz user ID (required if using Qobuz)
	# Get this from your browser after logging into play.qobuz.com
	QOBUZ_USER_ID=
	
	# Preferred audio quality: FLAC, FLAC_24_HIGH, FLAC_24_LOW, FLAC_16, MP3_320 (optional)
	# If not specified, the highest available quality will be used
	QOBUZ_QUALITY=
	
	# ===== GENERAL SETTINGS =====
	# External playlists support (optional, default: true)
	# When enabled, allows searching and downloading playlists from Deezer/Qobuz
	# Starring a playlist triggers automatic download of all tracks and creates an M3U file
	ENABLE_EXTERNAL_PLAYLISTS=true
	
	# Playlists directory name (optional, default: playlists)
	# M3U playlist files will be created in {DOWNLOAD_PATH}/{PLAYLISTS_DIRECTORY}/
	PLAYLISTS_DIRECTORY=playlists
	
	# Explicit content filter (optional, default: All)
	# - All: Show all tracks (no filtering)
	# - ExplicitOnly: Exclude clean/edited versions, keep original explicit content
	# - CleanOnly: Only show clean content (naturally clean or edited versions)
	# Note: This only works with Deezer, Qobuz doesn't expose explicit content flags
	EXPLICIT_FILTER=All
	
	# Download mode (optional, default: Track)
	# - Track: Download only the played track
	# - Album: When playing a track, download the entire album in background
	#          The played track is downloaded first, remaining tracks are queued
	DOWNLOAD_MODE=Track
	
	# Storage mode (optional, default: Permanent)
	# - Permanent: Files are saved to the library permanently and registered in Navidrome
	# - Cache: Files are stored in /tmp and automatically cleaned up after CACHE_DURATION_HOURS
	#          Not registered in Navidrome, ideal for streaming without library bloat
	#          Note: On Linux/Docker, you can customize cache location by setting TMPDIR environment variable
	STORAGE_MODE=Permanent
	
	# Cache duration in hours (optional, default: 1)
	# Files older than this duration will be automatically deleted when STORAGE_MODE=Cache
	# Based on last access time (updated each time the file is streamed)
	# Cache location: /tmp/octo-fiesta-cache (or $TMPDIR/octo-fiesta-cache if TMPDIR is set)
	CACHE_DURATION_HOURS=1   
	```

3. **Start the container**
   ```bash
   docker-compose up -d
   ```
   
   The proxy will be available at `http://localhost:5274`.

4. **Configure your Subsonic client**
   
   Point your Subsonic client to `http://localhost:5274` instead of your Navidrome server directly.

> **Tip**: Make sure the `DOWNLOAD_PATH` points to a directory that Navidrome can scan, so downloaded songs appear in your library.

## Configuration

### General Settings

| Setting | Description |
|---------|-------------|
| `Subsonic:Url` | URL of your Navidrome/Subsonic server |
| `Subsonic:MusicService` | Music provider to use: `SquidWTF`, `Deezer`, or `Qobuz` (default: `SquidWTF`) |
| `Library:DownloadPath` | Directory where downloaded songs are stored |

### SquidWTF Settings

| Setting | Description |
|---------|-------------|
| `SquidWTF:Quality` | Preferred audio quality: `FLAC`, `MP3_320`, `MP3_128`. If not specified, the highest available quality for your account will be used |

### Deezer Settings

| Setting | Description |
|---------|-------------|
| `Deezer:Arl` | Your Deezer ARL token (required if using Deezer) |
| `Deezer:ArlFallback` | Backup ARL token if primary fails |
| `Deezer:Quality` | Preferred audio quality: `FLAC`, `MP3_320`, `MP3_128`. If not specified, the highest available quality for your account will be used |

### Qobuz Settings

| Setting | Description |
|---------|-------------|
| `Qobuz:UserAuthToken` | Your Qobuz User Auth Token (required if using Qobuz) - [How to get it](https://github.com/V1ck3s/octo-fiesta/wiki/Getting-Qobuz-Credentials-(User-ID-&-Token)) |
| `Qobuz:UserId` | Your Qobuz User ID (required if using Qobuz) |
| `Qobuz:Quality` | Preferred audio quality: `FLAC`, `FLAC_24_HIGH`, `FLAC_24_LOW`, `FLAC_16`, `MP3_320`. If not specified, the highest available quality will be used |

### External Playlists

Octo-Fiesta supports discovering and downloading playlists from your streaming providers (SquidWTF, Deezer, and Qobuz).

| Setting | Description |
|---------|-------------|
| `Subsonic:EnableExternalPlaylists` | Enable/disable external playlist support (default: `true`) |
| `Subsonic:PlaylistsDirectory` | Directory name where M3U playlist files are created (default: `playlists`) |

**How it works:**
1. Search for playlists from an external provider using the global search in your Subsonic client
2. When you "star" (favorite) a playlist, Octo-Fiesta automatically downloads all tracks
3. An M3U playlist file is created in `{DownloadPath}/playlists/` with relative paths to downloaded tracks
4. Individual tracks are added to the M3U as they are played or downloaded

**Environment variable:**
```bash
# To disable playlists
Subsonic__EnableExternalPlaylists=false
```

> **Note**: Due to client-side filtering, playlists from streaming providers may not appear in the "Playlists" tab of some clients, but will show up in global search results.

### Getting Credentials

#### Deezer ARL Token

See the [Wiki guide](https://github.com/V1ck3s/octo-fiesta/wiki/Getting-Deezer-Credentials-(ARL-Token)) for detailed instructions on obtaining your Deezer ARL token.

#### Qobuz Credentials

See the [Wiki guide](https://github.com/V1ck3s/octo-fiesta/wiki/Getting-Qobuz-Credentials-(User-ID-&-Token)) for detailed instructions on obtaining your Qobuz User ID and User Auth Token.

## Limitations

- **Playlist Search**: Subsonic clients like Aonsoku filter playlists client-side from a cached `getPlaylists` call. Streaming provider playlists appear in global search (`search3`) but not in the Playlists tab filter.
- **Region Restrictions**: Some tracks may be unavailable depending on your region and provider.
- **Token Expiration**: Provider authentication tokens expire and need periodic refresh.

## Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  Subsonic       │────▶│   Octo-Fiesta    │────▶│   Navidrome     │
│  Client         │◀────│   (Proxy)        │◀────│   Server        │
│  (Aonsoku)      │     │                  │     │                 │
└─────────────────┘     └────────┬─────────┘     └─────────────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │ Music Providers │
                        │  - Deezer       │
                        │  - Qobuz        │
                        │  - (more...)    │
                        └─────────────────┘
```

## Manual Installation

If you prefer to run Octo-Fiesta without Docker:

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-username/octo-fiesta.git
   cd octo-fiesta
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure the application**
   
   Edit `octo-fiesta/appsettings.json`:
   ```json
{
  "Subsonic": {
    "Url": "https://navidrome.local.bransonb.com",
    "MusicService": "SquidWTF",
    "ExplicitFilter": "All",
    "DownloadMode": "Track",
    "StorageMode": "Permanent",
    "CacheDurationHours": 1
  },
  "Library": {
    "DownloadPath": "./downloads"
  },
  "Qobuz": {
    "UserAuthToken": "your-qobuz-token",
    "UserId": "your-qobuz-user-id",
    "Quality": "FLAC"
  },
  "Deezer": {
    "Arl": "your-deezer-arl-token",
    "ArlFallback": "",
    "Quality": "FLAC"
  },
  "SquidWTF": {
	"Quality": "FLAC"
  }  
}
```

4. **Run the server**
   ```bash
   cd octo-fiesta
   dotnet run
   ```
   
   The proxy will start on `http://localhost:5274` by default.

5. **Configure your Subsonic client**
   
   Point your Subsonic client to `http://localhost:5274` instead of your Navidrome server directly.

## API Endpoints

The proxy implements the Subsonic API and adds transparent streaming provider integration to:

| Endpoint | Description |
|----------|-------------|
| `GET /rest/search3` | Merged search results from Navidrome + streaming provider (including playlists) |
| `GET /rest/stream` | Streams audio, downloading from provider if needed |
| `GET /rest/getSong` | Returns song details (local or from provider) |
| `GET /rest/getAlbum` | Returns album with tracks from both sources |
| `GET /rest/getArtist` | Returns artist with albums from both sources |
| `GET /rest/getCoverArt` | Proxies cover art for external content |
| `GET /rest/star` | Stars items; triggers automatic playlist download for external playlists |

All other Subsonic API endpoints are passed through to Navidrome unchanged.

## External ID Format

External (streaming provider) content uses typed IDs:

| Type | Format | Example |
|------|--------|---------|
| Song | `ext-{provider}-song-{id}` | `ext-deezer-song-123456`, `ext-qobuz-song-789012` |
| Album | `ext-{provider}-album-{id}` | `ext-deezer-album-789012`, `ext-qobuz-album-456789` |
| Artist | `ext-{provider}-artist-{id}` | `ext-deezer-artist-259`, `ext-qobuz-artist-123` |

Legacy format `ext-deezer-{id}` is also supported (assumes song type).

## Download Folder Structure

Downloaded music is organized as:
```
downloads/
├── Artist Name/
│   ├── Album Title/
│   │   ├── 01 - Track One.mp3
│   │   ├── 02 - Track Two.mp3
│   │   └── ...
│   └── Another Album/
│       └── ...
├── Another Artist/
│   └── ...
└── playlists/
    ├── My Favorite Songs.m3u
    ├── Chill Vibes.m3u
    └── ...
```

Playlists are stored as M3U files with relative paths to downloaded tracks, making them portable and compatible with most music players.

## Metadata Embedding

Downloaded files include:
- **Basic**: Title, Artist, Album, Album Artist
- **Track Info**: Track Number, Total Tracks, Disc Number
- **Dates**: Year, Release Date
- **Audio**: BPM, Duration
- **Identifiers**: ISRC (in comments)
- **Credits**: Contributors/Composers
- **Visual**: Embedded cover art (high resolution)
- **Rights**: Copyright, Label

## Development

### Build
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

### Project Structure

```
octo-fiesta/
├── Controllers/
│   └── SubsonicController.cs              # Main API controller
├── Middleware/
│   └── GlobalExceptionHandler.cs          # Global error handling
├── Models/
│   ├── Domain/                            # Domain entities
│   │   ├── Song.cs
│   │   ├── Album.cs
│   │   └── Artist.cs
│   ├── Settings/                          # Configuration models
│   │   ├── SubsonicSettings.cs
│   │   ├── DeezerSettings.cs
│   │   └── QobuzSettings.cs
│   ├── Download/                          # Download-related models
│   │   ├── DownloadInfo.cs
│   │   └── DownloadStatus.cs
│   ├── Search/
│   │   └── SearchResult.cs
│   └── Subsonic/
│       └── ScanStatus.cs
├── Services/
│   ├── Common/                            # Shared services
│   │   ├── BaseDownloadService.cs         # Template method base class
│   │   ├── PathHelper.cs                  # Path utilities
│   │   ├── Result.cs                      # Result<T> pattern
│   │   └── Error.cs                       # Error types
│   ├── Deezer/                            # Deezer provider
│   │   ├── DeezerDownloadService.cs
│   │   ├── DeezerMetadataService.cs
│   │   └── DeezerStartupValidator.cs
│   ├── Qobuz/                             # Qobuz provider
│   │   ├── QobuzDownloadService.cs
│   │   ├── QobuzMetadataService.cs
│   │   ├── QobuzBundleService.cs
│   │   └── QobuzStartupValidator.cs
│   ├── Local/                             # Local library
│   │   ├── ILocalLibraryService.cs
│   │   └── LocalLibraryService.cs
│   ├── Subsonic/                          # Subsonic API logic
│   │   ├── SubsonicProxyService.cs        # Request proxying
│   │   ├── SubsonicModelMapper.cs         # Model mapping
│   │   ├── SubsonicRequestParser.cs       # Request parsing
│   │   └── SubsonicResponseBuilder.cs     # Response building
│   ├── Validation/                        # Startup validation
│   │   ├── IStartupValidator.cs
│   │   ├── BaseStartupValidator.cs
│   │   ├── SubsonicStartupValidator.cs
│   │   ├── StartupValidationOrchestrator.cs
│   │   └── ValidationResult.cs
│   ├── IDownloadService.cs                # Download interface
│   ├── IMusicMetadataService.cs           # Metadata interface
│   └── StartupValidationService.cs
├── Program.cs                             # Application entry point
└── appsettings.json                       # Configuration

octo-fiesta.Tests/
├── DeezerDownloadServiceTests.cs          # Deezer download tests
├── DeezerMetadataServiceTests.cs          # Deezer metadata tests
├── QobuzDownloadServiceTests.cs           # Qobuz download tests (127 tests)
├── LocalLibraryServiceTests.cs            # Local library tests
├── SubsonicModelMapperTests.cs            # Model mapping tests
├── SubsonicProxyServiceTests.cs           # Proxy service tests
├── SubsonicRequestParserTests.cs          # Request parser tests
└── SubsonicResponseBuilderTests.cs        # Response builder tests
```

### Dependencies

- **BouncyCastle.Cryptography** - Blowfish decryption for Deezer streams
- **TagLibSharp** - ID3 tag and cover art embedding
- **Swashbuckle.AspNetCore** - Swagger/OpenAPI documentation
- **xUnit** - Unit testing framework
- **Moq** - Mocking library for tests
- **FluentAssertions** - Fluent assertion library for tests

## License

GPL-3.0

## Acknowledgments

- [Navidrome](https://www.navidrome.org/) - The excellent self-hosted music server
- [Deezer](https://www.deezer.com/) - Music streaming service
- [Qobuz](https://www.qobuz.com/) - Hi-Res music streaming service
- [Subsonic API](http://www.subsonic.org/pages/api.jsp) - The API specification
