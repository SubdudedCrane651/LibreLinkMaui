using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Text;

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

        public class LibreLinkUpClient
        {
            private readonly HttpClient _httpClient;
            private string? _authToken;
            private string? _patientId;
            private string? _sha256Hash;

            public LibreLinkUpClient()
            {
                _httpClient = new HttpClient();
            }

            public async Task<string> LoginAsync(string email, string password)
            {
                try
                {
                    var loginUrl = "https://api.libreview.io/llu/auth/login";
                    var conUrl = "https://api.libreview.io/llu/connections";

                    var requestBody = new
                    {
                        email = email,
                        password = password
                    };

                    var json = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    ConfigureHeaders(_httpClient, null, null);
                    // ✅ Send Login Request

                    var response = await _httpClient.PostAsync(loginUrl, content).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Login failed: {response.StatusCode}");
                        return "Login failed";
                    }

                    // ✅ Decompress GZIP Content
                    using var responseStream = await response.Content.ReadAsStreamAsync();
                    using var decompressedStream = new GZipStream(responseStream, CompressionMode.Decompress);
                    using var streamReader = new StreamReader(decompressedStream, Encoding.UTF8);
                    string responseString = await streamReader.ReadToEndAsync();

                    JObject jsonResponse = JObject.Parse(responseString);

                    // ✅ Extract "status" field from JSON
                    int? errorCode = jsonResponse["status"]?.ToObject<int>();

                    if (errorCode != null && errorCode == 2)
                        return "Email or Password incorrect try again " + responseString;
                    else
                        return "OK";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return ex.Message;
                }
            }
        }
        public class LibreLinkUpClient_iOS
        {
            private readonly HttpClient _httpClient;
            private string? _authToken;
            private string? _patientId;
            private string? _sha256Hash;

            public LibreLinkUpClient_iOS()
            {
                _httpClient = new HttpClient();
            }

            public async Task<string> LoginAsync(string email, string password)
            {
                try
                {
                    var loginUrl = "https://api.libreview.io/llu/auth/login";
                    var conUrl = "https://api.libreview.io/llu/connections";

                    var requestBody = new
                    {
                        email = email,
                        password = password
                    };

                    var json = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    ConfigureHeaders(_httpClient, null, null);
                    // ✅ Send Login Request

                    var response = await _httpClient.PostAsync(loginUrl, content).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Login failed: {response.StatusCode}");
                        return "Login failed";
                    }

                    // ✅ Read response as a byte array
                    var responseBytes = await response.Content.ReadAsByteArrayAsync();

                    // ✅ Check if response is GZIP compressed
                    bool isGzip = response.Headers.TryGetValues("Content-Encoding", out var encodings) &&
                                  encodings.Contains("gzip");

                    string responseString;

                    if (isGzip)
                    {
                        using var decompressedStream = new GZipStream(new MemoryStream(responseBytes), CompressionMode.Decompress);
                        using var streamReader = new StreamReader(decompressedStream, Encoding.UTF8);
                        responseString = await streamReader.ReadToEndAsync();
                    }
                    else
                    {
                        responseString = Encoding.UTF8.GetString(responseBytes);
                    }

                    // ✅ Parse JSON correctly
                    JObject jsonResponse = JObject.Parse(responseString);

                    // ✅ Extract "status" field from JSON
                    int? errorCode = jsonResponse["status"]?.ToObject<int>();

                    if (errorCode != null && errorCode == 2)
                        return "Email or Password incorrect try again " + responseString;
                    else
                        return "OK";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return "iOS code error " + ex.Message;
                }
            }
        }
        private static void ConfigureHeaders(HttpClient client, string authToken, string sha256Hash)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
            client.DefaultRequestHeaders.Add("Pragma", "no-cache");
            client.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
            client.DefaultRequestHeaders.Add("Sec-CH-UA-Mobile", "?0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "HTTP Debugger/9.0.0.12");
            client.DefaultRequestHeaders.Add("Product", "llu.android");
            client.DefaultRequestHeaders.Add("Version", "4.12.0");
            client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            if (!string.IsNullOrEmpty(authToken))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);

            if (!string.IsNullOrEmpty(sha256Hash))
                client.DefaultRequestHeaders.Add("Account-Id", sha256Hash);
        }

        public async Task<string> CheckCredentials(string email, string password)
        {

            var _client = new LibreLinkUpClient();

            string result = "";

            try
            {

                var loginSuccess = await _client.LoginAsync(email, password).ConfigureAwait(false);
                result = loginSuccess;
                if (loginSuccess == "OK")
                {
                    return "OK";
                }
            }
            catch { }

            return result;
        }

        public async Task<string> CheckCredentials_iOS(string email, string password)
        {
            var _client = new LibreLinkUpClient_iOS();

            string result = "";

            try
            {

                var loginSuccess = await _client.LoginAsync(email, password).ConfigureAwait(false);
                result = loginSuccess;
                if (loginSuccess == "OK")
                {
                    return "OK";
                }
            }
            catch { }

            return result;
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
#if WINDOWS || ANDROID
            var doit = CheckCredentials(emailEntry.Text, passwordEntry.Text);
            if (doit.Result == "OK")
            {

                string loginjson = @"{'email':'" + emailEntry.Text + "','password':'" + passwordEntry.Text + "'}";

                class1.SaveData(loginjson);

                await Navigation.PushAsync(new ConnectPage(emailEntry.Text, passwordEntry.Text));


            }
            else
            {
                await DisplayAlert("Alert", doit.Result, "OK");
            }
#else
            var doit = CheckCredentials_iOS(emailEntry.Text, passwordEntry.Text);
            if (doit.Result=="OK")
            {

                string loginjson = @"{'email':'" + emailEntry.Text + "','password':'" + passwordEntry.Text + "'}";

                class1.SaveData(loginjson);

                await Navigation.PushAsync(new ConnectPage(emailEntry.Text, passwordEntry.Text));


            }
            else
            {
                await DisplayAlert("Alert", doit.Result, "OK");
            }
#endif

        }
    }
}
