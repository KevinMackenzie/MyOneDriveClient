namespace LocalCloudStorage.ViewModel
{
    public class CloseAppRequestViewModel : FileStoreRequestViewModelBase
    {
        public CloseAppRequestViewModel(FileStoreRequestViewModel me) : base(me.RequestId)
        {
            InnerRequest = me;
        }

        public string Path => InnerRequest.Path;
        public RequestType Type => InnerRequest.Type;
        public FileStoreRequestViewModel InnerRequest { get; }
    }
}
