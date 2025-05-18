using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Security.Cryptography;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO.Compression;


namespace LibreLinkMaui
{
    public partial class ConnectPage : ContentPage
    {
        public List<GlucoseData> GraphDataList { get; private set; } // Store parsed data
        public string LatestGlucoseValue { get; private set; }
        public string LatestTimestamp { get; private set; }
#if WINDOWS || ANDROID
        private readonly LibreLinkUpClient _client;
#else
        private readonly LibreLinkUpClient_iOS _client;
#endif
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
#if WINDOWS || ANDROID
            _client = new LibreLinkUpClient();
#else
            _client = new LibreLinkUpClient_iOS();
#endif

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
        private readonly HttpClient _httpClient;
        private string? _authToken;
        private string? _patientId;
        private string? _sha256Hash;

        public LibreLinkUpClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<bool> LoginAsync(string email, string password)
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
                var response = await _httpClient.PostAsync(loginUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Login failed: {response.StatusCode}");
                    return false;
                }

                // ✅ Decompress GZIP Content
                using var responseStream = await response.Content.ReadAsStreamAsync();
                using var decompressedStream = new GZipStream(responseStream, CompressionMode.Decompress);
                using var streamReader = new StreamReader(decompressedStream, Encoding.UTF8);
                string responseString = await streamReader.ReadToEndAsync();

                // ✅ Parse JSON after decompression
                dynamic JsonLogin = JsonConvert.DeserializeObject(responseString);
                string region = JsonLogin.data.region != null ? $"-{JsonLogin.data.region}" : "";
              
                // ✅ Handle region-based login
                if (!string.IsNullOrEmpty(region))
                {
                    loginUrl = $"https://api{region}.libreview.io/llu/auth/login";
                    conUrl = $"https://api{region}.libreview.io/llu/connections";

                    ConfigureHeaders(_httpClient,null, null);
                    response = await _httpClient.PostAsync(loginUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Regional login failed: {response.StatusCode}");
                        return false;
                    }

                    responseString = await response.Content.ReadAsStringAsync();
                    JsonLogin = JsonConvert.DeserializeObject(responseString);
                }

                _authToken = JsonLogin.data.authTicket.token;
                _patientId = JsonLogin.data.user.id;
                _sha256Hash = ComputeSha256Hash(_patientId);

                //_httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                //_httpClient.DefaultRequestHeaders.Add("Account-Id", _sha256Hash);

                ConfigureHeaders(_httpClient,_authToken, _sha256Hash);
                // ✅ Fetch patient connection details
                response = await _httpClient.GetAsync(conUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to retrieve patient connections: {response.StatusCode}");
                    return false;
                }

                // ✅ Decompress GZIP Content
                using var responseStream2 = await response.Content.ReadAsStreamAsync();
                using var decompressedStream2 = new GZipStream(responseStream2, CompressionMode.Decompress);
                using var streamReader2 = new StreamReader(decompressedStream2, Encoding.UTF8);
                string responseString2 = await streamReader2.ReadToEndAsync();
                dynamic jsonCon = JsonConvert.DeserializeObject(responseString2);
                _patientId = jsonCon.data[0].patientId;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }

        private void ConfigureHeaders(HttpClient client, string authToken, string sha256Hash)
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

