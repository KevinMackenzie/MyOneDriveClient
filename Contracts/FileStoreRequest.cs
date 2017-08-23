using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage;

namespace LocalCloudStorage
{
    public interface IFileStoreRequestExtraData
    { }

    #region Request Extra Datas
    public class RequestRenameExtraData : IFileStoreRequestExtraData
    {
        public RequestRenameExtraData(string newName)
        {
            NewName = newName;
        }
        public string NewName { get; }
    }
    public class RequestMoveExtraData : IFileStoreRequestExtraData
    {
        public RequestMoveExtraData(string newParentPath)
        {
            NewParentPath = newParentPath;
        }
        public string NewParentPath { get; }
    }
    #endregion

    public class FileStoreRequest : IFileStoreRequestIdentifiable
    {
        private int _requestId;
        public FileStoreRequest(ref int id, RequestType type, string path, IFileStoreRequestExtraData extraData)
        {
            _requestId = Interlocked.Increment(ref id);

            Path = path;
            Status = RequestStatus.Pending;
            ErrorMessage = null;
            Type = type;
            ExtraData = extraData;
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
        /// <summary>
        /// Whether <see cref="Status"/> is an end-state status
        /// </summary>
        public bool Complete => Status == RequestStatus.Cancelled || 
                                //Status == RequestStatus.Failure ||
                                Status == RequestStatus.Success;

        public IFileStoreRequestExtraData ExtraData { get; set; }
    }
}
