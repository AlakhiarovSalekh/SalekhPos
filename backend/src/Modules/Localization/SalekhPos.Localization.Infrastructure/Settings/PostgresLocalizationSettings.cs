using Npgsql;
using NpgsqlTypes;
using SalekhPos.Localization.Application.Settings;
using SalekhPos.Localization.Contracts.Settings;
using SalekhPos.Localization.Domain.Settings;

namespace SalekhPos.Localization.Infrastructure.Settings;

public sealed class PostgresLocalizationSettings(NpgsqlDataSource? source) : ILocalizationSettings
{
    private sealed record Row(string CountryCode, string DefaultLocale, string DefaultCurrency,
        string TimeZone, string[] SupportedLocales, int FirstDayOfWeek, long Version,
        DateTimeOffset UpdatedAt);
    private sealed record OperationRow(Row Settings, long? ExpectedVersion, bool Applied);

    public async Task<LocalizationSettingsResponse> ReadAsync(LocalizationIdentity identity,
        Guid organizationId, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (organizationId == Guid.Empty) throw new ArgumentException("Organization is required.");
        var data = source ?? throw new LocalizationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, organizationId, identity, cancellationToken);
        await Demand(connection, transaction, organizationId, identity, "localization.view", cancellationToken);
        var row = await ReadSettings(connection, transaction, organizationId, cancellationToken)
            ?? throw new LocalizationNotFoundException();
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(row);
    }

    public async Task<LocalizationWriteResult> UpdateAsync(LocalizationIdentity identity,
        UpdateLocalizationCommand command, CancellationToken cancellationToken)
    {
        identity.Validate();
        if (command.OperationId == Guid.Empty) throw new ArgumentException("Operation ID is required.");
        var settings = command.ToSettings();
        var data = source ?? throw new LocalizationUnavailableException();
        await using var connection = await data.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await Prepare(connection, transaction, settings.OrganizationId, identity, cancellationToken);
        await Demand(connection, transaction, settings.OrganizationId, identity, "localization.manage", cancellationToken);
        await LockOrganization(connection, transaction, settings.OrganizationId, cancellationToken);

        var replay = await ReadOperation(connection, transaction, settings.OrganizationId,
            command.OperationId, cancellationToken);
        if (replay is not null)
        {
            EnsureEquivalent(replay, settings, command.ExpectedVersion);
            await transaction.CommitAsync(cancellationToken);
            return new(ToResponse(replay.Settings), false);
        }
        var current = await ReadSettings(connection, transaction, settings.OrganizationId, cancellationToken);
        Row written;
        if (current is null)
        {
            if (command.ExpectedVersion is not null) throw new LocalizationConflictException();
            written = await InsertSettings(connection, transaction, settings, identity, cancellationToken);
        }
        else
        {
            if (command.ExpectedVersion != current.Version) throw new LocalizationConflictException();
            written = await UpdateSettings(connection, transaction, settings, identity,
                current.Version, cancellationToken);
        }

        await InsertOperation(connection, transaction, settings.OrganizationId, command.OperationId,
            written, command.ExpectedVersion, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ToResponse(written), true);
    }

    private static async Task<Row> InsertSettings(NpgsqlConnection connection, NpgsqlTransaction transaction,
        OrganizationLocalization settings, LocalizationIdentity identity, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO localization.organization_settings(organization_id,country_code,default_locale,
              default_currency,time_zone,supported_locales,first_day_of_week,issuer,subject)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            RETURNING country_code,default_locale,default_currency,time_zone,supported_locales,
              first_day_of_week,row_version,updated_at
            """, connection, transaction);
        BindSettings(command, settings, identity);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LocalizationUnavailableException();
        return ReadRow(reader);
    }
    private static async Task<Row> UpdateSettings(NpgsqlConnection connection, NpgsqlTransaction transaction,
        OrganizationLocalization settings, LocalizationIdentity identity, long expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE localization.organization_settings
            SET country_code=$2,default_locale=$3,default_currency=$4,time_zone=$5,supported_locales=$6,
              first_day_of_week=$7,row_version=row_version+1,issuer=$8,subject=$9,updated_at=statement_timestamp()
            WHERE organization_id=$1 AND row_version=$10
            RETURNING country_code,default_locale,default_currency,time_zone,supported_locales,
              first_day_of_week,row_version,updated_at
            """, connection, transaction);
        BindSettings(command, settings, identity);
        command.Parameters.AddWithValue(expectedVersion);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LocalizationConflictException();
        return ReadRow(reader);
    }

    private static void BindSettings(NpgsqlCommand command, OrganizationLocalization settings,
        LocalizationIdentity identity)
    {
        command.Parameters.AddWithValue(settings.OrganizationId);
        command.Parameters.AddWithValue(settings.CountryCode);
        command.Parameters.AddWithValue(settings.DefaultLocale);
        command.Parameters.AddWithValue(settings.DefaultCurrency);
        command.Parameters.AddWithValue(settings.TimeZone);
        command.Parameters.AddWithValue(settings.SupportedLocales.ToArray());
        command.Parameters.AddWithValue(settings.FirstDayOfWeek);
        command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject);
    }
    private static async Task<Row?> ReadSettings(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT country_code,default_locale,default_currency,time_zone,supported_locales,
              first_day_of_week,row_version,updated_at
            FROM localization.organization_settings WHERE organization_id=$1
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRow(reader) : null;
    }

    private static async Task InsertOperation(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid operationId, Row row, long? expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO localization.operations(organization_id,operation_id,country_code,default_locale,
              default_currency,time_zone,supported_locales,first_day_of_week,expected_version,version_after,
              applied,updated_at_after)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,true,$11)
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(operationId);
        command.Parameters.AddWithValue(row.CountryCode); command.Parameters.AddWithValue(row.DefaultLocale);
        command.Parameters.AddWithValue(row.DefaultCurrency); command.Parameters.AddWithValue(row.TimeZone);
        command.Parameters.AddWithValue(row.SupportedLocales); command.Parameters.AddWithValue(row.FirstDayOfWeek);
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Bigint,
            Value = expectedVersion.HasValue ? expectedVersion.Value : DBNull.Value
        });
        command.Parameters.AddWithValue(row.Version); command.Parameters.AddWithValue(row.UpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private static async Task<OperationRow?> ReadOperation(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, Guid operationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT country_code,default_locale,default_currency,time_zone,supported_locales,
              first_day_of_week,version_after,updated_at_after,expected_version,applied
            FROM localization.operations WHERE organization_id=$1 AND operation_id=$2
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(operationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var row = new Row(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetFieldValue<string[]>(4), reader.GetInt32(5), reader.GetInt64(6),
            reader.GetFieldValue<DateTimeOffset>(7));
        long? expected = reader.IsDBNull(8) ? null : reader.GetInt64(8);
        return new(row, expected, reader.GetBoolean(9));
    }

    private static void EnsureEquivalent(OperationRow replay, OrganizationLocalization settings, long? expectedVersion)
    {
        if (replay.ExpectedVersion != expectedVersion
            || replay.Settings.CountryCode != settings.CountryCode
            || replay.Settings.DefaultLocale != settings.DefaultLocale
            || replay.Settings.DefaultCurrency != settings.DefaultCurrency
            || replay.Settings.TimeZone != settings.TimeZone
            || replay.Settings.FirstDayOfWeek != settings.FirstDayOfWeek
            || !replay.Settings.SupportedLocales.SequenceEqual(settings.SupportedLocales, StringComparer.Ordinal))
            throw new LocalizationConflictException();
    }

    private static Row ReadRow(NpgsqlDataReader reader) => new(reader.GetString(0), reader.GetString(1),
        reader.GetString(2), reader.GetString(3), reader.GetFieldValue<string[]>(4), reader.GetInt32(5),
        reader.GetInt64(6), reader.GetFieldValue<DateTimeOffset>(7));
    private static LocalizationSettingsResponse ToResponse(Row row) => new(row.CountryCode,
        row.DefaultLocale, row.DefaultCurrency, row.TimeZone, row.SupportedLocales,
        row.FirstDayOfWeek, row.Version, row.UpdatedAt);
    private static async Task LockOrganization(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended($1,0))", connection, transaction);
        command.Parameters.AddWithValue($"localization:{organizationId:D}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task Prepare(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, LocalizationIdentity identity, CancellationToken cancellationToken)
    {
        await using var safety = new NpgsqlCommand("""
            SELECT current_user='salekhpos_runtime' AND EXISTS(
              SELECT FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
              WHERE n.nspname='localization' AND c.relname='organization_settings'
                AND c.relrowsecurity AND c.relforcerowsecurity
                AND c.relowner<>(SELECT oid FROM pg_roles WHERE rolname=current_user))
            """, connection, transaction);
        if (await safety.ExecuteScalarAsync(cancellationToken) is not true)
            throw new LocalizationUnavailableException();
        await using var context = new NpgsqlCommand("""
            SELECT set_config('app.organization_id',$1,true),set_config('app.issuer',$2,true),
              set_config('app.subject',$3,true)
            """, connection, transaction);
        context.Parameters.AddWithValue(organizationId.ToString());
        context.Parameters.AddWithValue(identity.Issuer); context.Parameters.AddWithValue(identity.Subject);
        await context.ExecuteNonQueryAsync(cancellationToken);
    }
    private static async Task Demand(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid organizationId, LocalizationIdentity identity, string permission,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS(SELECT FROM access.memberships m
              JOIN access.permission_grants g ON g.organization_id=m.organization_id
                AND g.membership_id=m.membership_id
              WHERE m.organization_id=$1 AND m.issuer=$2 AND m.subject=$3 AND m.is_active
                AND m.valid_from<=statement_timestamp()
                AND (m.valid_until IS NULL OR m.valid_until>statement_timestamp())
                AND g.permission=$4 AND g.scope_kind='organization')
            """, connection, transaction);
        command.Parameters.AddWithValue(organizationId); command.Parameters.AddWithValue(identity.Issuer);
        command.Parameters.AddWithValue(identity.Subject); command.Parameters.AddWithValue(permission);
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
            throw new LocalizationDeniedException();
    }
}
