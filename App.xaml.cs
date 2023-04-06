using RosyCrow.Views;

namespace RosyCrow;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

        MainPage = new NavigationPage(MauiProgram.Services.GetRequiredService<MainPage>());
    }
}
