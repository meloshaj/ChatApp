namespace ChatCore.Security
{
    public sealed class SecureTransportSession
    {
        private const string HandshakeMessagePrefix = "ECDH:";
        private readonly RsaService rsaService = new();
        private readonly EcdhService ecdhService = new();
        private readonly AesService aesService = new();
        private readonly RsaKeyPair rsaKeys;
        private readonly EcdhKeyPair ecdhKeys;
        private byte[]? sharedKey;

        public SecureTransportSession()
            : this(new RsaService().GenerateKeyPair())
        {
        }

        public SecureTransportSession(RsaKeyPair identityKeys)
        {
            rsaKeys = identityKeys;
            ecdhKeys = ecdhService.GenerateKeyPair();
        }

        public bool HasSharedKey => sharedKey != null;

        public CryptoHandshake CreateHandshake()
        {
            return new CryptoHandshake(
                rsaKeys.PublicKey,
                ecdhKeys.PublicKey,
                rsaService.Sign(GetSignedHandshakeText(ecdhKeys.PublicKey), rsaKeys.PrivateKey));
        }

        public bool CompleteHandshake(CryptoHandshake remoteHandshake, string? expectedRsaPublicKey = null)
        {
            if (!string.IsNullOrWhiteSpace(expectedRsaPublicKey) &&
                !string.Equals(remoteHandshake.RsaPublicKey, expectedRsaPublicKey, StringComparison.Ordinal))
            {
                return false;
            }

            bool verified = rsaService.Verify(
                GetSignedHandshakeText(remoteHandshake.EcdhPublicKey),
                remoteHandshake.Signature,
                remoteHandshake.RsaPublicKey);

            if (!verified)
            {
                return false;
            }

            sharedKey = ecdhService.DeriveSharedKey(ecdhKeys.PrivateKey, remoteHandshake.EcdhPublicKey);
            return true;
        }

        public EncryptedMessage Protect(string plainText)
        {
            return aesService.Encrypt(plainText, GetSharedKey());
        }

        public string Unprotect(EncryptedMessage protectedMessage)
        {
            return aesService.Decrypt(protectedMessage, GetSharedKey());
        }

        private byte[] GetSharedKey()
        {
            if (sharedKey == null)
            {
                throw new InvalidOperationException("Secure transport session has not completed the ECDH handshake.");
            }

            return sharedKey;
        }

        private static string GetSignedHandshakeText(string ecdhPublicKey) => HandshakeMessagePrefix + ecdhPublicKey;
    }

    public sealed record CryptoHandshake(string RsaPublicKey, string EcdhPublicKey, string Signature);
}
