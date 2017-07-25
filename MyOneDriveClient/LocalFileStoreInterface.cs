using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyOneDriveClient.Events;
using System.Threading;
using System.Collections.Concurrent;

namespace MyOneDriveClient
{
    public class LocalFileStoreInterface
    {
        public enum RequestStatus
        {
            /// <summary>
            /// Request was successfuly completed
            /// </summary>
            Success,
            /// <summary>
            /// Request is in queue/await network connection
            /// </summary>
            Pending,
            /// <summary>
            /// Request did not successfully complete
            /// </summary>
            Failure,
            /// <summary>
            /// Request was cancelled
            /// </summary>
            Cancelled
        }

        public enum RequestType
        {
            Rename,
            Move,
            Create,
            Delete
        }

        public class RemoteFileStoreRequest
        {
            private int _requestId;
            public RemoteFileStoreRequest(ref int id, RequestType type, string path)
            {
                _requestId = Interlocked.Increment(ref id);

                Path = path;
                Status = RequestStatus.Pending;
                ErrorMessage = null;
                Type = type;
            }
            /// <summary>
            /// The ID of the request
            /// </summary>
            public int RequestId => _requestId;
            /// <summary>
            /// The path of the item in the request
            /// </summary>
            public string Path { get; }
            /// <summary>
            /// The current status of the request
            /// </summary>
            public RequestStatus Status { get; set; }
            /// <summary>
            /// If <see cref="Status"/> is <see cref="RequestStatus.Failure"/>, this will tell why
            /// </summary>
            public string ErrorMessage { get; set; }
            /// <summary>
            /// The type of the request
            /// </summary>
            public RequestType Type { get; }
        }

        #region Private Fields
        private ILocalFileStore _local;
        private ConcurrentQueue<RemoteFileStoreRequest> _requests = new ConcurrentQueue<RemoteFileStoreRequest>();
        private ConcurrentDictionary<int, RemoteFileStoreRequest> _limboRequests = new ConcurrentDictionary<int, RemoteFileStoreRequest>();
        private ConcurrentDictionary<int, object> _cancelledRequests = new ConcurrentDictionary<int, object>();
        private int _requestId;//TODO: should this be volatile
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
        public int RequestRenameItem(string path, string newName)
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
        public event EventDelegates.RequestStatusChangedHandler OnRequestStatusChanged;//TODO: local status' are a bit different, because there is a rename/merge option
        #endregion
    }
}
