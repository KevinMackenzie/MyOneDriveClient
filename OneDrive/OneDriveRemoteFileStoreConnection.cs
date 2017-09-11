using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Utils;

namespace LocalCloudStorage.OneDrive
{
    internal class OneDriveRemoteFileStoreConnection : IRemoteFileStoreConnection
    {
        private static string[] _scopes = new string[] { "files.readwrite" };
        //Below is the clientId of your app registration. 
        //You have to replace the below with the Application Id for your app registration
        private static string ClientId = "f9dc0bbd-fc1b-4cf4-ac6c-e2a41a05d583";//"0b8b0665-bc13-4fdc-bd72-e0227b9fc011";
        private static string _onedriveEndpoint = "https://graph.microsoft.com/v1.0/me/drive";


        private static int _4MB = 4 * 1024 * 1024;
        private static long _5MB = 320 * 1024 * 16;
        private static long _minFragLen = 320 * 1024;

        private long FragLength { get; }

        private HttpClientHelper _httpClient = new HttpClientHelper();
        private AuthenticationResult _authResult = null;

        private PublicClientApplication _clientApp;
        public PublicClientApplication PublicClientApp { get { return _clientApp; } }

        //private string _deltaUrl = "";

        ///private GraphServiceClient _graphClient;
        private TokenCacheHelper _tch;
        public OneDriveRemoteFileStoreConnection(TokenCacheHelper tch)
        {
            HttpClientHelper.Timeout = TimeSpan.FromSeconds(30);
            FragLength = _5MB;
            _tch = tch;
            _clientApp = new PublicClientApplication(ClientId, "https://login.microsoftonline.com/common", tch.GetUserCache());
            try
            {
                /*_graphClient = new GraphServiceClient(
                            "https://graph.microsoft.com/v1.0",
                            new DelegateAuthenticationProvider(
                                async (requestMessage) =>
                                {
                                    await PromptUserLogin();
                                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", _authResult.AccessToken);
                                }));*/
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Could not create a graph client: " + ex.Message);
            }
        }

        #region IRemoteFileStoreConnection
        public async Task<IDeltaList> GetDeltasAsync(string deltaLink, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            List<IRemoteItemUpdate> allDeltas = new List<IRemoteItemUpdate>();
            var nextPage = deltaLink;
            DeltaPage deltaPage = null;
            do
            {
                //get the delta page
                deltaPage = await GetDeltasPageAsync(nextPage, ct);

                //copy its contents to our list
                allDeltas.AddRange(deltaPage);

                //set the next page equal to this next page
                nextPage = deltaPage.NextPage;

            } while (nextPage != null);

            //TODO: I'd like to avoid this copying
            var ret = new DeltaPage(null, deltaPage.NextRequestData);
            ret.AddRange(allDeltas);
            return ret;
        }
        public async Task<DeltaPage> GetDeltasPageAsync(string deltaLink, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string downloadUrl = "";
            if (string.IsNullOrEmpty(deltaLink))
            {
                downloadUrl = $"{_onedriveEndpoint}/root/delta";
            }
            else
            {
                downloadUrl = deltaLink;
            }
            return await GetDeltasPageInternalAsync(downloadUrl, ct);
        }
        public async Task<IDeltaList> GetDeltasPageAsync(DeltaPage prevPage, CancellationToken ct)
        {
            if (prevPage == null)
                return await GetDeltasAsync("", ct);

            return await GetDeltasPageInternalAsync(prevPage.NextPage ?? prevPage.NextRequestData, ct);
        }
        private async Task<DeltaPage> GetDeltasPageInternalAsync(string downloadUrl, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string nextPage = null;
            string deltaLink = null;


            var httpResponse = await _httpClient.StartAuthenticatedRequest(downloadUrl, HttpMethod.Get).SendAsync(ct);
            string json = await HttpClientHelper.ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            //get the delta link and next page link
            deltaLink = (string)obj["@odata.deltaLink"];
            nextPage = (string)obj["@odata.nextLink"];

            DeltaPage ret = new DeltaPage(nextPage, deltaLink);

            //no values
            if (obj["value"] == null)
                return ret;

            //get the file handles
            foreach(JObject value in obj["value"])
            {
                ret.Add(new RemoteItemUpdate(value["deleted"] != null, new OneDriveRemoteFileHandle(this, value)));
            }

            return ret;
        }


