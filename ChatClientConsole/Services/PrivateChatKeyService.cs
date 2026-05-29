using ChatClientConsole.Configs;
using ChatCommons;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ChatClientConsole.Services;

public class PrivateChatKeyService(IOptions<ClientSettings> options)
{
    private readonly ClientSettings _settings = options.Value;
    private readonly Dictionary<string, string> _sessionPairKeys = new(StringComparer.Ordinal);

    private string _username = string.Empty;
    private string _masterKey = string.Empty;

    public bool TryInitializeSession(string username, string loginPassword, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(loginPassword))
        {
            error = "Credenziali di sessione non valide per inizializzare le chiavi E2E.";
            return false;
        }

        _username = username;
        _masterKey = CryptoService.HashCredentials(username, loginPassword);
        _sessionPairKeys.Clear();

        if (!_settings.E2EPrivate.EnableLocalKeyVault)
        {
            return true;
        }

        try
        {
            LoadVault();
            return true;
        }
        catch (Exception ex)
        {
            _sessionPairKeys.Clear();
            error = $"Vault E2E non leggibile: {ex.Message}";
            return false;
        }
    }

    public bool HasKeyForPeer(string peer)
    {
        if (!IsSessionReady() || string.IsNullOrWhiteSpace(peer))
        {
            return false;
        }

        return _sessionPairKeys.ContainsKey(BuildPairId(_username, peer));
    }

    public bool TrySetKeyForPeer(string peer, string sharedSecret, out string error)
    {
        error = string.Empty;

        if (!IsSessionReady())
        {
            error = "Sessione E2E non inizializzata.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(peer) || string.IsNullOrWhiteSpace(sharedSecret))
        {
            error = "Utente o chiave condivisa non validi.";
            return false;
        }

        var pairId = BuildPairId(_username, peer);
        if (_sessionPairKeys.ContainsKey(pairId))
        {
            error = $"Chiave per {peer} gia configurata. Usa #keyreset {peer} prima di reinserirla.";
            return false;
        }

        _sessionPairKeys[pairId] = sharedSecret;

        if (_settings.E2EPrivate.EnableLocalKeyVault)
        {
            PersistVault();
        }

        return true;
    }

    public bool RemoveKeyForPeer(string peer, out string error)
    {
        error = string.Empty;

        if (!IsSessionReady())
        {
            error = "Sessione E2E non inizializzata.";
            return false;
        }

        var pairId = BuildPairId(_username, peer);
        if (!_sessionPairKeys.Remove(pairId))
        {
            error = $"Nessuna chiave presente per {peer}.";
            return false;
        }

        if (_settings.E2EPrivate.EnableLocalKeyVault)
        {
            PersistVault();
        }

        return true;
    }

    public bool TryEncryptForPeer(string peer, string plaintext, out string ciphertext, out string error)
    {
        ciphertext = string.Empty;
        error = string.Empty;

        if (!TryGetPeerKey(peer, out var key, out error))
        {
            return false;
        }

        try
        {
            ciphertext = CryptoService.EncryptMessage(plaintext, key);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Errore durante la cifratura: {ex.Message}";
            return false;
        }
    }

    public bool TryDecryptForPeer(string peer, string ciphertext, out string plaintext, out string error)
    {
        plaintext = string.Empty;
        error = string.Empty;

        if (!TryGetPeerKey(peer, out var key, out error))
        {
            return false;
        }

        try
        {
            plaintext = CryptoService.DecryptMessage(ciphertext, key);
            return true;
        }
        catch (Exception)
        {
            error = $"Impossibile decifrare i messaggi della chat con {peer}. Chiave mancante o non corretta.";
            return false;
        }
    }

    public void ClearSession()
    {
        _username = string.Empty;
        _masterKey = string.Empty;
        _sessionPairKeys.Clear();
    }

    private bool TryGetPeerKey(string peer, out string key, out string error)
    {
        key = string.Empty;
        error = string.Empty;

        if (!IsSessionReady())
        {
            error = "Sessione E2E non inizializzata.";
            return false;
        }

        var pairId = BuildPairId(_username, peer);
        if (!_sessionPairKeys.TryGetValue(pairId, out key))
        {
            error = $"Nessuna chiave E2E per {peer}. Usa #keyset {peer}.";
            return false;
        }

        return true;
    }

    private bool IsSessionReady()
    {
        return !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_masterKey);
    }

    private string BuildPairId(string userA, string userB)
    {
        var normalizedA = userA.Trim().ToLowerInvariant();
        var normalizedB = userB.Trim().ToLowerInvariant();

        return string.CompareOrdinal(normalizedA, normalizedB) <= 0
            ? $"{normalizedA}|{normalizedB}"
            : $"{normalizedB}|{normalizedA}";
    }

    private string ResolveVaultPath()
    {
        var configuredPath = _settings.E2EPrivate.LocalKeyVaultPath;
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(AppContext.BaseDirectory, configuredPath);
    }

    private void LoadVault()
    {
        var path = ResolveVaultPath();
        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var vault = JsonSerializer.Deserialize<E2EPrivateVault>(json) ?? new E2EPrivateVault();
        foreach (var entry in vault.EncryptedKeys)
        {
            var decryptedKey = CryptoService.DecryptMessage(entry.Value, _masterKey);
            _sessionPairKeys[entry.Key] = decryptedKey;
        }
    }

    private void PersistVault()
    {
        var path = ResolveVaultPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encryptedEntries = _sessionPairKeys.ToDictionary(
            pair => pair.Key,
            pair => CryptoService.EncryptMessage(pair.Value, _masterKey),
            StringComparer.Ordinal);

        var vault = new E2EPrivateVault { EncryptedKeys = encryptedEntries };
        var json = JsonSerializer.Serialize(vault, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private sealed class E2EPrivateVault
    {
        public Dictionary<string, string> EncryptedKeys { get; init; } = new(StringComparer.Ordinal);
    }
}
