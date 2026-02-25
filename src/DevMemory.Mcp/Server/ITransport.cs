namespace DevMemory.Mcp.Server;

public interface ITransport
{
    Task RunAsync(JsonRpcHandler handler, CancellationToken cancellationToken = default);
}
