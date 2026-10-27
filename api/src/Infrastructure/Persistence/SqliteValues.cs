using System.Data.Common;
using System.Globalization;
using BallastLane.Domain.Enums;

namespace BallastLane.Infrastructure.Persistence;

/// <summary>
/// Converts between the domain value objects and the text representation stored in SQLite.
/// </summary>
internal static class SqliteValues
{
    public const string TimestampFormat = "O";
    public const string DateFormat = "yyyy-MM-dd";

    public static string ToText(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    public static string ToText(DateTimeOffset value) => value.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static string ToText(DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string ToText(TaskItemStatus status) => status switch
    {
        TaskItemStatus.Todo => nameof(TaskItemStatus.Todo),
        TaskItemStatus.InProgress => nameof(TaskItemStatus.InProgress),
        TaskItemStatus.Done => nameof(TaskItemStatus.Done),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status.")
    };

    public static Guid ReadGuid(DbDataReader reader, string column)
        => Guid.Parse(reader.GetString(reader.GetOrdinal(column)), CultureInfo.InvariantCulture);

    public static string ReadString(DbDataReader reader, string column)
        => reader.GetString(reader.GetOrdinal(column));

    public static string? ReadNullableString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int ReadInt32(DbDataReader reader, string column)
        => reader.GetInt32(reader.GetOrdinal(column));

    public static DateTimeOffset ReadTimestamp(DbDataReader reader, string column)
        => DateTimeOffset.Parse(
            reader.GetString(reader.GetOrdinal(column)),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

    public static DateOnly? ReadNullableDate(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : DateOnly.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);
    }

    public static TaskItemStatus ReadStatus(DbDataReader reader, string column)
    {
        var text = reader.GetString(reader.GetOrdinal(column));

        return text switch
        {
            nameof(TaskItemStatus.Todo) => TaskItemStatus.Todo,
            nameof(TaskItemStatus.InProgress) => TaskItemStatus.InProgress,
            nameof(TaskItemStatus.Done) => TaskItemStatus.Done,
            _ => throw new InvalidOperationException($"Unknown task status '{text}' was stored in the database.")
        };
    }
}
