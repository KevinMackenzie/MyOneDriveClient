using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MyOneDriveClient
{
    public class DownloadedFileStore : IRemoteFileStoreDownload
    {
        public DownloadedFileStore(string pathRoot)
        {
            if (pathRoot.Last() == '/')
                PathRoot = pathRoot;
            else
                PathRoot = $"{pathRoot}/";
            LoadLocalItemDataAsync().Wait();
        }

        public string PathRoot { get; }

        private string BuildPath(string path)
        {
            if (path.First() == '/')
                return $"{PathRoot}{path}";
            else
                return $"{PathRoot}/{path}";
        }

        private static string _localItemDataDB = "ItemMetadata";
        private Dictionary<string, RemoteItemMetadata> _localItems = null;
        private async Task LoadLocalItemDataAsync()
        {
            if (File.Exists(BuildPath(_localItemDataDB)))
            {
                using (var itemMetadataFile = new FileStream(BuildPath(_localItemDataDB), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    StreamReader strReader = new StreamReader(itemMetadataFile, Encoding.UTF8);
                    try
                    {
                        _localItems = JsonConvert.DeserializeObject<Dictionary<string, RemoteItemMetadata>>(await strReader.ReadToEndAsync());
                    }
                    catch (Exception)
                    {
                        //this failed, so we want to build this database
                        await BuildLocalItemDataAsync();
                    }
                }
            }
            else
            {
                await BuildLocalItemDataAsync();
            }
        }
        private async Task BuildLocalItemDataAsync()
        {
            //goes through all of the local files and creates the RemoteItemMetadata's
            throw new NotImplementedException();
        }
        private async Task SaveLocalItemDataAsync()
        {
            using (var itemMetadataFile = new FileStream(BuildPath(_localItemDataDB), FileMode.Open, FileAccess.Write, FileShare.None))
            {
                StreamWriter strWriter = new StreamWriter(itemMetadataFile, Encoding.UTF8);
                await strWriter.WriteLineAsync(JsonConvert.SerializeObject(_localItemDataDB));
            }
        }

        private class RemoteItemMetadata
        {
            public bool IsFolder { get; set; }
            public string Path { get; set; }
            public DateTime RemoteLastModified { get; set; }
            public string Id { get; set; }
        }

        private RemoteItemMetadata GetItemMetadata(string localPath)
        {
            var items = (from localItem in _localItems
                         where localItem.Value.Path == localPath
                         select localItem).ToList();

            if (items.Count == 0)
                return null;
            //if (items.Count > 1) ;//???
            return items.First().Value;
        }
        //Takes the file name and renames it so it is unique, but different from its original
        private string RenameFile(string localPath)
        {
            return "";
        }

        public bool CreateLocalFolder(string folderPath)
        {
            try
            {
                System.IO.Directory.CreateDirectory(BuildPath(folderPath));
                return true;
            }
            catch(Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeleteLocalItemAsync(IRemoteItemHandle remoteHandle)
        {
            var item = GetItemMetadata(remoteHandle.Path);
            if (item == null)
                return false;

            if(item.RemoteLastModified < remoteHandle.LastModified)
            {
                //rename local file instead
                return await MoveLocalItemAsync(item.Path, RenameFile(item.Path));
            }
            else
            {
                try
                {
                    //delete the file
                    File.Delete(BuildPath(item.Path));
                    return true;
                }
                catch(Exception)
                {
                    return false;
                }
            }
        }

        public Task<IItemHandle> GetFileHandleAsync(string localPath)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetLocalSHA1Async(string id)
        {
            throw new NotImplementedException();
        }

        public Task<IRemoteItemHandle> GetRemoteFileHandleAsync(string localPath)
        {
            throw new NotImplementedException();
        }

        public Task<bool> MoveLocalItemAsync(string localPath, string newLocalPath)
        {
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(IRemoteItemHandle file)
        {
            throw new NotImplementedException();
        }

        public Task SaveFileAsync(string localPath, Stream data)
        {
            //TODO: this should never be called
            throw new InvalidOperationException();
        }
    }
}
