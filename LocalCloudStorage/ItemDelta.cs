namespace LocalCloudStorage
{
    class ItemDelta : IItemDelta
    {
        /// <inheritdoc />
        public IItemHandle Handle { get; set; }
        /// <inheritdoc />
        public DeltaType Type { get; set; }
        /// <inheritdoc />
        public string OldPath { get; set; }
    }
}
