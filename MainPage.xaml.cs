using Newtonsoft.Json.Linq;

namespace LibreLinkMaui
{
    public partial class MainPage : ContentPage
    {

        public Class1 class1 = new Class1();
        public static string password = "";
        public static string email = "";
        public static string json = "";
        public MainPage()
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

            catch
            {
                string loginjson = @"{'email':'" + email + "','password':'" + password + "'}";

                class1.SaveData(loginjson);
            }

            if (email != "")
            {
                emailEntry.Text = email;
                passwordEntry.Text = password;
                LoadConnectPage(email, password);
            }
        }

        public async void LoadConnectPage(string email, string password)
        {
            await Navigation.PushAsync(new ConnectPage(email, password));
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            var doit = class1.CheckCredentials(emailEntry.Text, passwordEntry.Text);
            if (doit.Result)
            {

                string loginjson = @"{'email':'" + emailEntry.Text + "','password':'" + passwordEntry.Text + "'}";

                class1.SaveData(loginjson);

                await Navigation.PushAsync(new ConnectPage(emailEntry.Text, passwordEntry.Text));


            }
            else
            {
                await DisplayAlert("Alert", "The email or password is incorrect please try again!", "OK");
            }
        }
    }
}
