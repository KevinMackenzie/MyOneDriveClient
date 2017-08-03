using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalCloudStorage.Events;

namespace LocalCloudStorage
{
    public abstract class FileStoreInterface
    {
        public enum UserPrompts
        {
            KeepOverwriteOrRename,
            CloseApplication,
            Acknowledge
        }

        public enum ConflictResolutions
        {
            KeepLocal,
            KeepRemote,
            KeepBoth
        }


        #region Private Fields
        private ConcurrentQueue<FileStoreRequest> _requests = new ConcurrentQueue<FileStoreRequest>();
        private FileStoreRequestGraveyard _completedRequests = new FileStoreRequestGraveyard(TimeSpan.FromMinutes(5));
        /// <summary>
        /// Requests that failed for reasons other than network connection
        /// </summary>
        private ConcurrentDictionary<int, FileStoreRequest> _limboRequests = new ConcurrentDictionary<int, FileStoreRequest>();
        private ConcurrentDictionary<int, object> _cancelledRequests = new ConcurrentDictionary<int, object>();

        private CancellationTokenSource _processQueueCancellationTokenSource;
        private Task _processQueueTask;
        #endregion

        #region Protected Fields
        #endregion

        #region Private Methods
        private bool SkipRequest(FileStoreRequest request)
        {
            //was this request cancelled?
            if (!_cancelledRequests.ContainsKey(request.RequestId)) return false;

            //if so, move on
            _cancelledRequests.TryRemove(request.RequestId, out object alwaysNull);
            InvokeStatusChanged(request, FileStoreRequest.RequestStatus.Cancelled);
            return true;
        }
        private async Task ProcessQueueInternal(TimeSpan delay, TimeSpan errorDelay, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (ct.IsCancellationRequested)
                    break;

                while (!await ProcessQueueAsync())
                {
                    //something failed (failed request, or limbo), so we should wait a little bit before trying again
                    await Utils.DelayNoThrow(errorDelay, ct);
                }

                await Utils.DelayNoThrow(delay, ct);
            }
        }
        #endregion

        #region Abstract Methods
        protected abstract Task<bool> ProcessQueueItemAsync(FileStoreRequest request);
        #endregion

        #region Protected Methods
        protected void InvokeStatusChanged(FileStoreRequest request)
        {
            OnRequestStatusChanged?.Invoke(this, new RequestStatusChangedEventArgs(request));
            if (request.Complete)
                _completedRequests.Add(request);
        }
        protected void InvokeStatusChanged(FileStoreRequest request, FileStoreRequest.RequestStatus status)
        {
            if (request.Status == status)
                return;//it didn't actually change...
            request.Status = status;
            InvokeStatusChanged(request);
        }
        protected void FailRequest(FileStoreRequest request, string errorMessage)
        {
            RequestAwaitUser(request, UserPrompts.Acknowledge, errorMessage);
            /*request.Status = FileStoreRequest.RequestStatus.Failure;
            request.ErrorMessage = errorMessage;
            _limboRequests[request.RequestId] = request;
            InvokeStatusChanged(request);*/
        }
        protected void RequestAwaitUser(FileStoreRequest request, UserPrompts prompt, string message = null)
        {
            request.Status = FileStoreRequest.RequestStatus.WaitForUser;
            request.ErrorMessage = string.IsNullOrEmpty(message) ? prompt.ToString() : message;
            _limboRequests[request.RequestId] = request;
            InvokeStatusChanged(request);
        }
        protected int EnqueueRequest(FileStoreRequest request)
        {
            _requests.Enqueue(request);
            InvokeStatusChanged(request);
            return request.RequestId;
        }
        protected async Task<FileStoreRequest> ProcessRequestAsync(FileStoreRequest request)
        {
            InvokeStatusChanged(request);
            if (await ProcessQueueItemAsync(request))
            {
                _completedRequests.Add(request);
            }
            return request;
        }
        #endregion


        #region Public Methods
        /// <summary>
        /// Starts the processing of the request queue
        /// </summary>
        //public void StartRequestProcessing()
        //{
        //    if (_processQueueTask != null) return;
        //    _processQueueCancellationTokenSource = new CancellationTokenSource();
        //    _processQueueTask = ProcessQueueInternal(TimeSpan.FromMilliseconds(5000), TimeSpan.FromMilliseconds(100), _processQueueCancellationTokenSource.Token);
        //}
        /// <summary>
        /// Stops the processing of the request queue
        /// </summary>
        /// <returns></returns>
        //public async Task StopRequestProcessingAsync()
        //{
        //    if (_processQueueTask == null) return;
        //    _processQueueCancellationTokenSource.Cancel();
        //    await _processQueueTask;
        //}
        

