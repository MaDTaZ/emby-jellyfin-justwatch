using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JustWatch.Plugin.Core;

public interface IJustWatchClient
{
    Task<IReadOnlyList<JustWatchSearchResult>> SearchAsync(
        string title,
        int? year,
        string? mediaType,
        CancellationToken cancellationToken);
}
