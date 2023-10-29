using System.ComponentModel;
using RosyCrow.Services.Fingerprint.Abstractions;
using RosyCrow.Services.Fingerprint.Platforms.Android;

namespace RosyCrow.Services.Fingerprint
{
    /// <summary>
    /// Cross Platform Fingerprint.
    /// </summary>
    [Localizable(false)]
    public partial class CrossFingerprint
    {
        private static Lazy<IFingerprint> _implementation = new(CreateFingerprint, LazyThreadSafetyMode.PublicationOnly);

        /// <summary>
        /// Current plugin implementation to use
        /// </summary>
        public static IFingerprint Current
        {
            get => _implementation.Value;
            set
            {
                _implementation = new Lazy<IFingerprint>(() => value);
            }
        }

        static IFingerprint CreateFingerprint()
        {
#if NETSTANDARD2_0
            throw NotImplementedInReferenceAssembly();
#else
            return new FingerprintImplementation();
#endif
        }

        /// <summary>
        /// Cleans up implementation reference.
        /// </summary>
        public static void Dispose()
        {
            if (_implementation is { IsValueCreated: true })
            {
                _implementation = new Lazy<IFingerprint>(CreateFingerprint, LazyThreadSafetyMode.PublicationOnly);
            }
        }
    }
}