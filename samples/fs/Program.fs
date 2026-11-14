// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

open Nerdbank.Json
open PolyType
open PolyType.ReflectionProvider

type Animal =
    | Cow of name: string * weight: int
    | Horse of name: string * speed: int
    | Dog of name: string * color: string

type Farm = {
    Name: string
    Owner: string option
    Animals: Animal list
}

let serializer = JsonSerializer()
let shape = ReflectionTypeShapeProvider.Default.GetTypeShapeOrThrow<Farm>()

let roundTrip (farm: Farm) =
    // The explicit PolyType provider supports F# records, options and discriminated unions.
    // Reflection shapes are appropriate for this managed sample, not NativeAOT apps.
    let json = serializer.Serialize(&farm, shape)
    printfn "Farm as JSON: %s" json
    let restored =
        match serializer.Deserialize(json, shape) with
        | null -> failwith "The farm was null."
        | value -> value
    if restored <> farm then
        failwith "The farm did not round-trip."
    restored

let farm = {
    Name = "Sunny Acres"
    Owner = Some "Ada"
    Animals = [
        Cow("Bessie", 1500)
        Horse("Spirit", 45)
        Dog("Rex", "Brown")
    ]
}

// Byte-for-byte lock for shipped F# union output. Update only when the wire format is intentionally changed.
let assertUnchanged (expected: string) (value: Farm) =
    let json = serializer.Serialize(&value, shape)
    if json <> expected then
        failwith $"F# JSON changed.\nExpected: {expected}\nActual:   {json}"

assertUnchanged """{"name":"Sunny Acres","owner":"Ada","animals":[{"$type":"Cow","name":"Bessie","weight":1500},{"$type":"Horse","name":"Spirit","speed":45},{"$type":"Dog","name":"Rex","color":"Brown"}]}""" farm
assertUnchanged """{"name":"Sunny Acres","owner":null,"animals":[]}""" { farm with Owner = None; Animals = [] }

let restored = roundTrip farm
roundTrip { farm with Owner = None; Animals = [] } |> ignore

restored.Animals |> List.iter (function
    | Cow(name, weight) -> printfn "Cow: %s, Weight: %d" name weight
    | Horse(name, speed) -> printfn "Horse: %s, Speed: %d" name speed
    | Dog(name, color) -> printfn "Dog: %s, Color: %s" name color)
