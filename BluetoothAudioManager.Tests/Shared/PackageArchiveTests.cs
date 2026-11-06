using System.IO.Compression;
using BluetoothAudioManager.Shared.Helpers;

namespace BluetoothAudioManager.Tests.Shared;

public class PackageArchiveTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"PackageArchiveTests-{Guid.NewGuid():N}");

    [Fact]
    public void ExtractFolder_TakesOnlyThatFolder_WithEitherSlash()
    {
        using var archive = Archive(("app/App.exe", "app"), (@"app\runtimes\native.dll", "native"), ("updater/Updater.exe", "updater"));
        var destination = Path.Combine(_folder, "app");

        PackageArchive.ExtractFolder(archive, "app", destination);

        Assert.Equal("app", File.ReadAllText(Path.Combine(destination, "App.exe")));
        Assert.Equal("native", File.ReadAllText(Path.Combine(destination, "runtimes", "native.dll")));
        Assert.False(File.Exists(Path.Combine(destination, "Updater.exe")));
    }

    [Fact]
    public void ExtractFolder_EmptiesTheDestinationFirst()
    {
        var destination = Path.Combine(_folder, "app");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "old.dll"), "old");
        using var archive = Archive(("app/App.exe", "app"));

        PackageArchive.ExtractFolder(archive, "app", destination);

        Assert.False(File.Exists(Path.Combine(destination, "old.dll")));
    }

    [Fact]
    public void ExtractFolder_RefusesFilesOutsideTheFolder()
    {
        using var archive = Archive(("app/../../outside.exe", "bad"));

        Assert.Throws<InvalidDataException>(() => PackageArchive.ExtractFolder(archive, "app", Path.Combine(_folder, "app")));
        Assert.False(File.Exists(Path.Combine(_folder, "..", "outside.exe")));
    }

    [Fact]
    public void ExtractFolder_WithoutThatFolder_Throws()
    {
        using var archive = Archive(("updater/Updater.exe", "updater"));

        Assert.Throws<InvalidDataException>(() => PackageArchive.ExtractFolder(archive, "app", Path.Combine(_folder, "app")));
    }

    private static ZipArchive Archive(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var writer = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var entry = new StreamWriter(writer.CreateEntry(name).Open());
                entry.Write(content);
            }
        }

        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
