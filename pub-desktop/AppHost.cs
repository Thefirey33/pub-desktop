using Aspire.Hosting.ApplicationModel.Docker;
using Aspire.Hosting.Publishing;
#pragma warning disable ASPIREPIPELINES003
#pragma warning disable ASPIREDOCKERFILEBUILDER001
var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("compose");

var primaryMachine = builder.AddDockerfileBuilder("primarymachine", "../vm-init", context => {
        var runner = SetupInitialDockerStage(context);
        runner.Copy("primaryVM.sh", ".");
        runner.Run("chmod +x ./primaryVM.sh");
        runner.Entrypoint(["sh", "./primaryVM.sh"]);
})
.PublishAsDockerComposeService((resource, service) =>
{
    service.Devices = ["/dev/kvm:/dev/kvm"];
    service.Privileged = true;
})
.WithBindMount("../vm-init/iso/VM1.iso", "/data/VM1.iso")
.WithVolume("primary-machine-volume", "/data")
.WithHttpEndpoint(targetPort: 6080);

// This is to avoid any governmental filtering on Ralsei.
var spoofDpiProxy = builder
    .AddContainer("proxy", "ghcr.io/unmedius/spoof-dpi", "latest")
    .WithHttpEndpoint(8080, 8080, env: "PROXY_PORT", name: "PROXY")
    .PublishAsDockerComposeService((_, service) =>
        {
            service.Restart = "unless-stopped";
            service.Ports = ["8080:8080"];
        }
    );

var frontend = builder.AddProject<Projects.pub_desktop_frontend>("frontend")
    .WithEnvironment("CLIENT_ID", builder.AddParameter("client-id", true))
    .WithEnvironment("CLIENT_SECRET", builder.AddParameter("client-secret", true))
    .WithReference(spoofDpiProxy.GetEndpoint("PROXY"))
    .WithExternalHttpEndpoints()
    .WithHttpEndpoint(8080)
    .WithReference(primaryMachine.GetEndpoint("http"));

await builder.Build().RunAsync();
return;

DockerfileStage SetupInitialDockerStage(DockerfileBuilderCallbackContext context)
{
    var runner = context.Builder.From("debian:stable");
    runner.WorkDir("/data");
    runner.Run("""
               apt-get update && apt-get install -y --no-install-recommends novnc \ 
               python3-websockify qemu-system \
               qemu-utils && apt-get clean && \
               rm -rf /var/lib/apt/lists/*
               """);
    return runner;
}