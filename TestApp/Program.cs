using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            using (FileSystemWatcher watcher = new FileSystemWatcher())
            {
                watcher.Path = "C:/Users/kjmac/Documents/GitHub/MyOneDriveClient/TestFolder";
                watcher.IncludeSubdirectories = true;
                watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Attributes | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                watcher.Filter = "*";

                watcher.Changed += Watcher_Changed;
                watcher.Renamed += Watcher_Renamed;
                watcher.Deleted += Watcher_Changed;
                watcher.Created += Watcher_Changed;

                watcher.EnableRaisingEvents = true;//do we want this?
                Console.Read();
            }
        }

        private static void Watcher_Renamed(object sender, RenamedEventArgs e)
        {
            int i = 0;
        }

        private static void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            int i = 0;
        }
    }
}
