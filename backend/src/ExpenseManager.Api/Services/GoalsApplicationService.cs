using ExpenseManager.Api.Contracts;
using ExpenseManager.Api.Data;
using ExpenseManager.Api.Domain;
using ExpenseManager.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ExpenseManager.Api.Services;

public interface IGoalsApplicationService
{
    Task<ApplicationServiceResult<IReadOnlyList<GoalResponse>>> GetAllAsync(CancellationToken cancellationToken);
    Task<ApplicationServiceResult<GoalResponse>> CreateAsync(GoalRequest request, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<GoalResponse>> UpdateAsync(Guid id, GoalRequest request, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<object?>> DeleteAsync(Guid id, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<GoalResponse>> AddFundsAsync(Guid id, AddGoalFundsRequest request, string? idempotencyKey, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<GoalResponse>> WithdrawAsync(Guid id, WithdrawGoalFundsRequest request, string? idempotencyKey, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<AvailableBalanceResponse>> GetAvailableBalanceAsync(int? year, int? month, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<GoalResponse>> CancelAsync(Guid id, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>> GetHistoryAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class GoalsApplicationService(AppDbContext db, IUserContext userContext, ILogger<GoalsApplicationService> logger) : IGoalsApplicationService
{
    public async Task<ApplicationServiceResult<IReadOnlyList<GoalResponse>>> GetAllAsync(CancellationToken ct)
    {
        var goals = await db.Goals.AsNoTracking().Where(x => x.UserId == userContext.UserId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync(ct);
        var balances = await BalancesAsync(goals.Select(x => x.Id), ct);
        return ApplicationServiceResult<IReadOnlyList<GoalResponse>>.Ok(goals.Select(x => x.ToResponse(balances.GetValueOrDefault(x.Id))).ToList());
    }

    public async Task<ApplicationServiceResult<GoalResponse>> CreateAsync(GoalRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return ApplicationServiceResult<GoalResponse>.BadRequest("Tên mục tiêu không được để trống.");
        var goal = new Goal { UserId = userContext.UserId, Name = name, TargetAmount = request.TargetAmount, Status = GoalStatus.ACTIVE };
        db.Goals.Add(goal); await db.SaveChangesAsync(ct);
        return ApplicationServiceResult<GoalResponse>.Created(goal.ToResponse(0), goal.Version);
    }

    public async Task<ApplicationServiceResult<GoalResponse>> UpdateAsync(Guid id, GoalRequest request, string? ifMatch, CancellationToken ct)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userContext.UserId, ct);
        if (goal is null) return ApplicationServiceResult<GoalResponse>.NotFound();
        if (goal.Status == GoalStatus.CANCELLED) return ApplicationServiceResult<GoalResponse>.Conflict("Mục tiêu đã bị hủy và không thể chỉnh sửa.");
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version)) return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        var name = request.Name.Trim(); if (name.Length == 0) return ApplicationServiceResult<GoalResponse>.BadRequest("Tên mục tiêu không được để trống.");
        var current = await CurrentBalanceAsync(id, ct);
        goal.Name = name; goal.TargetAmount = request.TargetAmount; goal.Status = current >= goal.TargetAmount ? GoalStatus.COMPLETED : GoalStatus.ACTIVE;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return ApplicationServiceResult<GoalResponse>.PreconditionFailed(); }
        return ApplicationServiceResult<GoalResponse>.Ok(goal.ToResponse(current), goal.Version);
    }

    public async Task<ApplicationServiceResult<object?>> DeleteAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userContext.UserId, ct);
        if (goal is null) return ApplicationServiceResult<object?>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version)) return ApplicationServiceResult<object?>.PreconditionFailed();
        if (await CurrentBalanceAsync(id, ct) > 0 || goal.Status != GoalStatus.ACTIVE) return ApplicationServiceResult<object?>.Conflict("Mục tiêu đã có tiền hoặc đã kết thúc. Hãy dùng chức năng hủy mục tiêu để giữ lịch sử.");
        db.Goals.Remove(goal); try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return ApplicationServiceResult<object?>.PreconditionFailed(); }
        return ApplicationServiceResult<object?>.NoContent();
    }

    public Task<ApplicationServiceResult<GoalResponse>> AddFundsAsync(Guid id, AddGoalFundsRequest request, string? key, string? etag, CancellationToken ct) =>
        request.Amount <= 0 ? Task.FromResult(ApplicationServiceResult<GoalResponse>.BadRequest("Amount must be greater than zero.")) : ApplyChangeAsync(id, request.Amount, GoalHistoryActionType.FUND, key, etag, ct);

    public Task<ApplicationServiceResult<GoalResponse>> WithdrawAsync(Guid id, WithdrawGoalFundsRequest request, string? key, string? etag, CancellationToken ct) =>
        request.Amount <= 0 ? Task.FromResult(ApplicationServiceResult<GoalResponse>.BadRequest("Amount must be greater than zero.")) : ApplyChangeAsync(id, -request.Amount, GoalHistoryActionType.WITHDRAW, key, etag, ct);

    private async Task<ApplicationServiceResult<GoalResponse>> ApplyChangeAsync(Guid id, long delta, GoalHistoryActionType action, string? key, string? etag, CancellationToken ct)
    {
        var scope = action == GoalHistoryActionType.FUND ? "add-funds" : "withdraw";
        if (!IdempotencySupport.TryCreate(key, $"goals:{id}:{scope}", new { Amount = Math.Abs(delta) }, out var idem, out var keyError)) return ApplicationServiceResult<GoalResponse>.BadRequest(keyError!);
        await using var tx = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL" ? await db.Database.BeginTransactionAsync(ct) : null;
        await FinanceDatabaseLocks.LockUserForGoalFundingAsync(db, userContext.UserId, ct);
        var goal = tx is null ? await db.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userContext.UserId, ct) : await db.Goals.FromSqlInterpolated($"SELECT * FROM goals WHERE id = {id} AND user_id = {userContext.UserId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (goal is null) return ApplicationServiceResult<GoalResponse>.NotFound();
        var balance = await CurrentBalanceAsync(id, ct);
        var replay = await IdempotencySupport.FindAsync<GoalResponse>(db, userContext.UserId, idem, ct);
        if (replay?.RequestConflict == true) return ApplicationServiceResult<GoalResponse>.Conflict("Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
        if (replay?.Exists == true) return new ApplicationServiceResult<GoalResponse>(replay.StatusCode, replay.Response ?? goal.ToResponse(balance), Version: goal.Version);
        if (!OptimisticConcurrency.IfMatchSatisfied(etag, goal.Version)) return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        if (goal.Status == GoalStatus.CANCELLED) return ApplicationServiceResult<GoalResponse>.Conflict("Mục tiêu đã bị hủy.");
        if (action == GoalHistoryActionType.WITHDRAW && Math.Abs(delta) > balance)
        {
            var message = $"Không thể rút {Math.Abs(delta):N0} đ khỏi mục tiêu “{goal.Name}” vì hiện tại chỉ có {balance:N0} đ.";
            logger.LogWarning("{Message} UserId={UserId} GoalId={GoalId}", message, userContext.UserId, id);
            return ApplicationServiceResult<GoalResponse>.Conflict(message);
        }
        var newBalance = checked(balance + delta);
        goal.Status = newBalance >= goal.TargetAmount ? GoalStatus.COMPLETED : GoalStatus.ACTIVE;
        db.GoalHistories.Add(new GoalHistory { GoalId = goal.Id, AmountAdded = delta, ActionType = action });
        var response = goal.ToResponse(newBalance);
        IdempotencySupport.Add(db, userContext.UserId, idem, StatusCodes.Status200OK, response);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return ApplicationServiceResult<GoalResponse>.PreconditionFailed(); }
        if (tx is not null) await tx.CommitAsync(ct);
        return ApplicationServiceResult<GoalResponse>.Ok(response, goal.Version);
    }

    public async Task<ApplicationServiceResult<AvailableBalanceResponse>> GetAvailableBalanceAsync(int? year, int? month, CancellationToken ct)
    {
        var today = LocalToday(); var y = year ?? today.Year; var m = month ?? today.Month;
        if (y is < 2000 or > 2100 || m is < 1 or > 12) return ApplicationServiceResult<AvailableBalanceResponse>.BadRequest("year hoặc month không hợp lệ.");
        var startDay = await db.Users.AsNoTracking().Where(x => x.Id == userContext.UserId).Select(x => x.FinancialCycleStartDay).SingleOrDefaultAsync(ct); if (!FinancialCycleRules.IsValidStartDay(startDay)) startDay = 1;
        var start = FinancialCycleRules.StartFor(new DateOnly(y, m, DateTime.DaysInMonth(y, m)), startDay); var end = FinancialCycleRules.EndFor(start, startDay).AddDays(1);
        var income = await db.Transactions.Where(x => x.UserId == userContext.UserId && x.Type == TransactionType.INCOME && x.TransactionDate >= start && x.TransactionDate < end).SumAsync(x => (long?)x.Amount, ct) ?? 0L;
        var expense = await db.Transactions.Where(x => x.UserId == userContext.UserId && x.Type == TransactionType.EXPENSE && x.TransactionDate >= start && x.TransactionDate < end).SumAsync(x => (long?)x.Amount, ct) ?? 0L;
        var balance = StatisticsRules.Balance(income, expense); return ApplicationServiceResult<AvailableBalanceResponse>.Ok(new AvailableBalanceResponse(balance, 0, balance));
    }

    public async Task<ApplicationServiceResult<GoalResponse>> CancelAsync(Guid id, string? ifMatch, CancellationToken ct)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userContext.UserId, ct); if (goal is null) return ApplicationServiceResult<GoalResponse>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version)) return ApplicationServiceResult<GoalResponse>.PreconditionFailed(); if (goal.Status == GoalStatus.CANCELLED) return ApplicationServiceResult<GoalResponse>.Conflict("Mục tiêu đã bị hủy.");
        goal.Status = GoalStatus.CANCELLED; db.GoalHistories.Add(new GoalHistory { GoalId = goal.Id, AmountAdded = 0, ActionType = GoalHistoryActionType.CANCEL }); await db.SaveChangesAsync(ct);
        return ApplicationServiceResult<GoalResponse>.Ok(goal.ToResponse(await CurrentBalanceAsync(id, ct)), goal.Version);
    }

    public async Task<ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>> GetHistoryAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Goals.AnyAsync(x => x.Id == id && x.UserId == userContext.UserId, ct)) return ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>.NotFound();
        var items = await db.GoalHistories.AsNoTracking().Where(x => x.GoalId == id).OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToListAsync(ct);
        return ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>.Ok(items.Select(x => x.ToResponse()).ToList());
    }

    private async Task<long> CurrentBalanceAsync(Guid id, CancellationToken ct) =>
        await db.GoalHistories
            .Where(x => x.GoalId == id)
            .SumAsync(x => (long?)x.AmountAdded, ct) ?? 0L;
    private async Task<Dictionary<Guid, long>> BalancesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.ToList(); return await db.GoalHistories.Where(x => list.Contains(x.GoalId)).GroupBy(x => x.GoalId).Select(x => new { x.Key, Balance = x.Sum(y => y.AmountAdded) }).ToDictionaryAsync(x => x.Key, x => x.Balance, ct);
    }
    private static DateOnly LocalToday() { var zone = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh"); return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)); }
}
