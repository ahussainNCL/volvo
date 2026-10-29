using System.Buffers.Binary;
using System.IO.Pipes;
using Nordlink.Contracts;

// Test-only executable. It is never shipped with the gateway or selected by production code.
using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
await pipe.ConnectAsync(5000);
var request = await PipeProtocol.Read<WorkerRequest>(pipe, CancellationToken.None);
switch (args[1])
{
    case "crash": Environment.Exit(19); break;
    case "hang": await Task.Delay(Timeout.Infinite); break;
    case "oversize": var prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, PipeProtocol.MaxPayload + 1); await pipe.WriteAsync(prefix); break;
    case "malformed": await pipe.WriteAsync(new byte[] { 1, 0, 0, 0, (byte)'{' }); break;
    case "wrong-id": await PipeProtocol.Write(pipe, new WorkerResponse(1, Guid.NewGuid().ToString("N"), true, null, [], false, null, null, null, false), CancellationToken.None); break;
    case "wrong-version": await PipeProtocol.Write(pipe, new WorkerResponse(2, request.RequestId, true, null, [], false, null, null, null, false), CancellationToken.None); break;
    case "fabricated-vehicle": await PipeProtocol.Write(pipe, new WorkerResponse(1, request.RequestId, true, null, [], true, null, null, null, true), CancellationToken.None); break;
    default: await PipeProtocol.Write(pipe, new WorkerResponse(1, request.RequestId, true, null, [], false, null, null, null, false), CancellationToken.None); await Task.Delay(5000); break;
}
