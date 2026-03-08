using ChatCommons;

namespace TestProject1;

public class CryptoServiceTest
{
    [Fact]
    public void Encrypt_And_Decrypt_ValidInputs_ReturnsOriginalMessage()
    {
        // Arrange
        var message = "sopra la panca la capra campa";
        var password = "sotto la panca la capra crepa";

        // Act
        var encrypted = CryptoService.EncryptMessage(message, password);
        var decrypted = CryptoService.DecryptMessage(encrypted, password);

        // Assert
        Assert.Equal(message, decrypted);
    }

    [Fact]
    public void Encrypt_SameMessagePassword_ProducesDifferentCiphertexts()
    {
        // Arrange
        var message = "test message";
        var password = "test password";

        // Act
        var encrypted1 = CryptoService.EncryptMessage(message, password);
        var encrypted2 = CryptoService.EncryptMessage(message, password);

        // Assert
        // Due to random salt and nonce, ciphertexts should be different
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Decrypt_WrongPassword_ThrowsCryptographicException()
    {
        // Arrange
        var message = "secret message";
        var correctPassword = "correct password";
        var wrongPassword = "wrong password";
        var encrypted = CryptoService.EncryptMessage(message, correctPassword);

        // Act & Assert
        // Accept any CryptographicException or derived types (e.g. AuthenticationTagMismatchException)
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(
            () => CryptoService.DecryptMessage(encrypted, wrongPassword)
        );
    }

    [Fact]
    public void Encrypt_NullMessage_ThrowsArgumentException()
    {
        // Arrange
        string message = null!;
        var password = "password";

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(
            () => CryptoService.EncryptMessage(message, password)
        );
    }

    [Fact]
    public void Encrypt_EmptyMessage_ThrowsArgumentException()
    {
        // Arrange
        var message = string.Empty;
        var password = "password";

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => CryptoService.EncryptMessage(message, password)
        );
    }

    [Fact]
    public void Encrypt_NullPassword_ThrowsArgumentException()
    {
        // Arrange
        var message = "message";
        string password = null!;

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(
            () => CryptoService.EncryptMessage(message, password)
        );
    }

    [Fact]
    public void Encrypt_EmptyPassword_ThrowsArgumentException()
    {
        // Arrange
        var message = "message";
        var password = string.Empty;

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => CryptoService.EncryptMessage(message, password)
        );
    }

    [Fact]
    public void Decrypt_InvalidBase64_ThrowsFormatException()
    {
        // Arrange
        var invalidBase64 = "this is not valid base64!!!";
        var password = "password";

        // Act & Assert
        Assert.Throws<FormatException>(
            () => CryptoService.DecryptMessage(invalidBase64, password)
        );
    }

    [Fact]
    public void Decrypt_MalformedPayload_ThrowsArgumentException()
    {
        // Arrange
        // Create a Base64 string that's too short (less than salt + nonce + tag)
        var shortPayload = Convert.ToBase64String(new byte[20]); // Only 20 bytes
        var password = "password";

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => CryptoService.DecryptMessage(shortPayload, password)
        );
    }

    [Fact]
    public void Encrypt_LargeMessage_EncryptsSuccessfully()
    {
        // Arrange
        var largeMessage = string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString(), 100_000)); // Large text
        var password = "password";

        // Act
        var encrypted = CryptoService.EncryptMessage(largeMessage, password);
        var decrypted = CryptoService.DecryptMessage(encrypted, password);

        // Assert
        Assert.Equal(largeMessage, decrypted);
    }

    [Fact]
    public void Encrypt_SpecialCharacters_PreservesUtf8Correctly()
    {
        // Arrange
        var message = "Ciao! 你好 مرحبا שלום 🔐 Ñ é ü";
        var password = "password123";

        // Act
        var encrypted = CryptoService.EncryptMessage(message, password);
        var decrypted = CryptoService.DecryptMessage(encrypted, password);

        // Assert
        Assert.Equal(message, decrypted);
    }

    [Fact]
    public void HashCredentials_FixedInput_ReturnsCorrectHash()
    {
        // Arrange
        var username = "12345678";
        var password = "87654321";
        var expectedHash = "2c2e8d1c1972ed1a59b4b92c9d13d30b8ffc0665119f908f8107101ef6841a36";
        // Act
        var actualHash = CryptoService.HashCredentials(username, password);
        // Assert
        Assert.Equal(expectedHash, actualHash);
    }
}
