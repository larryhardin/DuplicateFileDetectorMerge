using System.Collections.Generic;
using DuplicateFileDetectorMerge.Models;

namespace DuplicateFileDetectorMerge.Services
{
    /// <summary>Encapsulates duplicate-detection behavior for a group of file extensions (e.g. images, video).</summary>
    public interface IExtensionHandler
    {
        string MediaTypeName { get; }
        IReadOnlyCollection<string> SupportedExtensions { get; }

        /// <summary>Called when name/extension match but file sizes differ; return true to treat the pair as a duplicate anyway.</summary>
        bool AreSimilar(ListElement fromElement, ListElement toElement);
    }
}
