using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Reflection;
using OmegaORM;
using System.Text;

namespace OmegaORM
{
    // For Primary Key
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class PrimaryKeyAttribute : Attribute
    {
        public bool IsIdentity { get; set; } = false; // For auto-increment
    }

    // For Foreign Key
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ForeignKeyAttribute : Attribute
    {
        public string ReferencedTable { get; }
        public string ReferencedColumn { get; }

        public ForeignKeyAttribute(string referencedTable, string referencedColumn)
        {
            ReferencedTable = referencedTable;
            ReferencedColumn = referencedColumn;
        }
    }

    // For Unique Constraint
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UniqueAttribute : Attribute { }

    // For Default Value
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DefaultValueAttribute : Attribute
    {
        public object Value { get; }
        public DefaultValueAttribute(object value)
        {
            Value = value;
        }
    }

    // For Relationships
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OneToOneAttribute : Attribute
    {
        public string ForeignKeyProperty { get; }
        public OneToOneAttribute(string foreignKeyProperty)
        {
            ForeignKeyProperty = foreignKeyProperty;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class OneToManyAttribute : Attribute
    {
        public string ForeignKeyProperty { get; }
        public OneToManyAttribute(string foreignKeyProperty)
        {
            ForeignKeyProperty = foreignKeyProperty;
        }
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ManyToManyAttribute : Attribute
    {
        public string JoinTable { get; }
        public string JoinColumn { get; }
        public string InverseJoinColumn { get; }

        public ManyToManyAttribute(string joinTable, string joinColumn, string inverseJoinColumn)
        {
            JoinTable = joinTable;
            JoinColumn = joinColumn;
            InverseJoinColumn = inverseJoinColumn;
        }
    }

    // For Column Mapping
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public bool IsNullable { get; set; } = true;
        public int Length { get; set; } = -1; // For variable-length columns
        public ColumnAttribute(string name)
        {
            Name = name;
        }
    }

    // For Table Mapping
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TableAttribute : Attribute
    {
        public string Name { get; }
        public TableAttribute(string name)
        {
            Name = name;
        }
    }




    public static class SchemaSynchronizer
    {
        public static void SynchronizeDatabase(string connectionString, IEnumerable<Type> modelTypes)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                var existingTables = GetDatabaseTables(connection);

                foreach (var modelType in modelTypes)
                {
                    var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
                    if (tableAttr == null)
                        continue;

                    var tableName = tableAttr.Name;

                    if (existingTables.ContainsKey(tableName))
                    {
                        UpdateTableSchema(connection, tableName, modelType, existingTables[tableName]);
                    }
                    else
                    {
                        CreateTable(connection, tableName, modelType);
                    }
                }
            }
        }

        private static Dictionary<string, Dictionary<string, string>> GetDatabaseTables(SqlConnection connection)
        {
            var tables = new Dictionary<string, Dictionary<string, string>>();

            var query = "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS";
            using (var command = new SqlCommand(query, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var tableName = reader.GetString(0);
                    var columnName = reader.GetString(1);
                    var dataType = reader.GetString(2);

                    if (!tables.ContainsKey(tableName))
                        tables[tableName] = new Dictionary<string, string>();

                    tables[tableName][columnName] = dataType;
                }
            }

            return tables;
        }

        private static void CreateTable(SqlConnection connection, string tableName, Type modelType)
        {
            var createScript = SchemaGenerator.GenerateTableScript(modelType);
            using (var command = new SqlCommand(createScript, connection))
            {
                command.ExecuteNonQuery();
                Console.WriteLine($"Created table: {tableName}");
            }
        }

        private static void UpdateTableSchema(SqlConnection connection, string tableName, Type modelType, Dictionary<string, string> existingColumns)
        {
            var properties = modelType.GetProperties()
                .Where(p => p.IsDefined(typeof(ColumnAttribute)))
                .ToList();

            foreach (var property in properties)
            {
                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                var columnName = columnAttr.Name;
                var columnType = SchemaGenerator.GetSqlType(property.PropertyType, columnAttr.Length);

                if (!existingColumns.ContainsKey(columnName))
                {
                    var alterScript = $"ALTER TABLE [{tableName}] ADD [{columnName}] {columnType} {(columnAttr.IsNullable ? "NULL" : "NOT NULL")};";
                    using (var command = new SqlCommand(alterScript, connection))
                    {
                        command.ExecuteNonQuery();
                        Console.WriteLine($"Added column: {columnName} to table: {tableName}");
                    }
                }
                else
                {
                    // You can add logic here to check if the column's type or constraints have changed and handle modifications accordingly
                }
            }
        }
    }
}
public static class SchemaGenerator
{
    public static string GenerateTableScript(Type modelType)
    {
        if (!modelType.IsDefined(typeof(TableAttribute), false))
            throw new InvalidOperationException($"Class {modelType.Name} does not have a [Table] attribute.");

        var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
        var tableName = tableAttr.Name;

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE [{tableName}] (");

        var properties = modelType.GetProperties();
        var primaryKeys = new List<string>();
        foreach (var property in properties)
        {
            var columnScript = GenerateColumnScript(property);
            if (!string.IsNullOrEmpty(columnScript))
            {
                sb.AppendLine(columnScript + ",");
            }

            if (property.IsDefined(typeof(PrimaryKeyAttribute), false))
            {
                primaryKeys.Add($"[{property.GetCustomAttribute<ColumnAttribute>().Name}]");
            }
        }

        if (primaryKeys.Any())
        {
            sb.AppendLine($"    PRIMARY KEY ({string.Join(", ", primaryKeys)})");
        }

        sb.AppendLine(");");
        return sb.ToString();
    }

    private static string GenerateColumnScript(PropertyInfo property)
    {
        if (!property.IsDefined(typeof(ColumnAttribute), false)) return null;

        var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
        var columnName = columnAttr.Name;
        var columnType = GetSqlType(property.PropertyType, columnAttr.Length);
        var isNullable = columnAttr.IsNullable ? "NULL" : "NOT NULL";

        var sb = new StringBuilder();
        sb.Append($"    [{columnName}] {columnType} {isNullable}");

        if (property.IsDefined(typeof(PrimaryKeyAttribute), false))
        {
            var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
            if (pkAttr.IsIdentity)
            {
                sb.Append(" IDENTITY(1,1)");
            }
        }

        if (property.IsDefined(typeof(DefaultValueAttribute), false))
        {
            var defaultAttr = property.GetCustomAttribute<DefaultValueAttribute>();
            sb.Append($" DEFAULT '{defaultAttr.Value}'");
        }

        return sb.ToString();
    }

    public static string GetSqlType(Type type, int length)
    {
        if (type == typeof(int)) return "INT";
        if (type == typeof(string)) return length > 0 ? $"NVARCHAR({length})" : "NVARCHAR(MAX)";
        if (type == typeof(DateTime)) return "DATETIME";
        if (type == typeof(bool)) return "BIT";
        if (type == typeof(decimal)) return "DECIMAL(18, 2)";
        if (type == typeof(float)) return "FLOAT";
        if (type == typeof(double)) return "DOUBLE";

        throw new NotSupportedException($"Type {type.Name} is not supported.");
    }
}
namespace OmegaORM
    {
        public class OmegaContext
        {
            private readonly string _connectionString;

