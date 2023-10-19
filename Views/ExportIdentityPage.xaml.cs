using System.Text;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Storage;
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
    private ICommand _copyText;
    private ICommand _export;
    private string _fingerprint;
    private bool _hidePassword;
    private Identity _identity;
    private string _name;
    private string _password;
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
        try
        {
            _logger.LogInformation("Exporting the certificate for Identity {ID} ({Name})", Identity.Id, Identity.Name);

            var cert = await _identityService.LoadCertificate(Identity);

            if (cert == null)
            {
                await Toast.Make(Text.ExportIdentityPage_ExportKey_The_identity_could_not_be_decrypted).Show();
                return;
            }

            var password = !string.IsNullOrEmpty(Password) ? Encoding.UTF8.GetBytes(Password) : null;

            // storage permission doesn't apply starting in 33
            if (!OperatingSystem.IsAndroidVersionAtLeast(33) &&
                await Permissions.CheckStatusAsync<Permissions.StorageWrite>() != PermissionStatus.Granted)
            {
                _logger.LogInformation("Requesting permission to write to external storage");

                var status = await Permissions.RequestAsync<Permissions.StorageWrite>();

                if (status != PermissionStatus.Granted && Permissions.ShouldShowRationale<Permissions.StorageWrite>())
                {
                    await DisplayAlert("Lacking Permission",
                        "Identities cannot be exported unless Rosy Crow has permission to write to you device's storage.\n\nTry again after you've granted the app permission to do so.",
                        "OK");
                    return;
                }
            }

            FileSaverResult result;

            await using (var buffer = new MemoryStream())
            {
                await using (var writer = new StreamWriter(buffer, leaveOpen: true))
                {
                    await cert.WriteCertificate(writer, password);
                }

                buffer.Seek(0, SeekOrigin.Begin);
                result = await FileSaver.Default.SaveAsync($"{Identity.SanitizedName}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.pem",
                    buffer,
                    CancellationToken.None);
            }

            if (result.IsSuccessful)
            {
                if (password != null)
                {
                    _logger.LogInformation("Certificate for Identity {ID} exported encrypted to {Path}", Identity.Id, result.FilePath);
                    await Clipboard.Default.SetTextAsync(Password);
                    await Toast.Make("Password copied to the clipboard").Show();
                    Password = string.Empty;
                }
                else
                {
                    _logger.LogInformation("Certificate for Identity {ID} exported unencrypted to {Path}", Identity.Id, result.FilePath);
                }
            }
            else
            {
                await Toast.Make("Could not save the file").Show();
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