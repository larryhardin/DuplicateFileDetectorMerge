using System;
using DuplicateFileDetectorMerge.Models;

namespace DuplicateFileDetectorMerge.Services
{
    /// <summary>Notifies subscribers (e.g. the UI) when a content-level comparison starts/ends, without coupling handlers to any window.</summary>
    public static class ComparisonActivity
    {
        public static event Action<ListElement, ListElement>? Started;
        public static event Action? Ended;

        public static void RaiseStarted(ListElement fromElement, ListElement toElement) => Started?.Invoke(fromElement, toElement);
        public static void RaiseEnded() => Ended?.Invoke();
    }
}
