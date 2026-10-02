namespace CtrRoster.Domain.Tests.TestHelpers;

public static class DockerPlatformHelper
{
    public static bool IsDockerAvailable()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return System.IO.File.Exists(@"\\.\pipe\docker_engine");
            }
            return System.IO.File.Exists("/var/run/docker.sock");
        }
        catch
        {
            return false;
        }
    }
}
