namespace Rescauta.Application.Common;

/// <summary>
/// Envoltura de paginacion reutilizable por los tres modulos (listados de puntos de rescate,
/// de movimientos de kardex y de donaciones).
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;

    public static PagedResult<T> Create(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize) =>
        new()
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
}
