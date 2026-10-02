using CtrRoster.Application.Availabilities.Commands;
using CtrRoster.Application.Games.Commands;
using CtrRoster.Application.Games.Queries;
using CtrRoster.Application.Sessions.Commands;
using CtrRoster.Application.Tables.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace CtrRoster.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Tables
        services.AddScoped<CreateTableHandler>();
        services.AddScoped<JoinTableHandler>();
        services.AddScoped<LeaveTableHandler>();
        services.AddScoped<DissolveTableHandler>();

        // Availabilities
        services.AddScoped<DeclareAvailabilityHandler>();
        services.AddScoped<SetAbsentHandler>();

        // Sessions
        services.AddScoped<CreateSessionHandler>();
        services.AddScoped<SetSessionCapacityHandler>();

        // Games
        services.AddScoped<AddGameHandler>();
        services.AddScoped<RemoveGameHandler>();
        services.AddScoped<ToggleGameActiveHandler>();
        services.AddScoped<GetActiveGamesHandler>();

        return services;
    }
}
