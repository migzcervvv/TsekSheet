using System.Security.Cryptography;
using System.Text.Json;

namespace TsekSheet.ExcelNative.Poc;

public static class ExperimentPaths
{
    public static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && File.Exists(Path.Combine(directory.FullName, "GOALS.md"))) return directory.FullName;
            throw new ExperimentException("REPOSITORY_ROOT_UNAVAILABLE");
        }
    }
    public static string CreateRun()
    {
        var root = Path.Combine(RepositoryRoot, "artifacts", "excel-native-poc", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); ValidateRoot(root); return root;
    }
    public static void ValidateRoot(string root)
    {
        var full = Path.GetFullPath(root);
        var expected = Path.Combine(RepositoryRoot, "artifacts", "excel-native-poc");
        if (!Path.IsPathFullyQualified(root) || Path.GetDirectoryName(full) != expected || !Guid.TryParseExact(Path.GetFileName(full), "N", out _))
            throw new ExperimentException("EXPERIMENT_PATH_REJECTED");
        for (var directory = new DirectoryInfo(full); directory != null; directory = directory.Parent)
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new ExperimentException("EXPERIMENT_REPARSE_OR_MISSING_PATH");
    }
    public static void Validate(ExperimentRequest request)
    {
        ValidateRoot(request.Root);
        if (request.Version != 1 || request.Operation is not ("probe" or "noop")) throw new ExperimentException("REQUEST_REJECTED");
        if (request.Operation == "noop" && (request.SourceSha256?.Length != 64 || !request.SourceSha256.All(Uri.IsHexDigit))) throw new ExperimentException("SOURCE_BINDING_REQUIRED");
    }
    public static void CheckCancellation(string root)
    {
        if (File.Exists(Path.Combine(root, "cancel"))) throw new ExperimentException("CANCELLED");
    }
    public static string Hash(string path) { using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); return Hash(input); }
    public static string Hash(Stream input) { input.Position = 0; var result = Convert.ToHexString(SHA256.HashData(input)); input.Position = 0; return result; }
    public static void WriteNew<T>(string path, T value)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, value, new JsonSerializerOptions { WriteIndented = true });
    }
}
