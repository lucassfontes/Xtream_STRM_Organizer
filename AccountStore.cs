using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace XtreamStrmOrganizer;

public sealed class AccountStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private sealed class Account
    {
        public Account() { }
        public string Username { get; set; } = "";
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
        public int Iterations { get; set; } = 210000;
    }
    public AccountStore(IConfiguration config)
    {
        var configFile = Path.GetFullPath(config["CONFIG_FILE"] ?? "/app/data/config.json");
        _path = Path.Combine(Path.GetDirectoryName(configFile)!, "account.json");
    }
    public bool Initialized { get { lock (_gate) return File.Exists(_path); } }
    public string Username
    {
        get
        {
            lock (_gate)
            {
                if (!File.Exists(_path)) return "";
                return JsonSerializer.Deserialize<Account>(File.ReadAllText(_path))?.Username ?? "";
            }
        }
    }
    public bool Setup(string username, string password)
    {
        username = (username ?? "").Trim();
        if (username.Length is < 3 or > 32 || !username.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
            throw new ArgumentException("Usuário deve ter 3 a 32 caracteres (letras, números, ponto, _ ou -).");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6 || password.Length > 256)
            throw new ArgumentException("A senha precisa ter de 6 a 256 caracteres.");
        lock (_gate)
        {
            if (File.Exists(_path)) return false;
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 210000, HashAlgorithmName.SHA256, 32);
            var account = new Account { Username = username, Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(hash) };
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonSerializer.Serialize(account), new UTF8Encoding(false));
                FilePermissions.Private(tmp);
                File.Move(tmp, _path); // Sem substituir se a conta já existir.
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
            return true;
        }
    }
    public bool Verify(string username, string password)
    {
        Account? a;
        lock (_gate)
        {
            if (!File.Exists(_path)) return false;
            a = JsonSerializer.Deserialize<Account>(File.ReadAllText(_path));
        }
        if (a is null || username != a.Username) return false;
        var supplied = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password ?? ""), Convert.FromBase64String(a.Salt), a.Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(supplied, Convert.FromBase64String(a.Hash));
    }
}
public static class FilePermissions
{
    public static void Private(string path)
    {
        if (!OperatingSystem.IsLinux()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
