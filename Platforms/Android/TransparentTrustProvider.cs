using Java.Lang;
using Java.Net;
using Java.Security;
using Java.Security.Cert;
using Javax.Net.Ssl;

namespace RosyCrow.Platforms.Android;

internal class TransparentTrustProvider : Provider
{
    private const string TRUST_PROVIDER_ALG = "TransparentTrustAlgorithm";
    private const string TRUST_PROVIDER_ID = "TransparentTrustProvider";

    public TransparentTrustProvider() : base(TRUST_PROVIDER_ID, 1, string.Empty)
    {
        var key = "TrustManagerFactory." + TransparentTrustManagerFactory.GetAlgorithm();
        var val = Class.FromType(typeof(TransparentTrustManagerFactory)).Name;
        Put(key, val);
    }

    public static void Register()
    {
        var registered = Security.GetProvider(TRUST_PROVIDER_ID);
        if (null == registered)
        {
            Security.InsertProviderAt(new TransparentTrustProvider(), 1);
            Security.SetProperty("ssl.TrustManagerFactory.algorithm", TRUST_PROVIDER_ALG);
        }
    }

    public class TransparentTrustManager : X509ExtendedTrustManager
    {
        public override void CheckClientTrusted(X509Certificate[] chain, string authType, Socket socket)
        {
        }

        public override void CheckClientTrusted(X509Certificate[] chain, string authType, SSLEngine engine)
        {
        }

        public override void CheckClientTrusted(X509Certificate[] chain, string authType)
        {
        }

        public override void CheckServerTrusted(X509Certificate[] chain, string authType, Socket socket)
        {
        }

        public override void CheckServerTrusted(X509Certificate[] chain, string authType, SSLEngine engine)
        {
        }

        public override void CheckServerTrusted(X509Certificate[] chain, string authType)
        {
        }

        public override X509Certificate[] GetAcceptedIssuers()
        {
            return Array.Empty<X509Certificate>();
        }
    }

    public class TransparentTrustManagerFactory : TrustManagerFactorySpi
    {
        protected override void EngineInit(IManagerFactoryParameters mgrparams)
        {
        }

        protected override void EngineInit(KeyStore keystore)
        {
        }

        protected override ITrustManager[] EngineGetTrustManagers()
        {
            return new ITrustManager[] { new TransparentTrustManager() };
        }

        public static string GetAlgorithm()
        {
            return TRUST_PROVIDER_ALG;
        }
    }
}