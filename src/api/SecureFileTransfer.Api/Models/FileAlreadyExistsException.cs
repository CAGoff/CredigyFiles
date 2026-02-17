namespace SecureFileTransfer.Api.Models;

public class FileAlreadyExistsException : Exception
{
    public FileAlreadyExistsException(string fileName, string containerName, string directory)
        : base($"File '{fileName}' already exists in {containerName}/{directory}.")
    {
        FileName = fileName;
        ContainerName = containerName;
        Directory = directory;
    }

    public string FileName { get; }
    public string ContainerName { get; }
    public string Directory { get; }
}