        private static string ComputeSha256Hash(string input)
        {
            using var sha256Hash = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
        public async Task<List<GlucoseData>> GetGlucoseDataAsync()
        {
            if (string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(_patientId))
                throw new InvalidOperationException("Not authenticated. Call LoginAsync first.");

            using var httpClient = new HttpClient();
            ConfigureHeaders(httpClient, _authToken, _sha256Hash);

            var glucoseUrl = $"https://api.libreview.io/llu/connections/{_patientId}/graph";
            var response = await httpClient.GetAsync(glucoseUrl);

            if (response.IsSuccessStatusCode)
            {
                //var jsonResp = await response.Content.ReadAsStringAsync();
                using var responseStream = await response.Content.ReadAsStreamAsync();
                using var decompressedStream = new GZipStream(responseStream, CompressionMode.Decompress);
                using var streamReader = new StreamReader(decompressedStream, Encoding.UTF8);
                string responseString = await streamReader.ReadToEndAsync();

                // ✅ Parse JSON after decompression
                //dynamic jsonResp = JsonConvert.DeserializeObject(responseString);
                return ParseGraphData(responseString);
            }

            throw new Exception($"Failed to retrieve glucose data. Status Code: {response.StatusCode}");
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

        public async Task<bool> LoginAsync(string email, string password)
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
                var response = await _httpClient.PostAsync(loginUrl, content); 
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Login failed: {response.StatusCode}");
                    return false;
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

               
                dynamic JsonLogin = JsonConvert.DeserializeObject(responseString);
                string region = JsonLogin.data.region != null ? $"-{JsonLogin.data.region}" : "";

                // ✅ Handle region-based login
                if (!string.IsNullOrEmpty(region))
                {
                    loginUrl = $"https://api{region}.libreview.io/llu/auth/login";
                    conUrl = $"https://api{region}.libreview.io/llu/connections";

                    ConfigureHeaders(_httpClient, null, null);
                    response = await _httpClient.PostAsync(loginUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Regional login failed: {response.StatusCode}");
                        return false;
                    }

                    responseString = await response.Content.ReadAsStringAsync();
                    JsonLogin = JsonConvert.DeserializeObject(responseString);
                }

                _authToken = JsonLogin.data.authTicket.token;
                _patientId = JsonLogin.data.user.id;
                _sha256Hash = ComputeSha256Hash(_patientId);

                //_httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                //_httpClient.DefaultRequestHeaders.Add("Account-Id", _sha256Hash);

                ConfigureHeaders(_httpClient, _authToken, _sha256Hash);
                // ✅ Fetch patient connection details
                response = await _httpClient.GetAsync(conUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to retrieve patient connections: {response.StatusCode}");
                    return false;
                }

                // ✅ Read response as a byte array
                var responseBytes2 = await response.Content.ReadAsByteArrayAsync();

                // ✅ Check if response is GZIP compressed
                bool isGzip2 = response.Headers.TryGetValues("Content-Encoding", out var encodings2) &&
                              encodings2.Contains("gzip");

                string responseString2;

                if (isGzip2)
                {
                    using var decompressedStream = new GZipStream(new MemoryStream(responseBytes2), CompressionMode.Decompress);
                    using var streamReader = new StreamReader(decompressedStream, Encoding.UTF8);
                    responseString2 = await streamReader.ReadToEndAsync();
                }
                else
                {
                    responseString2 = Encoding.UTF8.GetString(responseBytes2);
                }
                dynamic jsonCon = JsonConvert.DeserializeObject(responseString2);
                _patientId = jsonCon.data[0].patientId;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return false;
            }
        }

        private void ConfigureHeaders(HttpClient client, string authToken, string sha256Hash)
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

        private static string ComputeSha256Hash(string input)
        {
            using var sha256Hash = System.Security.Cryptography.SHA256.Create();
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
        public async Task<List<GlucoseData>> GetGlucoseDataAsync()
        {
            if (string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(_patientId))
                throw new InvalidOperationException("Not authenticated. Call LoginAsync first.");

            using var httpClient = new HttpClient();
            ConfigureHeaders(httpClient, _authToken, _sha256Hash);

            var glucoseUrl = $"https://api.libreview.io/llu/connections/{_patientId}/graph";
            var response = await httpClient.GetAsync(glucoseUrl);

            if (response.IsSuccessStatusCode)
            {
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

                // ✅ Parse JSON after decompression
                //dynamic jsonResp = JsonConvert.DeserializeObject(responseString);
                return ParseGraphData(responseString);
            }

            throw new Exception($"Failed to retrieve glucose data. Status Code: {response.StatusCode}");
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