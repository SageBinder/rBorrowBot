using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace rBorrowBot.Resources {
public static class Resources {
    public static readonly string ResourcesPath =
        Path.Combine(new[] {AppDomain.CurrentDomain.BaseDirectory, "../../../../rBorrowBot.Resources/Resources/"});

    public static string ReadAllText(string resourceName) {
        return File.ReadAllText(GetResourcePath(resourceName));
    }

    public static Task<string> ReadAllTextAsync(string resourceName) {
        return File.ReadAllTextAsync(GetResourcePath(resourceName));
    }

    public static FileStream OpenResourceRead(string resourceName) {
        return File.OpenRead(GetResourcePath(resourceName));
    }

    public static FileStream OpenResourceWrite(string resourceName) {
        return File.OpenWrite(GetResourcePath(resourceName));
    }

    public static FileStream OpenResource(string resourceName, FileMode fileMode) {
        return File.Open(GetResourcePath(resourceName), fileMode);
    }

    public static bool ResourceExists(string resourceName) {
        return File.Exists(GetResourcePath(resourceName));
    }

    public static FileStream CreateNew(string resourceName) {
        return File.Create(GetResourcePath(resourceName));
    }

    public static string GetResourcePath(string resourceName) {
        return $"{ResourcesPath}{resourceName}";
    }
    
    public static void WriteTo(string filename, string content) {
        var fs = ResourceExists(filename)
            ? OpenResourceWrite(filename)
            : CreateNew(filename);
        var lastPostId = new UTF8Encoding(true).GetBytes(content);
        fs.Write(lastPostId, 0, content.Length);
    }
}
}