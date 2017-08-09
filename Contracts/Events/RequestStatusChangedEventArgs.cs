using System;

namespace LocalCloudStorage.Events
{
    public class RequestStatusChangedEventArgs : EventArgs, IFileStoreRequestIdentifiable
    {
        public RequestStatusChangedEventArgs(FileStoreRequest request)
        {
            RequestId = request.RequestId;
            Path = request.Path;
            Status = request.Status;
            ErrorMessage = request.ErrorMessage;
            Type = request.Type;
            ExtraData = request.ExtraData;
            Complete = request.Complete;
        }

        /// <summary>
        /// The id of the request
        /// </summary>
        public int RequestId { get; }
        /// <summary>
        /// The path of the item in the request
        /// </summary>
        public string Path { get; }
        /// <summary>
        /// The current status of the request
        /// </summary>
        public RequestStatus Status { get; }
        /// <summary>
        /// If <see cref="Status"/> is <see cref="FileStoreRequest.RequestStatus.Failure"/>, this will tell why
        /// </summary>
        public string ErrorMessage { get; }
        /// <summary>
        /// The type of the request
        /// </summary>
        public RequestType Type { get; }
        /// <summary>
        /// Whether <see cref="Status"/> is an end-state status
        /// </summary>
        public bool Complete { get; }

        public IFileStoreRequestExtraData ExtraData { get; }
    }
}
