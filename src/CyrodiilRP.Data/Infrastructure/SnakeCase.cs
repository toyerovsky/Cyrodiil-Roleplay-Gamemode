using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CyrodiilRP.Data;

internal static class SnakeCase
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
                property.SetColumnName(Convert(property.Name));

            foreach (var key in entity.GetKeys())
                key.SetName(Convert(key.GetName() ?? ""));

            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName(Convert(foreignKey.GetConstraintName() ?? ""));

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(Convert(index.GetDatabaseName() ?? ""));
        }
    }

    private static string Convert(string name)
    {
        var snake = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && name[i - 1] != '_' && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
                    snake.Append('_');

                snake.Append(char.ToLowerInvariant(c));
            }
            else
            {
                snake.Append(c);
            }
        }

        return snake.ToString();
    }
}
