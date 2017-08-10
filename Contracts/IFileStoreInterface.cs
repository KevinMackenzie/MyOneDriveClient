using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;
using LocalCloudStorage.Threading;

namespace LocalCloudStorage
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

        /// <summary>
        /// Processes the request queue until completion or cancellation
        /// </summary>
        /// <param name="pt">pauses the task mid-way</param>
        /// <returns>Whether the queue completed without user intervention</returns>
        /// <remarks>the pause token can be cancelled and will behave like
        ///  a cancellation token</remarks>
        Task<bool> ProcessQueueAsync(PauseToken pt);

        /// <summary>
        /// When the status of an existing request changes or a new request is started.  Note
        /// that if the status has been changed to <see cref="RequestStatus.Success"/>, there
        /// is no guarantee that the request still exists.
        /// </summary>
        event EventDelegates.RequestStatusChangedHandler OnRequestStatusChanged;
    }
}
