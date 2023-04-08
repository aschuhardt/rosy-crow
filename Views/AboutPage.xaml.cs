using RosyCrow.Models;

namespace RosyCrow.Views;

public partial class AboutPage : ContentPage
{
	public AboutPage()
	{
		InitializeComponent();
	}

    private void AboutPage_OnAppearing(object sender, EventArgs e)
    {
        Browser.Location = new Uri($"{Constants.InternalScheme}://about");
    }
}