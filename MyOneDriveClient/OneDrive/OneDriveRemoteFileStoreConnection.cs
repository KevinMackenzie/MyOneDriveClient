﻿using Microsoft.Graph;
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

        private string _deltaUrl = "";

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
        public void Initialize(string data)
        {
            //TODO: some checking that this is ACTUALLY a good link
            _deltaUrl = data;
        }
        public event Events.EventDelegates.RemoteFileStoreConnectionUpdateHandler OnUpdate;

        [Obsolete]
        public async Task<IEnumerable<IRemoteItemHandle>> EnumerateFilesAsync()
        {
            //this should be OK
            return (from update in (await EnumerateUpdatesInternalAsync($"{_onedriveEndpoint}/root/delta")) select update.ItemHandle);
        }

        public async Task<IEnumerable<IRemoteItemUpdate>> EnumerateUpdatesAsync()
        {
            string downloadUrl = "";
            if (_deltaUrl == "")
            {
                downloadUrl = $"{_onedriveEndpoint}/root/delta";
            }
            else
            {
                downloadUrl = _deltaUrl;
            }
            return await EnumerateUpdatesInternalAsync(downloadUrl);
        }
        private async Task<IEnumerable<IRemoteItemUpdate>> EnumerateUpdatesInternalAsync(string downloadUrl)
        {

            List<IRemoteItemUpdate> ret = new List<IRemoteItemUpdate>();

            JObject obj = null;
            do
            {
                var httpResponse = await AuthenticatedHttpRequestAsync(downloadUrl, _authResult.AccessToken, HttpMethod.Get);
                string json = await ReadResponseAsStringAsync(httpResponse);
                obj = (JObject)JsonConvert.DeserializeObject(json);

                //no values
                if (obj["value"] == null)
                    break;

                //set the download Url to the next link
                downloadUrl = (string)obj["@odata.nextLink"];

                //get the file handles
                foreach(var value in obj["value"])
                {
                    //TODO: folders are important deltas, because when folders are renamed, their descendents DON"T get the delta update
                    if (value["folder"] != null)
                        continue;

                    ret.Add(new RemoteItemUpdate(value["deleted"] != null, new OneDriveRemoteFileHandle(this, value["@microsoft.graph.downloadUrl"].ToString(), value.ToString())));
                }
            }
            while (obj["@odata.deltaLink"] == null && downloadUrl != null);

            //once we get all the updates, set the delta link equal to the one given
            _deltaUrl = (string)obj["@odata.deltaLink"] ?? "";

            //call the event handler (TODO: do we want to await this?)
            await OnUpdate.Invoke(this, new Events.RemoteFileStoreDataChanged(_deltaUrl));

            return ret;
        }


        public async Task<string> GetItemMetadataAsync(string remotePath)
        {
            //TODO: return null if the item cannot be found.  This code is kinda bad.
            return await ReadResponseAsStringAsync(await AuthenticatedHttpRequestAsync($"{_onedriveEndpoint}/root:{remotePath}", _authResult.AccessToken, System.Net.Http.HttpMethod.Get));
        }
        public async Task<IRemoteItemHandle> GetFileHandleAsync(string remotePath)
        {
            string downloadUrl = "";
            string metadata = "";

            //get the download URL
            try
            {
                metadata = await GetItemMetadataAsync(remotePath);
                var data = (JObject)JsonConvert.DeserializeObject(metadata);
                downloadUrl = data["@microsoft.graph.downloadUrl"].Value<string>();
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Failed to retrieve file handle: {ex.Message}");
            }

            //now download the text
            return new OneDriveRemoteFileHandle(this, downloadUrl, metadata);
        }
        private static int _4MB = 4 * 1024 * 1024;
        public async Task<string> UploadFileAsync(string remotePath, Stream data)
        {
            if (data.Length > _4MB) //if data > 4MB, then use chunked upload
            {
                return null;
            }
            else //use regular upload
            {
                var httpResponse = await AuthenticatedHttpRequestAsync($"{_onedriveEndpoint}/root:{remotePath}:/content", _authResult.AccessToken, HttpMethod.Put, data);
                string json = await ReadResponseAsStringAsync(httpResponse);
                var obj = (JObject)JsonConvert.DeserializeObject(json);
                return (string)obj["id"];
            }
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


        public async Task<string> GetItemMetadataByIdAsync(string id)
        {
            //TODO: return null if the item cannot be found.  This code is kinda bad.
            return await ReadResponseAsStringAsync(await AuthenticatedHttpRequestAsync($"{_onedriveEndpoint}/items/{id}", _authResult.AccessToken, System.Net.Http.HttpMethod.Get));
        }
        public async Task<IRemoteItemHandle> GetFileHandleByIdAsync(string id)
        {
            string downloadUrl = "";
            string metadata = "";

            //get the download URL
            try
            {
                metadata = await GetItemMetadataByIdAsync(id);
                var data = (JObject)JsonConvert.DeserializeObject(metadata);
                downloadUrl = data["@microsoft.graph.downloadUrl"].Value<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to retrieve file handle: {ex.Message}");
            }

            //now download the text
            return new OneDriveRemoteFileHandle(this, downloadUrl, metadata);
        }
        public async Task<string> UploadFileByIdAsync(string parentId, string name, Stream data)
        {
            if (data.Length > _4MB) //if data > 4MB, then use chunked upload
            {
                return null;
            }
            else //use regular upload
            {
                var httpResponse = await AuthenticatedHttpRequestAsync($"{_onedriveEndpoint}/items/{parentId}:/{name}:/content", _authResult.AccessToken, HttpMethod.Put, data);
                string json = await ReadResponseAsStringAsync(httpResponse);
                var obj = (JObject)JsonConvert.DeserializeObject(json);
                return (string)obj["id"];
            }
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
            private string _metadata;
            private OneDriveRemoteFileStoreConnection _fileStore;

            public string Metadata { get => _metadata; }

            public OneDriveRemoteFileHandle(OneDriveRemoteFileStoreConnection fileStore, string downloadUrl, string metadata)
            {
                _fileStore = fileStore;
                _downloadUrl = downloadUrl;
                _metadata = metadata;
                IsFolder = ((JObject)JsonConvert.DeserializeObject(metadata))["folder"] != null;
            }

            public bool IsFolder { get; }

            public async Task<Stream> DownloadFileAsync()
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
