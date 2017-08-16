using System;
using LocalCloudStorage.Events;
using LocalCloudStorage.ViewModel;

namespace LocalCloudStorage
{
    public class RequestsViewModel
    {
        public RequestsViewModel(Action<EventDelegates.RequestStatusChangedHandler> registerLocalStatusChanged,
            Action<EventDelegates.RequestStatusChangedHandler> registerRemoteStatusChanged,
            Action<EventDelegates.RemoteRequestProgressChangedHandler> registerRemoteProgressChanged)
        {
            LocalRequests = new BaseRequestsViewModel(registerLocalStatusChanged);
            RemoteRequests = new RemoteRequestsViewModel(registerRemoteStatusChanged, registerRemoteProgressChanged);
        }

        public BaseRequestsViewModel LocalRequests { get; }
        public RemoteRequestsViewModel RemoteRequests { get; }
    }
}
