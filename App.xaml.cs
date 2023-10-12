using RosyCrow.Services.Document;
using RosyCrow.Views;

namespace RosyCrow;

public partial class App : Application
{
    public static string StartupUri;

	public App()
	{
		InitializeComponent();
        
        MainPage = new NavigationPage(MauiProgram.Services.GetRequiredService<MainPage>());
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        var window = base.CreateWindow(activationState);

        window.Created += async (_, _) => await MauiProgram.Services.GetRequiredService<IDocumentService>().LoadResources();

        return window;
    }
}
