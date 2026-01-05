using Microsoft.Data.Sqlite;
using Zarla.Core.Data.Models;

namespace Zarla.Core.Data;

public class Database : IDisposable
{
    private readonly SqliteConnection _connection;
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Zarla", "zarla.db");

    public Database()
    {
        var directory = Path.GetDirectoryName(DbPath)!;
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        _connection = new SqliteConnection($"Data Source={DbPath}");
        _connection.Open();
        Initialize();
    }

    private void Initialize()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                url TEXT NOT NULL,
                title TEXT NOT NULL,
                visited_at TEXT NOT NULL,
                visit_count INTEGER DEFAULT 1,
                favicon TEXT
            );

            CREATE TABLE IF NOT EXISTS bookmark_folders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                parent_id INTEGER,
                created_at TEXT NOT NULL,
                sort_order INTEGER DEFAULT 0,
                FOREIGN KEY (parent_id) REFERENCES bookmark_folders(id)
            );

            CREATE TABLE IF NOT EXISTS bookmarks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                url TEXT NOT NULL,
                title TEXT NOT NULL,
                favicon TEXT,
                folder_id INTEGER,
                created_at TEXT NOT NULL,
                sort_order INTEGER DEFAULT 0,
                FOREIGN KEY (folder_id) REFERENCES bookmark_folders(id)
            );

            CREATE TABLE IF NOT EXISTS downloads (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                url TEXT NOT NULL,
                file_name TEXT NOT NULL,
                file_path TEXT NOT NULL,
                total_bytes INTEGER,
                received_bytes INTEGER DEFAULT 0,
                status TEXT NOT NULL,
                started_at TEXT NOT NULL,
                completed_at TEXT,
                mime_type TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_history_url ON history(url);
            CREATE INDEX IF NOT EXISTS idx_history_visited ON history(visited_at DESC);
            CREATE INDEX IF NOT EXISTS idx_bookmarks_folder ON bookmarks(folder_id);
            """;
        cmd.ExecuteNonQuery();
    }

    // History Methods
    public async Task AddHistoryEntryAsync(string url, string title, string? favicon = null)
    {
        await using var cmd = _connection.CreateCommand();

        // Check if URL exists
        cmd.CommandText = "SELECT id, visit_count FROM history WHERE url = @url";
        cmd.Parameters.AddWithValue("@url", url);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var count = reader.GetInt32(1);
            reader.Close();

            cmd.CommandText = "UPDATE history SET title = @title, visited_at = @visited, visit_count = @count, favicon = @favicon WHERE id = @id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@visited", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@count", count + 1);
            cmd.Parameters.AddWithValue("@favicon", favicon ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@id", id);
        }
        else
        {
            reader.Close();
            cmd.CommandText = "INSERT INTO history (url, title, visited_at, favicon) VALUES (@url, @title, @visited, @favicon)";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@visited", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@favicon", favicon ?? (object)DBNull.Value);
        }

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<HistoryEntry>> GetHistoryAsync(int limit = 100, int offset = 0)
    {
        var entries = new List<HistoryEntry>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, url, title, visited_at, visit_count, favicon FROM history ORDER BY visited_at DESC LIMIT @limit OFFSET @offset";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new HistoryEntry
            {
                Id = reader.GetInt64(0),
                Url = reader.GetString(1),
                Title = reader.GetString(2),
                VisitedAt = DateTime.Parse(reader.GetString(3)),
                VisitCount = reader.GetInt32(4),
                Favicon = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        return entries;
    }

    public async Task<List<HistoryEntry>> SearchHistoryAsync(string query, int limit = 20)
    {
        var entries = new List<HistoryEntry>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, url, title, visited_at, visit_count, favicon FROM history WHERE url LIKE @query OR title LIKE @query ORDER BY visit_count DESC, visited_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@query", $"%{query}%");
        cmd.Parameters.AddWithValue("@limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new HistoryEntry
            {
                Id = reader.GetInt64(0),
                Url = reader.GetString(1),
                Title = reader.GetString(2),
                VisitedAt = DateTime.Parse(reader.GetString(3)),
                VisitCount = reader.GetInt32(4),
                Favicon = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        return entries;
    }

    public async Task ClearHistoryAsync()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM history";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteHistoryEntryAsync(long id)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM history WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // Bookmark Methods
    public async Task<long> AddBookmarkAsync(string url, string title, string? favicon = null, long? folderId = null)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO bookmarks (url, title, favicon, folder_id, created_at) VALUES (@url, @title, @favicon, @folder, @created) RETURNING id";
        cmd.Parameters.AddWithValue("@url", url);
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@favicon", favicon ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@folder", folderId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("o"));

        var result = await cmd.ExecuteScalarAsync();
        return (long)result!;
    }

    public async Task<List<Bookmark>> GetBookmarksAsync(long? folderId = null)
    {
        var bookmarks = new List<Bookmark>();
        await using var cmd = _connection.CreateCommand();

        if (folderId.HasValue)
        {
            cmd.CommandText = "SELECT id, url, title, favicon, folder_id, created_at, sort_order FROM bookmarks WHERE folder_id = @folder ORDER BY sort_order, created_at";
            cmd.Parameters.AddWithValue("@folder", folderId.Value);
        }
        else
        {
            cmd.CommandText = "SELECT id, url, title, favicon, folder_id, created_at, sort_order FROM bookmarks WHERE folder_id IS NULL ORDER BY sort_order, created_at";
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            bookmarks.Add(new Bookmark
            {
                Id = reader.GetInt64(0),
                Url = reader.GetString(1),
                Title = reader.GetString(2),
                Favicon = reader.IsDBNull(3) ? null : reader.GetString(3),
                FolderId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                SortOrder = reader.GetInt32(6)
            });
        }
        return bookmarks;
    }

    public async Task DeleteBookmarkAsync(long id)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM bookmarks WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsBookmarkedAsync(string url)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bookmarks WHERE url = @url";
        cmd.Parameters.AddWithValue("@url", url);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    // Download Methods
    public async Task<long> AddDownloadAsync(Download download)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO downloads (url, file_name, file_path, total_bytes, received_bytes, status, started_at, mime_type)
            VALUES (@url, @fileName, @filePath, @total, @received, @status, @started, @mime) RETURNING id
            """;
        cmd.Parameters.AddWithValue("@url", download.Url);
        cmd.Parameters.AddWithValue("@fileName", download.FileName);
        cmd.Parameters.AddWithValue("@filePath", download.FilePath);
        cmd.Parameters.AddWithValue("@total", download.TotalBytes);
        cmd.Parameters.AddWithValue("@received", download.ReceivedBytes);
        cmd.Parameters.AddWithValue("@status", download.Status.ToString());
        cmd.Parameters.AddWithValue("@started", download.StartedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@mime", download.MimeType ?? (object)DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return (long)result!;
    }

    public async Task UpdateDownloadProgressAsync(long id, long receivedBytes, DownloadStatus status)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE downloads SET received_bytes = @received, status = @status WHERE id = @id";
        cmd.Parameters.AddWithValue("@received", receivedBytes);
        cmd.Parameters.AddWithValue("@status", status.ToString());
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CompleteDownloadAsync(long id, DownloadStatus status)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE downloads SET status = @status, completed_at = @completed WHERE id = @id";
        cmd.Parameters.AddWithValue("@status", status.ToString());
        cmd.Parameters.AddWithValue("@completed", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Download>> GetDownloadsAsync(int limit = 100)
    {
        var downloads = new List<Download>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id, url, file_name, file_path, total_bytes, received_bytes, status, started_at, completed_at, mime_type FROM downloads ORDER BY started_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            downloads.Add(new Download
            {
                Id = reader.GetInt64(0),
                Url = reader.GetString(1),
                FileName = reader.GetString(2),
                FilePath = reader.GetString(3),
                TotalBytes = reader.GetInt64(4),
                ReceivedBytes = reader.GetInt64(5),
                Status = Enum.Parse<DownloadStatus>(reader.GetString(6)),
                StartedAt = DateTime.Parse(reader.GetString(7)),
                CompletedAt = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                MimeType = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }
        return downloads;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
