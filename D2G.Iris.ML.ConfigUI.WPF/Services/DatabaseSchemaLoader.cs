using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using D2G.Iris.ML.Core.Models;

namespace D2G.Iris.ML.ConfigUI.WPF.Services
{
    public class DatabaseSchemaLoader : IDatabaseSchemaLoader
    {
        public List<string> LoadTableColumns(DatabaseConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.Server) ||
                string.IsNullOrWhiteSpace(config.Database) ||
                string.IsNullOrWhiteSpace(config.TableName))
            {
                throw new ArgumentException("Database configuration is incomplete.");
            }

            var connectionString = BuildConnectionString(config);
            var columns = new List<string>();

            try
            {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var (schemaName, tableName) = ParseTableName(config.TableName);

                var query = @"
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName
                    AND (@SchemaName IS NULL OR TABLE_SCHEMA = @SchemaName)
                    ORDER BY ORDINAL_POSITION";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@SchemaName", (object)schemaName ?? DBNull.Value);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    columns.Add(reader.GetString("COLUMN_NAME"));
                }

                if (columns.Count == 0)
                {
                    throw new InvalidOperationException($"Table '{config.TableName}' not found or has no columns.");
                }

                return columns;
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException($"Database error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading table columns: {ex.Message}", ex);
            }
        }

        public bool TestConnection(DatabaseConfig config)
        {
            try
            {
                var connectionString = BuildConnectionString(config);
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<string> LoadDatabases(DatabaseConfig config)
        {
            var databases = new List<string>();

            try
            {
                var connectionString = BuildConnectionString(config, useMaster: true);
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var query = @"
                    SELECT name
                    FROM sys.databases
                    WHERE state = 0
                    AND name NOT IN ('master', 'tempdb', 'model', 'msdb')
                    ORDER BY name";

                using var command = new SqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    databases.Add(reader.GetString("name"));
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading databases: {ex.Message}", ex);
            }

            return databases;
        }

        public List<TableInfo> LoadTables(DatabaseConfig config)
        {
            var tables = new List<TableInfo>();

            try
            {
                var connectionString = BuildConnectionString(config);
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var query = @"
                    SELECT
                        TABLE_SCHEMA,
                        TABLE_NAME,
                        TABLE_TYPE
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW')
                    ORDER BY TABLE_SCHEMA, TABLE_NAME";

                using var command = new SqlCommand(query, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    tables.Add(new TableInfo
                    {
                        SchemaName = reader.GetString("TABLE_SCHEMA"),
                        TableName = reader.GetString("TABLE_NAME"),
                        TableType = reader.GetString("TABLE_TYPE"),
                        FullName = $"[{reader.GetString("TABLE_SCHEMA")}].[{reader.GetString("TABLE_NAME")}]"
                    });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading tables: {ex.Message}", ex);
            }

            return tables;
        }

        public List<ColumnInfo> LoadTableSchema(DatabaseConfig config, string tableName)
        {
            var columns = new List<ColumnInfo>();

            try
            {
                var connectionString = BuildConnectionString(config);
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var (schemaName, table) = ParseTableName(tableName);

                var query = @"
                    SELECT
                        COLUMN_NAME,
                        DATA_TYPE,
                        IS_NULLABLE,
                        CHARACTER_MAXIMUM_LENGTH,
                        NUMERIC_PRECISION,
                        NUMERIC_SCALE,
                        ORDINAL_POSITION
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = @TableName
                    AND (@SchemaName IS NULL OR TABLE_SCHEMA = @SchemaName)
                    ORDER BY ORDINAL_POSITION";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableName", table);
                command.Parameters.AddWithValue("@SchemaName", (object)schemaName ?? DBNull.Value);

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    columns.Add(new ColumnInfo
                    {
                        ColumnName = reader.GetString("COLUMN_NAME"),
                        DataType = reader.GetString("DATA_TYPE"),
                        IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                        MaxLength = reader.IsDBNull("CHARACTER_MAXIMUM_LENGTH") ? null : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                        Precision = reader.IsDBNull("NUMERIC_PRECISION") ? null : reader.GetByte("NUMERIC_PRECISION"),
                        Scale = reader.IsDBNull("NUMERIC_SCALE") ? null : reader.GetInt32("NUMERIC_SCALE"),
                        OrdinalPosition = reader.GetInt32("ORDINAL_POSITION")
                    });
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error loading table schema: {ex.Message}", ex);
            }

            return columns;
        }

        public DataTable PreviewTableData(DatabaseConfig config, string tableName, int maxRows = 100)
        {
            try
            {
                var connectionString = BuildConnectionString(config);
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var query = $"SELECT TOP ({maxRows}) * FROM {tableName}";

                using var command = new SqlCommand(query, connection);
                using var adapter = new SqlDataAdapter(command);

                var dataTable = new DataTable();
                adapter.Fill(dataTable);

                return dataTable;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error previewing table data: {ex.Message}", ex);
            }
        }
        
        public long GetTableRowCount(DatabaseConfig config, string tableName)
        {
            try
            {
                var connectionString = BuildConnectionString(config);
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var query = $"SELECT COUNT(*) FROM {tableName}";

                using var command = new SqlCommand(query, connection);
                var result = command.ExecuteScalar();

                return Convert.ToInt64(result);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error getting table row count: {ex.Message}", ex);
            }
        }

        private string BuildConnectionString(DatabaseConfig config, bool useMaster = false)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = config.Server,
                InitialCatalog = useMaster ? "master" : config.Database,
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 30
            };

            return builder.ConnectionString;
        }

        private (string schema, string table) ParseTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return (null, tableName);

            tableName = tableName.Trim('[', ']');

            var parts = tableName.Split('.');

            if (parts.Length == 1)
            {
                return (null, parts[0].Trim('[', ']'));
            }
            else if (parts.Length == 2)
            {
                return (parts[0].Trim('[', ']'), parts[1].Trim('[', ']'));
            }
            else
            {
                var table = parts[parts.Length - 1].Trim('[', ']');
                var schema = string.Join(".", parts[0..^1]).Trim('[', ']');
                return (schema, table);
            }
        }
    }
}