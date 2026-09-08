using System;
using System.IO;

namespace DuplicateFileDetectorMerge.Services
{
    public static class FolderValidation
    {
        /// <summary>Returns true if candidate is the same as, or a subfolder of, potentialParent.</summary>
        public static bool IsSubfolderOf(string candidate, string potentialParent)
        {
            var candidateFull = NormalizePath(candidate);
            var parentFull = NormalizePath(potentialParent);

            if (string.Equals(candidateFull, parentFull, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return candidateFull.StartsWith(
                parentFull + System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            var full = Path.GetFullPath(path);
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
