using System.Text;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Logging;
using RosyCrow.Extensions;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;
using RosyCrow.Services.Identity;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class ExportIdentityPage : ContentPage
{
    private readonly IIdentityService _identityService;
    private readonly ILogger<ExportIdentityPage> _logger;
    private ICommand _export;
    private Identity _identity;
    private string _password;
    private bool _usePassword;
    private string _fingerprint;
    private ICommand _copyText;
    private string _name;
    private bool _hidePassword;
    private ICommand _togglePasswordHidden;

    public ExportIdentityPage(IIdentityService identityService, ILogger<ExportIdentityPage> logger)
    {
        _identityService = identityService;
        _logger = logger;

        InitializeComponent();

        BindingContext = this;

        Export = new Command(async () => await ExportKey());
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

    public string Name
    {
        get => _name;
        set
        {
            if (value == _name) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public Identity Identity
    {
        get => _identity;
        set
        {
            if (Equals(value, _identity)) return;
            _identity = value;
            Fingerprint = _identity.Hash.ToFriendlyFingerprint();
            Name = _identity.Name;
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

    public ICommand Export
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

    private async Task ExportKey()
    {
        if (UsePassword && string.IsNullOrEmpty(Password))
        {
            await Toast.Make(Text.ExportIdentityPage_ExportKey_Enter_a_password_or_disable_password_protection).Show();
            return;
        }

        try
        {
            _logger.LogInformation("Exporting the certificate for Identity {ID} ({Name})", Identity.Id, Identity.Name);

            var cert = await _identityService.LoadCertificate(Identity);

            if (cert == null)
            {
                await Toast.Make(Text.ExportIdentityPage_ExportKey_The_identity_could_not_be_decrypted).Show();
                return;
            }

            var password = UsePassword ? Encoding.UTF8.GetBytes(Password) : null;

            var path = Path.Combine(Path.GetTempPath(),
                $"{Identity.SanitizedName}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.pem");

            await using (var file = File.Create(path))
            await using (var writer = new StreamWriter(file))
            {
                if (!await cert.WriteCertificate(writer, password))
                {
                    await Toast.Make(Text.ExportIdentityPage_ExportKey_Failed_to_export_the_certificate).Show();
                    return;
                }
            }

            var mimeType = UsePassword ? "application/pkcs8-encrypted" : "application/pkcs8";

            await Share.Default.RequestAsync(new ShareFileRequest(Identity.Name, new ReadOnlyFile(path, mimeType)));

            if (UsePassword)
            {
                _logger.LogInformation("Certificate for Identity {ID} exported encrypted to {Path}", Identity.Id, path);
                await Clipboard.Default.SetTextAsync(Password);
                await Toast.Make("Password copied to the clipboard").Show();
                Password = string.Empty;
            }
            else
            {
                _logger.LogInformation("Certificate for Identity {ID} exported unencrypted to {Path}", Identity.Id,
                    path);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Exception thrown while exporting identity {ID}", Identity?.Id);
        }
        finally
        {
            await Navigation.PopModalAsync(true);
        }
    }

    private void ExportIdentityPage_OnAppearing(object sender, EventArgs e)
    {
        HidePassword = true;
    }
}