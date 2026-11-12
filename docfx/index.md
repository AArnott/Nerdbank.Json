# Overview

Nerdbank.Json is a JSON serializer designed along the same architectural direction as Nerdbank.MessagePack while remaining fully independent from System.Text.Json and Newtonsoft.Json.

## Install

```bash
dotnet add package Nerdbank.Json
```

[![Nerdbank.Json NuGet package](https://img.shields.io/nuget/v/Nerdbank.Json.svg?label=Nerdbank.Json)](https://www.nuget.org/packages/Nerdbank.Json)

## Highlights

* Low-level UTF-8 readers and writers.
* PolyType contracts for mutable objects, records, and constructor-bound types.
* Incremental asynchronous streaming and asynchronous sequences.
* Shape-declared unions and runtime union configuration.
* Reference preservation, including optional cycles through mutable objects and collections.
* JSON Schema export and targeted deserialization.

The [Features](docs/features.md) page describes the full surface, including converters, callbacks, collections, and ASP.NET Core MVC.

## Getting started

[Getting Started](docs/getting-started.md) shows how to install the package, generate a PolyType shape, and serialize an object.
