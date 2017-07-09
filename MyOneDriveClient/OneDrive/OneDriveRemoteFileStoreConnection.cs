using Microsoft.Graph;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            if (deltaLink == "" || deltaLink == null)
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


            var httpResponse = await AuthenticatedHttpRequestAsync(downloadUrl, _authResult.AccessToken, HttpMethod.Get);
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

                ret.Add(new RemoteItemUpdate(value["deleted"] != null, new OneDriveRemoteFileHandle(this, value["@microsoft.graph.downloadUrl"]?.ToString() ?? null, value["folder"] != null, (string)value["id"], value)));
            }

            //call the event handler (TODO: do we want to await this?)
            //await OnUpdate.Invoke(this, new Events.RemoteFileStoreDataChanged(_deltaUrl));

            return ret;
        }


        public async Task<string> GetItemMetadataAsync(string remotePath)
        {
            //TODO: return null if the item cannot be found.  This code is kinda bad.
            return await GetItemMetadataByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}");
        }
        public async Task<IRemoteItemHandle> GetItemHandleAsync(string remotePath)
        {
            return await GetItemHandleByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}");
        }
        private static int _4MB = 4 * 1024 * 1024;
        public async Task<string> UploadFileAsync(string remotePath, Stream data)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}:/content", data);
        }
        public async Task<string> CreateFolderAsync(string remotePath)
        {
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
                    parentUrl = $"{parentUrl}/{pathParts[i]}";
                }
            }

            var httpResponse = await AuthenticatedHttpRequestAsync(parentUrl, _authResult.AccessToken, HttpMethod.Get);
            string json = await ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            string parentId = (string)obj["id"];
            if (parentId == null)
                return null;

            return await CreateFolderByIdAsync(parentId, folderName);
        }
        public async Task<bool> DeleteItemAsync(string remotePath)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}");
        }
        public async Task<bool> UpdateItemAsync(string remotePath, string json)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/root:{remotePath}", json);
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
        public async Task<string> UploadFileByIdAsync(string parentId, string name, Stream data)
        {
            return await UploadFileByUrlAsync($"{_onedriveEndpoint}/items/{parentId}:/{name}:/content", data);
        }
        public async Task<string> CreateFolderByIdAsync(string parentId, string name)
        {
            string requestUrl = $"{_onedriveEndpoint}/items/{parentId}/children";
            string requestJson = $"{{\"name\": \"{name}\", \"folder\": {{}} }}";

            var httpResponse = await AuthenticatedHttpRequestAsync(requestUrl, _authResult.AccessToken, HttpMethod.Post, requestJson);
            string json = await ReadResponseAsStringAsync(httpResponse);
            var obj = (JObject)JsonConvert.DeserializeObject(json);

            return (string)obj["id"];
        }
        public async Task<bool> DeleteItemByIdAsync(string id)
        {
            return await DeleteFileByUrlAsync($"{_onedriveEndpoint}/items/{id}");
        }
        public async Task<bool> UpdateItemByIdAsync(string id, string json)
        {
            return await UpdateItemByUrlAsync($"{_onedriveEndpoint}/items/{id}", json);
        }


        #region Helper Methods
        private async Task<string> GetItemMetadataByUrlAsync(string url)
        {
            //TODO: return null if the item cannot be found.  This code is kinda bad.
            return await ReadResponseAsStringAsync(await AuthenticatedHttpRequestAsync(url, _authResult.AccessToken, System.Net.Http.HttpMethod.Get));
        }
        private async Task<IRemoteItemHandle> GetItemHandleByUrlAsync(string url)
        {
            string downloadUrl = "";
            string metadata = "";
            JObject metadataObj = null;
            bool isFolder = false;
            string id = "";

            //get the download URL
            try
            {
                metadata = await GetItemMetadataByUrlAsync(url);
                metadataObj = (JObject)JsonConvert.DeserializeObject(metadata);
                downloadUrl = metadataObj["@microsoft.graph.downloadUrl"].Value<string>();
                isFolder = metadataObj["folder"] != null;
                id = (string)metadataObj["id"];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to retrieve file handle: {ex.Message}");
            }

            //now download the text
            return new OneDriveRemoteFileHandle(this, downloadUrl, isFolder, id, metadataObj);
        }
        private async Task<string> UploadFileByUrlAsync(string url, Stream data)
        {
            if (data.Length > _4MB) //if data > 4MB, then use chunked upload
            {
                return null;
            }
            else //use regular upload
            {
                var httpResponse = await AuthenticatedHttpRequestAsync(url, _authResult.AccessToken, HttpMethod.Put, data);
                string json = await ReadResponseAsStringAsync(httpResponse);
                var obj = (JObject)JsonConvert.DeserializeObject(json);
                return (string)obj["id"];
            }
        }
        private async Task<bool> DeleteFileByUrlAsync(string url)
        {
            return (await AuthenticatedHttpRequestAsync(url, _authResult.AccessToken, HttpMethod.Delete)).StatusCode == System.Net.HttpStatusCode.NoContent;
        }
        private HttpMethod _patch = new HttpMethod("PATCH");
        private async Task<bool> UpdateItemByUrlAsync(string url, string json)
        {
            return (await AuthenticatedHttpRequestAsync(url, _authResult.AccessToken, _patch, json)).StatusCode == System.Net.HttpStatusCode.OK;
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
            return await (await AuthenticatedHttpRequestAsync(downloadUrl, _authResult.AccessToken, System.Net.Http.HttpMethod.Get))?.Content.ReadAsStreamAsync();
        }

        public class OneDriveRemoteFileHandle : IRemoteItemHandle
        {
            private string _downloadUrl;
            private JObject _metadata;
            private OneDriveRemoteFileStoreConnection _fileStore;

            public JObject Metadata { get => _metadata; }

            public OneDriveRemoteFileHandle(OneDriveRemoteFileStoreConnection fileStore, string downloadUrl, bool isFolder, string id, JObject metadata)
            {
                _fileStore = fileStore;
                _downloadUrl = downloadUrl;
                _metadata = metadata;
                IsFolder = isFolder;
                Id = id;
            }

            public bool IsFolder { get; }

            public string Id { get; }

            private string _name = null;
            public string Name
            {
                get
                {
                    if(_name == null)
                    {
                        _name = (string)_metadata["name"];
                    }
                    return Name;
                }
            }

            private string _path = null;
            public string Path
            {
                get
                {
                    if(_path == null)
                    {
                        var parentReference = _metadata["parentReference"];
                        _path = $"{(string)parentReference["path"]}/{Name}";
                    }
                    return _path;
                }
            }

            private string _sha1Hash = null;
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

            public async Task<Stream> GetFileDataAsync()
            {
                return await _fileStore.DownloadFileWithLinkAsync(_downloadUrl);
            }
        }

        /// <summary>
        /// A test method for making HTTP requests
        /// </summary>
        /// <param name="url"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, string token, System.Net.Http.HttpMethod verb, Stream body)
        {
            if (body != null)
            {
                return await AuthenticatedHttpRequestAsync(url, token, verb, new StreamContent(body));
            }
            else
            {
                return await AuthenticatedHttpRequestAsync(url, token, verb, (HttpContent)null);
            }
        }
        public async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, string token, System.Net.Http.HttpMethod verb, string body)
        {
            if (body != null)
            {
                return await AuthenticatedHttpRequestAsync(url, token, verb, new StringContent(body));
            }
            else
            {
                return await AuthenticatedHttpRequestAsync(url, token, verb, (HttpContent)null);
            }
        }
        public async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, string token, System.Net.Http.HttpMethod verb, byte[] body)
        {
            if (body != null)
            {
                return await AuthenticatedHttpRequestAsync(url, token, verb, new MemoryStream(body));
            }
            else
            {
                return await AuthenticatedHttpRequestAsync(url, token, verb, (HttpContent)null);
            }
        }
        private async Task<HttpResponseMessage> AuthenticatedHttpRequestAsync(string url, string token, HttpMethod verb, HttpContent content = null)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            var request = new System.Net.Http.HttpRequestMessage(verb, url);
            request.Content = content;

            //Add the token in Authorization header
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            response = await httpClient.SendAsync(request);
            return response;
        }
        public async Task<string> ReadResponseAsStringAsync(HttpResponseMessage message)
        {
            var stream = new StreamReader(await message.Content.ReadAsStreamAsync());
            return await stream.ReadToEndAsync();
        }
    }
}
