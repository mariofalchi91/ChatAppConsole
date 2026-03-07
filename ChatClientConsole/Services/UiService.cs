using ChatCommons;

namespace ChatClientConsole.Services;

public class UiService
{
    private readonly Lock _consoleLock = new();
    private bool _isPromptVisible = false;

    public void ClearCurrentLine()
    {
        lock (_consoleLock)
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
            _isPromptVisible = false;
        }
    }

    public void PrintMessage(string sender, string content, DateTime time, bool isMe, bool isRead, MessageType type, bool reprintPrompt = true)
    {
        lock (_consoleLock)
        {
            ClearCurrentLine();
            var timestamp = $"[{time:HH:mm.ss}]";
            string prefix = type == MessageType.Private ? "[PRIVATO] " : "";
            var formattedMsg = $"{timestamp} {prefix}{sender}: {content}";

            if (isRead)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else if (isMe)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else if (type == MessageType.Private)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }

            // 4. Stampa effettiva
            Console.WriteLine(formattedMsg);

            // 5. Ripristina colore default
            Console.ResetColor();

            // 6. Ristampa il cursore "> " per permettere all'utente di continuare a scrivere
            // (Si usa false quando stiamo stampando una lista intera di storico per evitare 100 cursori)
            if (reprintPrompt)
            {
                PrintPrompt();
            }
        }
    }

    public void PrintSystemMessage(string text, bool reprintPrompt = true)
    {
        lock (_consoleLock)
        {
            ClearCurrentLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ResetColor();

            if (reprintPrompt)
            {
                PrintPrompt();
            }
        }
    }

    public void PrintPrompt()
    {
        lock (_consoleLock)
        {
            if (_isPromptVisible)
            {
                return;
            }

            Console.Write("> ");
            _isPromptVisible = true;
        }
    }

    public string ReadPassword()
    {
        lock (_consoleLock)
        {
            string pass = "";
            do
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    pass = pass.Substring(0, (pass.Length - 1));
                    Console.Write("\b \b");
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
            } while (true);
            return pass;
        }
    }

    public void Clear()
    {
        lock (_consoleLock)
        {
            Console.Clear();
        }
    }

    public void SetTitle(string title)
    {
        lock (_consoleLock)
        {
            Console.Title = title;
        }
    }

    public void Print(string message, bool isInline = false)
    {
        lock (_consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (isInline)
            {
                Console.Write(message);
            }
            else
            {
                Console.WriteLine(message);
            }                
            Console.ResetColor();
        }
    }

    public string ReadInput(bool reprintPrompt = true)
    {
        if (reprintPrompt)
        {
            lock (_consoleLock)
            {
                PrintPrompt();
            }
        }
        lock (_consoleLock)
        {
            _isPromptVisible = false;
        }
        string input = Console.ReadLine();
        return input ?? string.Empty;
    }
}