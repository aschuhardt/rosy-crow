using RosyCrow.Extensions;
using RosyCrow.Models;

namespace RosyCrow.Views;

public partial class WhatsNewPage : ContentPage
{
	public WhatsNewPage()
	{
		InitializeComponent();
	}

    private void WhatsNewPage_OnAppearing(object sender, EventArgs e)
    {
        Browser.ParentPage = this.FindParentPage();
        Browser.Location = new Uri($"{Constants.InternalScheme}://whats-new");
    }
}