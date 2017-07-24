using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;

namespace MyOneDriveClient
{
    public class LocalFileStoreInterface
    {
        #region Private Fields
        private ILocalFileStore _local;
        #endregion

        public LocalFileStoreInterface(ILocalFileStore local)
        {
            _local = local;
        }

        #region Public Properties
        public string MetadataCache { get; }
        #endregion

        #region Public Methods
        public bool TryGetItemHandle(string path, out ILocalItemHandle itemHandle)
        {
            throw new NotImplementedException();
        }
        public bool TryGetWritableStream(string path, out Stream writableStream)
        {
            throw new NotImplementedException();
        }
        public bool TrySetItemLastModified(string path, DateTime lastModified)
        {
            throw new NotImplementedException();
        }
        public bool ItemExists(string path)
        {
            throw new NotImplementedException();
        }
        public IEnumerable<ItemDelta> GetDeltas()
        {
            throw new NotImplementedException();
        }

        public int RequestDeleteItem(string path)
        {
            throw new NotImplementedException();
        }
        public int RequestFolderCreate(string path)
        {
            throw new NotImplementedException();
        }
        public int RequestMoveItem(string path, string newPath)
        {
            throw new NotImplementedException();
        }

        public async Task SaveNonSyncFile(string path, string content)
        {
            if (TryGetWritableStream(path, out Stream writableStream))
            {
                using (writableStream)
                {
                    using (var contentStream = content.ToStream(Encoding.UTF8))
                    {
                        await contentStream.CopyToStreamAsync(writableStream);
                    }
                }
                _local.SetItemAttributes(path, FileAttributes.Hidden);
            }
        }
        #endregion

        #region Public Events
        /// <summary>
        /// When the status of an existing request changes or a new request is started.  Note
        /// that if the status has been changed to <see cref="BufferedRemoteFileStoreInterface.RequestStatus.Success"/>, there
        /// is no guarantee that the request still exists.
        /// </summary>
        public event EventDelegates.RequestStatusChangedHandler OnRequestStatusChanged;
        #endregion
    }
}
