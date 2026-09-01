namespace Api.Battles;

/// <summary>A typed failure at the API boundary. See CLAUDE.md: errors here are results, not exceptions.</summary>
public sealed record ErrorResponse(string Message);
