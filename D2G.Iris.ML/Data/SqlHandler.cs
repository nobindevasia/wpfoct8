using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Data;
using D2G.Iris.ML.Core.Enums;
using D2G.Iris.ML.Core.Models;
using D2G.Iris.ML.Core.Interfaces;

namespace D2G.Iris.ML.Data
{
    public class SqlHandler : ISqlHandler
    {
        private SqlConnectionStringBuilder _builder;
        private readonly string _tableName;

        public SqlHandler(string tableName)
        {
            _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        }

        public void Connect(DatabaseConfig dbConfig)
        {
            _builder = new SqlConnectionStringBuilder
            {
                DataSource = dbConfig.Server,
                InitialCatalog = dbConfig.Database,
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 60
            };
        }

        public string GetConnectionString()
        {
            if (_builder == null)
                throw new InvalidOperationException("Database connection not initialized.");
            return _builder.ConnectionString;
        }

        public void SaveToSql(
            string tableName,
            IDataView data,
            string[] featureNames,
            string targetField,
            ModelType modelType)
        {
            var destTable = string.IsNullOrWhiteSpace(tableName) ? _tableName : tableName;
            if (string.IsNullOrWhiteSpace(destTable))
                throw new ArgumentException("Table name must be provided.", nameof(tableName));

            // Parse database name from table name if specified (e.g., [Database].[Schema].[Table])
            string outputDatabase = null;
            string tableNameOnly = destTable;

            var cleanedName = destTable.Replace("[", "").Replace("]", "");
            var parts = cleanedName.Split('.');

            if (parts.Length == 3)
            {
                // Format: Database.Schema.Table
                outputDatabase = parts[0];
                tableNameOnly = $"[{parts[1]}].[{parts[2]}]";
            }
            else if (parts.Length == 2)
            {
                // Format: Schema.Table (use current database)
                tableNameOnly = $"[{parts[0]}].[{parts[1]}]";
            }
            else if (parts.Length == 1)
            {
                // Format: Table (use current database and default schema)
                tableNameOnly = $"[{parts[0]}]";
            }

            var dataTable = new DataTable();

            foreach (var feature in featureNames)
            {
                dataTable.Columns.Add(feature, typeof(float));
            }

            Type targetDotNetType = modelType switch
            {
                ModelType.BinaryClassification => typeof(long),
                ModelType.MultiClassClassification => typeof(long),
                _ => typeof(float)
            };
            dataTable.Columns.Add(targetField, targetDotNetType);

            var featuresCol = data.Schema.GetColumnOrNull("Features");
            var targetCol = data.Schema.GetColumnOrNull(targetField);

            if (!featuresCol.HasValue)
                throw new InvalidOperationException("Features column not found");
            if (!targetCol.HasValue)
                throw new InvalidOperationException($"Target column '{targetField}' not found");

            // CRITICAL: Enumerate IDataView using EXISTING connection BEFORE opening new connection
            // This is essential when data is backed by SQL queries (views, tables)

            // Use the existing connection string (same database context as the source)
            using (var sourceConnection = new SqlConnection(GetConnectionString()))
            {
                sourceConnection.Open();

                // Get cursor for all needed columns
                DataViewRowCursor cursor;
                try
                {
                    cursor = data.GetRowCursor(data.Schema);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to get data cursor: {ex.Message}.", ex);
                }

                var featuresGetter = cursor.GetGetter<VBuffer<float>>(featuresCol.Value);
                var targetType = targetCol.Value.Type;

                ValueGetter<float> targetFloatGetter = null;
                ValueGetter<long> targetLongGetter = null;
                ValueGetter<bool> targetBoolGetter = null;

                if (targetType is NumberDataViewType numType)
                {
                    if (numType.RawType == typeof(long))
                        targetLongGetter = cursor.GetGetter<long>(targetCol.Value);
                    else
                        targetFloatGetter = cursor.GetGetter<float>(targetCol.Value);
                }
                else if (targetType is BooleanDataViewType)
                {
                    targetBoolGetter = cursor.GetGetter<bool>(targetCol.Value);
                }

                // Enumerate and populate DataTable while source connection is active
                var featureBuffer = default(VBuffer<float>);
                try
                {
                    while (cursor.MoveNext())
                    {
                        var row = dataTable.NewRow();

                        featuresGetter(ref featureBuffer);
                        var denseValues = featureBuffer.GetValues().ToArray();

                        for (int i = 0; i < featureNames.Length && i < denseValues.Length; i++)
                        {
                            row[featureNames[i]] = denseValues[i];
                        }

                        if (targetLongGetter != null)
                        {
                            long val = 0;
                            targetLongGetter(ref val);
                            row[targetField] = val;
                        }
                        else if (targetFloatGetter != null)
                        {
                            float val = 0;
                            targetFloatGetter(ref val);
                            row[targetField] = modelType == ModelType.BinaryClassification ?
                                (val > 0.5f ? 1L : 0L) : val;
                        }
                        else if (targetBoolGetter != null)
                        {
                            bool val = false;
                            targetBoolGetter(ref val);
                            row[targetField] = modelType == ModelType.BinaryClassification ?
                                (val ? 1L : 0L) : (val ? 1f : 0f);
                        }

                        dataTable.Rows.Add(row);
                    }
                }
                finally
                {
                    cursor.Dispose();
                }
            } // Source connection closes here - data is now fully in memory

            // Now open NEW connection for writing (can be different database)
            using var connection = new SqlConnection(GetConnectionString());
            connection.Open();

            // If output database is specified and different, switch database context
            if (!string.IsNullOrEmpty(outputDatabase))
            {
                using var useDbCmd = new SqlCommand($"USE [{outputDatabase}]", connection);
                useDbCmd.ExecuteNonQuery();
                Console.WriteLine($"Switched to database: {outputDatabase}");
            }

            var columnDefinitions = featureNames
                .Select(f => $"[{f}] FLOAT")
                .Concat(new[]
                {
                    $"[{targetField}] {(modelType == ModelType.Regression ? "FLOAT" : "BIGINT")}",
                    "ProcessedDateTime DATETIME DEFAULT GETDATE()"
                });

            string createSql = $@"
                    IF OBJECT_ID(N'{tableNameOnly}', N'U') IS NOT NULL
                    DROP TABLE {tableNameOnly};
                    CREATE TABLE {tableNameOnly} (
                    {string.Join(",\n    ", columnDefinitions)}
                    );";

            using (var cmd = new SqlCommand(createSql, connection))
            {
                cmd.ExecuteNonQuery();
            }

            using var bulk = new SqlBulkCopy(connection)
            {
                DestinationTableName = tableNameOnly,
                BatchSize = 10000,
                BulkCopyTimeout = 300
            };

            foreach (var feature in featureNames)
            {
                bulk.ColumnMappings.Add(feature, feature);
            }
            bulk.ColumnMappings.Add(targetField, targetField);

            bulk.WriteToServer(dataTable);
        }

