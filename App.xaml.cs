namespace Yarrow;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

        MainPage = MauiProgram.Services.GetRequiredService<MainPage>();
    }
}
