using System.Data.OleDb;
using Dapper;
using Npgsql;

public class AccessToPostgresSync
{
    private readonly string _accessConnStr = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=C:\ruta\basedatos.accdb;";
    private readonly string _postgresConnStr = "Host=localhost;Database=mi_db;Username=usuario;Password=clave;";

    public async Task<int> SincronizarClientesAsync()
    {
        int registrosSincronizados = 0;

        // 1. Leer datos modificados desde Access
        using var accessConn = new OleDbConnection(_accessConnStr);
        var queryAccess = @"SELECT Id, Nombre, Email, FechaModificacion 
                            FROM Clientes 
                            WHERE Sincronizado = False OR FechaModificacion > @UltimaSync";
        
        // Usamos dynamic para flexibilidad con esquemas antiguos
        var clientesNuevos = await accessConn.QueryAsync(queryAccess, new { UltimaSync = DateTime.Now.AddDays(-1) });

        if (!clientesNuevos.Any()) return 0;

        // 2. Insertar/Actualizar en PostgreSQL (Upsert)
        using var pgConn = new NpgsqlConnection(_postgresConnStr);
        
        var queryPostgres = @"
            INSERT INTO clientes (id, nombre, email, fecha_modificacion)
            VALUES (@Id, @Nombre, @Email, @FechaModificacion)
            ON CONFLICT (id) DO UPDATE 
            SET nombre = EXCLUDED.nombre, 
                email = EXCLUDED.email, 
                fecha_modificacion = EXCLUDED.fecha_modificacion
            RETURNING id;";

        foreach (var cliente in clientesNuevos)
        {
            await pgConn.ExecuteAsync(queryPostgres, cliente);
            registrosSincronizados++;
        }

        // 3. Marcar como sincronizado en Access (opcional, si tienes permisos de escritura)
        // await accessConn.ExecuteAsync("UPDATE Clientes SET Sincronizado = True WHERE Id IN (...)", ...);

        return registrosSincronizados;
    }
}