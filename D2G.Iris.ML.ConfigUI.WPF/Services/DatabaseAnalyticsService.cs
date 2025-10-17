using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace D2G.Iris.ML.ConfigUI.WPF.Services
{
    public class DatabaseAnalyticsService : IDatabaseAnalyticsService
    {
        private readonly string _connectionString;

        public DatabaseAnalyticsService()
        {
            _connectionString = string.Empty;
        }

        public DatabaseAnalyticsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<ColumnStatistics> GetColumnStatisticsAsync(string tableName, string columnName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = $@"
                    SELECT
                        COUNT(*) as Count,
                        COUNT({columnName}) as NonNullCount,
                        AVG(CAST({columnName} AS FLOAT)) as Mean,
                        MIN({columnName}) as Min,
                        MAX({columnName}) as Max,
                        STDEV({columnName}) as StdDev,
                        VAR({columnName}) as Variance
                    FROM {tableName}
                    WHERE {columnName} IS NOT NULL";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new ColumnStatistics
                            {
                                Count = reader.GetInt32(0),
                                NonNullCount = reader.GetInt32(1),
                                Mean = reader.IsDBNull(2) ? (double?)null : reader.GetDouble(2),
                                Min = reader.IsDBNull(3) ? (double?)null : Convert.ToDouble(reader.GetValue(3)),
                                Max = reader.IsDBNull(4) ? (double?)null : Convert.ToDouble(reader.GetValue(4)),
                                StdDev = reader.IsDBNull(5) ? (double?)null : Convert.ToDouble(reader.GetValue(5)),
                                Variance = reader.IsDBNull(6) ? (double?)null : Convert.ToDouble(reader.GetValue(6))
                            };
                        }
                    }
                }
            }

            return null;
        }

        public async Task<List<ColumnStatistics>> GetColumnStatisticsAsync(string connectionString, string tableName, IEnumerable<string> columns, string? whereClause = null)
        {
            var statistics = new List<ColumnStatistics>();
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var whereCondition = string.IsNullOrWhiteSpace(whereClause) ? "" : $" WHERE {whereClause}";

                string schemaName = "dbo";
                string actualTableName = tableName;

                if (tableName.Contains('.'))
                {
                    var parts = tableName.Split('.');
                    if (parts.Length == 2)
                    {
                        schemaName = parts[0];
                        actualTableName = parts[1];
                    }
                }

                var dataTypeQuery = $@"
                    SELECT COLUMN_NAME, DATA_TYPE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = '{schemaName}'
                    AND TABLE_NAME = '{actualTableName}'";

                var dataTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (var command = new SqlCommand(dataTypeQuery, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            dataTypes[reader.GetString(0)] = reader.GetString(1);
                        }
                    }
                }

                foreach (var columnName in columns)
                {
                    // Build WHERE clause for percentiles CTE
                    var percentileWhereClause = string.IsNullOrWhiteSpace(whereClause)
                        ? $" WHERE {columnName} IS NOT NULL"
                        : $" WHERE ({whereClause}) AND {columnName} IS NOT NULL";

                    var query = $@"
                        WITH Percentiles AS (
                            SELECT DISTINCT
                                PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY {columnName}) OVER () as Q1,
                                PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY {columnName}) OVER () as Median,
                                PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY {columnName}) OVER () as Q3
                            FROM {tableName}{percentileWhereClause}
                        )
                        SELECT
                            COUNT(*) as Count,
                            COUNT({columnName}) as NonNullCount,
                            AVG(CAST({columnName} AS FLOAT)) as Mean,
                            MIN({columnName}) as Min,
                            MAX({columnName}) as Max,
                            STDEV({columnName}) as StdDev,
                            VAR({columnName}) as Variance,
                            (SELECT Q1 FROM Percentiles) as Q1,
                            (SELECT Median FROM Percentiles) as Median,
                            (SELECT Q3 FROM Percentiles) as Q3
                        FROM {tableName}{whereCondition}";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 300; // 5 minutes timeout
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                statistics.Add(new ColumnStatistics
                                {
                                    ColumnName = columnName,
                                    DataType = dataTypes.ContainsKey(columnName) ? dataTypes[columnName] : "unknown",
                                    Count = reader.GetInt32(0),
                                    NonNullCount = reader.GetInt32(1),
                                    Mean = reader.IsDBNull(2) ? (double?)null : reader.GetDouble(2),
                                    Min = reader.IsDBNull(3) ? (double?)null : Convert.ToDouble(reader.GetValue(3)),
                                    Max = reader.IsDBNull(4) ? (double?)null : Convert.ToDouble(reader.GetValue(4)),
                                    StdDev = reader.IsDBNull(5) ? (double?)null : Convert.ToDouble(reader.GetValue(5)),
                                    Variance = reader.IsDBNull(6) ? (double?)null : Convert.ToDouble(reader.GetValue(6)),
                                    Q1 = reader.IsDBNull(7) ? 0 : Convert.ToDouble(reader.GetValue(7)),
                                    Median = reader.IsDBNull(8) ? 0 : Convert.ToDouble(reader.GetValue(8)),
                                    Q3 = reader.IsDBNull(9) ? 0 : Convert.ToDouble(reader.GetValue(9))
                                });
                            }
                        }
                    }
                }
            }

            return statistics;
        }

        public async Task<Percentiles> GetPercentilesAsync(string tableName, string columnName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = $@"
                    SELECT DISTINCT
                        PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY {columnName}) OVER () as Q1,
                        PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY {columnName}) OVER () as Median,
                        PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY {columnName}) OVER () as Q3
                    FROM {tableName}
                    WHERE {columnName} IS NOT NULL";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new Percentiles
                            {
                                Q1 = reader.IsDBNull(0) ? 0 : Convert.ToDouble(reader.GetValue(0)),
                                Median = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1)),
                                Q3 = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2))
                            };
                        }
                    }
                }
            }

            return null;
        }

        public async Task<List<HistogramBin>> GetHistogramAsync(string tableName, string columnName, int binCount = 10)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = $@"
                    WITH Stats AS (
                        SELECT
                            MIN({columnName}) as MinVal,
                            MAX({columnName}) as MaxVal,
                            (MAX({columnName}) - MIN({columnName})) / {binCount} as BinWidth
                        FROM {tableName}
                        WHERE {columnName} IS NOT NULL
                    )
                    SELECT
                        FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)) as BinIndex,
                        Stats.MinVal + (FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)) * Stats.BinWidth) as BinStart,
                        Stats.MinVal + ((FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)) + 1) * Stats.BinWidth) as BinEnd,
                        COUNT(*) as Frequency
                    FROM {tableName}
                    CROSS JOIN Stats
                    WHERE {columnName} IS NOT NULL
                    GROUP BY
                        FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)),
                        Stats.MinVal,
                        Stats.BinWidth
                    ORDER BY BinIndex";

                var histogram = new List<HistogramBin>();
                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            histogram.Add(new HistogramBin
                            {
                                BinIndex = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0)),
                                BinStart = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1)),
                                BinEnd = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2)),
                                Frequency = reader.GetInt32(3)
                            });
                        }
                    }
                }

                return histogram;
            }
        }

        public async Task<List<CategoryFrequency>> GetCategoryFrequencyAsync(string tableName, string columnName, int topN = 20)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = $@"
                    SELECT TOP {topN}
                        {columnName} as Category,
                        COUNT(*) as Frequency,
                        CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER () AS DECIMAL(10,2)) as Percentage
                    FROM {tableName}
                    WHERE {columnName} IS NOT NULL
                    GROUP BY {columnName}
                    ORDER BY Frequency DESC";

                var frequencies = new List<CategoryFrequency>();
                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            frequencies.Add(new CategoryFrequency
                            {
                                Category = reader.GetValue(0)?.ToString(),
                                Frequency = reader.GetInt32(1),
                                Percentage = reader.GetDecimal(2)
                            });
                        }
                    }
                }

                return frequencies;
            }
        }

        public async Task<List<ColumnNullInfo>> GetNullAnalysisAsync(string tableName, List<string> columns)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                var selectClauses = columns.Select(col =>
                    $"SUM(CASE WHEN {col} IS NULL THEN 1 ELSE 0 END) as [{col}_NullCount]");

                var query = $@"
                    SELECT
                        COUNT(*) as TotalRows,
                        {string.Join(",\n                        ", selectClauses)}
                    FROM {tableName}";

                var results = new List<ColumnNullInfo>();
                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var totalRows = reader.GetInt32(0);

                            for (int i = 0; i < columns.Count; i++)
                            {
                                var nullCount = reader.GetInt32(i + 1);
                                results.Add(new ColumnNullInfo
                                {
                                    ColumnName = columns[i],
                                    NullCount = nullCount,
                                    NonNullCount = totalRows - nullCount,
                                    NullPercentage = (nullCount * 100.0) / totalRows
                                });
                            }
                        }
                    }
                }

                return results;
            }
        }

        public async Task<double> GetCorrelationAsync(string tableName, string column1, string column2)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = $@"
                    SELECT
                        (COUNT(*) * SUM(CAST({column1} AS FLOAT) * CAST({column2} AS FLOAT)) -
                         SUM(CAST({column1} AS FLOAT)) * SUM(CAST({column2} AS FLOAT))) /
                        SQRT(
                            (COUNT(*) * SUM(POWER(CAST({column1} AS FLOAT), 2)) - POWER(SUM(CAST({column1} AS FLOAT)), 2)) *
                            (COUNT(*) * SUM(POWER(CAST({column2} AS FLOAT), 2)) - POWER(SUM(CAST({column2} AS FLOAT)), 2))
                        ) as Correlation
                    FROM {tableName}
                    WHERE {column1} IS NOT NULL AND {column2} IS NOT NULL";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    var result = await command.ExecuteScalarAsync();
                    return result != DBNull.Value ? Convert.ToDouble(result) : 0;
                }
            }
        }

        public async Task<int> GetDistinctCountAsync(string tableName, string columnName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = $@"
                    SELECT COUNT(DISTINCT {columnName})
                    FROM {tableName}
                    WHERE {columnName} IS NOT NULL";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        public async Task<OutlierInfo> GetOutliersAsync(string tableName, string columnName, double iqrMultiplier = 1.5)
        {
            var percentiles = await GetPercentilesAsync(tableName, columnName);
            var iqr = percentiles.Q3 - percentiles.Q1;
            var lowerBound = percentiles.Q1 - (iqrMultiplier * iqr);
            var upperBound = percentiles.Q3 + (iqrMultiplier * iqr);

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var query = $@"
                    SELECT
                        COUNT(CASE WHEN {columnName} < {lowerBound} OR {columnName} > {upperBound} THEN 1 END) as OutlierCount,
                        COUNT(*) as TotalCount
                    FROM {tableName}
                    WHERE {columnName} IS NOT NULL";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            var outlierCount = reader.GetInt32(0);
                            var totalCount = reader.GetInt32(1);

                            return new OutlierInfo
                            {
                                OutlierCount = outlierCount,
                                TotalCount = totalCount,
                                OutlierPercentage = (outlierCount * 100.0) / totalCount,
                                LowerBound = lowerBound,
                                UpperBound = upperBound,
                                IQR = iqr
                            };
                        }
                    }
                }
            }

            return null;
        }

        public async Task<NumericColumnSummary> GetNumericColumnSummaryAsync(string tableName, string columnName)
        {
            var statistics = await GetColumnStatisticsAsync(tableName, columnName);
            var percentiles = await GetPercentilesAsync(tableName, columnName);
            var outliers = await GetOutliersAsync(tableName, columnName);
            var distinctCount = await GetDistinctCountAsync(tableName, columnName);

            return new NumericColumnSummary
            {
                ColumnName = columnName,
                Statistics = statistics,
                Percentiles = percentiles,
                Outliers = outliers,
                DistinctCount = distinctCount
            };
        }

        public async Task<DatasetSummary> GetDatasetSummaryAsync(string connectionString, string tableName, IEnumerable<string> columns, string? whereClause = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var whereCondition = string.IsNullOrWhiteSpace(whereClause) ? "" : $" WHERE {whereClause}";

                var countQuery = $"SELECT COUNT(*) FROM {tableName}{whereCondition}";
                long totalRows;
                using (var command = new SqlCommand(countQuery, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    totalRows = (int)await command.ExecuteScalarAsync();
                }

                var missingValueInfoList = new List<MissingValueInfo>();
                foreach (var column in columns)
                {
                    var nullCountQuery = $@"
                        SELECT
                            COUNT(*) as TotalCount,
                            SUM(CASE WHEN {column} IS NULL THEN 1 ELSE 0 END) as NullCount
                        FROM {tableName}{whereCondition}";

                    using (var command = new SqlCommand(nullCountQuery, connection))
                    {
                        command.CommandTimeout = 300; // 5 minutes timeout
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var totalCount = reader.GetInt32(0);
                                var nullCount = reader.GetInt32(1);

                                missingValueInfoList.Add(new MissingValueInfo
                                {
                                    ColumnName = column,
                                    MissingCount = nullCount,
                                    TotalCount = totalCount,
                                    MissingPercentage = totalCount > 0 ? (nullCount * 100.0) / totalCount : 0
                                });
                            }
                        }
                    }
                }

                return new DatasetSummary
                {
                    TotalRows = totalRows,
                    TotalColumns = columns.Count(),
                    MissingValues = missingValueInfoList
                };
            }
        }

        public async Task<CorrelationMatrix> GetCorrelationMatrixAsync(string connectionString, string tableName, IEnumerable<string> numericColumns, string? whereClause = null)
        {
            var columnsList = numericColumns.ToList();
            var correlationMatrix = new CorrelationMatrix
            {
                Columns = columnsList,
                Values = new double[columnsList.Count, columnsList.Count]
            };

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var whereCondition = string.IsNullOrWhiteSpace(whereClause) ? "" : $" WHERE {whereClause}";

                for (int i = 0; i < columnsList.Count; i++)
                {
                    for (int j = 0; j < columnsList.Count; j++)
                    {
                        if (i == j)
                        {
                            correlationMatrix.Values[i, j] = 1.0;
                        }
                        else if (i < j)
                        {
                            var col1 = columnsList[i];
                            var col2 = columnsList[j];

                            var nullCheckCondition = $"{col1} IS NOT NULL AND {col2} IS NOT NULL";
                            var fullWhereClause = string.IsNullOrWhiteSpace(whereClause)
                                ? $" WHERE {nullCheckCondition}"
                                : $" WHERE ({whereClause}) AND {nullCheckCondition}";

                            var query = $@"
                                SELECT
                                    (COUNT(*) * SUM(CAST({col1} AS FLOAT) * CAST({col2} AS FLOAT)) -
                                     SUM(CAST({col1} AS FLOAT)) * SUM(CAST({col2} AS FLOAT))) /
                                    NULLIF(SQRT(
                                        (COUNT(*) * SUM(POWER(CAST({col1} AS FLOAT), 2)) - POWER(SUM(CAST({col1} AS FLOAT)), 2)) *
                                        (COUNT(*) * SUM(POWER(CAST({col2} AS FLOAT), 2)) - POWER(SUM(CAST({col2} AS FLOAT)), 2))
                                    ), 0) as Correlation
                                FROM {tableName}{fullWhereClause}";

                            using (var command = new SqlCommand(query, connection))
                            {
                                command.CommandTimeout = 300; // 5 minutes timeout
                                var result = await command.ExecuteScalarAsync();
                                var correlation = result != DBNull.Value && result != null ? Convert.ToDouble(result) : 0;
                                correlationMatrix.Values[i, j] = correlation;
                                correlationMatrix.Values[j, i] = correlation;
                            }
                        }
                    }
                }
            }

            return correlationMatrix;
        }

        public async Task<bool> TestConnectionAsync(string connectionString)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<MissingValueInfo>> GetMissingValuesAnalysisAsync(string connectionString, string tableName, IEnumerable<string> columns, string? whereClause = null)
        {
            var datasetSummary = await GetDatasetSummaryAsync(connectionString, tableName, columns, whereClause);
            return datasetSummary.MissingValues;
        }

        public async Task<List<OutlierResult>> DetectOutliersAsync(string connectionString, string tableName, string columnName, OutlierDetectionMethod method, double threshold, string? whereClause = null)
        {
            var outliers = new List<OutlierResult>();
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var whereCondition = string.IsNullOrWhiteSpace(whereClause) ? "" : $" WHERE {whereClause}";

                string query = "";
                if (method == OutlierDetectionMethod.ZScore)
                {
                    query = $@"
                        WITH Stats AS (
                            SELECT AVG(CAST({columnName} AS FLOAT)) as Mean,
                                   STDEV(CAST({columnName} AS FLOAT)) as StdDev
                            FROM {tableName}{whereCondition}
                        ),
                        OutlierData AS (
                            SELECT
                                CAST({columnName} AS FLOAT) as Value,
                                ABS((CAST({columnName} AS FLOAT) - Stats.Mean) / NULLIF(Stats.StdDev, 0)) as Score
                            FROM {tableName}
                            CROSS JOIN Stats
                            WHERE ABS((CAST({columnName} AS FLOAT) - Stats.Mean) / NULLIF(Stats.StdDev, 0)) > {threshold}{(string.IsNullOrWhiteSpace(whereClause) ? "" : $" AND ({whereClause})")}
                        )
                        SELECT
                            ROW_NUMBER() OVER (ORDER BY Score DESC) as RowId,
                            Value,
                            Score
                        FROM OutlierData";
                }
                else if (method == OutlierDetectionMethod.IQR)
                {
                    query = $@"
                        WITH Percentiles AS (
                            SELECT
                                PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY {columnName}) OVER () as Q1,
                                PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY {columnName}) OVER () as Q3
                            FROM {tableName}{whereCondition}
                        ),
                        Bounds AS (
                            SELECT DISTINCT
                                Q1,
                                Q3,
                                Q1 - ({threshold} * (Q3 - Q1)) as LowerBound,
                                Q3 + ({threshold} * (Q3 - Q1)) as UpperBound
                            FROM Percentiles
                        ),
                        OutlierData AS (
                            SELECT
                                CAST({columnName} AS FLOAT) as Value,
                                CASE
                                    WHEN {columnName} < Bounds.LowerBound THEN (Bounds.Q1 - {columnName}) / NULLIF((Bounds.Q3 - Bounds.Q1), 0)
                                    ELSE ({columnName} - Bounds.Q3) / NULLIF((Bounds.Q3 - Bounds.Q1), 0)
                                END as Score
                            FROM {tableName}
                            CROSS JOIN Bounds
                            WHERE ({columnName} < Bounds.LowerBound OR {columnName} > Bounds.UpperBound){(string.IsNullOrWhiteSpace(whereClause) ? "" : $" AND ({whereClause})")}
                        )
                        SELECT
                            ROW_NUMBER() OVER (ORDER BY Score DESC) as RowId,
                            Value,
                            Score
                        FROM OutlierData";
                }
                else if (method == OutlierDetectionMethod.ModifiedZScore)
                {
                    var statsWhere = string.IsNullOrWhiteSpace(whereClause)
                        ? $" WHERE {columnName} IS NOT NULL"
                        : $" WHERE ({whereClause}) AND {columnName} IS NOT NULL";

                    var deviationsWhere = string.IsNullOrWhiteSpace(whereClause)
                        ? $" WHERE {columnName} IS NOT NULL"
                        : $" WHERE ({whereClause}) AND {columnName} IS NOT NULL";

                    var outlierWhere = string.IsNullOrWhiteSpace(whereClause)
                        ? $" WHERE {columnName} IS NOT NULL AND ABS(0.6745 * (CAST({columnName} AS FLOAT) - Deviations.MedianVal) / NULLIF(Deviations.MAD, 0)) > {threshold}"
                        : $" WHERE ({whereClause}) AND {columnName} IS NOT NULL AND ABS(0.6745 * (CAST({columnName} AS FLOAT) - Deviations.MedianVal) / NULLIF(Deviations.MAD, 0)) > {threshold}";

                    query = $@"
                        WITH Stats AS (
                            SELECT DISTINCT
                                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY {columnName}) OVER () as MedianVal
                            FROM {tableName}{statsWhere}
                        ),
                        Deviations AS (
                            SELECT DISTINCT
                                Stats.MedianVal,
                                PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY ABS(CAST({columnName} AS FLOAT) - Stats.MedianVal)) OVER () as MAD
                            FROM {tableName}
                            CROSS JOIN Stats{deviationsWhere}
                        ),
                        OutlierData AS (
                            SELECT
                                CAST({columnName} AS FLOAT) as Value,
                                ABS(0.6745 * (CAST({columnName} AS FLOAT) - Deviations.MedianVal) / NULLIF(Deviations.MAD, 0)) as Score
                            FROM {tableName}
                            CROSS JOIN Deviations{outlierWhere}
                        )
                        SELECT
                            ROW_NUMBER() OVER (ORDER BY Score DESC) as RowId,
                            Value,
                            Score
                        FROM OutlierData
                        ORDER BY Score DESC";
                }

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            outliers.Add(new OutlierResult
                            {
                                RowId = reader.GetValue(0),
                                Value = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1)),
                                Score = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2)),
                                RowData = new Dictionary<string, object>()
                            });
                        }
                    }
                }
            }

            return outliers;
        }

        public async Task<List<TableSchemaInfo>> GetTableSchemaAsync(string connectionString, string tableName)
        {
            var schema = new List<TableSchemaInfo>();
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                string schemaName = "dbo";
                string actualTableName = tableName;

                if (tableName.Contains('.'))
                {
                    var parts = tableName.Split('.');
                    if (parts.Length == 2)
                    {
                        schemaName = parts[0];
                        actualTableName = parts[1];
                    }
                }

                var query = $@"
                    SELECT
                        COLUMN_NAME,
                        DATA_TYPE,
                        IS_NULLABLE,
                        CHARACTER_MAXIMUM_LENGTH
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = '{schemaName}'
                    AND TABLE_NAME = '{actualTableName}'";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            schema.Add(new TableSchemaInfo
                            {
                                ColumnName = reader.GetString(0),
                                DataType = reader.GetString(1),
                                IsNullable = reader.GetString(2),
                                MaxLength = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)
                            });
                        }
                    }
                }
            }

            return schema;
        }

        public async Task<List<HistogramBin>> GetHistogramDataAsync(string connectionString, string tableName, string columnName, int binCount, string? whereClause = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var whereCondition = string.IsNullOrWhiteSpace(whereClause) ? "" : $" WHERE {whereClause}";

                var query = $@"
                    WITH Stats AS (
                        SELECT
                            MIN({columnName}) as MinVal,
                            MAX({columnName}) as MaxVal,
                            (MAX({columnName}) - MIN({columnName})) / {binCount} as BinWidth
                        FROM {tableName}{whereCondition}
                    )
                    SELECT
                        FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)) as BinIndex,
                        Stats.MinVal + (FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)) * Stats.BinWidth) as BinStart,
                        Stats.MinVal + ((FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)) + 1) * Stats.BinWidth) as BinEnd,
                        COUNT(*) as Count
                    FROM {tableName}
                    CROSS JOIN Stats{whereCondition}
                    GROUP BY
                        FLOOR(({columnName} - Stats.MinVal) / NULLIF(Stats.BinWidth, 0)),
                        Stats.MinVal,
                        Stats.BinWidth
                    ORDER BY BinIndex";

                var histogram = new List<HistogramBin>();
                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            histogram.Add(new HistogramBin
                            {
                                BinIndex = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0)),
                                BinStart = reader.IsDBNull(1) ? 0 : Convert.ToDouble(reader.GetValue(1)),
                                BinEnd = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2)),
                                Frequency = reader.GetInt32(3)
                            });
                        }
                    }
                }

                return histogram;
            }
        }

        public async Task<List<CategoryFrequency>> GetCategoricalDataAsync(string connectionString, string tableName, string columnName, int topN, string? whereClause = null)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                var whereCondition = string.IsNullOrWhiteSpace(whereClause) ? "" : $" WHERE {whereClause}";

                var query = $@"
                    SELECT TOP {topN}
                        {columnName} as Category,
                        COUNT(*) as Frequency,
                        CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER () AS DECIMAL(10,2)) as Percentage
                    FROM {tableName}{whereCondition}
                    GROUP BY {columnName}
                    ORDER BY Frequency DESC";

                var frequencies = new List<CategoryFrequency>();
                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            frequencies.Add(new CategoryFrequency
                            {
                                Category = reader.GetValue(0)?.ToString(),
                                Frequency = reader.GetInt32(1),
                                Percentage = reader.GetDecimal(2)
                            });
                        }
                    }
                }

                return frequencies;
            }
        }

        public async Task<string> DebugHistogramStepsAsync(string connectionString, string tableName, string columnName, int binCount, string? whereClause = null)
        {
            var histogram = await GetHistogramDataAsync(connectionString, tableName, columnName, binCount, whereClause);
            var debugMessage = $"Histogram for {columnName}: {histogram.Count} bins";
            Console.WriteLine(debugMessage);
            return debugMessage;
        }

        public async Task<string> DebugBasicOperationsAsync(string connectionString, string tableName, string? whereClause = null)
        {
            var canConnect = await TestConnectionAsync(connectionString);
            var debugMessage = $"Connection test: {canConnect}, Table: {tableName}";
            Console.WriteLine(debugMessage);
            return debugMessage;
        }

        public async Task<List<ScatterPlotPoint>> GetScatterPlotDataAsync(string connectionString, string tableName, string xColumn, string yColumn, int? maxPoints = null, string? whereClause = null)
        {
            var scatterPlotData = new List<ScatterPlotPoint>();
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var nullCheckCondition = $"{xColumn} IS NOT NULL AND {yColumn} IS NOT NULL";
                var fullWhereClause = string.IsNullOrWhiteSpace(whereClause)
                    ? $" WHERE {nullCheckCondition}"
                    : $" WHERE ({whereClause}) AND {nullCheckCondition}";

                var topClause = maxPoints.HasValue ? $"TOP {maxPoints.Value} " : "";
                var orderClause = maxPoints.HasValue ? $" ORDER BY {xColumn}, {yColumn}" : "";

                var query = $@"
                    SELECT {topClause}CAST({xColumn} AS FLOAT) as XValue,
                        CAST({yColumn} AS FLOAT) as YValue
                    FROM {tableName}{fullWhereClause}{orderClause}";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            scatterPlotData.Add(new ScatterPlotPoint
                            {
                                X = reader.IsDBNull(0) ? 0 : reader.GetDouble(0),
                                Y = reader.IsDBNull(1) ? 0 : reader.GetDouble(1)
                            });
                        }
                    }
                }
            }

            return scatterPlotData;
        }

        public async Task<List<double>> GetDistributionDataAsync(string connectionString, string tableName, string columnName, string? whereClause = null, int maxSampleSize = 10000)
        {
            var distributionData = new List<double>();
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var nullCheckCondition = $"{columnName} IS NOT NULL";
                var fullWhereClause = string.IsNullOrWhiteSpace(whereClause)
                    ? $" WHERE {nullCheckCondition}"
                    : $" WHERE ({whereClause}) AND {nullCheckCondition}";

                // Use TABLESAMPLE or TOP with random sampling for large datasets
                var query = $@"
                    SELECT TOP {maxSampleSize} CAST({columnName} AS FLOAT) as Value
                    FROM {tableName}{fullWhereClause}
                    ORDER BY NEWID()";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = 300; // 5 minutes timeout
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            distributionData.Add(reader.IsDBNull(0) ? 0 : reader.GetDouble(0));
                        }
                    }
                }
            }

            return distributionData;
        }
    }

    #region Data Models

    public class ColumnStatistics
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public int Count { get; set; }
        public int NonNullCount { get; set; }
        public double? Mean { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? StdDev { get; set; }
        public double? Variance { get; set; }
        public double? StandardDeviation => StdDev;
        public long? UniqueCount { get; set; }
        public double Q1 { get; set; }
        public double Median { get; set; }
        public double Q3 { get; set; }
    }

    public class Percentiles
    {
        public double Q1 { get; set; }
        public double Median { get; set; }
        public double Q3 { get; set; }
        public double IQR => Q3 - Q1;
    }

    public class HistogramBin
    {
        public int BinIndex { get; set; }
        public double BinStart { get; set; }
        public double BinEnd { get; set; }
        public double BinCenter => (BinStart + BinEnd) / 2.0;
        public int Frequency { get; set; }
        public long Count => Frequency;
        public string CategoryName { get; set; }
    }

    public class CategoryFrequency
    {
        public string Category { get; set; }
        public int Frequency { get; set; }
        public decimal Percentage { get; set; }
        public long Count => Frequency;
    }

    public class ColumnNullInfo
    {
        public string ColumnName { get; set; }
        public int NullCount { get; set; }
        public int NonNullCount { get; set; }
        public double NullPercentage { get; set; }
    }

    public class OutlierInfo
    {
        public int OutlierCount { get; set; }
        public int TotalCount { get; set; }
        public double OutlierPercentage { get; set; }
        public double LowerBound { get; set; }
        public double UpperBound { get; set; }
        public double IQR { get; set; }
    }

    public class NumericColumnSummary
    {
        public string ColumnName { get; set; }
        public ColumnStatistics Statistics { get; set; }
        public Percentiles Percentiles { get; set; }
        public OutlierInfo Outliers { get; set; }
        public int DistinctCount { get; set; }
    }

    public class DatasetSummary
    {
        public long TotalRows { get; set; }
        public int TotalColumns { get; set; }
        public List<MissingValueInfo> MissingValues { get; set; }
    }

    public class MissingValueInfo
    {
        public string ColumnName { get; set; }
        public int MissingCount { get; set; }
        public int TotalCount { get; set; }
        public double MissingPercentage { get; set; }
    }

    public class CorrelationMatrix
    {
        public List<string> Columns { get; set; }
        public double[,] Values { get; set; }

        public double GetCorrelation(string col1, string col2)
        {
            var index1 = Columns.IndexOf(col1);
            var index2 = Columns.IndexOf(col2);

            if (index1 == -1 || index2 == -1)
                return 0;

            return Values[index1, index2];
        }
    }

    public class TableSchemaInfo
    {
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public string IsNullable { get; set; }
        public int? MaxLength { get; set; }
    }

    public class OutlierResult
    {
        public object RowId { get; set; }
        public double Value { get; set; }
        public double Score { get; set; }
        public Dictionary<string, object> RowData { get; set; }
    }

    public class ScatterPlotPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    #endregion
}
