using RosyCrow.Services.Fingerprint.Abstractions;
using RosyCrow.Services.Fingerprint.Platforms.Android.Contract;

namespace RosyCrow.Services.Fingerprint.Platforms.Android.Utils
{
    public static class AuthenticationHelpTextsExtension
    {
        public static string GetText(this AuthenticationHelpTexts texts, FingerprintAuthenticationHelp help,
            string nativeText)
        {
            return help switch
            {
                FingerprintAuthenticationHelp.MovedTooFast when !string.IsNullOrEmpty(texts.MovedTooFast) => texts
                    .MovedTooFast,
                FingerprintAuthenticationHelp.MovedTooSlow when !string.IsNullOrEmpty(texts.MovedTooSlow) => texts
                    .MovedTooSlow,
                FingerprintAuthenticationHelp.Partial when !string.IsNullOrEmpty(texts.Partial) => texts.Partial,
                FingerprintAuthenticationHelp.Insufficient when !string.IsNullOrEmpty(texts.Insufficient) => texts
                    .Insufficient,
                FingerprintAuthenticationHelp.Dirty when !string.IsNullOrEmpty(texts.Dirty) => texts.Dirty,
                _ => nativeText
            };
        }
    }
}