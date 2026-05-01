using System.Security.Cryptography;

namespace ChatCore.Security
{
    public sealed class EcdhService
    {
        public EcdhKeyPair GenerateKeyPair()
        {
            using ECDiffieHellman ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            return new EcdhKeyPair(
                Convert.ToBase64String(ecdh.ExportSubjectPublicKeyInfo()),
                Convert.ToBase64String(ecdh.ExportPkcs8PrivateKey()));
        }

        public byte[] DeriveSharedKey(string privateKey, string otherPublicKey)
        {
            using ECDiffieHellman local = ECDiffieHellman.Create();
            local.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);

            using ECDiffieHellman remote = ECDiffieHellman.Create();
            remote.ImportSubjectPublicKeyInfo(Convert.FromBase64String(otherPublicKey), out _);

            byte[] sharedSecret = local.DeriveKeyMaterial(remote.PublicKey);
            return SHA256.HashData(sharedSecret);
        }
    }

    public sealed record EcdhKeyPair(string PublicKey, string PrivateKey);
}
