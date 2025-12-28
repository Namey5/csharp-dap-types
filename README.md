# Debug Adapter Protocol bindings for .NET
C# bindings generator for the [Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/) targeting `.NET Standard 2.0` for universal support.

Adapted from Zed's `rust` bindings.

## Usage
See [Releases](https://github.com/Namey5/csharp-dap-types/releases) for the built C# bindings:
- `Dap-vX.X.X.zip` contains the generated source files + `.csproj`
- `Dap-vX.X.X-NetStandard20.zip` contains the compiled `.NET Standard 2.0` library

Alternatively, you can clone `csharp-dap-types` as a [`git submodule`](https://git-scm.com/book/en/v2/Git-Tools-Submodules) and reference the `.csproj` directly, i.e.
```csproj
<ItemGroup>
  <ProjectReference Include="./csharp-dap-types/Dap/Dap.csproj" />
</ItemGroup>
```

To parse a received `JSON-RPC` message from the DAP you can call `Newtonsoft.Json.JsonConvert.DeserializeObject<Dap.ProtocolMessage>(string json)` -
this will automatically deserialize into the appropriate type (a subclass of `Dap.Request`, `Dap.Response` or `Dap.Event`, depending on the message's `type`):
```csharp
using Newtonsoft.Json;
using Dap;

// ...

string json = """{
  "type": "response",
  "seq": 32,
  "success": false,
  "request_seq": 78,
  "command": "attach",
  "message": "failed to attach to debugger 'test'",
  "body": {
    "error": {
      "id": 715,
      "format": "failed to attach to debugger '{_name}'",
      "variables": {
        "_name": "test"
      }
    }
  }
}""";

ProtocolMessage message = JsonConvert.DeserializeObject<ProtocolMessage>(json);
switch (message.MessageType)
{
    case MessageType.Response:
    {
        Response response = (Response)message;
        if (!response.Success)
        // if (response is ErrorResponse error)
        {
            ErrorResponse error = (ErrorResponse)response;
            throw new System.Exception($"response #{error.seq} had error: {error.message}");
        }

// ...
```

To send a new message, you can simply serialize the type to json:
```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Dap;

// ...

ErrorResponse response = new ErrorResponse(
  request.Command,
  new Message
  {
    id = 753.
    format = "failed to resolve breakpoint '{_breakpointId}' at '{path}'",
    variables = new JObject(
      new JProperty("path", path),
      new JProperty("_breakpointId", 0)),
  });
string json = JsonConvert.SerializeObject(response);
```

## Building
The actual generator is written in `rust`, so you'll need to [install the toolchain](https://rust-lang.org/tools/install/).

The bindings generator can be run using `cargo`:
```sh
cd csharp-dap-types
cargo run
```

Generated files will be placed in `csharp-dap-types/Dap` alongside some hand-written bindings in `Common.cs`.
