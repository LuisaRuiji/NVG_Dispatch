using System.Data;
using Microsoft.EntityFrameworkCore;
using NVGInventory.Data;

namespace NVGInventory.Security;

public sealed record PiiFieldEncryptionMigrationResult(int DispatchCustomersUpdated, int SuppliersUpdated);

public sealed class PiiFieldEncryptionMigrationService
{
    private readonly InventoryDbContext _dbContext;

    public PiiFieldEncryptionMigrationService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PiiFieldEncryptionMigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        SensitiveFieldProtector.RequireConfigured();

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var dispatchCustomersUpdated = await MigrateDispatchCustomersAsync(connection, cancellationToken);
        var suppliersUpdated = await MigrateSuppliersAsync(connection, cancellationToken);
        return new PiiFieldEncryptionMigrationResult(dispatchCustomersUpdated, suppliersUpdated);
    }

    private static async Task<int> MigrateDispatchCustomersAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT [id], [address], [contact], [contact_person], [contact_email], [phone]
            FROM [dbo].[dispatch_customers]
            WHERE ([address] IS NOT NULL AND [address_encrypted] IS NULL)
               OR ([contact] IS NOT NULL AND [contact_encrypted] IS NULL)
               OR ([contact_person] IS NOT NULL AND [contact_person_encrypted] IS NULL)
               OR ([contact_email] IS NOT NULL AND [contact_email_encrypted] IS NULL)
               OR ([phone] IS NOT NULL AND [phone_encrypted] IS NULL);
            """;

        const string updateSql = """
            UPDATE [dbo].[dispatch_customers]
            SET [address_encrypted] = COALESCE([address_encrypted], @AddressEncrypted),
                [contact_encrypted] = COALESCE([contact_encrypted], @ContactEncrypted),
                [contact_person_encrypted] = COALESCE([contact_person_encrypted], @ContactPersonEncrypted),
                [contact_email_encrypted] = COALESCE([contact_email_encrypted], @ContactEmailEncrypted),
                [phone_encrypted] = COALESCE([phone_encrypted], @PhoneEncrypted),
                [address] = NULL,
                [contact] = NULL,
                [contact_person] = NULL,
                [contact_email] = NULL,
                [phone] = NULL
            WHERE [id] = @Id;
            """;

        return await MigrateRowsAsync(
            connection,
            selectSql,
            updateSql,
            static reader => new Dictionary<string, object?>
            {
                ["@Id"] = reader.GetGuid(reader.GetOrdinal("id")),
                ["@AddressEncrypted"] = ProtectNullable(reader, "address"),
                ["@ContactEncrypted"] = ProtectNullable(reader, "contact"),
                ["@ContactPersonEncrypted"] = ProtectNullable(reader, "contact_person"),
                ["@ContactEmailEncrypted"] = ProtectNullable(reader, "contact_email"),
                ["@PhoneEncrypted"] = ProtectNullable(reader, "phone")
            },
            cancellationToken);
    }

    private static async Task<int> MigrateSuppliersAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT [id], [contact_phone], [contact_email], [address]
            FROM [dbo].[suppliers]
            WHERE ([contact_phone] IS NOT NULL AND [contact_phone_encrypted] IS NULL)
               OR ([contact_email] IS NOT NULL AND [contact_email_encrypted] IS NULL)
               OR ([address] IS NOT NULL AND [address_encrypted] IS NULL);
            """;

        const string updateSql = """
            UPDATE [dbo].[suppliers]
            SET [contact_phone_encrypted] = COALESCE([contact_phone_encrypted], @ContactPhoneEncrypted),
                [contact_email_encrypted] = COALESCE([contact_email_encrypted], @ContactEmailEncrypted),
                [address_encrypted] = COALESCE([address_encrypted], @AddressEncrypted),
                [contact_phone] = NULL,
                [contact_email] = NULL,
                [address] = NULL
            WHERE [id] = @Id;
            """;

        return await MigrateRowsAsync(
            connection,
            selectSql,
            updateSql,
            static reader => new Dictionary<string, object?>
            {
                ["@Id"] = reader.GetGuid(reader.GetOrdinal("id")),
                ["@ContactPhoneEncrypted"] = ProtectNullable(reader, "contact_phone"),
                ["@ContactEmailEncrypted"] = ProtectNullable(reader, "contact_email"),
                ["@AddressEncrypted"] = ProtectNullable(reader, "address")
            },
            cancellationToken);
    }

    private static async Task<int> MigrateRowsAsync(
        IDbConnection connection,
        string selectSql,
        string updateSql,
        Func<IDataRecord, IReadOnlyDictionary<string, object?>> buildParameters,
        CancellationToken cancellationToken)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();

        await using (var selectCommand = CreateCommand(connection, selectSql))
        await using (var reader = await selectCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(buildParameters(reader));
            }
        }

        foreach (var parameters in rows)
        {
            await using var updateCommand = CreateCommand(connection, updateSql);
            foreach (var (name, value) in parameters)
            {
                var parameter = updateCommand.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value ?? DBNull.Value;
                updateCommand.Parameters.Add(parameter);
            }

            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return rows.Count;
    }

    private static string? ProtectNullable(IDataRecord reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return SensitiveFieldProtector.Protect(reader.GetString(ordinal));
    }

    private static System.Data.Common.DbCommand CreateCommand(IDbConnection connection, string commandText)
    {
        var command = (System.Data.Common.DbCommand)connection.CreateCommand();
        command.CommandText = commandText;
        return command;
    }
}
