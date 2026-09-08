using System;
using System.Collections.Generic;
using DuplicateFileDetectorMerge.Models;

namespace DuplicateFileDetectorMerge.Services
{
    /// <summary>Fallback handler for extensions with no dedicated media-type handler; sizes must match exactly.</summary>
    public sealed class DefaultExtensionHandler : IExtensionHandler
    {
        public static readonly DefaultExtensionHandler Instance = new();

        private DefaultExtensionHandler()
        {
        }

        public string MediaTypeName => "Other";

        public IReadOnlyCollection<string> SupportedExtensions => Array.Empty<string>();

        public bool AreSimilar(ListElement fromElement, ListElement toElement) => false;
    }
}
