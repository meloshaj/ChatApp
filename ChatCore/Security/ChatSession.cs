using ChatCore.Models;

namespace ChatCore.Security
{
    public sealed class ChatSession
    {
        private readonly User localUser;
        private readonly User remoteUser;
        private readonly RsaService rsaService = new();
        private readonly EcdhService ecdhService = new();
        private readonly AesService aesService = new();
        private readonly EcdhKeyPair ecdhKeys;
        private byte[]? sharedKey;

        public ChatSession(User localUser, User remoteUser)
        {
            this.localUser = localUser;
            this.remoteUser = remoteUser;
            ecdhKeys = ecdhService.GenerateKeyPair();
        }

        public string EcdhPublicKey => ecdhKeys.PublicKey;
        public bool HasSharedKey => sharedKey != null;

        public string SignAuthenticationMessage(string message)
        {
            return rsaService.Sign(message, localUser.RsaPrivateKey);
        }

        public bool VerifyAuthenticationMessage(string message, string signature)
        {
            return rsaService.Verify(message, signature, remoteUser.RsaPublicKey);
        }

        public void DeriveSharedKey(string remoteEcdhPublicKey)
        {
            sharedKey = ecdhService.DeriveSharedKey(ecdhKeys.PrivateKey, remoteEcdhPublicKey);
        }

        public EncryptedMessage EncryptMessage(string plainText)
        {
            return aesService.Encrypt(plainText, GetSharedKey());
        }

        public string DecryptMessage(EncryptedMessage encryptedMessage)
        {
            return aesService.Decrypt(encryptedMessage, GetSharedKey());
        }

        private byte[] GetSharedKey()
        {
            if (sharedKey == null)
            {
                throw new InvalidOperationException("ECDH shared key has not been created yet.");
            }

            return sharedKey;
        }
    }
}
