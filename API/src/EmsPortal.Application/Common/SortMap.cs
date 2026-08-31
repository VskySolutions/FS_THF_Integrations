using System.Linq.Expressions;

namespace EmsPortal.Application.Common;

/// <summary>
/// Builds a <see cref="SortMap{T}"/> for a query whose row type cannot be written down — the joined
/// <c>{ Request, Form }</c> shapes the REMS lists order on, which are anonymous types. The query itself
/// supplies the type; it is never enumerated.
/// </summary>
public static class SortMap
{
    /// <param name="shape">The query being ordered. Used only to infer <typeparamref name="T"/>.</param>
    /// <param name="fallbackKey">The column to order by when none is named, or an unknown one is.</param>
    public static SortMap<T> For<T>(IQueryable<T> shape, string fallbackKey) => new(fallbackKey);
}

/// <summary>
/// The sortable columns of one list, and how each of them orders it.
///
/// <para>
/// Every list page sends the column the reader clicked (<c>sortBy</c>) and a direction. That name is a UI
/// label arriving over the wire, so it is matched against a map declared next to the query rather than
/// used to build an expression: nothing outside this map can be ordered by, which is what keeps a query
/// parameter from reaching into the model.
/// </para>
/// <para>
/// The alternative — ordering in the browser — cannot be made to work, which is why every list endpoint
/// carries one of these. A page is a slice of a set, and a slice can only be reordered WITHIN itself:
/// "oldest first" over the twenty rows already fetched is not the oldest of the two hundred. Ordering
/// belongs where the whole set is.
/// </para>
///
/// <example>
/// <code>
/// private static readonly SortMap&lt;Tenant&gt; Sorts = new SortMap&lt;Tenant&gt;("updatedOnUtc")
///     .Add("name", t =&gt; t.Name)
///     .Add("updatedOnUtc", t =&gt; t.UpdatedOnUtc);
///
/// query = Sorts.Apply(query, sortBy, descending);
/// </code>
/// </example>
/// </summary>
/// <typeparam name="T">The row type being ordered.</typeparam>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> _sorts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _fallbackKey;

    /// <param name="fallbackKey">
    /// The column used when the caller names none, or names one this list cannot order by. A fallback
    /// rather than an error: an unknown column is a page asking for something this endpoint does not
    /// offer, and the reader is better served by the list's usual order than by a failed request.
    /// </param>
    public SortMap(string fallbackKey)
    {
        _fallbackKey = fallbackKey;
    }

    /// <summary>
    /// Declares that <paramref name="key"/> — the column name the UI sends — orders by
    /// <paramref name="selector"/>. Generic in the key type so the expression reaches EF as it was
    /// written: a DateTime orders chronologically and an int numerically, neither of them as text.
    /// </summary>
    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        _sorts[key] = (query, descending) =>
            descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
        return this;
    }

    /// <summary>
    /// Adds a second, fixed ordering applied after <paramref name="key"/> — for a column whose values
    /// repeat often enough that ties would otherwise fall in an order the database is free to change
    /// between pages, which shows up as a row appearing twice or not at all while paging.
    /// </summary>
    public SortMap<T> Add<TKey, TThen>(string key, Expression<Func<T, TKey>> selector, Expression<Func<T, TThen>> thenBy)
    {
        _sorts[key] = (query, descending) => descending
            ? query.OrderByDescending(selector).ThenByDescending(thenBy)
            : query.OrderBy(selector).ThenBy(thenBy);
        return this;
    }

    /// <summary>True when this list can order by <paramref name="key"/>.</summary>
    public bool Knows(string? key) => key is not null && _sorts.ContainsKey(key);

    /// <summary>
    /// Orders <paramref name="query"/> by the named column, falling back to this list's default when the
    /// name is missing or unknown.
    /// </summary>
    public IOrderedQueryable<T> Apply(IQueryable<T> query, string? sortBy, bool descending)
    {
        if (sortBy is not null && _sorts.TryGetValue(sortBy, out var sort))
        {
            return sort(query, descending);
        }

        return _sorts[_fallbackKey](query, descending);
    }

    /// <summary>
    /// The in-memory twin, for the handful of lists whose rows are already materialised (a projection the
    /// database cannot express, or a set small enough that it is loaded whole). Same map, same names —
    /// so a list does not change which columns it can be sorted by depending on how it is fetched.
    /// </summary>
    public IEnumerable<T> Apply(IEnumerable<T> items, string? sortBy, bool descending)
        => Apply(items.AsQueryable(), sortBy, descending);
}