        /// <summary>
        /// Waits for the given request status to reach a conclusive statis
        ///  (<see cref="FileStoreRequest.RequestStatus.Cancelled"/>
        ///  or <see cref="FileStoreRequest.RequestStatus.Success"/>
        ///  or <see cref="FileStoreRequest.RequestStatus.Failure"/>
        /// </summary>
        /// <param name="requestId">the id of the request to wait for</param>
        /// <returns>the request with that id</returns>
        public async Task<FileStoreRequest> AwaitRequest(int requestId)
        {
            if (TryGetRequest(requestId, out FileStoreRequest request))
            {
                //check to see if it's already complete
                if (request.Complete)
                    return request;

                var cts = new CancellationTokenSource();
                OnRequestStatusChanged += async (sender, args) =>
                {
                    if (args.RequestId == requestId && args.Complete)
                        cts.Cancel();
                };

                while (!cts.IsCancellationRequested)
                {
                    if (request.Complete)//if the event handler didn't catch it
                        break;
                    await Utils.DelayNoThrow(TimeSpan.FromMilliseconds(50), cts.Token);
                }

                return request;
            }
            else
            {
                return null;
            }
        }
        public bool TryGetRequest(int requestId, out FileStoreRequest request)
        {
            //are there any queue items?
            var reqs = _requests.Where(item => item.RequestId == requestId);
            if (!reqs.Any())
            {
                //no queue items... let's check limbo
                if (_limboRequests.TryGetValue(requestId, out FileStoreRequest limboReq))
                {
                    //there is a limbo request
                    request = limboReq;
                    return true;
                }

                //no limbo items... let's check completed ones
                if (_completedRequests.TryGetItem(requestId, out FileStoreRequest completedRequest))
                {
                    request = completedRequest;
                    return true;
                }
                //nope, no requests at all
                request = null;
                return false;
            }

            //there is a queue item!
            request = reqs.First();
            return true;
        }
        public void CancelRequest(int requestId)
        {
            var skipInvoke = false;
            //are there any queue items?
            var reqs = _requests.Where(item => item.RequestId == requestId);
            if (reqs.Any())
            {
                //there is a queue item, so add it to the cancellation dictionary
                _cancelledRequests.TryAdd(requestId, null);

                skipInvoke = true;
            }

            //let's check limbo too
            if (!_limboRequests.TryRemove(requestId, out FileStoreRequest value)) return;
            if (!skipInvoke)
            {
                InvokeStatusChanged(value, FileStoreRequest.RequestStatus.Cancelled);
            }
        }

        public void ResolveRequest(int requestId)
        {
            if (!_limboRequests.TryRemove(requestId, out FileStoreRequest request)) return;
            request.Status = FileStoreRequest.RequestStatus.Pending;
            request.ErrorMessage = "";
        }

        /// <summary>
        /// Processes the request queue until an error comes up or
        ///  the user needs to be prompted
        /// </summary>
        /// <returns>whether the queue was successfully emptied</returns>
        public async Task<bool> ProcessQueueAsync()
        {
            while (_requests.TryPeek(out FileStoreRequest request))
            {
                if (!_limboRequests.IsEmpty)
                    return false; //stop on a limbo request

                if (SkipRequest(request))
                {
                    //dequeue and move on
                    _requests.TryDequeue(out FileStoreRequest result);
                    continue;
                }

                var dequeue = await ProcessQueueItemAsync(request);

                //should we dequeue the item?
                if (dequeue)
                {
                    //yes
                    _requests.TryDequeue(out FileStoreRequest result);
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Enumerates the currently active requests
        /// </summary>
        /// <returns></returns>
        public IEnumerable<FileStoreRequest> EnumerateActiveRequests()
        {
            //this creates a copy
            return new List<FileStoreRequest>(_requests);
        }
        #endregion
        /// <summary>
        /// When the status of an existing request changes or a new request is started.  Note
        /// that if the status has been changed to <see cref="FileStoreRequest.RequestStatus.Success"/>, there
        /// is no guarantee that the request still exists.
        /// </summary>
        public EventDelegates.RequestStatusChangedHandler OnRequestStatusChanged;
    }
}
