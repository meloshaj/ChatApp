using System.Security.Cryptography;
using System.Text;

namespace ChatCore.Security
{
    public sealed class RsaService
    {
        public RsaKeyPair GenerateKeyPair()
        {
            using RSA rsa = RSA.Create(2048);
            return new RsaKeyPair(
                Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
                Convert.ToBase64String(rsa.ExportPkcs8PrivateKey()));
        }

        public string Sign(string message, string privateKey)
        {
            using RSA rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);

            byte[] data = Encoding.UTF8.GetBytes(message);
            byte[] signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        public bool Verify(string message, string signature, string publicKey)
        {
            using RSA rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);

            byte[] data = Encoding.UTF8.GetBytes(message);
            byte[] signatureBytes = Convert.FromBase64String(signature);
            return rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
    }

    public sealed record RsaKeyPair(string PublicKey, string PrivateKey);
}
