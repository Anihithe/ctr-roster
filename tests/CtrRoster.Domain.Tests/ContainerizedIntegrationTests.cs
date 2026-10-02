using CtrRoster.Domain.Tests.TestHelpers;
using DotNet.Testcontainers.Builders;
using Xunit;

namespace CtrRoster.Domain.Tests;

/// <summary>
/// Tests d'intégration utilisant le package officiel .NET 'Testcontainers'.
/// Instancie des conteneurs à la volée via l'API C# sans aucun script externe.
/// </summary>
public class ContainerizedIntegrationTests
{
    [Fact]
    public async Task Testcontainers_WhenDockerIsAvailable_CanInstantiateContainer()
    {
        if (!DockerPlatformHelper.IsDockerAvailable())
        {
            // Environnement sans démon Docker (ex: Raspberry Pi en direct ou machine sans Docker) :
            // Le test s'achève avec succès sans bloquer le build.
            return;
        }

        var container = new ContainerBuilder("alpine:latest")
            .WithCommand("echo", "CTR-Roster Testcontainers OK")
            .Build();

        await container.StartAsync();
        var (stdout, _) = await container.GetLogsAsync();
        await container.DisposeAsync();

        Assert.Contains("CTR-Roster", stdout);
    }
}
