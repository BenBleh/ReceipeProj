using Nibbles.Models;
using System.Text.Json;

namespace Nibbles.Services;

public sealed class CookingSessionStore
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private static string Root => Path.Combine(FileSystem.Current.AppDataDirectory, "CookingSessions");
    private static string SessionPath(string id) => Guid.TryParse(id, out var uuid)
        ? Path.Combine(Root, uuid + ".json") : throw new ArgumentException("The recipe ID must be a UUID.", nameof(id));

    public async Task<CookingSession?> LoadAsync(string id)
    {
        await gate.WaitAsync();
        try
        {
            var path = SessionPath(id);
            if (!File.Exists(path))
                return null;
            var session = JsonSerializer.Deserialize<CookingSession>(await File.ReadAllTextAsync(path))
                ?? throw new InvalidDataException("The saved cooking session is empty.");
            if (session.RecipeId != id || session.Multiplier <= 0 ||
                !double.IsFinite(session.IngredientsScroll) || !double.IsFinite(session.MethodScroll) ||
                session.IngredientsScroll < 0 || session.MethodScroll < 0 || session.CheckedIngredients.Any(index => index < 0))
                throw new InvalidDataException("The saved cooking session is invalid.");
            if (session.CanResume(DateTime.Now))
                return session;
            File.Delete(path);
            return null;
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(CookingSession session)
    {
        await gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(Root);
            var path = SessionPath(session.RecipeId);
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(session));
            File.Move(temporary, path, overwrite: true);
        }
        finally { gate.Release(); }
    }

    public async Task DeleteAsync(string id)
    {
        await gate.WaitAsync();
        try { File.Delete(SessionPath(id)); }
        finally { gate.Release(); }
    }
}