            public OmegaContext(string connectionString)
            {
                _connectionString = connectionString;
            }

            public void SynchronizeDatabase(IEnumerable<Type> modelTypes)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var existingTables = GetDatabaseTables(connection);

                    foreach (var modelType in modelTypes)
                    {
                        var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
                        if (tableAttr == null)
                            continue;

                        var tableName = tableAttr.Name;

                        if (existingTables.ContainsKey(tableName))
                        {
                            UpdateTableSchema(connection, tableName, modelType, existingTables[tableName]);
                        }
                        else
                        {
                            CreateTable(connection, tableName, modelType);
                        }
                    }
                }
            }

            private Dictionary<string, Dictionary<string, string>> GetDatabaseTables(SqlConnection connection)
            {
                var tables = new Dictionary<string, Dictionary<string, string>>();

                var query = "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS";
                using (var command = new SqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tableName = reader.GetString(0);
                        var columnName = reader.GetString(1);
                        var dataType = reader.GetString(2);

                        if (!tables.ContainsKey(tableName))
                            tables[tableName] = new Dictionary<string, string>();

                        tables[tableName][columnName] = dataType;
                    }
                }

                return tables;
            }

            private void CreateTable(SqlConnection connection, string tableName, Type modelType)
            {
                var createScript = SchemaGenerator.GenerateTableScript(modelType);
                using (var command = new SqlCommand(createScript, connection))
                {
                    command.ExecuteNonQuery();
                    Console.WriteLine($"Created table: {tableName}");
                }
            }

            private void UpdateTableSchema(SqlConnection connection, string tableName, Type modelType, Dictionary<string, string> existingColumns)
            {
                var properties = modelType.GetProperties()
                    .Where(p => p.IsDefined(typeof(ColumnAttribute)))
                    .ToList();

                foreach (var property in properties)
                {
                    var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                    var columnName = columnAttr.Name;
                    var columnType = SchemaGenerator.GetSqlType(property.PropertyType, columnAttr.Length);

                    if (!existingColumns.ContainsKey(columnName))
                    {
                        var alterScript = $"ALTER TABLE [{tableName}] ADD [{columnName}] {columnType} {(columnAttr.IsNullable ? "NULL" : "NOT NULL")};";
                        using (var command = new SqlCommand(alterScript, connection))
                        {
                            command.ExecuteNonQuery();
                            Console.WriteLine($"Added column: {columnName} to table: {tableName}");
                        }
                    }
                }
            }

            public void Save<T>(T entity)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var insertScript = SchemaGenerator.GenerateInsertScript(entity);
                    using (var command = new SqlCommand(insertScript, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }

            public List<T> Query<T>() where T : new()
            {
                var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
                if (tableAttr == null)
                    throw new InvalidOperationException($"Class {typeof(T).Name} does not have a [Table] attribute.");

                var tableName = tableAttr.Name;
                var results = new List<T>();

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var query = $"SELECT * FROM [{tableName}]";
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var obj = new T();
                            foreach (var property in typeof(T).GetProperties())
                            {
                                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                                if (columnAttr == null) continue;

                                var columnName = columnAttr.Name;
                                var value = reader[columnName];
                                property.SetValue(obj, value == DBNull.Value ? null : value);
                            }
                            results.Add(obj);
                        }
                    }
                }

                return results;
            }
        }

        public static class SchemaGenerator
        {
            public static string GenerateTableScript(Type modelType)
            {
                if (!modelType.IsDefined(typeof(TableAttribute), false))
                    throw new InvalidOperationException($"Class {modelType.Name} does not have a [Table] attribute.");

                var tableAttr = modelType.GetCustomAttribute<TableAttribute>();
                var tableName = tableAttr.Name;

                var sb = new StringBuilder();
                sb.AppendLine($"CREATE TABLE [{tableName}] (");

                var properties = modelType.GetProperties();
                var primaryKeys = new List<string>();
                foreach (var property in properties)
                {
                    var columnScript = GenerateColumnScript(property);
                    if (!string.IsNullOrEmpty(columnScript))
                    {
                        sb.AppendLine(columnScript + ",");
                    }

                    if (property.IsDefined(typeof(PrimaryKeyAttribute), false))
                    {
                        primaryKeys.Add($"[{property.GetCustomAttribute<ColumnAttribute>().Name}]");
                    }
                }

                if (primaryKeys.Any())
                {
                    sb.AppendLine($"    PRIMARY KEY ({string.Join(", ", primaryKeys)})");
                }

                sb.AppendLine(");");
                return sb.ToString();
            }

            private static string GenerateColumnScript(PropertyInfo property)
            {
                if (!property.IsDefined(typeof(ColumnAttribute), false)) return null;

                var columnAttr = property.GetCustomAttribute<ColumnAttribute>();
                var columnName = columnAttr.Name;
                var columnType = GetSqlType(property.PropertyType, columnAttr.Length);
                var isNullable = columnAttr.IsNullable ? "NULL" : "NOT NULL";

                var sb = new StringBuilder();
                sb.Append($"    [{columnName}] {columnType} {isNullable}");

                if (property.IsDefined(typeof(PrimaryKeyAttribute), false))
                {
                    var pkAttr = property.GetCustomAttribute<PrimaryKeyAttribute>();
                    if (pkAttr.IsIdentity)
                    {
                        sb.Append(" IDENTITY(1,1)");
                    }
                }

                if (property.IsDefined(typeof(DefaultValueAttribute), false))
                {
                    var defaultAttr = property.GetCustomAttribute<DefaultValueAttribute>();
                    sb.Append($" DEFAULT '{defaultAttr.Value}'");
                }

                return sb.ToString();
            }

        public static string GetSqlType(Type type, int length)
        {
            // Check if the type is nullable (e.g., Nullable<int> or int?)
            if (Nullable.GetUnderlyingType(type) != null)
            {
                type = Nullable.GetUnderlyingType(type); // Get the underlying non-nullable type
            }

            // Now handle the non-nullable type
            if (type == typeof(int)) return "INT";
            if (type == typeof(string)) return length > 0 ? $"NVARCHAR({length})" : "NVARCHAR(MAX)";
            if (type == typeof(DateTime)) return "DATETIME";
            if (type == typeof(bool)) return "BIT";
            if (type == typeof(decimal)) return "DECIMAL(18, 2)";
            if (type == typeof(float)) return "FLOAT";
            if (type == typeof(double)) return "DOUBLE";

            throw new NotSupportedException($"Type {type.Name} is not supported.");
        }


        public static string GenerateInsertScript<T>(T entity)
            {
                var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>();
                if (tableAttr == null)
                    throw new InvalidOperationException($"Class {typeof(T).Name} does not have a [Table] attribute.");

                var tableName = tableAttr.Name;

                var properties = typeof(T).GetProperties()
                    .Where(p => p.IsDefined(typeof(ColumnAttribute)))
                    .ToList();

                var columnNames = properties.Select(p => $"[{p.GetCustomAttribute<ColumnAttribute>().Name}]").ToList();
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(entity);
                    return value is string || value is DateTime || value is bool
                        ? $"'{value}'"
                        : value?.ToString() ?? "NULL";
                }).ToList();

                return $"INSERT INTO [{tableName}] ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", values)});";
            }
        }
    }


