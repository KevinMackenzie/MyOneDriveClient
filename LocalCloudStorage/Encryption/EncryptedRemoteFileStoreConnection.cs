using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient;

namespace LocalCloudStorage.Encryption
{
    public class EncryptedRemoteFileStoreConnection : IRemoteFileStoreConnection
    {
        private IRemoteFileStoreConnection _remoteFileStoreConnectionImplementation;
        private IEncryptionProvider _encryptionProvider;
        private bool _encryptFileNames;
        private bool _retainDirectoryStructure;

        public EncryptedRemoteFileStoreConnection(IRemoteFileStoreConnection remoteFileStoreConnection,
            IEncryptionProvider encryptionProvider,
            bool encryptFileNames, bool retainDirectoryStructure)
        {
            /*switch (type)
            {
                case EncryptionType.AES128:
                    if (key.Count != 16)
                        throw new ArgumentException("Key for AES128 encryption must be 16 bytes!");
                    break;
                case EncryptionType.Twofish:
                    if (key.Count != 32)
                        throw new ArgumentException("Key for Twofish encryption must be 32 bytes");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }*/

            _remoteFileStoreConnectionImplementation = remoteFileStoreConnection;
            _encryptionProvider = encryptionProvider;
            _encryptFileNames = encryptFileNames;
            _retainDirectoryStructure = retainDirectoryStructure;
        }

        /// <inheritdoc />
        public async Task PromptUserLoginAsync()
        {
            await _remoteFileStoreConnectionImplementation.PromptUserLoginAsync();
        }
        /// <inheritdoc />
        public void LogUserOut()
        {
            _remoteFileStoreConnectionImplementation.LogUserOut();
        }
        /// <inheritdoc />
        public async Task<DeltaPage> GetDeltasAsync(string deltaLink)
        {
            var deltaPage =  await _remoteFileStoreConnectionImplementation.GetDeltasAsync(deltaLink);

            var newDeltaPage = new DeltaPage(deltaPage.NextPage, deltaPage.DeltaLink);
            newDeltaPage.AddRange(deltaPage
                .Select(delta => new RemoteItemUpdate(delta.Deleted,
                    new EncryptedRemoteItemHandle(delta.ItemHandle, _encryptionProvider))).Cast<IRemoteItemUpdate>());
            return newDeltaPage;
        }
        /// <inheritdoc />
        public async Task<HttpResult<string>> GetItemMetadataAsync(string remotePath)
        {
            return await _remoteFileStoreConnectionImplementation.GetItemMetadataAsync(remotePath);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> GetItemHandleAsync(string remotePath)
        {
            return await _remoteFileStoreConnectionImplementation.GetItemHandleAsync(remotePath);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> UploadFileAsync(string remotePath, Stream data)
        {
            return await _remoteFileStoreConnectionImplementation.UploadFileAsync(remotePath, data);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> CreateFolderAsync(string remotePath)
        {
            return await _remoteFileStoreConnectionImplementation.CreateFolderAsync(remotePath);
        }
        /// <inheritdoc />
        public async Task<HttpResult<bool>> DeleteItemAsync(string remotePath)
        {
            return await _remoteFileStoreConnectionImplementation.DeleteItemAsync(remotePath);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> RenameItemAsync(string remotePath, string newName)
        {
            return await _remoteFileStoreConnectionImplementation.RenameItemAsync(remotePath, newName);
        }
        /// <inheritdoc />
        public async Task<HttpResult<string>> GetItemMetadataByIdAsync(string id)
        {
            return await _remoteFileStoreConnectionImplementation.GetItemMetadataByIdAsync(id);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> GetItemHandleByIdAsync(string id)
        {
            return await _remoteFileStoreConnectionImplementation.GetItemHandleByIdAsync(id);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> UploadFileByIdAsync(string parentId, string fileName, Stream data)
        {
            return await _remoteFileStoreConnectionImplementation.UploadFileByIdAsync(parentId, fileName, data);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> CreateFolderByIdAsync(string parentId, string name)
        {
            return await _remoteFileStoreConnectionImplementation.CreateFolderByIdAsync(parentId, name);
        }
        /// <inheritdoc />
        public async Task<HttpResult<bool>> DeleteItemByIdAsync(string id)
        {
            return await _remoteFileStoreConnectionImplementation.DeleteItemByIdAsync(id);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> RenameItemByIdAsync(string id, string newName)
        {
            return await _remoteFileStoreConnectionImplementation.RenameItemByIdAsync(id, newName);
        }
        /// <inheritdoc />
        public async Task<HttpResult<IRemoteItemHandle>> MoveItemByIdAsync(string id, string newParentId)
        {
            return await _remoteFileStoreConnectionImplementation.MoveItemByIdAsync(id, newParentId);
        }
    }
}
