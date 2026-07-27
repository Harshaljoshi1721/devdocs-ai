using DevDocsAI.Domain.Enums;

namespace DevDocsAI.Application.Features.Ingestion;

/// <summary>
/// Decides which uploaded files may be ingested. A file is allowed only if its
/// extension is on the supported list AND it is not classified as a secret.
/// </summary>
public interface IFileFilter
{
    /// <summary>True for sensitive files (env files, keys, certs) that must never be stored.</summary>
    bool IsSecret(string fileName);

    /// <summary>True if the extension is one of the supported code/doc/config types.</summary>
    bool IsSupported(string fileName);

    /// <summary>Supported and not secret.</summary>
    bool IsAllowed(string fileName);

    FileType Categorize(string fileName);
}

public sealed class ExtensionFileFilter : IFileFilter
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".jsx", ".ts", ".tsx", ".py", ".java", ".go", ".rs", ".php", ".rb",
    };

    private static readonly HashSet<string> DocExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".rst",
    };

    private static readonly HashSet<string> ConfigExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".yaml", ".yml", ".xml", ".toml",
    };

    /// <summary>Extensions that always indicate a secret/credential, regardless of allowlist.</summary>
    private static readonly HashSet<string> SecretExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pem", ".key", ".crt", ".cer", ".der", ".pfx", ".p12", ".pkcs12", ".jks", ".keystore",
    };

    /// <summary>File names (basename, no extension) that indicate private keys.</summary>
    private static readonly HashSet<string> SecretNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519",
    };

    public bool IsSecret(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // .env, .env.local, .env.production, etc.
        if (name.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase))
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

    public bool IsSupported(string fileName)
    {
        var ext = Path.GetExtension(Path.GetFileName(fileName));
        return CodeExtensions.Contains(ext) || DocExtensions.Contains(ext) || ConfigExtensions.Contains(ext);
    }

    public bool IsAllowed(string fileName) => IsSupported(fileName) && !IsSecret(fileName);

    public FileType Categorize(string fileName)
    {
        var ext = Path.GetExtension(Path.GetFileName(fileName));
        if (CodeExtensions.Contains(ext)) return FileType.Code;
        if (DocExtensions.Contains(ext)) return FileType.Documentation;
        if (ConfigExtensions.Contains(ext)) return FileType.Configuration;
        return FileType.Other;
    }
}
