using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MusicBeePlugin.Domain;

/// <summary>
///     Utility class that handles encryption
/// </summary>
internal static class AesEncryption
{
    /// <summary>
    ///     Encrypts a string
    /// </summary>
    /// <param name="plainText">Text to be encrypted</param>
    /// <param name="password">Password to encrypt with</param>
    /// <param name="salt">Salt to encrypt with</param>
    /// <param name="passwordIterations">Number of iterations to do</param>
    /// <param name="initialVector">Needs to be 16 ASCII characters long</param>
    /// <param name="keySize">Can be 128, 192, or 256</param>
    /// <returns>An encrypted string</returns>
    public static string Encrypt(string plainText, string password, string salt = "Preposterous",
        int passwordIterations = 1000, string initialVector = "OFRna73m*aze01xY", int keySize = 256)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var initialVectorBytes = Encoding.ASCII.GetBytes(initialVector);
        var saltValueBytes = Encoding.ASCII.GetBytes(salt);
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        var derivedPassword = new Rfc2898DeriveBytes(password, saltValueBytes, passwordIterations);
        var keyBytes = derivedPassword.GetBytes(keySize / 8);
        var symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC };

        using var encryptor = symmetricKey.CreateEncryptor(keyBytes, initialVectorBytes);
        using var memStream = new MemoryStream();
        using var cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write);

        cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
        cryptoStream.FlushFinalBlock();

        return Convert.ToBase64String(memStream.ToArray());
    }

    /// <summary>
    ///     Decrypts a string
    /// </summary>
    /// <param name="cipherText">Text to be decrypted</param>
    /// <param name="password">Password to decrypt with</param>
    /// <param name="salt">Salt to decrypt with</param>
    /// <param name="passwordIterations">Number of iterations to do</param>
    /// <param name="initialVector">Needs to be 16 ASCII characters long</param>
    /// <param name="keySize">Can be 128, 192, or 256</param>
    /// <returns>A decrypted string</returns>
    public static string Decrypt(string cipherText, string password, string salt = "Preposterous",
        int passwordIterations = 1000, string initialVector = "OFRna73m*aze01xY", int keySize = 256)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        var initialVectorBytes = Encoding.ASCII.GetBytes(initialVector);
        var saltValueBytes = Encoding.ASCII.GetBytes(salt);
        var cipherTextBytes = Convert.FromBase64String(cipherText);
        var derivedPassword = new Rfc2898DeriveBytes(password, saltValueBytes, passwordIterations);
        var keyBytes = derivedPassword.GetBytes(keySize / 8);
        var symmetricKey = new RijndaelManaged { Mode = CipherMode.CBC };

        using var decryptor = symmetricKey.CreateDecryptor(keyBytes, initialVectorBytes);
        using var memStream = new MemoryStream(cipherTextBytes);
        using var cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read);

        var plainTextBytes = new byte[cipherTextBytes.Length];
        var byteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);

        return Encoding.UTF8.GetString(plainTextBytes, 0, byteCount);
    }
}