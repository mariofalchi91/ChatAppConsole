namespace ChatCommons;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid(); // TODO utile per la persistenza?
    public MessageType Type { get; set; }
    public string Sender { get; set; }
    public string Receiver { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
}