        public async Task<HttpResult<string>> GetItemMetadataAsync(string remotePath, CancellationToken ct)
        {
            return await GetItemMetadataByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}", ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> GetItemHandleAsync(string remotePath, CancellationToken ct)
        {
            string url;
            if (remotePath == "" || remotePath == "/")
            {
                url = $"{_onedriveEndpoint}/root";
            }
            else
            {
                url = $"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}";
            }
            return await GetItemHandleByUrlAsync(url, ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> UploadFileAsync(string remotePath, Stream data, CancellationToken ct)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}", data, ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> CreateFolderAsync(string remotePath, CancellationToken ct)
        {
            //var httpEncodedPath = HttpUtility.UrlEncode(remotePath);
            //if (httpEncodedPath == null)
            //    return null;

            var pathParts = remotePath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string folderName = pathParts.Last();

            //first, get the id of the parent folder
            string parentUrl = "";
            if(pathParts.Length == 1)//creating folder in root
            {
                parentUrl = $"{_onedriveEndpoint}/root";
            }
            else
            {
                parentUrl = $"{_onedriveEndpoint}/root:/";
                for(int i = 0; i < pathParts.Length - 1; ++i)
                {
                    parentUrl = $"{parentUrl}/{HttpUtility.UrlEncode(pathParts[i])}";
                }
            }

            var httpResponse = await _httpClient.StartAuthenticatedRequest(parentUrl, HttpMethod.Get).SendAsync(ct);
            string json = await HttpClientHelper.ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            string parentId = (string)obj["id"];
            if (parentId == null)
                return null;

            return await CreateFolderByIdAsync(parentId, folderName, ct);
        }
        public async Task<HttpResult<bool>> DeleteItemAsync(string remotePath, CancellationToken ct)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}", ct);
        }
        private async Task<HttpResult<IRemoteItemHandle>> UpdateItemAsync(string remotePath, string json, CancellationToken ct)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}", json, ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> RenameItemAsync(string remotePath, string newName, CancellationToken ct)
        {
            return await UpdateItemAsync(remotePath, $"{{ \"name\": \"{newName}\" }}", ct);
        }


        public async Task<HttpResult<string>> GetItemMetadataByIdAsync(string id, CancellationToken ct)
        {
            return await GetItemMetadataByUrlAsync($"{_onedriveEndpoint}/items/{id}", ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> GetItemHandleByIdAsync(string id, CancellationToken ct)
        {
            return await GetItemHandleByUrlAsync($"{_onedriveEndpoint}/items/{id}", ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> UploadFileByIdAsync(string parentId, string name, Stream data, CancellationToken ct)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/items/{parentId}:/{name}", data, ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> CreateFolderByIdAsync(string parentId, string name, CancellationToken ct)
        {
            string requestUrl = $"{_onedriveEndpoint}/items/{parentId}/children";
            string requestJson = $"{{\"name\": \"{name}\", \"folder\": {{}} }}";

            var httpResponse = await _httpClient.StartAuthenticatedRequest(requestUrl, HttpMethod.Post)
                .SetContent(requestJson)
                .SendAsync(ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                return new HttpResult<IRemoteItemHandle>(httpResponse, null);
            }

            string json = await HttpClientHelper.ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            return new HttpResult<IRemoteItemHandle>(httpResponse, new OneDriveRemoteFileHandle(this, obj));
        }
        public async Task<HttpResult<bool>> DeleteItemByIdAsync(string id, CancellationToken ct)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/items/{id}", ct);
        }
        private async Task<HttpResult<IRemoteItemHandle>> UpdateItemByIdAsync(string id, string json, CancellationToken ct)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/items/{id}", json, ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> RenameItemByIdAsync(string id, string newName, CancellationToken ct)
        {
            return await UpdateItemByIdAsync(id, $"{{ \"name\": \"{newName}\" }}", ct);
        }
        public async Task<HttpResult<IRemoteItemHandle>> MoveItemByIdAsync(string id, string newParentId, CancellationToken ct)
        {
            return await UpdateItemByIdAsync(id, $"{{ \"parentReference\": {{ \"id\": \"{newParentId}\" }} }}", ct);
        }


        #region Helper Methods
        private async Task<HttpResult<string>> GetItemMetadataByUrlAsync(string url, CancellationToken ct)
        {
            var httpResponse = await _httpClient.StartAuthenticatedRequest(url, HttpMethod.Get).SendAsync(ct);
            return !httpResponse.IsSuccessStatusCode ? 
                new HttpResult<string>(httpResponse, null) : 
                new HttpResult<string>(httpResponse, await HttpClientHelper.ReadResponseAsStringAsync(httpResponse));
        }
        private async Task<HttpResult<IRemoteItemHandle>> GetItemHandleByUrlAsync(string url, CancellationToken ct)
        {
            //get the download URL
            var result = await GetItemMetadataByUrlAsync(url, ct);
            if (result.Value == null)
                return new HttpResult<IRemoteItemHandle>(result.HttpMessage, null);

            var metadata = result.Value;
            var metadataObj = (JObject)JsonConvert.DeserializeObject(metadata);

            //now download the text
            return new HttpResult<IRemoteItemHandle>(result.HttpMessage, new OneDriveRemoteFileHandle(this, metadataObj));
        }
        private async Task<HttpResult<IRemoteItemHandle>> UploadFileByUrlAsync(string url, Stream data, CancellationToken ct)
        {
            if (data.Length > _4MB) //if data > 4MB, then use chunked upload
            {
                //the url's are different for the different methods
                url = $"{url}:/createUploadSession";

                //first, create an upload session
                var httpResponse = await _httpClient.StartAuthenticatedRequest(url, HttpMethod.Post).SendAsync(ct);
                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    return new HttpResult<IRemoteItemHandle>(httpResponse, null);
                }


                //get the upload URL
                var uploadSessionRequestObject = await HttpClientHelper.ReadResponseAsJObjectAsync(httpResponse);

                var uploadUrl = (string)uploadSessionRequestObject["uploadUrl"];
                if (uploadUrl == null)
                {
                    Debug.WriteLine("Successful OneDrive CreateSession request had invalid body!");
                    //TODO: what to do here?
                }

                //the length of the file total
                var length = data.Length;

                //setup the headers
                var headers = new List<KeyValuePair<string, string>>()
                {
                    new KeyValuePair<string, string>("Content-Length", ""),
                    new KeyValuePair<string, string>("Content-Range","")
                };

                JObject responseJObject;
                //the response that will be returned
                HttpResponseMessage response = null;

                //get the chunks
                var chunks = ParseLargeUploadChunks(uploadSessionRequestObject, FragLength, length);
                do
                {

                    //HttpResult<List<Tuple<long, long>>> chunksResult;
                    //get the chunks
                    /*do
                    {
                        chunksResult = await RetrieveLargeUploadChunksAsync(uploadUrl, _5MB, length, ct);
                        //TODO: should we delay on failure?
                    } while (chunksResult.Value == null);//keep trying to get thre results until we're successful

                    chunks = chunksResult.Value;*/

                    //upload each fragment
                    var chunkStream = new ChunkedReadStreamWrapper(data);
                    foreach (var fragment in chunks)
                    {
                        //setup the chunked stream with the next fragment
                        chunkStream.ChunkStart = fragment.Item1;

                        //the size is one more than the difference (because the range is inclusive)
                        chunkStream.ChunkSize = fragment.Item2 - fragment.Item1 + 1;
                        
                        //setup the headers for this request
                        headers[0] = new KeyValuePair<string, string>("Content-Length", chunkStream.ChunkSize.ToString());
                        headers[1] = new KeyValuePair<string, string>("Content-Range", $"bytes {fragment.Item1}-{fragment.Item2}/{length}");

                        //submit the request until it is successful
                        do
                        {
                            //this should not be authenticated
                            response = await _httpClient.StartRequest(uploadUrl, HttpMethod.Put)
                                .SetContent(chunkStream)
                                .SetContentHeaders(headers)
                                //we MUST read this response before attempting the next request, or it will stop responding
                                .SetCompletionOption(HttpCompletionOption.ResponseContentRead)
                                .SendAsync(ct, true);

                        } while (!response.IsSuccessStatusCode); // keep retrying until success
                    }

                    //parse the response to see if there are more chunks or the final metadata
                    responseJObject = await HttpClientHelper.ReadResponseAsJObjectAsync(response);

                    //try to get chunks from the response to see if we need to retry anything
                    chunks = ParseLargeUploadChunks(responseJObject, FragLength, length);
                }
                while (chunks.Count > 0);//keep going until no chunks left

                if (responseJObject == null)
                {
                    Debug.WriteLine("Large upload completed, but did not have a valid response");
                    //TODO:
                }
                return new HttpResult<IRemoteItemHandle>(response, new OneDriveRemoteFileHandle(this, responseJObject));
            }
            else //use regular upload
            {
                url = $"{url}:/content";

                var httpResponse = await _httpClient.StartAuthenticatedRequest(url, HttpMethod.Put)
                    .SetContent(data)
                    .SendAsync(ct);

                if (!httpResponse.IsSuccessStatusCode) 
                    return new HttpResult<IRemoteItemHandle>(httpResponse, null);

                var json = await HttpClientHelper.ReadResponseAsStringAsync(httpResponse);
                var metadataObj = (JObject) JsonConvert.DeserializeObject(json);
                return new HttpResult<IRemoteItemHandle>(httpResponse, new OneDriveRemoteFileHandle(this, metadataObj));
            }
        }
        private async Task<HttpResult<List<Tuple<long, long>>>> RetrieveLargeUploadChunksAsync(string uploadUrl, long chunkSize, long length, CancellationToken ct)
        { 
            //Get the status of the upload session
            var httpResponse = await _httpClient.StartRequest(uploadUrl, HttpMethod.Get)
                .SendAsync(ct);

            if (httpResponse.StatusCode != HttpStatusCode.OK)
            {
                return new HttpResult<List<Tuple<long, long>>> (httpResponse, null);
            }
            //parse the expected ranges
            var jObject = await HttpClientHelper.ReadResponseAsJObjectAsync(httpResponse);
            return new HttpResult<List<Tuple<long, long>>>(httpResponse, ParseLargeUploadChunks(jObject, chunkSize, length));
        }
        private List<Tuple<long, long>> ParseLargeUploadChunks(JObject jObject, long chunkSize, long length)
        {
            //use null the GetValue method instead of [] becauase [] doesn't support null propogation
            var nextExpectedRanges = jObject?.GetValue("nextExpectedRanges")?.Values<string>();

            //return a blank list if this request is irrelevent
            if(nextExpectedRanges == null)
                return new List<Tuple<long, long>>();

            var expectedRanges = new List<Tuple<long, long>>();
            foreach (var range in nextExpectedRanges)
            {
                var parts = range.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                long start;
                long end;

                if (!long.TryParse(parts[0], out start))
                {
                    Debug.WriteLine("Expected Ranges from OneDrive were corrupt!");
                    //TODO: what to do here?
                }

                if (parts.Length == 1)
                {
                    //this means "until the end"
                    end = length;
                }
                else
                {
                    if (!long.TryParse(parts[1], out end))
                    {
                        Debug.WriteLine("Expected Ranges from OneDrive were corrupt!");
                        //TODO: what to do here?
                    }
                }

                expectedRanges.Add(new Tuple<long, long>(start, end));
            }

            //split the expected ranges into chunks
            var chunks = new List<Tuple<long, long>>();
            foreach (var expectedRange in expectedRanges)
            {
                //TODO: what if the expected range IS a multiple of "chunkSize"?
                var i = expectedRange.Item1;
                while (i <= expectedRange.Item2)
                {
                    var min = i;
                    var max = Math.Min(min + chunkSize - 1, length - 1);
                    chunks.Add(new Tuple<long, long>(i, max));

                    i += chunkSize;
                }
            }

            return chunks;
        }
        private async Task<HttpResult<bool>> DeleteFileByUrlAsync(string url, CancellationToken ct)
        {
            var httpResponse = await _httpClient.StartAuthenticatedRequest(url, HttpMethod.Delete).SendAsync(ct);
            return new HttpResult<bool>(httpResponse,
                httpResponse.StatusCode == HttpStatusCode.NotFound ||
                httpResponse.StatusCode == HttpStatusCode.NoContent);
        }
        private HttpMethod _patch = new HttpMethod("PATCH");
        private async Task<HttpResult<IRemoteItemHandle>> UpdateItemByUrlAsync(string url, string json, CancellationToken ct)
        {
            var httpResponse = await _httpClient.StartAuthenticatedRequest(url, _patch)
                .SetContent(json)
                .SendAsync(ct);

            if (!httpResponse.IsSuccessStatusCode) 
                return new HttpResult<IRemoteItemHandle>(httpResponse, null);

            var responseJson = await HttpClientHelper.ReadResponseAsStringAsync(httpResponse);
            var metadataObj = (JObject)JsonConvert.DeserializeObject(responseJson);
            return new HttpResult<IRemoteItemHandle>(httpResponse, new OneDriveRemoteFileHandle(this, metadataObj));
        }
        #endregion

        /// <summary>
        /// Gets the file store connection authenticated
        /// </summary>
        /// <returns></returns>
        public async Task LogUserInAsync()
        {
            string resultText = string.Empty;
            AuthenticationResult newAuthenticationResult = null;
            try
            {
                newAuthenticationResult = await PublicClientApp.AcquireTokenSilentAsync(_scopes, PublicClientApp.Users.FirstOrDefault());
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token
                //System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    newAuthenticationResult = await PublicClientApp.AcquireTokenAsync(_scopes);
                }
                catch (MsalException msalex)
                {
                    resultText = $"Error Acquiring Token:{System.Environment.NewLine}{msalex}";
                }
            }
            catch (Exception ex)
            {
                resultText = $"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}";
                return;
            }
            
            if (newAuthenticationResult != null)
            {
                if (newAuthenticationResult.AccessToken != _authResult?.AccessToken)//only do this if the access token changes
                {
                    _authResult = newAuthenticationResult;

                    _httpClient.AuthenticationHeader = new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);
                }
            }
        }
        public void LogUserOut()
        {
            if (PublicClientApp.Users.Any())
            {
                PublicClientApp.Remove(PublicClientApp.Users.FirstOrDefault());
            }
        }

        #endregion


        private async Task<HttpResult<Stream>> DownloadFileWithLinkAsync(string downloadUrl, CancellationToken ct)
        {
            var result = await _httpClient.StartAuthenticatedRequest(downloadUrl, HttpMethod.Get).SendAsync(ct);
            if (!result.IsSuccessStatusCode)
            {
                return new HttpResult<Stream>(result, null);
            }
            var readAsStreamAsync = result.Content?.ReadAsStreamAsync();
            return readAsStreamAsync != null ? new HttpResult<Stream>(result, await readAsStreamAsync) : new HttpResult<Stream>(result, null);
        }

        public class OneDriveRemoteFileHandle : IRemoteItemHandle
        {
            private string _downloadUrl;
            private JObject _metadata;
            private OneDriveRemoteFileStoreConnection _fileStore;


            private bool _dtInitialized = false;
            private DateTime _lastModified;
            private string _sha1Hash = null;
            private string _path = null;
            private string _name = null;
            private string _parentId;
            private bool _isFolderInitialized = false;
            private bool _isFolder;
            private long _size = -1;

            public OneDriveRemoteFileHandle(OneDriveRemoteFileStoreConnection fileStore, JObject metadata)
            {
                Id = (string)metadata["id"];
                _downloadUrl = $"{_onedriveEndpoint}/items/{Id}/content";//only use id for content

                _fileStore = fileStore;
                _metadata = metadata;
            }

            #region IRemoteItemHandle
            public bool IsFolder
            {
                get
                {
                    if(!_isFolderInitialized)
                    {
                        _isFolder = _metadata["folder"] != null;
                        _isFolderInitialized = true;
                    }
                    return _isFolder;
                }
            }
            public string Id { get; }
            public string Name
            {
                get
                {
                    if(_name == null)
                    {
                        _name = (string)_metadata["name"];
                    }
                    return _name;
                }
            }
            public string ParentId
            {
                get
                {
                    if (_parentId == null)
                    {
                        var parentReference = _metadata["parentReference"];
                        _parentId = (string) parentReference["id"];
                    }
                    return _parentId;
                }

            }
            public string Path
            {
                get
                {
                    if(_path == null)
                    {
                        var parentReference = _metadata["parentReference"];
                        _path = HttpUtility.UrlDecode($"{(string) parentReference["path"]}/{Name}"
                            .Split(new char[] {':'}, 2, StringSplitOptions.RemoveEmptyEntries).Last());
                        if (_path == "/root")
                            _path = "/";//we need this to be uniform across all remotes
                    }
                    return _path;
                }
            }
            public long Size
            {
                get
                {
                    if (_size == -1)
                    {
                        if (long.TryParse((string) _metadata["size"], out long size))
                        {
                            _size = size;
                        }
                    }
                    return _size;
                }

            }
            public string Sha1
            {
                get
                {
                    if(_sha1Hash == null)
                    {
                        var file = _metadata["file"];
                        if (file == null)
                            _sha1Hash = "";
                        else
                        {
                            var hashes = file["hashes"];
                            if (hashes == null)
                                _sha1Hash = "";
                            else
                                _sha1Hash = (string)hashes["sha1Hash"];
                        }
                    }
                    return _sha1Hash;
                }
            }
            public async Task<string> GetSha1HashAsync()
            {
                return Sha1;
            }
            public async Task<string> GetSha1HashAsync(CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return _sha1Hash;
            }
            public DateTime LastModified
            {
                get
                {
                    if(!_dtInitialized)
                    {
                        var lastModifiedText = _metadata["lastModifiedDateTime"];
                        if (lastModifiedText == null)
                            _lastModified = new DateTime();
                        else
                        {
                            if(!DateTime.TryParse((string)lastModifiedText, out _lastModified))
                            {
                                _lastModified = new DateTime();//is this what we want?
                            }
                        }
                        _dtInitialized = true;
                    }
                    return _lastModified;
                }
            }
            public async Task<Stream> GetFileDataAsync(CancellationToken ct)
            {
                return (await TryGetFileDataAsync(ct))?.Value;
            }
            public async Task<HttpResult<Stream>> TryGetFileDataAsync(CancellationToken ct)
            {
                return await _fileStore.DownloadFileWithLinkAsync(_downloadUrl, ct);
            }
            #endregion
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
