namespace RosyCrow.Services.Fingerprint.Abstractions
{
    public class FingerprintEncryptionResult
    {
        public FingerprintEncryptionResult(byte[] ciphertext, FingerprintAuthenticationResult authenticationResult)
        {
            Ciphertext = ciphertext;
            AuthenticationResult = authenticationResult;
        }

        public byte[] Ciphertext { get; }
        public byte[] Iv { get; set; }
        public FingerprintAuthenticationResult AuthenticationResult { get; }
    }
}