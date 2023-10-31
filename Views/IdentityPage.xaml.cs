using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Maui.Alerts;
using Microsoft.Extensions.Logging;
using RosyCrow.Extensions;
using RosyCrow.Interfaces;
using RosyCrow.Models;
using RosyCrow.Resources.Localization;
using RosyCrow.Services.Fingerprint.Platforms.Android.Utils;
using RosyCrow.Services.Identity;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class IdentityPage : ContentPage
{
    private readonly IBrowsingDatabase _browsingDatabase;
    private readonly IIdentityService _identityService;
    private readonly ILogger<IdentityPage> _logger;
    private readonly ISettingsDatabase _settingsDatabase;

    private ICommand _delete;
    private ICommand _export;
    private ICommand _generateNew;
    private ObservableCollection<Identity> _identities;
    private ICommand _import;
    private ICommand _toggleActive;

    public IdentityPage(IBrowsingDatabase browsingDatabase,
        IIdentityService identityService, ISettingsDatabase settingsDatabase, ILogger<IdentityPage> logger)
    {
        _browsingDatabase = browsingDatabase;
        _identityService = identityService;
        _settingsDatabase = settingsDatabase;
        _logger = logger;

        InitializeComponent();

        BindingContext = this;

        GenerateNew = new Command(async () => await GenerateNewKey());
        Delete = new Command(async id => await DeleteKey((int)id));
        ToggleActive = new Command(async id => await ToggleActiveKey((int)id));
        Import = new Command(async () => await Navigation.PushModalPageAsync<ImportIdentityPage>());
        Export = new Command(async id => await TryOpeningExportKeyPage((int)id));
    }

    public ObservableCollection<Identity> Identities
    {
        get => _identities;
        set
        {
            if (Equals(value, _identities)) return;

            _identities = value;
            OnPropertyChanged();
        }
    }

    public ICommand GenerateNew
    {
        get => _generateNew;
        set
        {
            if (Equals(value, _generateNew)) return;

            _generateNew = value;
            OnPropertyChanged();
        }
    }

    public ICommand Delete
    {
        get => _delete;
        set
        {
            if (Equals(value, _delete)) return;

            _delete = value;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleActive
    {
        get => _toggleActive;
        set
        {
            if (Equals(value, _toggleActive)) return;

            _toggleActive = value;
            OnPropertyChanged();
        }
    }

    public ICommand Import
    {
        get => _import;
        set
        {
            if (Equals(value, _import)) return;

            _import = value;
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

    private async Task ToggleActiveKey(int id)
    {
        try
        {
            var identity = Identities.FirstOrDefault(i => i.Id == id);
            if (identity == null)
                return;

            if (id == _settingsDatabase.ActiveIdentityId.GetValueOrDefault(-1))
            {
                ClearIdentityActiveIndicator();
                _identityService.ClearActiveCertificate();
                _logger.LogInformation(@"Deactivated identity {ID} ({Name})", identity.Id, identity.Name);
            }
            else
            {
                SetIdentityActiveIndicator(id);
                await _identityService.Activate(identity);
                _logger.LogInformation(@"Activated identity {ID} ({Name})", identity.Id, identity.Name);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while toggling the active status of identity {ID}", id);
        }
    }

    private void SetIdentityActiveIndicator(int activeId)
    {
        foreach (var identity in Identities)
            identity.IsActive = identity.Id == activeId;
    }

    private void ClearIdentityActiveIndicator()
    {
        foreach (var identity in Identities)
            identity.IsActive = false;
    }

    private async Task DeleteKey(int id)
    {
        try
        {
            var identity = Identities.FirstOrDefault(i => i.Id == id);
            if (identity == null)
                return;

            if (!await DisplayAlert(Text.IdentityPage_DeleteKey_Delete_Identity,
                    Text.IdentityPage_DeleteKey_Confirm,
                    Text.Global_Yes,
                    Text.Global_No))
                return;

            if (string.IsNullOrEmpty(identity.EncryptedPassword) || new CryptoObjectHelper(identity.SemanticKey).Delete())
            {
                Identities.Remove(identity);
                if (File.Exists(identity.CertificatePath))
                    File.Delete(identity.CertificatePath);
                await Toast.Make(Text.IdentityPage_DeleteKey_Identity_deleted).Show();
            }
            else
            {
                await Toast.Make(Text.IdentityPage_DeleteKey_Failed_to_delete_identity).Show();
            }

            _logger.LogInformation(@"Deleted identity {ID} ({Name})", identity.Id, identity.Name);
        }
        catch (Exception e)
        {
            _logger.LogError(e, @"Exception thrown while deleting identity {ID}", id);
        }
    }

    private async Task TryOpeningExportKeyPage(int id)
    {
        var identity = _browsingDatabase.Identities.FirstOrDefault(i => i.Id == id);

        if (identity == null)
        {
            await Toast.Make(Text.IdentityPage_TryOpeningExportKeyPage_Cannot_export_this_identity).Show();
            return;
        }

        await Navigation.PushModalPageAsync<ExportIdentityPage>(page => page.Identity = identity);
    }

    private async Task<bool> PresentProtectionPrompt()
    {
        return await DisplayAlert(Text.IdentityPage_GenerateNewKey_Generate_Identity,
            Text.IdentityPage_GenerateNewKey_Secure,
            Text.Global_Yes,
            Text.Global_No);
    }

    private async Task GenerateNewKey()
    {
        var name = await DisplayPromptAsync(Text.IdentityPage_GenerateNewKey_Generate_Identity,
            Text.IdentityPage_GenerateNewKey_Prompt,
            maxLength: 400);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var identity = await _identityService.GenerateNewIdentity(name, PresentProtectionPrompt);

            if (identity == null)
            {
                _logger.LogInformation(@"No new identity was generated");
                return;
            }

            _logger.LogInformation(@"Saved the new identity named {Name}", identity.Name);
        }
    }

    private void IdentityPage_OnAppearing(object sender, EventArgs e)
    {
        Identities = _browsingDatabase.Identities;

        if (_settingsDatabase.ActiveIdentityId.HasValue)
            SetIdentityActiveIndicator(_settingsDatabase.ActiveIdentityId.Value);
        else
            ClearIdentityActiveIndicator();
    }
}