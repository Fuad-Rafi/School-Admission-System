using System.Data.Common;
using Admission.Api.Contracts;
using Admission.Api.Data;
using Admission.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Admission.Api.Services;

public sealed class SeatService(
    AdmissionDbContext db,
    AdmissionOptions options,
    ILogger<SeatService> logger)
{
    private const int MaxRetries = 3;

    // ---------------------------------------------------------------------
    // Classes
    // ---------------------------------------------------------------------

    public async Task<ClassCreatedResponse> CreateClassAsync(CreateClassRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (name.Length is < 1 or > 200)
        {
            throw ApiException.BadRequest(
                "INVALID_REQUEST",
                "Class name must be between 1 and 200 characters.");
        }

        if (request.TotalSeats is < 1 or > 10000)
        {
            throw ApiException.BadRequest(
                "INVALID_REQUEST",
                "Total seats must be between 1 and 10000.");
        }

        var entity = new AdmissionClass
        {
            Id = Guid.NewGuid(),
            Name = name,
            TotalSeats = request.TotalSeats,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Classes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return new ClassCreatedResponse(entity.Id, entity.Name, entity.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassResponse>> GetClassesAsync(CancellationToken cancellationToken)
    {
        var now = await DatabaseNowAsync(cancellationToken);

        var rows = await db.Classes.AsNoTracking()
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                Held = c.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > now),
                Confirmed = c.Holds.Count(h => h.Status == HoldStatus.CONFIRMED)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => ToClassResponse(r.Id, r.Name, r.TotalSeats, r.Held, r.Confirmed))
            .ToList();
    }

    public async Task<ClassResponse> GetClassAsync(Guid classId, CancellationToken cancellationToken)
    {
        var now = await DatabaseNowAsync(cancellationToken);

        var row = await db.Classes.AsNoTracking()
            .Where(c => c.Id == classId)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.TotalSeats,
                Held = c.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > now),
                Confirmed = c.Holds.Count(h => h.Status == HoldStatus.CONFIRMED)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            throw ApiException.NotFound("CLASS_NOT_FOUND", "This class no longer exists.");
        }

        return ToClassResponse(row.Id, row.Name, row.TotalSeats, row.Held, row.Confirmed);
    }

    private static ClassResponse ToClassResponse(Guid id, string name, int totalSeats, int heldSeats, int confirmedSeats)
        => new(
            id,
            name,
            totalSeats,
            heldSeats,
            confirmedSeats,
            Math.Max(0, totalSeats - heldSeats - confirmedSeats));

    // ---------------------------------------------------------------------
    // Apply / holds
    // ---------------------------------------------------------------------

    public async Task<HoldCreatedResponse> ApplyAsync(Guid classId, string studentId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ApplyOnceAsync(classId, studentId, cancellationToken);
            }
            catch (PostgresException ex) when (attempt < MaxRetries && ex.SqlState is "40001" or "40P01")
            {
                logger.LogWarning(
                    ex,
                    "Transient database conflict while applying for class {ClassId} (attempt {Attempt}); retrying.",
                    classId,
                    attempt);

                await Task.Delay(20 * attempt, cancellationToken);
            }
        }
    }

    private async Task<HoldCreatedResponse> ApplyOnceAsync(Guid classId, string studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var dbTransaction = transaction.GetDbTransaction();

        // Serialises all seat changes for this class, across every backend process.
        var totalSeats = await LockClassAsync(connection, dbTransaction, classId, cancellationToken);
        if (totalSeats is null)
        {
            throw ApiException.NotFound("CLASS_NOT_FOUND", "This class no longer exists.");
        }

        // Release overdue holds so they neither block capacity nor this student's re-apply.
        await ExpireOverdueInClassAsync(connection, dbTransaction, classId, cancellationToken);

        var (held, confirmed) = await CountOccupancyAsync(connection, dbTransaction, classId, cancellationToken);
        if (held + confirmed >= totalSeats.Value)
        {
            throw ApiException.Conflict(
                "SEAT_UNAVAILABLE",
                "All seats in this class are taken. Please choose another class.");
        }

        if (await HasActiveHoldAsync(connection, dbTransaction, classId, studentId, cancellationToken))
        {
            throw ApiException.Conflict(
                "DUPLICATE_ACTIVE_HOLD",
                "You already have an active application in this class.");
        }

        var holdId = Guid.NewGuid();
        var expiresAt = await InsertHoldAsync(
            connection,
            dbTransaction,
            holdId,
            classId,
            studentId,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new HoldCreatedResponse(holdId, classId, studentId, "HELD", expiresAt);
    }

    public async Task<HoldResponse> GetHoldAsync(Guid holdId, CancellationToken cancellationToken)
    {
        var row = await db.Holds.AsNoTracking()
            .Where(h => h.Id == holdId)
            .Select(h => new { h.Id, h.ClassId, h.Status, h.ExpiresAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            throw ApiException.NotFound("HOLD_NOT_FOUND", "This application could not be found.");
        }

        var now = await DatabaseNowAsync(cancellationToken);

        return new HoldResponse(
            row.Id,
            row.ClassId,
            row.Status.ToApiStatus(row.ExpiresAt, now),
            row.ExpiresAt);
    }

    public async Task<IReadOnlyList<MyHoldResponse>> GetMyHoldsAsync(string studentId, CancellationToken cancellationToken)
    {
        var rows = await db.Holds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .Select(h => new
            {
                h.Id,
                h.ClassId,
                ClassName = h.Class.Name,
                h.Status,
                h.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        var now = await DatabaseNowAsync(cancellationToken);

        return rows
            .Select(h => new MyHoldResponse(
                h.Id,
                h.ClassId,
                h.ClassName,
                h.Status.ToApiStatus(h.ExpiresAt, now),
                h.ExpiresAt))
            .ToList();
    }

    public async Task<ConfirmHoldResponse> ConfirmAsync(Guid holdId, string studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var dbTransaction = transaction.GetDbTransaction();

        var hold = await LockHoldAsync(connection, dbTransaction, holdId, cancellationToken);
        if (hold is null)
        {
            throw ApiException.NotFound("HOLD_NOT_FOUND", "This application could not be found.");
        }

        if (!string.Equals(hold.StudentId, studentId, StringComparison.Ordinal))
        {
            throw ApiException.Forbidden("This application belongs to another student.");
        }

        // Idempotent: a repeated confirm succeeds without changing any counts.
        if (hold.Status == HoldStatus.CONFIRMED)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ConfirmHoldResponse(holdId, "CONFIRMED");
        }

        if (hold.Status != HoldStatus.HELD)
        {
            throw ApiException.Conflict(
                "HOLD_NOT_CONFIRMABLE",
                "This application is no longer active.");
        }

        if (hold.IsExpired)
        {
            await MarkExpiredAsync(connection, dbTransaction, holdId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            throw ApiException.Conflict(
                "HOLD_NOT_CONFIRMABLE",
                "This application has expired and can no longer be confirmed.");
        }

        await ConfirmHoldRowAsync(connection, dbTransaction, holdId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ConfirmHoldResponse(holdId, "CONFIRMED");
    }

    public async Task CancelAsync(Guid holdId, string studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var dbTransaction = transaction.GetDbTransaction();

        var hold = await LockHoldAsync(connection, dbTransaction, holdId, cancellationToken);
        if (hold is null)
        {
            throw ApiException.NotFound("HOLD_NOT_FOUND", "This application could not be found.");
        }

        if (!string.Equals(hold.StudentId, studentId, StringComparison.Ordinal))
        {
            throw ApiException.Forbidden("This application belongs to another student.");
        }

        // Idempotent cancel.
        if (hold.Status == HoldStatus.CANCELLED)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (hold.Status == HoldStatus.CONFIRMED)
        {
            throw ApiException.Conflict(
                "HOLD_NOT_CANCELLABLE",
                "A confirmed admission cannot be cancelled here.");
        }

        if (hold.Status == HoldStatus.EXPIRED || hold.IsExpired)
        {
            if (hold.Status == HoldStatus.HELD)
            {
                await MarkExpiredAsync(connection, dbTransaction, holdId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            throw ApiException.Conflict(
                "HOLD_NOT_CANCELLABLE",
                "This application has already expired.");
        }

        await CancelHoldRowAsync(connection, dbTransaction, holdId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Raw SQL primitives
    // ---------------------------------------------------------------------

    private sealed record LockedHold(Guid ClassId, string StudentId, HoldStatus Status, bool IsExpired);

    private static async Task<int?> LockClassAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid classId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            "SELECT total_seats FROM classes WHERE id = @classId FOR UPDATE");

        AddParameter(command, "@classId", classId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            null => null,
            int value => value,
            _ => Convert.ToInt32(result)
        };
    }

    private static async Task ExpireOverdueInClassAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid classId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            UPDATE holds
            SET status = 'EXPIRED', ended_at = now()
            WHERE class_id = @classId
              AND status = 'HELD'
              AND expires_at <= now();
            """);

        AddParameter(command, "@classId", classId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(int Held, int Confirmed)> CountOccupancyAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid classId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT
                COUNT(*) FILTER (WHERE status = 'HELD')::int AS held,
                COUNT(*) FILTER (WHERE status = 'CONFIRMED')::int AS confirmed
            FROM holds
            WHERE class_id = @classId
              AND status IN ('HELD', 'CONFIRMED');
            """);

        AddParameter(command, "@classId", classId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0);
        }

        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    private static async Task<bool> HasActiveHoldAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid classId,
        string studentId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT EXISTS (
                SELECT 1
                FROM holds
                WHERE class_id = @classId
                  AND student_id = @studentId
                  AND status IN ('HELD', 'CONFIRMED')
            );
            """);

        AddParameter(command, "@classId", classId);
        AddParameter(command, "@studentId", studentId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private async Task<DateTimeOffset> InsertHoldAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid holdId,
        Guid classId,
        string studentId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            INSERT INTO holds (id, class_id, student_id, status, created_at, expires_at)
            VALUES (
                @id,
                @classId,
                @studentId,
                'HELD',
                now(),
                now() + (@durationSeconds * interval '1 second')
            )
            RETURNING expires_at;
            """);

        AddParameter(command, "@id", holdId);
        AddParameter(command, "@classId", classId);
        AddParameter(command, "@studentId", studentId);
        AddParameter(command, "@durationSeconds", options.HoldDurationSeconds);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return AsUtcDateTimeOffset(result);
    }

    private static async Task<LockedHold?> LockHoldAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT class_id, student_id, status, (expires_at <= now()) AS is_expired
            FROM holds
            WHERE id = @holdId
            FOR UPDATE;
            """);

        AddParameter(command, "@holdId", holdId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LockedHold(
            reader.GetGuid(0),
            reader.GetString(1),
            Enum.Parse<HoldStatus>(reader.GetString(2)),
            reader.GetBoolean(3));
    }

    private static async Task MarkExpiredAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            UPDATE holds
            SET status = 'EXPIRED', ended_at = now()
            WHERE id = @holdId AND status = 'HELD';
            """);

        AddParameter(command, "@holdId", holdId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ConfirmHoldRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            UPDATE holds
            SET status = 'CONFIRMED', confirmed_at = now(), ended_at = now()
            WHERE id = @holdId AND status = 'HELD';
            """);

        AddParameter(command, "@holdId", holdId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CancelHoldRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid holdId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            transaction,
            """
            UPDATE holds
            SET status = 'CANCELLED', ended_at = now()
            WHERE id = @holdId AND status = 'HELD';
            """);

        AddParameter(command, "@holdId", holdId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<DateTimeOffset> DatabaseNowAsync(CancellationToken cancellationToken)
    {
        var value = await db.Database
            .SqlQueryRaw<DateTime>("SELECT now() AS \"Value\"")
            .SingleAsync(cancellationToken);

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static DbCommand CreateCommand(DbConnection connection, DbTransaction transaction, string sql)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 30;
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static DateTimeOffset AsUtcDateTimeOffset(object? value)
        => value switch
        {
            DateTimeOffset dto => dto.ToUniversalTime(),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("The database returned an unexpected timestamp value.")
        };
}
