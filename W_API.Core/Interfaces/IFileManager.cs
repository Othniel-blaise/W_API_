namespace W_API.Core.Interfaces;

public interface IFileManager
{
    string CopyToDocsFolder(string sourcePath, string originalName);
    void MoveToVerifyFolder(string sourcePath);
    void DeleteFromDocsFolder(string docPath);
    void VerifyWriteAccess();
}
