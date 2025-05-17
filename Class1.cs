using System.Net;
using System.Net.Mail;
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
        public void SendPersonalData(string type, string content)
        {
            //_ = WritePersonalJson(type, content);
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
    }
}