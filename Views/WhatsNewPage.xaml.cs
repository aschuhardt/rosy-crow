using RosyCrow.Models;

namespace RosyCrow.Views;

public partial class WhatsNewPage : ContentPage
{
	public WhatsNewPage()
	{
		InitializeComponent();
	}

    private async void WhatsNewPage_OnAppearing(object sender, EventArgs e)
    {
        await Browser.Setup(this);
        Browser.Location = new Uri($"{Constants.InternalScheme}://whats-new");
    }
}