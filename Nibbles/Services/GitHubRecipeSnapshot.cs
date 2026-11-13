using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nibbles.Services;

public sealed record GitHubCachedSnapshot(string Directory, GitHubHead Head);

public sealed class GitHubRecipeSnapshot(GitHubRecipeRepository repository, string cache)
{
    public async Task<GitHubCachedSnapshot> DownloadAsync(GitHubHead head, GitHubCachedSnapshot? previous)
    {
        var indexBytes = await ReadCachedAsync("MasterList.csv", head, previous)
            ?? await repository.ReadBlobAsync(head.Files["MasterList.csv"]);
        Validate("MasterList.csv", indexBytes, head.Files["MasterList.csv"]);
        var entries = GitHubRecipeRepository.ParseIndex(indexBytes);
        var wanted = new HashSet<string>(StringComparer.Ordinal) { "MasterList.csv" };
        foreach (var entry in entries)
        {
            wanted.Add(entry.Id + ".json");
            foreach (var path in head.Files.Keys.Where(path => GitHubRecipeRepository.IsPhotoPath(entry.Id, path)))
                wanted.Add(path);
        }
        if (wanted.Any(path => !head.Files.ContainsKey(path)))
            throw new InvalidDataException("A listed recipe is missing from the GitHub snapshot.");

        Directory.CreateDirectory(cache);
        var name = head.Commit + "-" + Guid.NewGuid().ToString("N");
        var stage = Path.Combine(cache, name);
        var archive = Path.Combine(cache, name + ".zip");
        Directory.CreateDirectory(stage);
        var promoted = false;
        try
        {
            if (previous is null)
            {
                await repository.DownloadArchiveAsync(head.Commit, archive);
                using var zip = ZipFile.OpenRead(archive);
                var prefix = zip.Entries.FirstOrDefault()?.FullName.Split('/')[0] + "/";
                foreach (var file in wanted)
                {
                    var entry = zip.GetEntry(prefix + file) ?? throw new InvalidDataException("The GitHub archive is incomplete.");
                    await using var input = entry.Open();
                    using var output = new MemoryStream();
                    await input.CopyToAsync(output);
                    await WriteAsync(stage, file, output.ToArray(), head.Files[file]);
                }
            }
            else
            {
                foreach (var file in wanted)
                {
                    var bytes = file == "MasterList.csv" ? indexBytes :
                        await ReadCachedAsync(file, head, previous) ?? await repository.ReadBlobAsync(head.Files[file]);
                    await WriteAsync(stage, file, bytes, head.Files[file]);
                }
            }
            var snapshot = new GitHubCachedSnapshot(name, head);
            var pointer = Path.Combine(cache, "current.json");
            await File.WriteAllTextAsync(pointer + ".tmp", JsonSerializer.Serialize(snapshot));
            File.Move(pointer + ".tmp", pointer, true);
            promoted = true;
            return snapshot;
        }
        finally
        {
            if (File.Exists(archive)) File.Delete(archive);
            if (!promoted) Directory.Delete(stage, true);
        }
    }

    private async Task<byte[]?> ReadCachedAsync(string file, GitHubHead head, GitHubCachedSnapshot? previous)
    {
        if (previous is null || !previous.Head.Files.TryGetValue(file, out var sha) ||
            !sha.Equals(head.Files[file], StringComparison.OrdinalIgnoreCase))
            return null;
        var path = Path.Combine(cache, previous.Directory, file.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            Debug.WriteLine($"Cached recipe file is missing; downloading its GitHub revision: {file}");
            return null;
        }
        var bytes = await File.ReadAllBytesAsync(path);
        if (Matches(bytes, sha)) return bytes;
        Debug.WriteLine($"Cached recipe file does not match its GitHub revision; downloading it again: {file}");
        return null;
    }

    private static async Task WriteAsync(string stage, string file, byte[] bytes, string sha)
    {
        Validate(file, bytes, sha);
        var target = Path.Combine(stage, file.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllBytesAsync(target, bytes);
    }

    private static void Validate(string file, byte[] bytes, string sha)
    {
        if (!Matches(bytes, sha))
            throw new InvalidDataException("A downloaded recipe file does not match its GitHub revision.");
        if (file.EndsWith(".json", StringComparison.Ordinal))
        {
            var recipe = RecipeWritePreparation.Read(bytes);
            if (recipe.Id + ".json" != file)
                throw new InvalidDataException("A downloaded recipe has an inconsistent ID.");
        }
    }

    private static bool Matches(byte[] bytes, string sha)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"));
        hash.AppendData(bytes);
        return Convert.ToHexString(hash.GetHashAndReset()).Equals(sha, StringComparison.OrdinalIgnoreCase);
    }
}
