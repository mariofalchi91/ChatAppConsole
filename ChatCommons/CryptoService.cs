using System.Security.Cryptography;
using System.Text;

namespace ChatCommons;

/// <summary>
/// Provides cryptographic services for hashing credentials and encrypting/decrypting messages using AES-GCM with PBKDF2 key derivation.
/// </summary>
public class CryptoService
{
    /// <summary>
    /// Size of the randomly generated salt used for PBKDF2, in bytes.
    /// </summary>
    /// <remarks>
    /// Typical salt sizes are 16 bytes or larger to prevent precomputation attacks.
    /// </remarks>
    private const int SaltSize = 16;      // bytes

    /// <summary>
    /// Size of the nonce/IV for AES-GCM, in bytes.
    /// </summary>
    /// <remarks>
    /// 12 bytes is the recommended nonce size for AES-GCM and provides best interoperability.
    /// </remarks>
    private const int NonceSize = 12;     // bytes (recommended for AES-GCM)

    /// <summary>
    /// Desired length of the derived AES key, in bytes.
    /// </summary>
    /// <remarks>
    /// 32 bytes = 256-bit key for AES-256.
    /// </remarks>
    private const int KeySize = 32;       // 256-bit key

    /// <summary>
    /// Size of the authentication tag produced by AES-GCM, in bytes.
    /// </summary>
    /// <remarks>
    /// 16 bytes = 128-bit tag, common and recommended for AES-GCM.
    /// </remarks>
    private const int TagSize = 16;       // 128-bit tag

    /// <summary>
    /// Number of iterations used by PBKDF2 (Rfc2898) to derive the key from the password.
    /// </summary>
    /// <remarks>
    /// A higher count increases computation cost for attackers. Tune based on your environment.
    /// </remarks>
    private const int Iterations = 100_000;

    /// <summary>
    /// Encrypts the specified plaintext using AES-GCM with a key derived from the provided password.
    /// </summary>
    /// <param name="message">The UTF-8 string to encrypt. Must not be null.</param>
    /// <param name="password">The password used to derive the encryption key. Must not be null or empty.</param>
    /// <returns>
    /// A Base64-encoded string containing the concatenation: salt || nonce || tag || ciphertext.
    /// The receiver must split these fields in the same order to decrypt.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="password"/> is null or empty.</exception>
    /// <remarks>
    /// Steps performed:
    /// 1. Generate a random salt and nonce.
    /// 2. Derive an AES key from the password and salt using PBKDF2 (SHA-256).
    /// 3. Encrypt the UTF-8 plaintext with AES-GCM producing ciphertext and authentication tag.
    /// 4. Concatenate salt || nonce || tag || ciphertext and return as Base64.
    /// </remarks>
    public static string EncryptMessage(string message, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize
        );

        var plaintext = Encoding.UTF8.GetBytes(message);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        // result = salt || nonce || tag || ciphertext
        var result = new byte[SaltSize + NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
        Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
        Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, SaltSize + NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a Base64-encoded payload produced by <see cref="EncryptMessage(string,string)"/> using the same password.
    /// </summary>
    /// <param name="cipherBase64">The Base64 string produced by <see cref="EncryptMessage(string,string)"/> (salt || nonce || tag || ciphertext).</param>
    /// <param name="password">The password used to derive the AES key for decryption. Must not be null or empty.</param>
    /// <returns>The decrypted plaintext as a UTF-8 string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cipherBase64"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="password"/> is null or empty, or when the input is malformed.</exception>
    /// <exception cref="CryptographicException">
    /// Thrown when authentication fails during AES-GCM decryption (tag validation fails) or other cryptographic errors occur.
    /// </exception>
    /// <remarks>
    /// Steps performed:
    /// 1. Decode the Base64 input and split into salt, nonce, tag and ciphertext.
    /// 2. Derive the AES key from the password and extracted salt using PBKDF2.
    /// 3. Attempt AES-GCM decryption; if tag verification fails, a <see cref="CryptographicException"/> is thrown.
    /// 4. Return the UTF-8 decoded plaintext.
    /// </remarks>
    public static string DecryptMessage(string cipherBase64, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var data = Convert.FromBase64String(cipherBase64);

        if (data.Length < SaltSize + NonceSize + TagSize)
            throw new ArgumentException("Invalid cipher text.", nameof(cipherBase64));

        var salt = new byte[SaltSize];
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertextLength = data.Length - SaltSize - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];

        Buffer.BlockCopy(data, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(data, SaltSize, nonce, 0, NonceSize);
        Buffer.BlockCopy(data, SaltSize + NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(data, SaltSize + NonceSize + TagSize, ciphertext, 0, ciphertextLength);

        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize
        );

        var plaintext = new byte[ciphertextLength];

        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>
    /// Computes a hash of the given username and password using PBKDF2 with SHA-256.
    /// The username is used as a deterministic salt (converted to lowercase).
    /// </summary>
    /// <param name="username">The username to use as a salt.</param>
    /// <param name="password">The password to hash.</param>
    /// <returns>The computed hash as a hexadecimal string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="username"/> or <paramref name="password"/> is null.</exception>
    public static string HashCredentials(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        byte[] salt = Encoding.UTF8.GetBytes(username.ToLowerInvariant());

        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize
        );

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
