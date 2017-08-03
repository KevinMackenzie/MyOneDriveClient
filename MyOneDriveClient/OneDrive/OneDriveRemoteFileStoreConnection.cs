using Microsoft.Graph;
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
using System.Threading.Tasks;
using System.Web;
using LocalCloudStorage;

namespace MyOneDriveClient.OneDrive
{
    public class OneDriveRemoteFileStoreConnection : IRemoteFileStoreConnection
    {
        private static string[] _scopes = new string[] { "files.readwrite" };
        //Below is the clientId of your app registration. 
        //You have to replace the below with the Application Id for your app registration
        private static string ClientId = "f9dc0bbd-fc1b-4cf4-ac6c-e2a41a05d583";//"0b8b0665-bc13-4fdc-bd72-e0227b9fc011";
        private static string _onedriveEndpoint = "https://graph.microsoft.com/v1.0/me/drive";

        private HttpClient _httpClient;
        private AuthenticationResult _authResult = null;

        private PublicClientApplication _clientApp;
        public PublicClientApplication PublicClientApp { get { return _clientApp; } }

        //private string _deltaUrl = "";

        ///private GraphServiceClient _graphClient;
        
        public OneDriveRemoteFileStoreConnection()
        {
            _clientApp = new PublicClientApplication(ClientId, "https://login.microsoftonline.com/common", TokenCacheHelper.GetUserCache());
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
        public async Task<DeltaPage> GetDeltasAsync(string deltaLink)
        {
            List<IRemoteItemUpdate> allDeltas = new List<IRemoteItemUpdate>();
            var nextPage = deltaLink;
            DeltaPage deltaPage = null;
            do
            {
                //get the delta page
                deltaPage = await GetDeltasPageAsync(nextPage);

                //copy its contents to our list
                allDeltas.AddRange(deltaPage);

                //set the next page equal to this next page
                nextPage = deltaPage.NextPage;

            } while (nextPage != null);

            //TODO: I'd like to avoid this copying
            var ret = new DeltaPage(null, deltaPage.DeltaLink);
            ret.AddRange(allDeltas);
            return ret;
        }
        public async Task<DeltaPage> GetDeltasPageAsync(string deltaLink)
        {
            string downloadUrl = "";
            if (string.IsNullOrEmpty(deltaLink))
            {
                downloadUrl = $"{_onedriveEndpoint}/root/delta";
            }
            else
            {
                downloadUrl = deltaLink;
            }
            return await GetDeltasPageInternalAsync(downloadUrl);
        }
        public async Task<DeltaPage> GetDeltasPageAsync(DeltaPage prevPage)
        {
            if (prevPage == null)
                return await GetDeltasAsync("");

            return await GetDeltasPageInternalAsync(prevPage.NextPage ?? prevPage.DeltaLink);
        }
        private async Task<DeltaPage> GetDeltasPageInternalAsync(string downloadUrl)
        {
            string nextPage = null;
            string deltaLink = null;


            var httpResponse = await AuthenticatedHttpRequestAsync(downloadUrl, HttpMethod.Get);
            string json = await ReadResponseAsStringAsync(httpResponse);
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


        public async Task<HttpResult<string>> GetItemMetadataAsync(string remotePath)
        {
            return await GetItemMetadataByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}");
        }
        public async Task<HttpResult<IRemoteItemHandle>> GetItemHandleAsync(string remotePath)
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
            return await GetItemHandleByUrlAsync(url);
        }
        private static int _4MB = 4 * 1024 * 1024;
        public async Task<HttpResult<IRemoteItemHandle>> UploadFileAsync(string remotePath, Stream data)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}:/content", data);
        }
        public async Task<HttpResult<IRemoteItemHandle>> CreateFolderAsync(string remotePath)
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

            var httpResponse = await AuthenticatedHttpRequestAsync(parentUrl, HttpMethod.Get);
            string json = await ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            string parentId = (string)obj["id"];
            if (parentId == null)
                return null;

            return await CreateFolderByIdAsync(parentId, folderName);
        }
        public async Task<HttpResult<bool>> DeleteItemAsync(string remotePath)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}");
        }
        private async Task<HttpResult<IRemoteItemHandle>> UpdateItemAsync(string remotePath, string json)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}", json);
        }
        public async Task<HttpResult<IRemoteItemHandle>> RenameItemAsync(string remotePath, string newName)
        {
            return await UpdateItemAsync(remotePath, $"{{ \"name\": \"{newName}\" }}");
        }


        public async Task<HttpResult<string>> GetItemMetadataByIdAsync(string id)
        {
            return await GetItemMetadataByUrlAsync($"{_onedriveEndpoint}/items/{id}");
        }
        public async Task<HttpResult<IRemoteItemHandle>> GetItemHandleByIdAsync(string id)
        {
            return await GetItemHandleByUrlAsync($"{_onedriveEndpoint}/items/{id}");
        }
        public async Task<HttpResult<IRemoteItemHandle>> UploadFileByIdAsync(string parentId, string name, Stream data)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/items/{parentId}:/{name}:/content", data);
        }
        public async Task<HttpResult<IRemoteItemHandle>> CreateFolderByIdAsync(string parentId, string name)
        {
            string requestUrl = $"{_onedriveEndpoint}/items/{parentId}/children";
            string requestJson = $"{{\"name\": \"{name}\", \"folder\": {{}} }}";

            var httpResponse = await AuthenticatedHttpRequestAsync(requestUrl, HttpMethod.Post, requestJson);
            if (!httpResponse.IsSuccessStatusCode)
            {
                return new HttpResult<IRemoteItemHandle>(httpResponse, null);
            }

            string json = await ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            return new HttpResult<IRemoteItemHandle>(httpResponse, new OneDriveRemoteFileHandle(this, obj));
        }
        public async Task<HttpResult<bool>> DeleteItemByIdAsync(string id)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/items/{id}");
        }
        private async Task<HttpResult<IRemoteItemHandle>> UpdateItemByIdAsync(string id, string json)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/items/{id}", json);
        }
        public async Task<HttpResult<IRemoteItemHandle>> RenameItemByIdAsync(string id, string newName)
        {
            return await UpdateItemByIdAsync(id, $"{{ \"name\": \"{newName}\" }}");
        }
        public async Task<HttpResult<IRemoteItemHandle>> MoveItemByIdAsync(string id, string newParentId)
        {
            return await UpdateItemByIdAsync(id, $"{{ \"parentReference\": {{ \"id\": \"{newParentId}\" }} }}");
        }


        #region Helper Methods
        private async Task<HttpResult<string>> GetItemMetadataByUrlAsync(string url)
        {
            var httpResponse = await AuthenticatedHttpRequestAsync(url, HttpMethod.Get);
            return !httpResponse.IsSuccessStatusCode ? 
                new HttpResult<string>(httpResponse, null) : 
                new HttpResult<string>(httpResponse, await ReadResponseAsStringAsync(httpResponse));
        }
        private async Task<HttpResult<IRemoteItemHandle>> GetItemHandleByUrlAsync(string url)
        {
            //get the download URL
            var result = await GetItemMetadataByUrlAsync(url);
            if (result.Value == null)
                return new HttpResult<IRemoteItemHandle>(result.HttpMessage, null);

            var metadata = result.Value;
            var metadataObj = (JObject)JsonConvert.DeserializeObject(metadata);

            //now download the text
            return new HttpResult<IRemoteItemHandle>(result.HttpMessage, new OneDriveRemoteFileHandle(this, metadataObj));
        }
        private async Task<HttpResult<IRemoteItemHandle>> UploadFileByUrlAsync(string url, Stream data)
        {
            if (data.Length > _4MB) //if data > 4MB, then use chunked upload
            {
                throw new NotImplementedException();
            }
            else //use regular upload
            {
                var httpResponse = await AuthenticatedHttpRequestAsync(url, HttpMethod.Put, data);
                if (!httpResponse.IsSuccessStatusCode) 
                    return new HttpResult<IRemoteItemHandle>(httpResponse, null);

                var json = await ReadResponseAsStringAsync(httpResponse);
                var metadataObj = (JObject) JsonConvert.DeserializeObject(json);
                return new HttpResult<IRemoteItemHandle>(httpResponse, new OneDriveRemoteFileHandle(this, metadataObj));
            }
        }
        private async Task<HttpResult<bool>> DeleteFileByUrlAsync(string url)
        {
            var httpResponse = await AuthenticatedHttpRequestAsync(url, HttpMethod.Delete);
            return new HttpResult<bool>(httpResponse,
                httpResponse.StatusCode == HttpStatusCode.NotFound ||
                httpResponse.StatusCode == HttpStatusCode.NoContent);
        }
        private HttpMethod _patch = new HttpMethod("PATCH");
        private async Task<HttpResult<IRemoteItemHandle>> UpdateItemByUrlAsync(string url, string json)
        {
            var httpResponse = await AuthenticatedHttpRequestAsync(url, _patch, json);
            if (!httpResponse.IsSuccessStatusCode) 
                return new HttpResult<IRemoteItemHandle>(httpResponse, null);

            var responseJson = await ReadResponseAsStringAsync(httpResponse);
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
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

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

                    _httpClient = new HttpClient();
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);
                    _httpClient.Timeout = new TimeSpan(0, 0, 0, 30);
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


        private async Task<HttpResult<Stream>> DownloadFileWithLinkAsync(string downloadUrl)
        {
            var result = await AuthenticatedHttpRequestAsync(downloadUrl, HttpMethod.Get);
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
            public async Task<Stream> GetFileDataAsync()
            {
                return (await TryGetFileDataAsync())?.Value;
            }
            public async Task<HttpResult<Stream>> TryGetFileDataAsync()
            {
                return await _fileStore.DownloadFileWithLinkAsync(_downloadUrl);
            }
            #endregion
        }

        /// <summary>
        /// A test method for making HTTP requests
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, HttpMethod verb, Stream body, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            if (body != null)
            {
                return await AuthenticatedHttpRequestAsync(url, verb, new StreamContent(body), headers);
            }
            else
            {
                return await AuthenticatedHttpRequestAsync(url, verb, (HttpContent)null, headers);
            }
        }
        public async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, HttpMethod verb, string body, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            if (body != null)
            {
                return await AuthenticatedHttpRequestAsync(url, verb, new StringContent(body, Encoding.UTF8, "application/json"), headers);
            }
            else
            {
                return await AuthenticatedHttpRequestAsync(url, verb, (HttpContent)null, headers);
            }
        }
        public async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, HttpMethod verb, byte[] body, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            if (body != null)
            {
                return await AuthenticatedHttpRequestAsync(url, verb, new MemoryStream(body), headers);
            }
            else
            {
                return await AuthenticatedHttpRequestAsync(url, verb, (HttpContent)null, headers);
            }
        }
        private async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, HttpMethod verb, HttpContent content = null, IEnumerable<KeyValuePair<string, string>> headers = null)
        {
            if(_httpClient == null)
                throw new Exception("Attempt to call http request before authentication!");
            var request = new HttpRequestMessage(verb, url);
            if (content != null)
            {
                request.Content = content;
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }
            var response = await _httpClient.SendAsync(request, 
                HttpCompletionOption.ResponseHeadersRead); //ONLY Read the headers, because reading the content will make this function last until the contents are downloaded (see issue #28)
            return response;
        }
        public async Task<string> ReadResponseAsStringAsync(HttpResponseMessage message)
        {
            var stream = new StreamReader(await message.Content.ReadAsStreamAsync());
            return await stream.ReadToEndAsync();
        }

        public async Task<bool> HasNetworkConnection()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://clients3.google.com/generate_204");
            var response = await _httpClient.SendAsync(request);
            return response.StatusCode == HttpStatusCode.NoContent;
        }
    }
}
