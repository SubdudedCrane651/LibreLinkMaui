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
using Leaf.xNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
