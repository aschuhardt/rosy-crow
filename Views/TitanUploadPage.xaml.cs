using System.Text;
using System.Windows.Input;
using RosyCrow.Models;

// ReSharper disable AsyncVoidLambda

namespace RosyCrow.Views;

public partial class TitanUploadPage : ContentPage
{
    private bool _hideToken;
    private string _textBody;
    private IEnumerable<string> _textMimeTypeChoices;
    private ICommand _toggleTokenHidden;
    private string _token;
    private ICommand _uploadFile;
    private ICommand _uploadText;
    private string _textMimeType;

    public TitanUploadPage()
    {
        InitializeComponent();

        BindingContext = this;

        HideToken = true;
        ToggleTokenHidden = new Command(() => HideToken = !HideToken);
        UploadFile = new Command(async () => await TrySubmittingFileUpload());
        UploadText = new Command(async () => await TrySubmittingTextUpload());

        TextMimeTypeChoices = new[]
        {
            "text/plain",
            "text/gemini",
            "application/octet-stream"
        };

        TextMimeType = TextMimeTypeChoices.First();
    }

    public BrowserView Browser { get; set; }

    public IEnumerable<string> TextMimeTypeChoices
    {
        get => _textMimeTypeChoices;
        set
        {
            if (Equals(value, _textMimeTypeChoices))
                return;

            _textMimeTypeChoices = value;
            OnPropertyChanged();
        }
    }

    public string TextBody
    {
        get => _textBody;
        set
        {
            if (value == _textBody)
                return;

            _textBody = value;
            OnPropertyChanged();
        }
    }

    public string TextMimeType
    {
        get => _textMimeType;
        set
        {
            if (value == _textMimeType)
                return;

            _textMimeType = value;
            OnPropertyChanged();
        }
    }

    public ICommand UploadFile
    {
        get => _uploadFile;
        set
        {
            if (Equals(value, _uploadFile))
                return;

            _uploadFile = value;
            OnPropertyChanged();
        }
    }

    public ICommand UploadText
    {
        get => _uploadText;
        set
        {
            if (Equals(value, _uploadText))
                return;

            _uploadText = value;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleTokenHidden
    {
        get => _toggleTokenHidden;
        set
        {
            if (Equals(value, _toggleTokenHidden))
                return;

            _toggleTokenHidden = value;
            OnPropertyChanged();
        }
    }

    public bool HideToken
    {
        get => _hideToken;
        set
        {
            if (value == _hideToken)
                return;

            _hideToken = value;
            OnPropertyChanged();
        }
    }

    public string Token
    {
        get => _token;
        set
        {
            if (value == _token)
                return;

            _token = value;
            OnPropertyChanged();
        }
    }

    private async Task TrySubmittingTextUpload()
    {
        var buffer = string.IsNullOrEmpty(TextBody)
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(TextBody);

        await Navigation.PopModalAsync(true);

        await Dispatcher.DispatchAsync(async () =>
        {
            await Browser.Upload(new TitanPayload
            {
                Contents = new MemoryStream(buffer),
                Token = Token,
                MimeType = TextMimeType,
                Size = buffer.Length
            });
        });
    }

    private async Task TrySubmittingFileUpload()
    {
        var file = await FilePicker.Default.PickAsync(PickOptions.Default);

        if (file == null)
            return;

        await Navigation.PopModalAsync(true);

        await Dispatcher.DispatchAsync(async () =>
        {
            if (string.IsNullOrEmpty(file.ContentType) && MimeTypes.TryGetMimeType(file.FullPath, out var mimeType))
                file.ContentType = mimeType;

            var stream = await file.OpenReadAsync();
            await Browser.Upload(new TitanPayload
            {
                Contents = stream,
                Token = Token,
                MimeType = file.ContentType,
                Size = (int)stream.Length
            });
        });
    }
}