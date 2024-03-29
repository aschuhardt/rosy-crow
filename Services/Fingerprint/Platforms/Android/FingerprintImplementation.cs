using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Biometric;
using Java.Util.Concurrent;
using RosyCrow.Services.Fingerprint.Abstractions;
using RosyCrow.Services.Fingerprint.Platforms.Android.Utils;
using Application = Android.App.Application;
using BiometricPrompt = Android.Hardware.Biometrics.BiometricPrompt;

namespace RosyCrow.Services.Fingerprint.Platforms.Android;

/// <summary>
///     Android fingerprint implementations.
/// </summary>
public class FingerprintImplementation : FingerprintImplementationBase
{
    private readonly BiometricManager _manager;

    public FingerprintImplementation()
    {
        _manager = BiometricManager.From(Application.Context);
    }

    public override async Task<AuthenticationType> GetAuthenticationTypeAsync()
    {
        var availability = await GetAvailabilityAsync();
        return availability is FingerprintAvailability.NoFingerprint or FingerprintAvailability.NoPermission
            or FingerprintAvailability.Available
            ? AuthenticationType.Fingerprint
            : AuthenticationType.None;
    }

    public override async Task<FingerprintEncryptionResult> NativeEncryptAsync(
        AuthenticationRequestConfiguration authRequestConfig, byte[] plaintext)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
            return null;

        var crypto = new CryptoObjectHelper(authRequestConfig.KeyName)
            .BuildCryptoObject(CryptoObjectHelper.CryptographicOperation.Encrypt);
        var (result, ciphertext) = await NativeAuthenticateAsync(authRequestConfig, plaintext, crypto);

        return new FingerprintEncryptionResult(ciphertext, result) { Iv = crypto.Cipher?.GetIV() };
    }

    public override async Task<FingerprintDecryptionResult> NativeDecryptAsync(
        AuthenticationRequestConfiguration authRequestConfig, byte[] ciphertext, byte[] iv)
    {
        var crypto = new CryptoObjectHelper(authRequestConfig.KeyName)
            .BuildCryptoObject(CryptoObjectHelper.CryptographicOperation.Decrypt, iv);
        var (result, plaintext) = await NativeAuthenticateAsync(authRequestConfig, ciphertext, crypto);

        return new FingerprintDecryptionResult(plaintext, result);
    }

    public override Task<FingerprintAvailability> GetAvailabilityAsync(
        bool allowAlternativeAuthentication = false)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            return Task.FromResult(FingerprintAvailability.NoApi);


        var biometricAvailability = GetBiometricAvailability();
        if (biometricAvailability == FingerprintAvailability.Available || !allowAlternativeAuthentication)
            return Task.FromResult(biometricAvailability);

        var context = Application.Context;

        try
        {
            var manager = (KeyguardManager)context.GetSystemService(Context.KeyguardService);
            if (manager.IsDeviceSecure) return Task.FromResult(FingerprintAvailability.Available);

            return Task.FromResult(FingerprintAvailability.NoFallback);
        }
        catch
        {
            return Task.FromResult(FingerprintAvailability.NoFallback);
        }
    }

    private FingerprintAvailability GetBiometricAvailability()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
            return FingerprintAvailability.NoImplementation;

        var context = Application.Context;

        if (context.CheckCallingOrSelfPermission(Manifest.Permission.UseBiometric) != Permission.Granted)
            return FingerprintAvailability.NoPermission;

        var code = _manager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong |
                                            BiometricManager.Authenticators.DeviceCredential);

        return code switch
        {
            BiometricManager.BiometricErrorNoHardware => FingerprintAvailability.NoSensor,
            BiometricManager.BiometricErrorHwUnavailable => FingerprintAvailability.Unknown,
            BiometricManager.BiometricErrorNoneEnrolled => FingerprintAvailability.NoFingerprint,
            BiometricManager.BiometricSuccess => FingerprintAvailability.Available,
            _ => FingerprintAvailability.Unknown
        };
    }

    private static async Task<(FingerprintAuthenticationResult, byte[])> NativeAuthenticateAsync(
        AuthenticationRequestConfiguration authRequestConfig, byte[] inputData,
        BiometricPrompt.CryptoObject cryptoObject)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(28))
            return (null, null);

        if (string.IsNullOrWhiteSpace(authRequestConfig.Title))
            throw new ArgumentException(@"Title must not be null or empty on Android.", nameof(authRequestConfig));

        try
        {
            var cancel = string.IsNullOrWhiteSpace(authRequestConfig.CancelTitle)
                ? Application.Context.GetString(global::Android.Resource.String.Cancel)
                : authRequestConfig.CancelTitle;

            var handler =
                new AuthenticationHandler(resultCryptoObject => 
                        OperatingSystem.IsAndroidVersionAtLeast(28) && 
                        resultCryptoObject.Cipher == cryptoObject.Cipher,
                    inputData);
            var builder = new BiometricPrompt.Builder(Application.Context)
                .SetTitle(authRequestConfig.Title)
                .SetDescription(authRequestConfig.Reason);

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
                builder.SetConfirmationRequired(authRequestConfig.ConfirmationRequired);

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                if (authRequestConfig.AllowAlternativeAuthentication)
                {
                    // It's not allowed to allow alternative auth & set the negative button
                    builder = builder.SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong |
                                                               BiometricManager.Authenticators.DeviceCredential);
                }
                else
                    builder = builder.SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong);
            }

            var executor = Executors.NewSingleThreadExecutor();

            builder.Build().Authenticate(cryptoObject, new CancellationSignal(), executor, handler);
            var result = await handler.GetTask();
            return (result, handler.OutputData);
        }
        catch (Exception e)
        {
            var result = new FingerprintAuthenticationResult
            {
                Status = FingerprintAuthenticationResultStatus.UnknownError,
                ErrorMessage = e.Message
            };

            return (result, null);
        }
    }
}