using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Umbraco.Core.IO;


namespace UmbracoFlare.FileSystemPickerControllers
{
  
    public class FileSystem
    {
        public IEnumerable<DirectoryInfo> GetFolders(string folder, string[] filter)
        {
            var path = IOHelper.MapPath("~/" + folder.TrimStart('~', '/'));
            if (filter != null && filter[0] != ".")
            {
                IEnumerable<DirectoryInfo> dirs = new DirectoryInfo(path).EnumerateDirectories();
                return dirs.Where(d => d.EnumerateFiles().Where(f => filter.Contains(f.Extension, StringComparer.OrdinalIgnoreCase)).Any());
            }

            return new DirectoryInfo(path).GetDirectories("*");
        }

        public IEnumerable<FileInfo> GetFiles(string folder, string[] filter)
        {
            var path = IOHelper.MapPath("~/" + folder.TrimStart('~', '/'));
            DirectoryInfo dir = new DirectoryInfo(path);
            IEnumerable<FileInfo> files = dir.EnumerateFiles();

            if (filter != null && filter[0] != ".")
                return files.Where(f => filter.Contains(f.Extension, StringComparer.OrdinalIgnoreCase));

            return new DirectoryInfo(path).GetFiles();
        }

        public IEnumerable<FileInfo> GetFilesIncludingSubDirs(string path)
        {
            Queue<string> queue = new Queue<string>();
            queue.Enqueue(path);
            while (queue.Count > 0)
            {
                path = queue.Dequeue();
                try
                {
                    foreach (string subDir in Directory.GetDirectories(path))
                    {
                        queue.Enqueue(subDir);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                FileInfo[] files = null;
                try
                {
                    files = new DirectoryInfo(path).GetFiles();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                if (files != null)
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        yield return files[i];
                    }
                }
            }
        }

       
    }
}