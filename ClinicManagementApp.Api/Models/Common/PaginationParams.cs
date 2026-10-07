namespace ClinicManagementApp.Api.Models.Common;

/// <summary>
/// Standard reusable query parameters for paginated endpoints with safe limits.
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    /// <summary>
    /// 1-based page number. Defaults to 1.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Number of items per page. Clamped between 1 and 100. Defaults to 20.
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? 1 : value);
    }

    /// <summary>
    /// Optional search term for text filtering.
    /// </summary>
    public string? Search { get; init; }
}

