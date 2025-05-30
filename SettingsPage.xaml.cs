using Microsoft.Maui.ApplicationModel.Communication;
using Newtonsoft.Json.Linq;

namespace LibreLinkMaui;

public partial class SettingsPage : ContentPage
{
    public Class1 class1 = new Class1();
    public double hyperlevel = 14;
    public string hyperspeak = "";
    public double hypolevel = 4;
    public string hypospeak = "";
    public static string password = "";
    public static string email = "";
    public SettingsPage()
	{
		InitializeComponent();

        string result = class1.GetLoginJson();
        var jsonObject = JObject.Parse(result);

        try
        {
            email = jsonObject["email"].ToString();
        }
        catch { }

        try
        {
            password = jsonObject["password"].ToString();
        }
        catch { }

        try
        {
            hyperlevel = Convert.ToDouble(jsonObject["hyperlevel"].ToString());
        }
        catch { }

        try
        {
            hyperspeak = jsonObject["hyperspeak"].ToString();
        }
        catch { }

        try
        {
            hypolevel = Convert.ToDouble(jsonObject["hypolevel"].ToString());
        }
        catch { }

        try
        {
            hypospeak = jsonObject["hypospeak"].ToString();
        }
        catch { }

        HyperEntry.Text = hyperlevel.ToString();
        HyperSpeak.Text=hyperspeak.ToString();
        HypoEntry.Text = hypolevel.ToString();
        HypoSpeak.Text = hypospeak.ToString();
    }

    private async void Savebtn_Clicked(object sender, EventArgs e)
    {
        string loginjson = @"{'email':'" + email + "','password':'" + password + "','hyperlevel':'" + HyperEntry.Text + "','hyperspeak':'" + HyperSpeak.Text + "','hypolevel':'" + HypoEntry.Text + "','hypospeak':'" + HypoSpeak.Text+ "'}";

        class1.SaveData(loginjson);

        await Navigation.PushAsync(new ConnectPage(email, password));
    }
}