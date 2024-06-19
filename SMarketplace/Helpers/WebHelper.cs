using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace SeniorS.SMarketplace.Helpers;
public class WebHelper
{
    public static async Task<string> GetLatestVersion()
    {
        using HttpClient httpClient = new HttpClient();
        HttpRequestMessage request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://unturnedstore.com/api/products/1618"),
        };

        request.Headers.Add("accept", "*/*");
        request.Headers.Add("accept-language", "en-US,en;q=0.8");
        request.Headers.Add("cache-control", "no-cache");
        request.Headers.Add("pragma", "no-cache");
        request.Headers.Add("priority", "u=1, i");
        request.Headers.Add("sec-ch-ua-mobile", "?0");
        request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
        request.Headers.Add("sec-fetch-dest", "empty");
        request.Headers.Add("sec-fetch-mode", "cors");
        request.Headers.Add("sec-fetch-site", "same-origin");
        request.Headers.Add("sec-gpc", "1");
        request.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        string responseBody = await response.Content.ReadAsStringAsync();

        JObject jsonObject = JObject.Parse(responseBody);

        return $"{jsonObject["branches"][0]["versions"].Last()["name"]}";
    }


    public static void LoadWebhookLibrary() // This initially wasn't planned but due recent issues with this library, for now on it will automatically download the latest version from github.
    {
        string webhookLibraryPath = Path.Combine(Rocket.Core.Environment.LibrariesDirectory, "SeniorS.WebhookHelper.dll");
        if (File.Exists(webhookLibraryPath))
        {
            File.Delete(webhookLibraryPath);
        }

        using WebClient webClient = new WebClient();

        byte[] libraryBytes = webClient.DownloadData("https://github.com/Senior-S/WebhookHelper/releases/download/Latest/SeniorS.WebhookHelper.dll");
        Assembly assembly = Assembly.Load(libraryBytes);

        webClient.Dispose();
    }
}
