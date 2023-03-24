using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BepInEx.IL2CPP.MSBuild.Runner
{
    public static class DownloadUtility
    {
        private static readonly HttpClient _httpClient = new();

        public static async Task<HttpResponseMessage?> DownloadAsync(string url, string etagPath, bool includeEtag = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (includeEtag && File.Exists(etagPath))
            {
                request.Headers.IfNoneMatch.ParseAdd(File.ReadAllText(etagPath));
            }

            var responseMessage = await _httpClient.SendAsync(request);

            if (responseMessage.StatusCode == HttpStatusCode.NotModified)
            {
                return null;
            }

            responseMessage.EnsureSuccessStatusCode();

            var etag = responseMessage.Headers.ETag;
            if (etag != null)
            {
                File.WriteAllText(etagPath, etag.ToString());
            }

            return responseMessage;
        }

        public static async Task DownloadFileAsync(string url, string path)
        {
            Directory.GetParent(path)!.Create();

            var etagPath = path + ".etag";
            using var responseMessage = await DownloadAsync(url, etagPath, File.Exists(path));

            if (responseMessage == null)
            {
                return;
            }

            using var destination = File.OpenWrite(path);
            await responseMessage.Content.CopyToAsync(destination);
        }
    }
}
