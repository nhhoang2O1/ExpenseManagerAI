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
    Task<ApplicationServiceResult<AvailableBalanceResponse>> GetAvailableBalanceAsync(int? year, int? month, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<GoalResponse>> CompleteAsync(Guid id, CompleteGoalRequest request, string? idempotencyKey, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<GoalResponse>> CancelAsync(Guid id, string? ifMatch, CancellationToken cancellationToken);
    Task<ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>> GetHistoryAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class GoalsApplicationService(AppDbContext db, IUserContext userContext)
    : IGoalsApplicationService
{
    public async Task<ApplicationServiceResult<IReadOnlyList<GoalResponse>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var items = await db.Goals.AsNoTracking()
            .Where(x => x.UserId == userContext.UserId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        IReadOnlyList<GoalResponse> result = items.Select(x => x.ToResponse()).ToList();
        return ApplicationServiceResult<IReadOnlyList<GoalResponse>>.Ok(result);
    }

    public async Task<ApplicationServiceResult<GoalResponse>> CreateAsync(
        GoalRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
            return ApplicationServiceResult<GoalResponse>.BadRequest("Tên mục tiêu không được để trống.");
        var goal = new Goal
        {
            UserId = userContext.UserId, Name = name, TargetAmount = request.TargetAmount,
            CurrentAmount = 0, Status = GoalStatus.ACTIVE
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return ApplicationServiceResult<GoalResponse>.Created(goal.ToResponse(), goal.Version);
    }

    public async Task<ApplicationServiceResult<GoalResponse>> UpdateAsync(
        Guid id, GoalRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null)
            return ApplicationServiceResult<GoalResponse>.NotFound();
        if (goal.Status is GoalStatus.COMPLETED or GoalStatus.CANCELLED)
            return ApplicationServiceResult<GoalResponse>.Conflict(
                "Mục tiêu đã kết thúc và không thể chỉnh sửa.");
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version))
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        var name = request.Name.Trim();
        if (name.Length == 0)
            return ApplicationServiceResult<GoalResponse>.BadRequest("Tên mục tiêu không được để trống.");
        if (request.TargetAmount < goal.CurrentAmount)
            return ApplicationServiceResult<GoalResponse>.Conflict(
                "Target amount cannot be lower than the current balance.");
        goal.Name = name;
        goal.TargetAmount = request.TargetAmount;
        goal.Status = goal.CurrentAmount == goal.TargetAmount
            ? GoalStatus.READY_TO_COMPLETE : GoalStatus.ACTIVE;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        }
        return ApplicationServiceResult<GoalResponse>.Ok(goal.ToResponse(), goal.Version);
    }

    public async Task<ApplicationServiceResult<object?>> DeleteAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null)
            return ApplicationServiceResult<object?>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version))
            return ApplicationServiceResult<object?>.PreconditionFailed();
        if (goal.CurrentAmount > 0 || goal.Status != GoalStatus.ACTIVE)
            return ApplicationServiceResult<object?>.Conflict(
                "Mục tiêu đã có tiền hoặc đã kết thúc. Hãy dùng chức năng hủy mục tiêu để giữ lịch sử.");
        db.Goals.Remove(goal);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<object?>.PreconditionFailed();
        }
        return ApplicationServiceResult<object?>.NoContent();
    }

    public async Task<ApplicationServiceResult<GoalResponse>> AddFundsAsync(
        Guid id, AddGoalFundsRequest request, string? idempotencyKey,
        string? ifMatch, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            return ApplicationServiceResult<GoalResponse>.BadRequest("Amount must be greater than zero.");
        if (!IdempotencySupport.TryCreate(
                idempotencyKey, $"goals:{id}:add-funds", request, out var idempotency, out var keyError))
            return ApplicationServiceResult<GoalResponse>.BadRequest(keyError!);

        await using var transaction = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        await FinanceDatabaseLocks.LockUserForGoalFundingAsync(db, userContext.UserId, cancellationToken);
        var goal = transaction is null
            ? await db.Goals.SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == userContext.UserId, cancellationToken)
            : await db.Goals.FromSqlInterpolated(
                $"SELECT * FROM goals WHERE id = {id} AND user_id = {userContext.UserId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        if (goal is null)
            return ApplicationServiceResult<GoalResponse>.NotFound();

        var replay = await IdempotencySupport.FindAsync<GoalResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return ApplicationServiceResult<GoalResponse>.Conflict(
                "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
        if (replay?.Exists == true)
            return new ApplicationServiceResult<GoalResponse>(
                replay.StatusCode, goal.ToResponse(), Version: goal.Version);
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version))
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        if (goal.Status is not GoalStatus.ACTIVE)
            return ApplicationServiceResult<GoalResponse>.Conflict(
                "Chỉ có thể nạp tiền vào mục tiêu đang hoạt động.");

        var remainingAmount = goal.TargetAmount - goal.CurrentAmount;
        if (request.Amount > remainingAmount)
            return ApplicationServiceResult<GoalResponse>.Conflict(
                $"Mục tiêu chỉ còn thiếu {remainingAmount:N0} đ.");
        var funding = GoalFundingRules.Calculate(goal.TargetAmount, goal.CurrentAmount, request.Amount);
        if (funding.WasAlreadyFunded)
            return ApplicationServiceResult<GoalResponse>.Conflict("This goal is already fully funded.");
        var appliedAmount = funding.AppliedAmount;
        goal.CurrentAmount = funding.BalanceAfter;
        goal.Status = goal.CurrentAmount == goal.TargetAmount
            ? GoalStatus.READY_TO_COMPLETE : GoalStatus.ACTIVE;
        if (appliedAmount > 0)
            db.GoalHistories.Add(new GoalHistory
            {
                GoalId = goal.Id, AmountAdded = appliedAmount, RequestedAmount = request.Amount,
                BalanceAfter = goal.CurrentAmount, ActionType = GoalHistoryActionType.FUND
            });
        IdempotencySupport.Add(db, userContext.UserId, idempotency,
            StatusCodes.Status200OK, goal.ToResponse());
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return ApplicationServiceResult<GoalResponse>.Ok(goal.ToResponse(), goal.Version);
    }

    public async Task<ApplicationServiceResult<AvailableBalanceResponse>> GetAvailableBalanceAsync(
        int? year, int? month, CancellationToken cancellationToken)
    {
        var today = LocalToday();
        var selectedYear = year ?? today.Year;
        var selectedMonth = month ?? today.Month;
        if (selectedYear is < 2000 or > 2100 || selectedMonth is < 1 or > 12)
            return ApplicationServiceResult<AvailableBalanceResponse>.BadRequest(
                "year hoặc month không hợp lệ.");
        var startDay = await db.Users.AsNoTracking()
            .Where(x => x.Id == userContext.UserId)
            .Select(x => x.FinancialCycleStartDay)
            .SingleOrDefaultAsync(cancellationToken);
        if (!FinancialCycleRules.IsValidStartDay(startDay)) startDay = 1;
        var start = FinancialCycleRules.StartFor(
            new DateOnly(selectedYear, selectedMonth,
                DateTime.DaysInMonth(selectedYear, selectedMonth)), startDay);
        var end = FinancialCycleRules.EndFor(start, startDay).AddDays(1);
        var income = await db.Transactions.AsNoTracking().Where(
            x => x.UserId == userContext.UserId && x.Type == TransactionType.INCOME &&
                 x.TransactionDate >= start && x.TransactionDate < end)
            .SumAsync(x => (long?)x.Amount, cancellationToken) ?? 0L;
        var expense = await db.Transactions.AsNoTracking().Where(
            x => x.UserId == userContext.UserId && x.Type == TransactionType.EXPENSE &&
                 x.TransactionDate >= start && x.TransactionDate < end)
            .SumAsync(x => (long?)x.Amount, cancellationToken) ?? 0L;
        var reserved = await db.Goals.AsNoTracking().Where(
            x => x.UserId == userContext.UserId &&
                 (x.Status == GoalStatus.ACTIVE || x.Status == GoalStatus.READY_TO_COMPLETE))
            .SumAsync(x => (long?)x.CurrentAmount, cancellationToken) ?? 0L;
        return ApplicationServiceResult<AvailableBalanceResponse>.Ok(new AvailableBalanceResponse(
            StatisticsRules.Balance(income, expense), reserved,
            StatisticsRules.AvailableBalance(income, expense, reserved)));
    }

    public async Task<ApplicationServiceResult<GoalResponse>> CompleteAsync(
        Guid id, CompleteGoalRequest request, string? idempotencyKey,
        string? ifMatch, CancellationToken cancellationToken)
    {
        if (!IdempotencySupport.TryCreate(
                idempotencyKey, $"goals:{id}:complete", request, out var idempotency, out var keyError))
            return ApplicationServiceResult<GoalResponse>.BadRequest(keyError!);
        await using var databaseTransaction =
            await FinanceDatabaseLocks.BeginIfPostgresAsync(db, cancellationToken);
        var goal = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? await db.Goals.FromSqlInterpolated(
                $"SELECT * FROM goals WHERE id = {id} AND user_id = {userContext.UserId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken)
            : await db.Goals.SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null)
            return ApplicationServiceResult<GoalResponse>.NotFound();
        var replay = await IdempotencySupport.FindAsync<GoalResponse>(
            db, userContext.UserId, idempotency, cancellationToken);
        if (replay?.RequestConflict == true)
            return ApplicationServiceResult<GoalResponse>.Conflict(
                "Idempotency-Key đã được dùng với nội dung yêu cầu khác.");
        if (replay?.Exists == true && replay.Response is not null)
            return new ApplicationServiceResult<GoalResponse>(
                replay.StatusCode, replay.Response, Version: replay.Response.Version);
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version))
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        if (goal.Status != GoalStatus.READY_TO_COMPLETE || goal.CurrentAmount != goal.TargetAmount)
            return ApplicationServiceResult<GoalResponse>.Conflict(
                "Mục tiêu chưa tích lũy đủ hoặc đã được xử lý.");
        goal.Status = GoalStatus.COMPLETED;
        goal.CompletedAt = DateTime.UtcNow;
        db.GoalHistories.Add(new GoalHistory
        {
            GoalId = goal.Id, Goal = goal, AmountAdded = 0, BalanceAfter = goal.CurrentAmount,
            ActionType = GoalHistoryActionType.COMPLETE
        });
        var response = goal.ToResponse();
        IdempotencySupport.Add(db, userContext.UserId, idempotency,
            StatusCodes.Status200OK, response);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        }
        if (databaseTransaction is not null) await databaseTransaction.CommitAsync(cancellationToken);
        return ApplicationServiceResult<GoalResponse>.Ok(goal.ToResponse(), goal.Version);
    }

    public async Task<ApplicationServiceResult<GoalResponse>> CancelAsync(
        Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (goal is null) return ApplicationServiceResult<GoalResponse>.NotFound();
        if (!OptimisticConcurrency.IfMatchSatisfied(ifMatch, goal.Version))
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        if (goal.Status is GoalStatus.COMPLETED or GoalStatus.CANCELLED)
            return ApplicationServiceResult<GoalResponse>.Conflict("Mục tiêu đã kết thúc.");
        goal.Status = GoalStatus.CANCELLED;
        db.GoalHistories.Add(new GoalHistory
        {
            GoalId = goal.Id, Goal = goal, AmountAdded = 0, BalanceAfter = goal.CurrentAmount,
            ActionType = GoalHistoryActionType.CANCEL
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApplicationServiceResult<GoalResponse>.PreconditionFailed();
        }
        return ApplicationServiceResult<GoalResponse>.Ok(goal.ToResponse(), goal.Version);
    }

    public async Task<ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>> GetHistoryAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var exists = await db.Goals.AnyAsync(
            x => x.Id == id && x.UserId == userContext.UserId, cancellationToken);
        if (!exists)
            return ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>.NotFound();
        var items = await db.GoalHistories.AsNoTracking().Where(x => x.GoalId == id)
            .OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
        IReadOnlyList<GoalHistoryResponse> result = items.Select(x => x.ToResponse()).ToList();
        return ApplicationServiceResult<IReadOnlyList<GoalHistoryResponse>>.Ok(result);
    }

    private async Task<AvailableBalanceResponse> CalculateAvailableBalance(
        CancellationToken cancellationToken)
    {
        var income = await db.Transactions.AsNoTracking().Where(
            x => x.UserId == userContext.UserId && x.Type == TransactionType.INCOME)
            .SumAsync(x => (long?)x.Amount, cancellationToken) ?? 0L;
        var expense = await db.Transactions.AsNoTracking().Where(
            x => x.UserId == userContext.UserId && x.Type == TransactionType.EXPENSE)
            .SumAsync(x => (long?)x.Amount, cancellationToken) ?? 0L;
        var reserved = await db.Goals.AsNoTracking().Where(
            x => x.UserId == userContext.UserId &&
                 (x.Status == GoalStatus.ACTIVE || x.Status == GoalStatus.READY_TO_COMPLETE))
            .SumAsync(x => (long?)x.CurrentAmount, cancellationToken) ?? 0L;
        return new AvailableBalanceResponse(
            StatisticsRules.Balance(income, expense), reserved,
            StatisticsRules.AvailableBalance(income, expense, reserved));
    }

    private static DateOnly LocalToday()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }
}
