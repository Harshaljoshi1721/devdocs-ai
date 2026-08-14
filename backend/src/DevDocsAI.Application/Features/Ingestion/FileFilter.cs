using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Application.Features.Ingestion;

/// <summary>
/// Decides which files may be ingested. Accept-by-default: any text file is allowed
/// unless it is a secret or a binary. Binaries are caught first by a known-binary
/// extension denylist, then by content sniffing (a NUL byte in the first bytes).
/// </summary>
public interface IFileFilter
{
    /// <summary>True for sensitive files (env files, keys, certs) that must never be stored.</summary>
    bool IsSecret(string fileName);

    /// <summary>True if the extension is a known binary type we cannot index as text.</summary>
    bool IsBinaryExtension(string fileName);

    /// <summary>True if a byte sample looks binary (contains a NUL byte).</summary>
    bool LooksBinary(ReadOnlySpan<byte> sample);

    /// <summary>Cheap filename-only pre-filter: not a secret and not a known binary extension.</summary>
    bool IsAllowed(string fileName);

    FileType Categorize(string fileName);
}

public sealed class ExtensionFileFilter : IFileFilter
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".jsx", ".ts", ".tsx", ".py", ".java", ".go", ".rs", ".php", ".rb",
        ".c", ".h", ".cpp", ".hpp", ".cc", ".swift", ".kt", ".kts", ".scala", ".clj", ".ex",
        ".exs", ".erl", ".dart", ".lua", ".r", ".m", ".mm", ".vue", ".svelte", ".sql", ".sh",
        ".bash", ".zsh", ".ps1", ".pl", ".html", ".htm", ".css", ".scss", ".sass", ".less",
    };

    private static readonly HashSet<string> DocExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".rst", ".adoc", ".mdx",
    };

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yaml", ".yml", ".xml", ".toml", ".ini", ".cfg", ".conf", ".properties",
    };

    /// <summary>Extensions that always indicate a secret/credential.</summary>
    private static readonly HashSet<string> SecretExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pem", ".key", ".crt", ".cer", ".der", ".pfx", ".p12", ".pkcs12", ".jks", ".keystore",
    };

    /// <summary>File names (basename, no extension) that indicate private keys.</summary>
    private static readonly HashSet<string> SecretNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519",
    };

    /// <summary>Known binary extensions we cannot embed as text. Content sniffing catches the rest.</summary>
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // images
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".tif", ".tiff", ".heic", ".avif",
        // video
        ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv", ".wmv", ".m4v",
        // audio
        ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma",
        // archives
        ".zip", ".tar", ".gz", ".tgz", ".rar", ".7z", ".bz2", ".xz", ".zst",
        // compiled / executables
        ".exe", ".dll", ".so", ".dylib", ".o", ".a", ".lib", ".class", ".jar", ".wasm",
        ".pyc", ".pyo", ".node", ".bin", ".obj",
        // documents (binary)
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods",
        // fonts
        ".ttf", ".otf", ".woff", ".woff2", ".eot",
        // databases
        ".db", ".sqlite", ".sqlite3", ".mdb",
        // disk / package images
        ".iso", ".dmg", ".pkg", ".deb", ".rpm",
        // design
        ".psd", ".ai", ".sketch", ".fig", ".xcf",
        // misc data
        ".dat", ".pack", ".idx",
    };

    public bool IsSecret(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // .env, .env.local, .env.production, etc. (but not .env.example — a template)
        if (name.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            (name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) &&
             !name.Equals(".env.example", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (SecretExtensions.Contains(Path.GetExtension(name)))
        {
            return true;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        return SecretNames.Contains(name) || SecretNames.Contains(stem);
    }

    public bool IsBinaryExtension(string fileName)
    {
        var ext = Path.GetExtension(Path.GetFileName(fileName));
        return ext.Length > 0 && BinaryExtensions.Contains(ext);
    }

    public bool LooksBinary(ReadOnlySpan<byte> sample)
    {
        // A NUL byte in the leading bytes is the classic, low-false-positive "this is binary"
        // signal (the same heuristic Git uses). Plain UTF-8/16 source never contains one.
        return sample.IndexOf((byte)0) >= 0;
    }

    public bool IsAllowed(string fileName) => !IsSecret(fileName) && !IsBinaryExtension(fileName);

    public FileType Categorize(string fileName)
    {
        var ext = Path.GetExtension(Path.GetFileName(fileName));
        if (CodeExtensions.Contains(ext)) return FileType.Code;
        if (DocExtensions.Contains(ext)) return FileType.Documentation;
        if (ConfigExtensions.Contains(ext)) return FileType.Configuration;
        return FileType.Other;
    }
}
