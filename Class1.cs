using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Threading.Tasks;


namespace LibreLinkMaui
{
    public class Class1
    {
        private static string mainDir = Microsoft.Maui.Storage.FileSystem.Current.AppDataDirectory;
        private static string MobileData = System.IO.Path.Combine(mainDir, "login.json");

        public async Task<string> SendMail(string subject, string body, string recipient, string Bcc, string attachments)
        {
            try
            {
                var smtpClient = new System.Net.Mail.SmtpClient("mail.noip.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("userName", "password"),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("username"),
                    Subject = subject,
                    Body = body,
                };

                if (attachments != "")
                    mailMessage.Attachments.Add(new Attachment(attachments, "application/png"));

                mailMessage.To.Add(recipient);
                mailMessage.IsBodyHtml = true;
                if (Bcc != "")
                    mailMessage.Bcc.Add(Bcc);

                await smtpClient.SendMailAsync(mailMessage);

                mailMessage.To.Clear();
                if (Bcc != "")
                {
                    mailMessage.To.Add(Bcc);

                    await smtpClient.SendMailAsync(mailMessage);
                }
                //await DisplayAlert("Success", "Message was sent.", "Ok");

                return "Success!";
            }
            catch (System.Exception ex)
            {
                //await DisplayAlert("Error", ex.Message, "Ok");
                return ex.Message;
            }
        }

        public void SaveData(string str)
        {

            File.WriteAllText(MobileData, str);//save
        }

        public async Task<bool> CheckCredentials(string email, string password)
        {
            var _client = new LibreLinkUpClient();

            try
            {
                var loginSuccess = await _client.LoginAsync(email, password);
                if (loginSuccess)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public string GetLoginJson()
        {
            var AllOfTexts = "";

            if (File.Exists(MobileData))
            {

                AllOfTexts = File.ReadAllText(MobileData);
            }
            else { AllOfTexts = @"{'email':'',password:''}"; }
            return AllOfTexts;
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

            private void ConfigureDefaultHeaders()
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
                _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
                _httpClient.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
                _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
                _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
                _httpClient.DefaultRequestHeaders.Add("Sec-CH-UA-Mobile", "?0");
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "HTTP Debugger/9.0.0.12");
                _httpClient.DefaultRequestHeaders.Add("Product", "llu.android");
                _httpClient.DefaultRequestHeaders.Add("Version", "4.12.0");
                _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            }

            public async Task<bool> LoginAsync(string email, string password)
            {
                try
                {
                    var loginUrl = "https://api.libreview.io/llu/auth/login";
                    var conUrl = "https://api.libreview.io/llu/connections";

                    var requestBody = new { email, password };
                    var json = JsonConvert.SerializeObject(requestBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    ConfigureDefaultHeaders();
                    var response = await _httpClient.PostAsync(loginUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Login failed: {response.StatusCode}");
                        return false;
                    }

                    var responseString = await response.Content.ReadAsStringAsync();
                    dynamic JsonLogin = JsonConvert.DeserializeObject(responseString);
                    string region = JsonLogin.data.region != null ? $"-{JsonLogin.data.region}" : "";

                    if (!string.IsNullOrEmpty(region))
                    {
                        loginUrl = $"https://api{region}.libreview.io/llu/auth/login";
                        conUrl = $"https://api{region}.libreview.io/llu/connections";

                        ConfigureDefaultHeaders();
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

                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                    _httpClient.DefaultRequestHeaders.Add("Account-Id", _sha256Hash);

                    response = await _httpClient.GetAsync(conUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Failed to retrieve patient connections: {response.StatusCode}");
                        return false;
                    }

                    responseString = await response.Content.ReadAsStringAsync();
                    dynamic jsonCon = JsonConvert.DeserializeObject(responseString);
                    _patientId = jsonCon.data[0].patientId;

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    return false;
                }
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
        }


        public string Translate(string phrase, string language)
        {
            string trans = "";

            var result = Task.Run(() => ReadLanguageJson()).Result;

            JArray rss = JArray.Parse(result.ToString());

            foreach (var item in rss)
            {
                if ((string)item["phrase"] == phrase)
                {
                    trans = (string)item[language];
                }
            }
            return trans;
        }

        private async Task<string> ReadLanguageJson()
        {
            using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("language.json");
            using StreamReader reader = new StreamReader(fileStream);

            return await reader.ReadToEndAsync();
        }
    }
}