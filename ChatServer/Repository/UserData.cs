namespace ChatServer.Repository;

public class UserData
{
    public string Username { get; set; }
    public string Password { get; set; }
    public DateTime LastLogout { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Dizionario che traccia il watermark (ultimo timestamp letto) per ogni mittente di messaggi privati.
    /// Key: username del mittente
    /// Value: timestamp dell'ultimo messaggio letto da quel mittente
    /// Questo evita di dover modificare i file JSONL.
    /// </summary>
    public Dictionary<string, DateTime> ReadWatermarks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
