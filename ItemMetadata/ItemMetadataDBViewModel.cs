using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using LocalCloudStorage;

namespace LocalCloudStorage.ItemMetadata
{
    public class ItemMetadataDbViewModel
    {
        private readonly ItemMetadataDb _model;
        public ItemMetadataDbViewModel(ItemMetadataDb model)
        {
            _model = model;
        }

        #region Helper Methods

        private ItemMetadata GetFirstWithPath(string path)
        {
            return (from item in _model.ItemMetadata
                where item.Name == path
                select item).First();
        }
        #endregion

        #region Update Helper Methods
        private bool UpdateCreated(IItemDelta delta, ItemMetadata item)
        {
            if (item != null)
            {
                // TODO: what do we do here
                return false;
            }
            _model.ItemMetadata.Add(new ItemMetadata()
            {
                Id = delta.Handle.Id,
                IsFolder = delta.Handle.IsFolder,
                Path = delta.Handle.Path,
                ParentId = delta.Handle.ParentId,
                Size = delta.Handle.Size
            });
            return true;
        }
        private bool UpdateDeleted(IItemDelta delta, ItemMetadata item)
        {
            if (item == null) return false;

            _model.Remove(item);
            return true;
        }
        private bool UpdateModified(IItemDelta delta, ItemMetadata item)
        {
            if (item != null)
            {
                if (item.Size == delta.Handle.Size) return false;

                item.Size = delta.Handle.Size;
                return true;
            }
            else
            {
                delta.Type = DeltaType.Created;
                return UpdateCreated(delta, null);
            }
        }
        private bool UpdateRenamed(IItemDelta delta, ItemMetadata item)
        {
            if (item != null)
            {
                if (item.Name == delta.Handle.Name) return false;

                item.Name = delta.Handle.Name;
                return true;
            }
            else
            {
                delta.Type = DeltaType.Created;
                return UpdateCreated(delta, null);
            }
        }
        private bool UpdateMoved(IItemDelta delta, ItemMetadata item)
        {
            if (item != null)
            {
                var newParent = GetFirstWithPath(PathUtils.GetParentItemPath(delta.OldPath));
                if (newParent == null)
                {
                    // ERROR
                    throw new Exception();
                }
                if (item.ParentId == newParent.Id) return false;

                item.ParentId = newParent.Id;
                return true;
            }
            else
            {
                delta.Type = DeltaType.Created;
                return UpdateCreated(delta, null);
            }
        }
        #endregion

        public void Update(IItemDelta delta)
        {
            // First, change the metadata based on the delta type
            bool changed;
            var item = GetFirstWithPath(delta.Handle.Path);
            switch (delta.Type)
            {
                case DeltaType.Created:
                    changed = UpdateCreated(delta, item);
                    break;
                case DeltaType.Deleted:
                    changed = UpdateDeleted(delta, item);
                    break;
                case DeltaType.Modified:
                    changed = UpdateModified(delta, item);
                    break;
                case DeltaType.Renamed:
                    changed = UpdateRenamed(delta, item);
                    break;
                case DeltaType.Moved:
                    changed = UpdateMoved(delta, item);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Now, if the data changed, send the results
            if (!changed) return;

            if (delta.Handle is IRemoteItemHandle)
            {
                // Tag as remote update
                OnRemotelyChanged?.Invoke(new ItemMetadataChangedEventData(delta));
            }
            else
            {
                // otherwise, tag as local update
                OnLocallyChanged?.Invoke(new ItemMetadataChangedEventData(delta));
            }
        }

        public bool TryGetItem(string filePath, IReadItemMetadata itemMetadata)
        {
            throw new NotImplementedException();
        }

        #region Events
        public event OnItemMetadataChanged OnLocallyChanged;
        public event OnItemMetadataChanged OnRemotelyChanged;
        #endregion
    }
}
