# F# contracts

Nerdbank.Json uses PolyType shapes to support F# records, options, lists, and discriminated unions.
The [F# sample source](https://github.com/AArnott/Nerdbank.Json/blob/main/samples/fs/Program.fs) defines a `Farm` record with an optional owner and a list of `Animal` union cases.
It serializes and deserializes both populated and empty values, failing explicitly if the restored value differs from the original.
The sample exercises `Some` and `None`, an empty list, and each declared union case.

## Shape provider

This sample explicitly obtains its root shape from `PolyType.ReflectionProvider.ReflectionTypeShapeProvider.Default`.
It is a managed reflection-provider example, not a reflection-free or NativeAOT demonstration.
Supplying a shape explicitly does not by itself make the shape provider reflection-free.

For trimming-sensitive or NativeAOT applications, use source-generated shapes for contracts supported by the generator.
A C# witness decorated with <xref:PolyType.GenerateShapeForAttribute`1> can supply shapes for referenced contracts without adding C# attributes to the F# declarations.
Pass that generated shape or provider to the serializer instead of discovering contracts through reflection.
This is an alternative integration approach, not the provider used by the current F# sample.

## Running the sample

Follow the repository's [contribution prerequisites](https://github.com/AArnott/Nerdbank.Json/blob/main/CONTRIBUTING.md) before running:

```powershell
dotnet run --project .\samples\fs\fs.fsproj -c Release
```

The program prints the serialized farm and restored union cases; its equality checks throw if round-tripping fails.
