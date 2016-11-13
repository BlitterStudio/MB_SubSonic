using System;
using System.IO;
using System.Text;
using PCLCrypto;

namespace Subsonic.Domain
{
    /// <summary>
    ///     Utility class that handles encryption
    /// </summary>
    public static class AesEncryption
    {
        #region Static Functions

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
                return "";
            var initialVectorBytes = Encoding.UTF8.GetBytes(initialVector);
            var saltValueBytes = Encoding.UTF8.GetBytes(salt);
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            //var derivedPassword = new Rfc2898DeriveBytes(password, saltValueBytes, passwordIterations);
            var derivedPassword = NetFxCrypto.DeriveBytes.GetBytes(password, saltValueBytes, passwordIterations, keySize);
            //var keyBytes = derivedPassword.GetBytes(keySize/8);
            //var symmetricKey = new RijndaelManaged {Mode = CipherMode.CBC};
            var provider = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithm.AesCbcPkcs7);
            var symmetricKey = provider.CreateSymmetricKey(derivedPassword);

            //using (var encryptor = symmetricKey.CreateEncryptor(keyBytes, initialVectorBytes))
            //{
            //    using (var memStream = new MemoryStream())
            //    {
            //        using (var cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write))
            //        {
            //            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            //            cryptoStream.FlushFinalBlock();
            //            cipherTextBytes = memStream.ToArray();
            //            //memStream.Close();
            //            //cryptoStream.Close();
            //        }
            //    }
            //}
            //symmetricKey.Clear();
            var cipherText = WinRTCrypto.CryptographicEngine.Encrypt(symmetricKey, plainTextBytes, initialVectorBytes);

            return Convert.ToBase64String(cipherText);
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
                return "";
            var initialVectorBytes = Encoding.UTF8.GetBytes(initialVector);
            var saltValueBytes = Encoding.UTF8.GetBytes(salt);
            var cipherTextBytes = Convert.FromBase64String(cipherText);
            //var derivedPassword = new Rfc2898DeriveBytes(password, saltValueBytes, passwordIterations);
            var derivedPassword = NetFxCrypto.DeriveBytes.GetBytes(password, saltValueBytes, passwordIterations, keySize);
            //var keyBytes = derivedPassword.GetBytes(keySize/8);
            //var symmetricKey = new RijndaelManaged {Mode = CipherMode.CBC};
            var provider = WinRTCrypto.SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithm.AesCbcPkcs7);
            var symmetricKey = provider.CreateSymmetricKey(derivedPassword);

            //var plainTextBytes = new byte[cipherTextBytes.Length];
            //int byteCount;
            //using (var decryptor = symmetricKey.CreateDecryptor(keyBytes, initialVectorBytes))
            //{
            //    using (var memStream = new MemoryStream(cipherTextBytes))
            //    {
            //        using (var cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read))
            //        {
            //            byteCount = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
            //            //memStream.Close();
            //            //cryptoStream.Close();
            //        }
            //    }
            //}
            //symmetricKey.Clear();
            var plaintText = WinRTCrypto.CryptographicEngine.Decrypt(symmetricKey, cipherTextBytes, initialVectorBytes);
            var byteCount = plaintText.Length;
            return Encoding.UTF8.GetString(plaintText, 0, byteCount);
        }

        #endregion
    }
}