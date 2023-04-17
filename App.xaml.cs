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
}
