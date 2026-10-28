namespace XtreamStrmOrganizer;

public static class MovieFolders
{
    public const string Adults = "FILMES ADULTOS";
    public static bool IsAdults(string category) =>
        string.Equals(PathRules.SafeName(category), Adults, StringComparison.OrdinalIgnoreCase);

    public static string CategoryPath(string root, string section, string category) =>
        section == "Filmes" && IsAdults(category)
            ? Path.Combine(root, Adults) : Path.Combine(root, section, category);

    // Same filesystem moves preserve all bytes, including STRMs, artwork and NFOs.
    // Never replace a destination. Conflicts stop before moving any file in that folder.
    public static void Migrate(string root, Action<string> log, CancellationToken token)
    {
        var movies = Path.Combine(root, "Filmes");
        if (!Directory.Exists(movies)) return;
        foreach (var source in Directory.GetDirectories(movies).Where(x => IsAdults(Path.GetFileName(x))))
        {
            token.ThrowIfCancellationRequested();
            var target = Path.Combine(root, Adults);
            Validate(source, target, token);
            Move(source, target, token);
            log("Pasta FILMES ADULTOS movida para o mesmo nível de Filmes, Series e TV ao vivo. STRMs, capas e NFOs preservados.");
        }
    }

    private static void Validate(string source, string target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0 ||
            (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0))
            throw new MediaInputException("A pasta FILMES ADULTOS contém um link de diretório. A mudança foi interrompida para preservar os arquivos.");
        if (File.Exists(target)) throw Conflict(target);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var destination = Path.Combine(target, Path.GetFileName(file));
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new MediaInputException("A pasta FILMES ADULTOS contém um link de arquivo. A mudança foi interrompida para preservar os arquivos.");
            if (Path.Exists(destination)) throw Conflict(destination);
        }
        foreach (var folder in Directory.EnumerateDirectories(source))
            Validate(folder, Path.Combine(target, Path.GetFileName(folder)), token);
    }

    private static MediaInputException Conflict(string path) => new(
        $"Não foi possível mover FILMES ADULTOS: já existe \"{WarningText.Name(path)}\" no destino. " +
        "Nenhum arquivo foi sobrescrito. Confira os arquivos nas duas pastas e resolva o conflito antes de executar novamente.");

    private static void Move(string source, string target, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Directory.Exists(target)) { Directory.Move(source, target); return; }
        foreach (var folder in Directory.GetDirectories(source))
            Move(folder, Path.Combine(target, Path.GetFileName(folder)), token);
        foreach (var file in Directory.GetFiles(source))
        {
            token.ThrowIfCancellationRequested();
            File.Move(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        }
        Directory.Delete(source, recursive: false); // Only the now-empty old directory.
    }
}
