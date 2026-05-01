using System.Security.Cryptography;
using System.Text;

namespace ChatCore.Security
{
    public sealed class AesService
    {
        public EncryptedMessage Encrypt(string plainText, byte[] key)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return new EncryptedMessage(
                Convert.ToBase64String(cipherBytes),
                Convert.ToBase64String(aes.IV));
        }

        public string Decrypt(EncryptedMessage encryptedMessage, byte[] key)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = Convert.FromBase64String(encryptedMessage.Iv);

            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] cipherBytes = Convert.FromBase64String(encryptedMessage.CipherText);
            byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }

    public sealed record EncryptedMessage(string CipherText, string Iv);
}
