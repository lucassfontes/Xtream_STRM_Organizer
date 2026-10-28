using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace XtreamStrmOrganizer;

public sealed class MediaWarningException(string subject, string failure, Exception cause)
    : Exception("Falha de processamento de mídia.", cause)
{
    public string Subject { get; } = subject;
    public string Failure { get; } = failure;
}

// Only controlled messages are exposed; exception messages can contain signed URLs/passwords.
public sealed class MediaInputException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

public static class WarningText
{
    public static string Name(string value)
    {
        var clean = Regex.Replace(value ?? "", @"https?://\S+", "[endereço oculto]", RegexOptions.IgnoreCase);
        clean = new string(clean.Where(c => !char.IsControl(c)).ToArray()).Replace('"', '\'');
        return clean.Length > 240 ? clean[..240] + "…" : clean;
    }

    public static string Item(string kind, string name) => $"do {kind} \"{Name(name)}\"";
    public static string Episode(string show, string title, int season, int number) =>
        $"do episódio S{season:00}E{number:00} \"{Name(title)}\" da série \"{Name(show)}\"";

    public static string Reason(Exception error) => error switch
    {
        MediaInputException input => input.Reason,
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "O servidor não encontrou o arquivo solicitado (HTTP 404).",
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => "O servidor exigiu autorização para acessar o conteúdo (HTTP 401).",
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => "O servidor bloqueou o acesso ao conteúdo (HTTP 403).",
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "O servidor recebeu muitas solicitações. Tente novamente mais tarde (HTTP 429).",
        HttpRequestException { StatusCode: { } status } when (int)status >= 500 =>
            $"O servidor apresentou uma falha temporária (HTTP {(int)status}).",
        HttpRequestException { StatusCode: { } status } => $"O servidor recusou a solicitação (HTTP {(int)status}).",
        HttpRequestException => "Não foi possível completar a conexão com o servidor. Verifique a rede ou tente mais tarde.",
        OperationCanceledException => "O servidor demorou demais para responder (tempo limite excedido).",
        UnauthorizedAccessException => "O programa não tem permissão para gravar na pasta de destino. Reaplique o Compose atualizado para preparar as subpastas antigas; confirme também que o volume permite escrita.",
        PathTooLongException => "O nome ou caminho do arquivo é longo demais para o sistema.",
        IOException => "Não foi possível acessar ou gravar o arquivo. Verifique espaço livre, permissões e disponibilidade do disco.",
        System.Text.Json.JsonException => "O provedor enviou dados em um formato inválido.",
        _ => "Não foi possível concluir a operação. Verifique os dados do provedor e a pasta de destino."
    };

    public static string Format(string context, Exception error)
    {
        if (error is MediaWarningException media)
            return $"Aviso: {media.Subject} {media.Failure}. Motivo: {Reason(media.InnerException!)}";
        return $"Aviso: não foi possível concluir {Name(context)}. Motivo: {Reason(error)}";
    }
}

public sealed class WarningLog(string configFile)
{
    private readonly object _gate = new();
    public string FilePath { get; } = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configFile))!, "log_avisos.txt");

    public bool Append(string message)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}", new UTF8Encoding(false));
                FilePermissions.Private(FilePath);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        }
    }
}
