using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage.Contracts
{
    public interface IFileStoreInterface
    {
        /// <summary>
        /// A Json-string representation of the metadata cache
        /// </summary>
        string MetadataCache { get; set; }

        bool TryGetRequest(int requestId, out FileStoreRequest request);
        void CancelRequest(int requestId);
        void SignalConflictResolved(int requestId);

        Task<bool> ProcessQueueAsync(CancellationToken ct);

        /// <summary>
        /// When the status of an existing request changes or a new request is started.  Note
        /// that if the status has been changed to <see cref="FileStoreRequest.RequestStatus.Success"/>, there
        /// is no guarantee that the request still exists.
        /// </summary>
        event EventDelegates.RequestStatusChangedHandler OnRequestStatusChanged;
    }
}
