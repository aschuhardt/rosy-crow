namespace RosyCrow.Services.Fingerprint.Abstractions
{
    public class FingerprintDecryptionResult 
    {
        public FingerprintDecryptionResult(byte[] plaintext, FingerprintAuthenticationResult authenticationResult)
        {
            Plaintext = plaintext;
            AuthenticationResult = authenticationResult;
        }

        public byte[] Plaintext { get; }
        public FingerprintAuthenticationResult AuthenticationResult { get; }
    }
}