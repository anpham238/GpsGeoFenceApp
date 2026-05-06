namespace MauiApp1.Services.Narration;

public sealed class PoiPriorityResolver
{
    private readonly PriorityResolverOptions _options;

    public PoiPriorityResolver(PriorityResolverOptions options)
    {
        _options = options;
    }

    public PoiPriorityResolver() : this(new PriorityResolverOptions()) { }

    /// <summary>
    /// Tính điểm ưu tiên cho tất cả candidates và trả về danh sách đã sắp xếp.
    /// </summary>
    public List<NarrationQueueItem> Resolve(
        IEnumerable<PoiCandidate> candidates,
        DateTime nowUtc,
        int? currentTourStep = null)
    {
        var result = new List<NarrationQueueItem>();

        foreach (var c in candidates)
        {
            double score = c.PriorityLevel * 10;

            if (c.IsTapped)
                score += _options.TapBoost;

            if (c.DistanceMeters <= 10)
                score += _options.DistanceNearBonus;
            else if (c.DistanceMeters <= 30)
                score += 1;

            if (c.LastPlayedAt.HasValue)
            {
                var secondsSinceLastPlay = (nowUtc - c.LastPlayedAt.Value).TotalSeconds;
                if (secondsSinceLastPlay < c.CooldownSeconds)
                    score -= _options.CooldownPenalty;
            }

            if (_options.EnableTourBoost && currentTourStep.HasValue && c.TourSortOrder.HasValue)
            {
                var delta = Math.Abs(c.TourSortOrder.Value - currentTourStep.Value);
                if (delta == 0) score += _options.TourBoost;
                else if (delta == 1) score += 1;
            }

            result.Add(new NarrationQueueItem
            {
                PoiId = c.PoiId,
                PoiName = c.PoiName,
                PriorityLevel = c.PriorityLevel,
                PriorityType = c.PriorityType,
                DistanceMeters = c.DistanceMeters,
                FinalPriorityScore = score,
                TriggeredAt = nowUtc,
                ExpiresAt = nowUtc.AddSeconds(30),
                AllowInterrupt = c.AllowInterrupt,
                IsTapBoosted = c.IsTapped
            });
        }

        return result
            .OrderByDescending(x => x.FinalPriorityScore)
            .ThenBy(x => x.DistanceMeters)
            .ThenBy(x => x.TriggeredAt)
            .ToList();
    }

    /// <summary>
    /// Xử lý xung đột khi nhiều POI chồng lấn: chỉ trả về POI thắng duy nhất.
    /// Tie-break: điểm cao hơn → gần hơn → được tap → chưa phát gần đây hơn.
    /// Dùng khi AllowQueueWhenConflict = false (mặc định).
    /// </summary>
    public NarrationQueueItem? ResolveConflictWinner(
        IEnumerable<PoiCandidate> candidates,
        DateTime nowUtc,
        int? currentTourStep = null)
    {
        var scored = Resolve(candidates, nowUtc, currentTourStep);
        if (scored.Count == 0) return null;

        // Đã sắp xếp: phần tử đầu là winner
        return scored[0];
    }

    /// <summary>
    /// Kiểm tra xem danh sách candidates có chứa POI chồng lấn không
    /// (>= 2 POI active trong cùng khoảng không gian).
    /// </summary>
    public static bool HasConflict(IReadOnlyList<PoiCandidate> candidates) =>
        candidates.Count >= 2;
}
