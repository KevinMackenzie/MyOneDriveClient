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

namespace MyOneDriveClient.OneDrive
{
    public class OneDriveRemoteFileStoreConnection : IRemoteFileStoreConnection
    {
        private static string[] _scopes = new string[] { "files.readwrite" };
        //Below is the clientId of your app registration. 
        //You have to replace the below with the Application Id for your app registration
        private static string ClientId = "f9dc0bbd-fc1b-4cf4-ac6c-e2a41a05d583";//"0b8b0665-bc13-4fdc-bd72-e0227b9fc011";
        private static string _onedriveEndpoint = "https://graph.microsoft.com/v1.0/me/drive";

        private HttpClient _httpClient = new HttpClient();
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
        //public void Initialize(string data)
        //{
            //TODO: some checking that this is ACTUALLY a good link
        //    _deltaUrl = data;
        //}
        //public event Events.EventDelegates.RemoteFileStoreConnectionUpdateHandler OnUpdate;
        
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
                return await GetDeltasPageAsync("");

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
                //TODO: folders are important deltas, because when folders are renamed, their descendents DON"T get the delta update
                //if (value["folder"] != null)
                //    continue;

                ret.Add(new RemoteItemUpdate(value["deleted"] != null, new OneDriveRemoteFileHandle(this, value)));
            }

            //call the event handler (TODO: do we want to await this?)
            //await OnUpdate.Invoke(this, new Events.RemoteFileStoreDataChanged(_deltaUrl));

            return ret;
        }


        public async Task<string> GetItemMetadataAsync(string remotePath)
        {
            //TODO: return null if the item cannot be found.  This code is kinda bad.
            return await GetItemMetadataByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}");
        }
        public async Task<IRemoteItemHandle> GetItemHandleAsync(string remotePath)
        {
            return await GetItemHandleByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}");
        }
        private static int _4MB = 4 * 1024 * 1024;
        public async Task<IRemoteItemHandle> UploadFileAsync(string remotePath, Stream data)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}:/content", data);
        }
        public async Task<IRemoteItemHandle> CreateFolderAsync(string remotePath)
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
        public async Task<bool> DeleteItemAsync(string remotePath)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}");
        }
        public async Task<IRemoteItemHandle> UpdateItemAsync(string remotePath, string json)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/root:{HttpUtility.UrlEncode(remotePath)}", json);
        }


        public async Task<string> GetItemMetadataByIdAsync(string id)
        {
            //TODO: return null if the item cannot be found.  This code is kinda bad.
            return await GetItemMetadataByUrlAsync($"{_onedriveEndpoint}/items/{id}");
        }
        public async Task<IRemoteItemHandle> GetItemHandleByIdAsync(string id)
        {
            return await GetItemHandleByUrlAsync($"{_onedriveEndpoint}/items/{id}");
        }
        public async Task<IRemoteItemHandle> UploadFileByIdAsync(string parentId, string name, Stream data)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/items/{parentId}:/{name}:/content", data);
        }
        public async Task<IRemoteItemHandle> CreateFolderByIdAsync(string parentId, string name)
        {
            string requestUrl = $"{_onedriveEndpoint}/items/{parentId}/children";
            string requestJson = $"{{\"name\": \"{name}\", \"folder\": {{}} }}";

            var httpResponse = await AuthenticatedHttpRequestAsync(requestUrl, HttpMethod.Post, requestJson);
            string json = await ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            return new OneDriveRemoteFileHandle(this, obj);
        }
        public async Task<bool> DeleteItemByIdAsync(string id)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/items/{id}");
        }
        public async Task<IRemoteItemHandle> UpdateItemByIdAsync(string id, string json)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/items/{id}", json);
        }


        #region Helper Methods
        private async Task<string> GetItemMetadataByUrlAsync(string url)
        {
            //TODO: return null if the item cannot be found.  This code is kinda bad.
            return await ReadResponseAsStringAsync(await AuthenticatedHttpRequestAsync(url, HttpMethod.Get));
        }
        private async Task<IRemoteItemHandle> GetItemHandleByUrlAsync(string url)
        {
            JObject metadataObj = null;

            //get the download URL
            try
            {
                string metadata = await GetItemMetadataByUrlAsync(url);
                metadataObj = (JObject)JsonConvert.DeserializeObject(metadata);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to retrieve file handle: {ex.Message}");
                return null;
            }

            //now download the text
            return new OneDriveRemoteFileHandle(this, metadataObj);
        }
        private async Task<IRemoteItemHandle> UploadFileByUrlAsync(string url, Stream data)
        {
            OneDriveRemoteFileHandle ret = null;

            if (data.Length > _4MB) //if data > 4MB, then use chunked upload
            {
                return null;
            }
            else //use regular upload
            {
                var httpResponse = await AuthenticatedHttpRequestAsync(url, HttpMethod.Put, data);
                string json = await ReadResponseAsStringAsync(httpResponse);
                var metadataObj = (JObject)JsonConvert.DeserializeObject(json);
                ret = new OneDriveRemoteFileHandle(this, metadataObj);
            }
            return ret;
        }
        private async Task<bool> DeleteFileByUrlAsync(string url)
        {
            var result = await AuthenticatedHttpRequestAsync(url, HttpMethod.Delete);
            return result.StatusCode == HttpStatusCode.NotFound || result.StatusCode == HttpStatusCode.NoContent;
        }
        private HttpMethod _patch = new HttpMethod("PATCH");
        private async Task<IRemoteItemHandle> UpdateItemByUrlAsync(string url, string json)
        {
            var result = await AuthenticatedHttpRequestAsync(url, _patch, json);
            if (!result.IsSuccessStatusCode) return null;
            string responseJson = await ReadResponseAsStringAsync(result);
            var metadataObj = (JObject)JsonConvert.DeserializeObject(responseJson);
            return new OneDriveRemoteFileHandle(this, metadataObj);
        }
        #endregion

        /// <summary>
        /// Gets the file store connection authenticated
        /// </summary>
        /// <returns></returns>
        public async Task PromptUserLoginAsync()
        {
            string resultText = string.Empty;

            try
            {
                _authResult = await PublicClientApp.AcquireTokenSilentAsync(_scopes, PublicClientApp.Users.FirstOrDefault());
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    _authResult = await PublicClientApp.AcquireTokenAsync(_scopes);
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
            
            if (_authResult != null)
            {
                //resultText = await GetHttpContentWithToken(_onedriveEndpoint, _authResult.AccessToken);
                //DisplayBasicTokenInfo(authResult);
                //this.SignOutButton.Visibility = Visibility.Visible;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authResult.AccessToken);
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


        private async Task<Stream> DownloadFileWithLinkAsync(string downloadUrl)
        {
            return await (await AuthenticatedHttpRequestAsync(downloadUrl, HttpMethod.Get))?.Content?.ReadAsStreamAsync();
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
            private bool _isFolderInitialized = false;
            private bool _isFolder;

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
            public string SHA1Hash
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
            var response = await _httpClient.SendAsync(request);
            return response;
        }
        public async Task<string> ReadResponseAsStringAsync(HttpResponseMessage message)
        {
            var stream = new StreamReader(await message.Content.ReadAsStreamAsync());
            return await stream.ReadToEndAsync();
        }
    }
}
