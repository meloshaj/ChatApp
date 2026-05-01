using ChatCore.Models;

namespace ChatCore.Security
{
    public static class SecureChatDemo
    {
        public static string Run()
        {
            RsaService rsaService = new();
            RsaKeyPair aliceRsa = rsaService.GenerateKeyPair();
            RsaKeyPair bobRsa = rsaService.GenerateKeyPair();

            User alice = new()
            {
                Id = "alice-id",
                Username = "Alice",
                RsaPublicKey = aliceRsa.PublicKey,
                RsaPrivateKey = aliceRsa.PrivateKey
            };

            User bob = new()
            {
                Id = "bob-id",
                Username = "Bob",
                RsaPublicKey = bobRsa.PublicKey,
                RsaPrivateKey = bobRsa.PrivateKey
            };

            ChatSession aliceSession = new(alice, bob);
            ChatSession bobSession = new(bob, alice);

            string authMessage = "Alice wants to chat with Bob";
            string signature = aliceSession.SignAuthenticationMessage(authMessage);
            bool aliceVerified = bobSession.VerifyAuthenticationMessage(authMessage, signature);

            aliceSession.DeriveSharedKey(bobSession.EcdhPublicKey);
            bobSession.DeriveSharedKey(aliceSession.EcdhPublicKey);

            EncryptedMessage encrypted = aliceSession.EncryptMessage("Hello Bob, this is secret.");
            string decrypted = bobSession.DecryptMessage(encrypted);

            return
                $"RSA verified: {aliceVerified}{Environment.NewLine}" +
                $"Encrypted text: {encrypted.CipherText}{Environment.NewLine}" +
                $"Decrypted text: {decrypted}";
        }
    }
}
