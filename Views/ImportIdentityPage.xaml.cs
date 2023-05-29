using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Logging;
using RosyCrow.Extensions;
using RosyCrow.Services.Identity;
using System.IO;
using System.Security.Cryptography;
using Java.Util.Concurrent;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class ImportIdentityPage : ContentPage
{
    private readonly IIdentityService _identityService;
    private readonly ILogger<ImportIdentityPage> _logger;
    private ICommand _export;
    private string _password;
    private bool _usePassword;
    private string _fingerprint;
    private ICommand _copyText;
    private bool _hidePassword;
    private ICommand _togglePasswordHidden;
    private string _tempFilePath;
    private X509Certificate2 _certificate;
    private string _subject;

    public ImportIdentityPage(IIdentityService identityService, ILogger<ImportIdentityPage> logger)
    {
        _identityService = identityService;
        _logger = logger;
        _certificate = null;

        InitializeComponent();

        BindingContext = this;

        Import = new Command(async () => await ImportKey());
        TogglePasswordHidden = new Command(() => HidePassword = !HidePassword);
        CopyText = new Command(async value =>
        {
            await Clipboard.SetTextAsync((string)value);
            await Toast.Make("Copied").Show();
        });
    }

    public string Fingerprint
    {
        get => _fingerprint;
        set
        {
            if (value == _fingerprint) return;
            _fingerprint = value;
            OnPropertyChanged();
        }
    }

    public string Subject
    {
        get => _subject;
        set
        {
            if (value == _subject) return;
            _subject = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (value == _password) return;
            _password = value;
            OnPropertyChanged();
        }
    }

    public bool HidePassword
    {
        get => _hidePassword;
        set
        {
            if (value == _hidePassword) return;
            _hidePassword = value;
            OnPropertyChanged();
        }
    }

    public bool UsePassword
    {
        get => _usePassword;
        set
        {
            if (value == _usePassword) return;
            _usePassword = value;

            if (!_usePassword)
                Password = string.Empty;

            OnPropertyChanged();
        }
    }

    public ICommand CopyText
    {
        get => _copyText;
        set
        {
            if (Equals(value, _copyText)) return;
            _copyText = value;
            OnPropertyChanged();
        }
    }

    public ICommand Import
    {
        get => _export;
        set
        {
            if (Equals(value, _export)) return;
            _export = value;
            OnPropertyChanged();
        }
    }

    public ICommand TogglePasswordHidden
    {
        get => _togglePasswordHidden;
        set
        {
            if (Equals(value, _togglePasswordHidden)) return;
            _togglePasswordHidden = value;
            OnPropertyChanged();
        }
    }

    private async Task<bool> PresentProtectionPrompt()
    {
        return await DisplayAlert(Text.ImportIdentityPage_PresentProtectionPrompt_Import_Identity, 
            Text.ImportIdentityPage_PresentProtectionPrompt_Do_you_want_to_secure_the_imported_identity_with_your_device_credentials_, Text.Global_Yes, Text.Global_No);
    }

    private async Task ImportKey()
    {
        if (UsePassword && string.IsNullOrEmpty(Password))
            UsePassword = false;

        try
        {
            _logger.LogInformation(Text.ImportIdentityPage_ImportKey_Importing_a_new_identity_certificate_with_subject__Subject_, _certificate.Subject);

            // import
            var certWithPrivateKey = UsePassword
                ? X509Certificate2.CreateFromEncryptedPemFile(_tempFilePath, Password)
                : X509Certificate2.CreateFromPemFile(_tempFilePath);

            if (!certWithPrivateKey.HasPrivateKey)
            {
                _logger.LogInformation(Text.ImportIdentityPage_ImportKey_Attempted_to_load_a_certificate_file_that_lacks_a_private_key);
                await DisplayAlert(Text.ImportIdentityPage_ImportKey_Missing_Private_Key,
                    Text.ImportIdentityPage_ImportKey_The_selected_certificate_file_lacks_a_private_key__and_therefore_cannot_be_imported_, Text.Global_OK);
                await Navigation.PopModalAsync(true);
                return;
            }

            var name = certWithPrivateKey.GetNameInfo(X509NameType.SimpleName, false);
            var identity = await _identityService.ImportIdentityCertificate(name,
                certWithPrivateKey, PresentProtectionPrompt);

            if (identity == null)
            {
                await Toast.Make(Text.ImportIdentityPage_ImportKey_The_import_could_not_be_completed).Show();
                return;
            }

            await Navigation.PopModalAsync(true);
        }
        catch (CryptographicException)
        {
            await DisplayAlert(Text.ImportIdentityPage_ImportKey_Decryption_Failed,
                Text.ImportIdentityPage_ImportKey_The_certificate_s_private_key_could_not_be_decrypted___The_password_is_likely_incorrect_, Text.Global_OK);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while importing a certificate");
        }
    }

    private async Task<X509Certificate2> LoadCertificateFile()
    {
        // get the chosen path from the user
        var picked = await FilePicker.Default.PickAsync();
        if (picked == null)
            return null;

        // copy the chosen file to a temp location
        _tempFilePath = System.IO.Path.GetTempFileName();
        await using var original = await picked.OpenReadAsync();
        await using (var copy = File.OpenWrite(_tempFilePath))
            await original.CopyToAsync(copy);

        // load the file from that temp location
        var cert = new X509Certificate2(_tempFilePath);

        return cert;
    }

    private async void ImportIdentityPage_OnAppearing(object sender, EventArgs _)
    {
        HidePassword = true;
        Password = string.Empty;

        try
        {
            _certificate = await LoadCertificateFile();

            if (_certificate == null)
            {
                await Navigation.PopModalAsync();
                return;
            }

            Fingerprint = _certificate.Thumbprint.ToFriendlyFingerprint();
            Subject = _certificate.Subject;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while attempting to read a certificate prior to importing it");
            await Toast.Make(Text.ImportIdentityPage_ImportIdentityPage_OnAppearing_Failed_to_read_the_certificate_file).Show();
            await Navigation.PopModalAsync(true);
        }
    }

    private void ImportIdentityPage_OnDisappearing(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);
    }
}