using Newtonsoft.Json.Linq;

namespace LibreLinkMaui
{
    public partial class MainPage : ContentPage
    {

        public Class1 class1 = new Class1();
        public string password = "";
        public string email = "";
        public string json = "";
        public MainPage()
        {
            InitializeComponent();

            string result = class1.GetLoginJson();
            var jsonObject = JObject.Parse(result);

            try
            {
                email = jsonObject["email"].ToString();
            }
            catch
            {
                email = "";
            }

            try
            {
                password = jsonObject["password"].ToString();
            }
            
            catch
            {
                password = "";
            }

            if(email!="" && password!="")
            {
                emailEntry.Text = email;
                passwordEntry.Text = password;
                LoadConnectPage(email,password);
            } else
            {
                json = @"{'email':'" + "','password':'" + "'}";

                class1.SaveData(json);
            }
        }

        public async void LoadConnectPage(string email,string password)
        {
            await Navigation.PushAsync(new ConnectPage(email, password));
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            bool doit = await class1.CheckCredentials(emailEntry.Text, passwordEntry.Text); // ✅ Await async function

            if (doit)
            {
                string loginjson = $"{{'email':'{emailEntry.Text}','password':'{passwordEntry.Text}'}}";

                class1.SaveData(loginjson);

                await Navigation.PushAsync(new ConnectPage(emailEntry.Text, passwordEntry.Text)); // ✅ Await navigation
            }
            else
            {
                await DisplayAlert("Alert", "The email or password is incorrect, please try again!", "OK");
            }
        }
    }
}
