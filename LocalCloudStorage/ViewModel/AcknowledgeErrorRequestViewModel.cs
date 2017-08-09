namespace LocalCloudStorage.ViewModel
{
    public class AcknowledgeErrorRequestViewModel : FileStoreRequestViewModelBase
    {
        public AcknowledgeErrorRequestViewModel(FileStoreRequestViewModel me, string errorMessage) : base(me.RequestId)
        {
            ErrorMessage = errorMessage;
            InnerRequest = me;
        }

        public string Path => InnerRequest.Path;
        public RequestType Type => InnerRequest.Type;
        public string ErrorMessage { get; }
        public FileStoreRequestViewModel InnerRequest { get; }
    }
}