        public void CopySqlToSql(
            string sourceTableOrView,
            string targetTable,
            string[] featureNames,
            string targetField,
            ModelType modelType,
            string whereClause = null)
        {
            if (string.IsNullOrWhiteSpace(sourceTableOrView))
                throw new ArgumentException("Source table/view name must be provided.", nameof(sourceTableOrView));
            if (string.IsNullOrWhiteSpace(targetTable))
                throw new ArgumentException("Target table name must be provided.", nameof(targetTable));

            // Parse target database name from table name if specified
            string outputDatabase = null;
            string tableNameOnly = targetTable;

            var cleanedName = targetTable.Replace("[", "").Replace("]", "");
            var parts = cleanedName.Split('.');

            if (parts.Length == 3)
            {
                outputDatabase = parts[0];
                tableNameOnly = $"[{parts[1]}].[{parts[2]}]";
            }
            else if (parts.Length == 2)
            {
                tableNameOnly = $"[{parts[0]}].[{parts[1]}]";
            }
            else if (parts.Length == 1)
            {
                tableNameOnly = $"[{parts[0]}]";
            }

            using var connection = new SqlConnection(GetConnectionString());
            connection.Open();

            // Switch database context if needed
            if (!string.IsNullOrEmpty(outputDatabase))
            {
                using var useDbCmd = new SqlCommand($"USE [{outputDatabase}]", connection);
                useDbCmd.ExecuteNonQuery();
            }

            // Build column list
            var columnList = string.Join(", ", featureNames.Select(f => $"[{f}]"));
            columnList += $", [{targetField}]";

            // Build column definitions for CREATE TABLE
            var columnDefinitions = featureNames
                .Select(f => $"[{f}] FLOAT")
                .Concat(new[]
                {
                    $"[{targetField}] {(modelType == ModelType.Regression ? "FLOAT" : "BIGINT")}",
                    "ProcessedDateTime DATETIME DEFAULT GETDATE()"
                });

            // Create target table
            string createTableSql = $@"
                IF OBJECT_ID(N'{tableNameOnly}', N'U') IS NOT NULL
                    DROP TABLE {tableNameOnly};
                CREATE TABLE {tableNameOnly} (
                    {string.Join(",\n    ", columnDefinitions)}
                );";

            using (var createCmd = new SqlCommand(createTableSql, connection))
            {
                createCmd.CommandTimeout = 120;
                createCmd.ExecuteNonQuery();
            }

            Console.WriteLine($"Created target table: {tableNameOnly}");

            // Build WHERE clause
            var whereClauseSql = string.IsNullOrWhiteSpace(whereClause) ? "" : $"WHERE {whereClause}";

            // Insert data directly from source to target (pure SQL, zero memory)
            string insertSql = $@"
                INSERT INTO {tableNameOnly} ({columnList})
                SELECT {columnList}
                FROM {sourceTableOrView}
                {whereClauseSql};";

            using (var insertCmd = new SqlCommand(insertSql, connection))
            {
                insertCmd.CommandTimeout = 300; // 5 minutes for large datasets
                var rowsAffected = insertCmd.ExecuteNonQuery();
                Console.WriteLine($"Copied {rowsAffected:N0} rows using direct SQL INSERT (zero memory usage)");
            }
        }
    }
}