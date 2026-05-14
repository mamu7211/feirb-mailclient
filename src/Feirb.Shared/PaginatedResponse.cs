namespace Feirb.Shared;

/// <summary>
/// Canonical envelope for paginated list responses across the API.
/// Endpoints that need extra metadata derive from this record via positional inheritance
/// (e.g. <c>MessageListResponse</c>) so the JSON payload stays flat.
/// </summary>
public record PaginatedResponse<T>(List<T> Items, int Page, int PageSize, int TotalCount);
