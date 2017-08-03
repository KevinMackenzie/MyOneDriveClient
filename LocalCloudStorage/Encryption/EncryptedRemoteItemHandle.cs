using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient;

namespace LocalCloudStorage.Encryption
{
    class EncryptedRemoteItemHandle : IRemoteItemHandle
    {
        private IRemoteItemHandle _remoteItemHandleImplementation;
        private IEncryptionProvider _encryptionProvider;
        public EncryptedRemoteItemHandle(IRemoteItemHandle handle, IEncryptionProvider provider)
        {
            _remoteItemHandleImplementation = handle;
            _encryptionProvider = provider;
        }

        /// <inheritdoc />
        public bool IsFolder
        {
            get { return _remoteItemHandleImplementation.IsFolder; }
        }
        /// <inheritdoc />
        public string Path
        {
            get { return _remoteItemHandleImplementation.Path; }
        }
        /// <inheritdoc />
        public string Name
        {
            get { return _remoteItemHandleImplementation.Name; }
        }
        /// <inheritdoc />
        public long Size
        {
            get { return _remoteItemHandleImplementation.Size; }
        }
        /// <inheritdoc />
        public async Task<string> GetSha1HashAsync()
        {
            return await _remoteItemHandleImplementation.GetSha1HashAsync();
        }
        /// <inheritdoc />
        public DateTime LastModified
        {
            get { return _remoteItemHandleImplementation.LastModified; }
        }
        /// <inheritdoc />
        public async Task<Stream> GetFileDataAsync()
        {
            return new EncryptedStreamWrapper(await _remoteItemHandleImplementation.GetFileDataAsync(),
                _encryptionProvider);
        }
        /// <inheritdoc />
        public string Id
        {
            get { return _remoteItemHandleImplementation.Id; }
        }
        /// <inheritdoc />
        public string ParentId
        {
            get { return _remoteItemHandleImplementation.ParentId; }
        }
        /// <inheritdoc />
        public string Sha1
        {
            get { return _remoteItemHandleImplementation.Sha1; } // TODO: how will we support this?
        }
        /// <inheritdoc />
        public async Task<HttpResult<Stream>> TryGetFileDataAsync()
        {
            var result =  await _remoteItemHandleImplementation.TryGetFileDataAsync();
            if (result.Value != null)
            {
                //apply the encryption
                result =  new HttpResult<Stream>(result.HttpMessage,
                    new EncryptedStreamWrapper(result.Value, _encryptionProvider));
            }
            return result;
        }
    }
}
