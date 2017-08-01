﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MyOneDriveClient
{
    /// <summary>
    /// The viewmodel for the <see cref="FileStoreInterface.UserPrompts.KeepOverwriteOrRename"/> type
    /// </summary>
    public class AwaitUserRequestViewModel : ViewModelBase
    {
        public AwaitUserRequestViewModel(FileStoreRequestViewModel me)
        {
            InnerRequest = me;
        }

        public string Path => InnerRequest.Path;
        public FileStoreRequestViewModel InnerRequest { get; }
    }
}
