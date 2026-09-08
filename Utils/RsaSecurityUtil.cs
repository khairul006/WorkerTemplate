using Jose;
using System.Security;
using System.Security.Cryptography;

namespace WorkerTemplate.Utils;

public static class RsaSecurityUtil
{
    // Sign (RS256)
    public static string Sign(string payload, RSA privateKey, string? kid = null)
    {
        try
        {
            if (privateKey == null)
                throw new ArgumentNullException(nameof(privateKey));

            var extraHeaders = kid != null
                ? new Dictionary<string, object> { { "kid", kid } }
                : null;

            return JWT.Encode(payload, privateKey, JwsAlgorithm.RS256, extraHeaders);
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Failed to sign payload: {ex.Message}", ex);
        }
    }

    // Verify (RS256)
    public static string Verify(string jwsToken, RSA publicKey)
    {
        try
        {
            if (publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));

            return JWT.Decode(jwsToken, publicKey, JwsAlgorithm.RS256);
        }
        catch (Jose.IntegrityException ex)
        {
            throw new SecurityException($"Signature verification failed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new SecurityException($"Failed to verify token: {ex.Message}", ex);
        }
    }
}