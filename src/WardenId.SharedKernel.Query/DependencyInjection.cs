using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WardenId.SharedKernel.Query.Utilities;

namespace WardenId.SharedKernel.Query;

public static class DependencyInjection
{
    public static void AddSharedKernelQuery(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<NpgsqlConnection>(_ => 
            new (new (configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey))));
        services.AddScoped<IConnectionWrapper, ConnectionWrapper>();
        
        RegisterSqlTypeMappers();
    }

    private static void RegisterSqlTypeMappers()
    {
        SqlMapper.AddTypeHandler(new GuidListTypeHandler());
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
    }
}