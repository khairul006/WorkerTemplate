using Jose;
using System.Security;
using System.Text;

namespace WorkerTemplate.Utils
{
    public static class SecurityUtil
    {
        // Sign with JWS HS256
        public static string Sign(string payload, string signingKey)
        {
            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(signingKey);

                if (keyBytes.Length != 32)
                    throw new ArgumentException("Signing key must be exactly 32 bytes");

                return JWT.Encode(payload, keyBytes, JwsAlgorithm.HS256);
            }
            catch (ArgumentException)
            {
                throw;  // let caller handle config issues
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Failed to sign payload: {ex.Message}", ex);
            }
        }

        // Encrypt with JWE dir + A256GCM
        public static string Encrypt(string jwsToken, string encryptionKey)
        {
            try
            {
                var keyBytes = Encoding.UTF8.GetBytes(encryptionKey);

                if (keyBytes.Length != 32)
                    throw new ArgumentException("Encryption key must be exactly 32 bytes");

                return JWT.Encode(jwsToken, keyBytes, JweAlgorithm.DIR, JweEncryption.A256GCM);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Failed to encrypt payload: {ex.Message}", ex);
            }
        }

        // Sign then Encrypt
        public static string SignThenEncrypt(string payload, string signingKey, string encryptionKey)
        {
            try
            {
                var jws = Sign(payload, signingKey);
                return Encrypt(jws, encryptionKey);
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Failed to sign and encrypt payload: {ex.Message}", ex);
            }
        }

        // Decrypt then Verify
        public static string DecryptAndVerify(string token, string signingKey, string encryptionKey)
        {
            try
            {
                var encKeyBytes = Encoding.UTF8.GetBytes(encryptionKey);
                var signKeyBytes = Encoding.UTF8.GetBytes(signingKey);

                var jws = JWT.Decode(token, encKeyBytes, JweAlgorithm.DIR, JweEncryption.A256GCM);
                return JWT.Decode(jws, signKeyBytes, JwsAlgorithm.HS256);

            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Jose.IntegrityException ex)
            {
                throw new SecurityException($"Signature verification failed: {ex.Message}", ex);
            }
            catch (Jose.EncryptionException ex)
            {
                throw new SecurityException($"Decryption failed: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Failed to decrypt and verify token: {ex.Message}", ex);
            }
        }
    }
}