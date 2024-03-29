﻿using System.ComponentModel;
using Android.Hardware.Biometrics;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace RosyCrow.Services.Fingerprint.Platforms.Android.Utils
{
    [Localizable(false)]
    internal class CryptoObjectHelper
    {
        // Fixed Android KeyStore Name
        private static readonly string KeyStoreName = "AndroidKeyStore";

        /*
         * Algorithm Setup - Based on
         * https://developer.android.com/training/articles/keystore#java
         * https://developer.android.com/training/sign-in/biometric-auth
         * https://docs.microsoft.com/en-us/xamarin/android/platform/fingerprint-authentication/creating-a-cryptoobject
         */
        private static readonly int KeySize = 256;
        private static readonly string KeyAlgorithm = KeyProperties.KeyAlgorithmAes;
        private static readonly string BlockMode = KeyProperties.BlockModeCbc;
        private static readonly string EncryptionPadding = KeyProperties.EncryptionPaddingPkcs7;
        private static readonly string Transfomration = $"{KeyAlgorithm}/{BlockMode}/{EncryptionPadding}";

        private readonly KeyStore _keystore;

        public string KeyName { get; }

        public enum CryptographicOperation
        {
            Decrypt,
            Encrypt
        }

        public CryptoObjectHelper(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                throw new ArgumentException($"{nameof(keyName)} can't be empty or null");
            }

            KeyName = keyName;
            _keystore = KeyStore.GetInstance(KeyStoreName);
            _keystore.Load(null);
        }

        public BiometricPrompt.CryptoObject BuildCryptoObject(CryptographicOperation operation, byte[] iv = null)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(28))
                return null;

            return operation switch
            {
                CryptographicOperation.Decrypt => new BiometricPrompt.CryptoObject(CreateCipher(CipherMode.DecryptMode, iv)),
                CryptographicOperation.Encrypt => new BiometricPrompt.CryptoObject(CreateCipher(CipherMode.EncryptMode)),
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
        }

        public bool Delete()
        {
            if (!_keystore.IsKeyEntry(KeyName))
                return false;

            _keystore.DeleteEntry(KeyStoreName);
            return true;
        }

        private Cipher CreateCipher(CipherMode mode, byte[] iv = null, int retries = 3)
        {
            var key = GetKey();
            var cipher = Cipher.GetInstance(Transfomration);

            try
            {
                if (mode == CipherMode.DecryptMode && iv != null)
                    cipher.Init(mode, key, new IvParameterSpec(iv));
                else
                    cipher.Init(mode, key);
            }
            catch (KeyPermanentlyInvalidatedException)
            {
                _keystore.DeleteEntry(KeyName);
                if (retries > 0)
                {
                    // Microsoft Docs doesn't overwrite the cipher.
                    // Without the implementation of GetInstance its hard to say if it doesn't need to be overwritten.
                    // So this is a just in case
                    cipher = CreateCipher(mode, iv, --retries);
                }
                else
                {
                    throw new KeyPermanentlyInvalidatedException($"Could not create the cipher for biometric authentication.");
                }
            }

            return cipher;
        }

        private IKey GetKey()
        {
            if (!_keystore.IsKeyEntry(KeyName))
            {
                CreateNewKey();
            }

            return _keystore.GetKey(KeyName, null);
        }

        private void CreateNewKey()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(28))
                return;

            var keyGen = KeyGenerator.GetInstance(KeyAlgorithm, KeyStoreName);
            var keyGenSpec = new KeyGenParameterSpec.Builder(KeyName, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                                    .SetBlockModes(BlockMode)
                                    .SetEncryptionPaddings(EncryptionPadding)
                                    .SetKeySize(KeySize)
                                    //.SetUserAuthenticationRequired(true)
                                    .SetUnlockedDeviceRequired(true)
                                    .SetInvalidatedByBiometricEnrollment(true)
                                    .Build();
            keyGen.Init(keyGenSpec);
            keyGen.GenerateKey();
        }
    }
}
