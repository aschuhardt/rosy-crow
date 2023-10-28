using System.ComponentModel;
using Android.App;

namespace RosyCrow.Services.Fingerprint.Platforms.Android
{
    [Localizable(false)]
    public partial class CrossFingerprint
    {
        private static Func<Activity> _activityResolver;

        public static Activity CurrentActivity => GetCurrentActivity();

        public static void SetCurrentActivityResolver(Func<Activity> activityResolver)
        {
            _activityResolver = activityResolver;
        }

        private static Activity GetCurrentActivity()
        {
            if (_activityResolver is null)
                throw new InvalidOperationException("Resolver for the current activity is not set. Call Fingerprint.SetCurrentActivityResolver somewhere in your startup code.");

            var activity = _activityResolver() ?? throw new InvalidOperationException("The configured CurrentActivityResolver returned null. " +
                                                    "You need to setup the Android implementation via CrossFingerprint.SetCurrentActivityResolver(). " +
                                                    "If you are using CrossCurrentActivity don't forget to initialize it, too!");
            return activity;
        }
    }
}