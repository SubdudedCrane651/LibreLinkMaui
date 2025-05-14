using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Security.Cryptography;
using Leaf.xNet;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using Syncfusion.Maui.Core.Carousel;

namespace LibreLinkMaui
{
    public partial class ConnectPage : ContentPage
    {
        public List<GlucoseData> GraphDataList { get; private set; } // Store parsed data
        public string LatestGlucoseValue { get; private set; }
        public string LatestTimestamp { get; private set; }

        private readonly LibreLinkUpClient _client;
        private CancellationTokenSource _cts;
        public string Email = "";
        public string Password = "";
        public Class1 class1 = new Class1();
        private readonly ConnectPageViewModel _viewModel;


        public ConnectPage(string email, string password)
        {
            Email = email;
            Password = password;
            InitializeComponent();
            _client = new LibreLinkUpClient();
            // Start looping Main method asynchronously
            //Main().ConfigureAwait(false);
            _viewModel = new ConnectPageViewModel();
            BindingContext = _viewModel; // ✅ Ensures bindings work
            Task.Run(() => StartLoopAsync());
        }

        public async Task StartLoopAsync()
        {
            _cts = new CancellationTokenSource();
            while (!_cts.Token.IsCancellationRequested)
            {
                await LoadChartDataAsync();
                await Task.Delay(1000); // Wait 1 second before repeating
            }
            Console.WriteLine("Loop Stopped.");
        }

        public void StopLoop()
        {
            _cts?.Cancel(); // Stops the loop
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopLoop(); // Stop loop on exit
        }

        public class ConnectPageViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private List<GlucoseData> _graphDataList;
            public List<GlucoseData> GraphDataList
            {
                get => _graphDataList;
                set { _graphDataList = value; OnPropertyChanged(nameof(GraphDataList)); }
            }

            private string _latestGlucoseValue;
            public string LatestGlucoseValue
            {
                get => _latestGlucoseValue;
                set { _latestGlucoseValue = value; OnPropertyChanged(nameof(LatestGlucoseValue)); }
            }

            private string _latestTimestamp;
            public string LatestTimestamp
            {
                get => _latestTimestamp;
                set { _latestTimestamp = value; OnPropertyChanged(nameof(LatestTimestamp)); }
            }

            protected void OnPropertyChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        private async Task LoadChartDataAsync()
        {
            try
            {
                var loginSuccess = await _client.LoginAsync(Email, Password);
                if (!loginSuccess)
                {
                    Console.WriteLine("Login failed.");
                    return;
                }

                var newData = await _client.GetGlucoseDataAsync();
                if (newData != null && newData.Count > 0)
                {
                    _viewModel.GraphDataList = newData; // ✅ Bind list dynamically

                    var latest = _viewModel.GraphDataList[^1];
                    _viewModel.LatestTimestamp = $"Timestamp: {latest.Timestamp}";
                    _viewModel.LatestGlucoseValue = $"Glucose: {latest.Value} mmol/L";
                }

                BindingContext = _viewModel; // ✅ Ensures bindings work
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }

        private async void Disconnect_Clicked(object sender, EventArgs e)
        {
            StopLoop(); // Stop the loop when disconnect is clicked
            string loginjson = @"{'email':'" + "','password':'" + "'}";
            class1.SaveData(loginjson);
            await Navigation.PushAsync(new MainPage());
        }
    }

    public class LibreLinkUpClient
    {
        private string _authToken;
        private string _patientId;
        private string _sha256Hash;

        public async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                HttpRequest postReq = new HttpRequest();
                var loginUrl = "https://api.libreview.io/llu/auth/login";
                var conUrl = "https://api.libreview.io/llu/connections";

                Utils.addHeaders(postReq, null, null);
                var requestBody = new { email, password };
                var json = JsonConvert.SerializeObject(requestBody);
                var request = postReq.Post(loginUrl, json, "application/json");
                string region = null;

                if (request.StatusCode.ToString() == "OK")
                {
                    string requested = null;
                    bool actual = false;
                    dynamic JsonLogin = JsonConvert.DeserializeObject(request.ToString());
                    if (JsonLogin.data.region != null)
                    {
                        region = $"-{JsonLogin.data.region}";
                        loginUrl = $"https://api{region}.libreview.io/llu/auth/login";
                        conUrl = $"https://api{region}.libreview.io/llu/connections";
                        postReq.ClearAllHeaders();
                        Utils.addHeaders(postReq, null, null);
                        requested = postReq.Post(loginUrl, json, "application/json").ToString();
                    }
                    else
                        actual = true;

                    string input = null;
                    if (requested != null || actual == true)
                    {
                        if (requested != null)
                        {
                            dynamic jsonResp = JsonConvert.DeserializeObject(requested);
                            _authToken = jsonResp.data.authTicket.token;
                            try
                            {
                                input = jsonResp.data.user.id;
                            }
                            catch { }
                        }
                        else if (actual == true)
                        {
                            _authToken = JsonLogin.data.authTicket.token;
                            input = JsonLogin.data.user.id;
                        }
                        if (input != null)
                            _sha256Hash = ComputeSha256Hash(input);

                        postReq.ClearAllHeaders();
                        Utils.addHeaders(postReq, _authToken, _sha256Hash);
                        var connectionsResp = postReq.Get(conUrl);

                        if (connectionsResp.StatusCode.ToString() == "OK")
                        {
                            dynamic jsonCon = JsonConvert.DeserializeObject(connectionsResp.ToString());
                            _patientId = jsonCon.data[0].patientId;
                            postReq.ClearAllHeaders();
                            Utils.addHeaders(postReq, _authToken, _sha256Hash);
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }

        private static string ComputeSha256Hash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public async Task<List<GlucoseData>> GetGlucoseDataAsync()
        {
            if (string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(_patientId))
                throw new InvalidOperationException("Not authenticated. Call LoginAsync first.");

            HttpRequest postReq = new HttpRequest();
            Utils.addHeaders(postReq, _authToken, _sha256Hash);
            var response = postReq.Get($"https://api.libreview.io/llu/connections/{_patientId}/graph");

            if (response.StatusCode.ToString() == "OK")
            {
                dynamic jsonResp = JsonConvert.DeserializeObject(response.ToString());
                return ParseGraphData(JsonConvert.SerializeObject(jsonResp, Formatting.Indented));
            }

            throw new Exception("Failed to retrieve glucose data");
        }

        public List<GlucoseData> ParseGraphData(string json)
        {
            dynamic jsonResp = JsonConvert.DeserializeObject(json);
            var dataList = new List<GlucoseData>();

            if (jsonResp?.data?.graphData != null)
            {
                foreach (var item in jsonResp.data.graphData)
                {
                    dataList.Add(new GlucoseData
                    {
                        Timestamp = DateTime.Parse(item.Timestamp.ToString()),
                        Value = float.Parse(item.Value.ToString())
                    });
                }
            }

            var data = jsonResp.data.connection.glucoseMeasurement;
            dataList.Add(new GlucoseData
            {
                Timestamp = DateTime.Parse(data.Timestamp.ToString()),
                Value = float.Parse(data.Value.ToString())
            });


            return dataList;
        }
    }
}