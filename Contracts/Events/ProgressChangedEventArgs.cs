namespace LocalCloudStorage
{
    public class ProgressChangedEventArgs
    {
        public ProgressChangedEventArgs(long amountComplete, long total)
        {
            Complete = amountComplete;
            Total = total;
        }

        public long Complete { get; }
        public long Total { get; }
        public double Progress => (double)Complete / Total;
    }
}
