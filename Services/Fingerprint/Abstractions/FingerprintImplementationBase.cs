namespace RosyCrow.Services.Fingerprint.Abstractions
{
    public abstract class FingerprintImplementationBase : IFingerprint
    {
        public async Task<FingerprintEncryptionResult> EncryptAsync(AuthenticationRequestConfiguration authRequestConfig, byte[] plaintext, CancellationToken cancellationToken = default)
        {
            if (authRequestConfig is null)
                throw new ArgumentNullException(nameof(authRequestConfig));

            var availability = await GetAvailabilityAsync(authRequestConfig.AllowAlternativeAuthentication);
            if (availability != FingerprintAvailability.Available)
            {
                var status = availability == FingerprintAvailability.Denied ?
                    FingerprintAuthenticationResultStatus.Denied :
                    FingerprintAuthenticationResultStatus.NotAvailable;

                return new FingerprintEncryptionResult(null, new FingerprintAuthenticationResult { Status = status, ErrorMessage = availability.ToString() });
            }

            return await NativeEncryptAsync(authRequestConfig, plaintext);
        }
        public async Task<FingerprintDecryptionResult> DecryptAsync(AuthenticationRequestConfiguration authRequestConfig, byte[] ciphertext, CancellationToken cancellationToken = default)
        {
            if (authRequestConfig is null)
                throw new ArgumentNullException(nameof(authRequestConfig));

            var availability = await GetAvailabilityAsync(authRequestConfig.AllowAlternativeAuthentication);
            if (availability != FingerprintAvailability.Available)
            {
                var status = availability == FingerprintAvailability.Denied ?
                    FingerprintAuthenticationResultStatus.Denied :
                    FingerprintAuthenticationResultStatus.NotAvailable;

                return new FingerprintDecryptionResult(null, new FingerprintAuthenticationResult { Status = status, ErrorMessage = availability.ToString() });
            }

            return await NativeDecryptAsync(authRequestConfig, ciphertext);
        }

        public async Task<bool> IsAvailableAsync(bool allowAlternativeAuthentication = false)
        {
            return await GetAvailabilityAsync(allowAlternativeAuthentication) == FingerprintAvailability.Available;
        }

        public abstract Task<FingerprintEncryptionResult> NativeEncryptAsync(AuthenticationRequestConfiguration authRequestConfig, byte[] plaintext);

        public abstract Task<FingerprintDecryptionResult> NativeDecryptAsync(AuthenticationRequestConfiguration authRequestConfig, byte[] ciphertext);

        public abstract Task<FingerprintAvailability> GetAvailabilityAsync(bool allowAlternativeAuthentication = false);

        public abstract Task<AuthenticationType> GetAuthenticationTypeAsync();
    }
}