using System.Collections.Generic;
using System.Linq;

namespace WileyWidget.Services.Extensions;

/// <summary>
/// Extension members for collections to provide convenient properties and methods.
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Extension block for IEnumerable&lt;T&gt; providing instance-like members.
    /// </summary>
    extension<TSource>(IEnumerable<TSource> source)
    {
        /// <summary>
        /// Gets a value indicating whether the collection is empty.
        /// </summary>
        public bool IsEmpty => !source.Any();

        /// <summary>
        /// Filters the collection to exclude null values and applies the predicate.
        /// </summary>
        public IEnumerable<TSource> WhereNotNull(Func<TSource, bool> predicate) => source.Where(static x => x is not null).Where(predicate);
    }
}
