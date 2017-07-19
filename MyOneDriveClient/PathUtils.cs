using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOneDriveClient
{
    static class PathUtils
    {
        public static string GetParentItemPath(string path)
        {
            var parts = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return "/";

            string ret = "";
            for (int i = 0; i < parts.Length - 1; ++i)
            {
                ret = $"{ret}/{parts[i]}";
            }
            return ret;
        }

        public static string GetItemName(string path)
        {
            return path.Split(new char[] { '/' }).Last();
        }

        public static string CompileString(IEnumerable<string> pathParts, string name)
        {
            var path = "";
            foreach (var part in pathParts)
            {
                path += $"/{part}";
            }

            return $"{path}/{name}";
        }
    }
}
