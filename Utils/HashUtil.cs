using System.Security.Cryptography;
using System.Text;

namespace WorkerTemplate.Utils
{
    public static class HashUtil
    {
        /// <summary>
        /// Generate HMAC SHA-256 signature (Base64) from any object.
        /// </summary>
        public static string GenerateSignature(string payload, string secretKey)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            if (string.IsNullOrEmpty(secretKey))
                throw new ArgumentException("Secret key cannot be empty", nameof(secretKey));

            byte[] keyBytes = Encoding.UTF8.GetBytes(secretKey);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(keyBytes);
            byte[] hashBytes = hmac.ComputeHash(bodyBytes);

            //Console.WriteLine("payload: " + payload);
            //Console.WriteLine("computedSignature: " + Convert.ToBase64String(hashBytes));

            return Convert.ToBase64String(hashBytes);
        }

        public static bool VerifySignature(string payload, string secretKey, string signatureToVerify)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            if (string.IsNullOrEmpty(secretKey))
                throw new ArgumentException("Secret key cannot be empty", nameof(secretKey));

            if (string.IsNullOrEmpty(signatureToVerify))
                throw new ArgumentException("Signature to verify cannot be empty", nameof(signatureToVerify));

            // Recompute signature
            string computedSignature = GenerateSignature(payload, secretKey);
            //Console.WriteLine("computedSignature: " + computedSignature);
            //Console.WriteLine("computedSignature: " + signatureToVerify);

            // Timing-safe comparison
            byte[] computedBytes = Encoding.UTF8.GetBytes(computedSignature);
            byte[] verifyBytes = Encoding.UTF8.GetBytes(signatureToVerify);

            return CryptographicOperations.FixedTimeEquals(computedBytes, verifyBytes);
        }
    }
}
