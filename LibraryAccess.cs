namespace XtreamStrmOrganizer;

public static class LibraryAccess
{
    // A successful probe at /media alone says nothing about legacy subfolders.
    // Check selected sections only; never follow directory links out of the library.
    public static void Verify(string root, bool live, bool movies, bool series, CancellationToken token)
    {
        Probe(root, token);
        foreach (var section in new[] { (live, "TV ao vivo"), (movies, "Filmes"), (series, "Series"), (movies, MovieFolders.Adults) })
        {
            if (!section.Item1) continue;
            var path = Path.Combine(root, section.Item2);
            if (!Directory.Exists(path)) continue;
            var pending = new Stack<string>();
            pending.Push(path);
            while (pending.TryPop(out var folder))
            {
                token.ThrowIfCancellationRequested();
                Probe(folder, token);
                try
                {
                    foreach (var child in Directory.EnumerateDirectories(folder))
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                            throw new MediaInputException($"A pasta \"{WarningText.Name(child)}\" é um link. Use uma pasta real dentro da biblioteca.");
                        pending.Push(child);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { throw Failure(folder, ex); }
            }
        }
    }

    private static void Probe(string folder, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            Directory.CreateDirectory(folder);
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                throw new MediaInputException($"A pasta \"{WarningText.Name(folder)}\" é um link. Use uma pasta real dentro da biblioteca.");
            var probe = Path.Combine(folder, ".xtream-write-test-" + Guid.NewGuid().ToString("N"));
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                1, FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            stream.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw Failure(folder, ex); }
    }

    private static MediaInputException Failure(string folder, Exception ex) => new(
        $"Não foi possível gravar na pasta \"{WarningText.Name(folder)}\". {WarningText.Reason(ex)} " +
        "A organização foi interrompida antes de consultar o provedor. Os arquivos existentes foram preservados.");
}
