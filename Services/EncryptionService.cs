using System.Security.Cryptography;
using System.Text;

namespace CnssProxy.Services;

public class EncryptionService
{
    private readonly byte[] _key;

    public EncryptionService(IConfiguration config)
    {
        var keyString =
            config["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key is required in configuration.");

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
    }

    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var combined = new byte[28 + ciphertext.Length];
        nonce.CopyTo(combined, 0);
        tag.CopyTo(combined, 12);
        ciphertext.CopyTo(combined, 28);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string ciphertextBase64)
    {
        var combined = Convert.FromBase64String(ciphertextBase64);
        var nonce = combined[..12];
        var tag = combined[12..28];
        var ciphertext = combined[28..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
