#if IOS
using Foundation;
using System;
using System.Threading.Tasks;
using UIKit;

public class LibreLinkUpClient_iOS
{
    private string? _authToken;
    private string? _patientId;
    private string? _sha256Hash;

    public async Task<string> LoginAsync(string email, string password)
    {
        try
        {
            var loginUrl = new NSUrl("https://api.libreview.io/llu/auth/login");
            var conUrl = new NSUrl("https://api.libreview.io/llu/connections");

            //var requestBody = new NSDictionary("email", email, "password", password);

            NSMutableDictionary requestBody = new NSMutableDictionary
            {
              { new NSString("email"), new NSString(email) },
              { new NSString("password"), new NSString(password) }
            };
            var jsonData = NSJsonSerialization.Serialize(requestBody, 0, out NSError error);

            var request = new NSMutableUrlRequest(loginUrl);
            request.HttpMethod = "POST";
            request.Body = jsonData;

            AddHeaders(request, null, null);  // ✅ Add Headers for Initial Login

            var session = NSUrlSession.SharedSession;
            var loginResponse = await session.CreateDataTaskAsync(request);

                if (loginResponse.Data == null)
                {
                    Console.WriteLine("Login response is null.");
                return "Login response is null." ;
                } else
                return loginResponse.Data.ToString();

            if (loginResponse.Response is NSHttpUrlResponse httpResponse && httpResponse.StatusCode == 200)
            {
                var jsonLogin = NSJsonSerialization.Deserialize(loginResponse.Data, 0, out NSError jsonError);

                var region = jsonLogin.ValueForKey(new NSString("data")).ValueForKey(new NSString("region"))?.ToString();

                if (!string.IsNullOrEmpty(region))
                {
                    loginUrl = new NSUrl($"https://api-{region}.libreview.io/llu/auth/login");
                    conUrl = new NSUrl($"https://api-{region}.libreview.io/llu/connections");

                    request = new NSMutableUrlRequest(loginUrl);
                    request.HttpMethod = "POST";
                    request.Body = jsonData;
                    AddHeaders(request, null, null);  // ✅ Add Headers Again for Regional Login

                    loginResponse = await session.CreateDataTaskAsync(request);
                    if (!(loginResponse.Response is NSHttpUrlResponse regResponse) || regResponse.StatusCode != 200)
                    {
                        return "No Login";
                    }
                    jsonLogin = NSJsonSerialization.Deserialize(loginResponse.Data, 0, out jsonError);
                }

                _authToken = jsonLogin.ValueForKey(new NSString("data")).ValueForKey(new NSString("authTicket")).ValueForKey(new NSString("token")).ToString();
                _patientId = jsonLogin.ValueForKey(new NSString("data")).ValueForKey(new NSString("user")).ValueForKey(new NSString("id")).ToString();
                _sha256Hash = ComputeSha256Hash(_patientId);

                request = new NSMutableUrlRequest(conUrl);
                request.HttpMethod = "GET";

                AddHeaders(request, _authToken, _sha256Hash);  // ✅ Headers for Authenticated Request

                var connectionsResponse = await session.CreateDataTaskAsync(request);
                if (connectionsResponse.Response is NSHttpUrlResponse conResponse && conResponse.StatusCode == 200)
                {
                    var jsonCon = NSJsonSerialization.Deserialize(connectionsResponse.Data, 0, out jsonError);
                    _patientId = jsonCon.ValueForKey(new NSString("data"))?.ValueForKey(new NSString("patientId"))?.ToString();

                    return "OK";
                }
            }
            return "No Data";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return ex.Message;
        }
    }

    private static void AddHeaders(NSMutableUrlRequest request, string? auth, string? hash)
    {
        NSMutableDictionary headers = new NSMutableDictionary
{
    { new NSString("Accept-Encoding"), new NSString("gzip") },
    { new NSString("Pragma"), new NSString("no-cache") },
    { new NSString("Connection"), new NSString("Keep-Alive") },
    { new NSString("Sec-Fetch-Mode"), new NSString("cors") },
    { new NSString("Sec-Fetch-Site"), new NSString("cross-site") },
    { new NSString("Sec-CH-UA-Mobile"), new NSString("?0") },
    { new NSString("User-Agent"), new NSString("HTTP Debugger/9.0.0.12") },
    { new NSString("Product"), new NSString("llu.android") },
    { new NSString("Version"), new NSString("4.12.0") },
    { new NSString("Cache-Control"), new NSString("no-cache") }
};

// ✅ Set Authorization and Account-Id Headers if Provided
if (!string.IsNullOrEmpty(auth))
    headers.SetValueForKey(new NSString($"Bearer {auth}"), new NSString("Authorization"));

if (!string.IsNullOrEmpty(hash))
    headers.SetValueForKey(new NSString(hash), new NSString("Account-Id"));

// ✅ Now Apply Headers to the Request Properly
request.SetValueForKey(headers, new NSString("allHTTPHeaderFields"));
    }

    private static string ComputeSha256Hash(string input)
    {
        using var sha256Hash = System.Security.Cryptography.SHA256.Create();
        byte[] bytes = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        var builder = new System.Text.StringBuilder();
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}
#endif