using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using W_API.Core.Configuration;
using W_API.Core.Interfaces;

namespace W_API.Infrastructure.IO;

public class FileManager : IFileManager
{
    private readonly PathsSettings _paths;
    private readonly ILogger<FileManager> _log;

    public FileManager(IOptions<AppSettings> opts, ILogger<FileManager> log)
    {
        _paths = opts.Value.Paths;
        _log = log;
    }

    public string CopyToDocsFolder(string sourcePath, string originalName)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var safeName = SanitizeName(Path.GetFileNameWithoutExtension(originalName));
        var ext = Path.GetExtension(originalName);
        var uniqueName = $"{timestamp}-{safeName}{ext}";
        var destPath = Path.Combine(_paths.DocsPhysicalFolder, uniqueName);

        Directory.CreateDirectory(_paths.DocsPhysicalFolder);
        File.Copy(sourcePath, destPath, overwrite: false);
        _log.LogDebug("Fichier copié : {Src} → {Dst}", sourcePath, destPath);
        return destPath;
    }

    public void MoveToVerifyFolder(string sourcePath)
    {
        Directory.CreateDirectory(_paths.ToVerifyFolder);
        var dest = Path.Combine(_paths.ToVerifyFolder, Path.GetFileName(sourcePath));
        if (File.Exists(dest))
            dest = Path.Combine(_paths.ToVerifyFolder,
                $"{DateTime.Now:yyyyMMddHHmmss}-{Path.GetFileName(sourcePath)}");
        File.Move(sourcePath, dest, overwrite: false);
        _log.LogDebug("Fichier déplacé vers _a_verifier : {Dest}", dest);
    }

    public void DeleteFromDocsFolder(string webPath)
    {
        var fileName = Path.GetFileName(webPath);
        var fullPath = Path.Combine(_paths.DocsPhysicalFolder, fileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _log.LogWarning("Rollback — fichier supprimé : {Path}", fullPath);
        }
    }

    public void VerifyWriteAccess()
    {
        Directory.CreateDirectory(_paths.DocsPhysicalFolder);
        var probe = Path.Combine(_paths.DocsPhysicalFolder, $".probe_{Guid.NewGuid():N}");
        File.WriteAllText(probe, "probe");
        File.Delete(probe);

        Directory.CreateDirectory(_paths.ToVerifyFolder);
    }

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
