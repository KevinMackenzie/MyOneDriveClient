namespace LocalCloudStorage.ViewModel
{
    /// <summary>
    /// The viewmodel for the <see cref="FileStoreInterface.UserPrompts.KeepOverwriteOrRename"/> type
    /// </summary>
    public class AwaitUserRequestViewModel : FileStoreRequestViewModelBase
    {
        public AwaitUserRequestViewModel(FileStoreRequestViewModel me) : base(me.RequestId)
        {
            InnerRequest = me;
        }

        public string Path => InnerRequest.Path;
        public FileStoreRequestViewModel InnerRequest { get; }
    }
}
