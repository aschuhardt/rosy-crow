using Android.Content;
using Android.Hardware.Biometrics;
using Java.Lang;
using Javax.Crypto;
using RosyCrow.Services.Fingerprint.Abstractions;

namespace RosyCrow.Services.Fingerprint.Platforms.Android
{
    public class AuthenticationHandler : BiometricPrompt.AuthenticationCallback, IDialogInterfaceOnClickListener
    {
        private readonly TaskCompletionSource<FingerprintAuthenticationResult> _taskCompletionSource;
        private readonly Func<BiometricPrompt.CryptoObject, bool> _validatedCipherFunc;

        public AuthenticationHandler(Func<BiometricPrompt.CryptoObject, bool> validatedCipherFunc, byte[] inputData)
        {
            _taskCompletionSource = new TaskCompletionSource<FingerprintAuthenticationResult>();
            _validatedCipherFunc = validatedCipherFunc;
            InputData = inputData;
        }

        public byte[] InputData { get; }
        public byte[] OutputData { get; private set; }

        public void OnClick(IDialogInterface dialog, int which)
        {
            var faResult = new FingerprintAuthenticationResult
                { Status = FingerprintAuthenticationResultStatus.Canceled };
            SetResultSafe(faResult);
        }

        public Task<FingerprintAuthenticationResult> GetTask()
        {
            return _taskCompletionSource.Task;
        }

        private void SetResultSafe(FingerprintAuthenticationResult result)
        {
            if (!(_taskCompletionSource.Task.IsCanceled || _taskCompletionSource.Task.IsCompleted ||
                  _taskCompletionSource.Task.IsFaulted)) _taskCompletionSource.SetResult(result);
        }

        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(28))
                return;

            base.OnAuthenticationSucceeded(result);

            var faResult = new FingerprintAuthenticationResult
                { Status = FingerprintAuthenticationResultStatus.Succeeded };
            if (result.CryptoObject == null)
                faResult = new FingerprintAuthenticationResult
                {
                    Status = FingerprintAuthenticationResultStatus.MissingCryptoObject,
                    ErrorMessage = "CryptoObject was empty"
                };
            else if (!_validatedCipherFunc(result.CryptoObject))
                faResult = new FingerprintAuthenticationResult
                {
                    Status = FingerprintAuthenticationResultStatus.InvalidCipher,
                    ErrorMessage = "Cipher changed since Authentication call. Maybe it was manipulated"
                };
            else
            {
                var errorMsg = string.Empty;
                if (result.CryptoObject.Cipher != null)
                {
                    var cipher = result.CryptoObject.Cipher;
                    try
                    {
                        // Ensuring encryption works (Authenticated & Valid)
                        OutputData = cipher.DoFinal(InputData);

                        if (OutputData == null) errorMsg = "DoFinal(..) was manipulated! Result was null";
                    }
                    catch (BadPaddingException bpe)
                    {
                        errorMsg =
                            $"Failed to encrypt the data with the generated key.{Environment.NewLine}{bpe.Message}";
                    }
                    catch (IllegalBlockSizeException ibse)
                    {
                        errorMsg =
                            $"Failed to encrypt the data with the generated key.{Environment.NewLine}{ibse.Message}";
                    }
                }
                else
                    errorMsg = "CryptoObject was given but Cipher was missing!";

                if (!string.IsNullOrEmpty(errorMsg))
                {
                    // Can't really trust the results.
                    faResult = new FingerprintAuthenticationResult
                        { Status = FingerprintAuthenticationResultStatus.InvalidCipher, ErrorMessage = errorMsg };
                }
            }

            SetResultSafe(faResult);
        }

        public override void OnAuthenticationError(BiometricErrorCode errorCode, ICharSequence errString)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(28))
                return;

            base.OnAuthenticationError(errorCode, errString);

            var message = errString != null ? errString.ToString() : string.Empty;
            var result = new FingerprintAuthenticationResult
                { Status = FingerprintAuthenticationResultStatus.Failed, ErrorMessage = message };

            result.Status = errorCode switch
            {
                BiometricErrorCode.Lockout => FingerprintAuthenticationResultStatus.TooManyAttempts,
                BiometricErrorCode.UserCanceled => FingerprintAuthenticationResultStatus.Canceled,
                BiometricErrorCode.Canceled => FingerprintAuthenticationResultStatus.Canceled,
                _ => FingerprintAuthenticationResultStatus.Failed
            };

            SetResultSafe(result);
        }
    }
}