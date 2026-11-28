// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS1591, SA1600, SA1601 // Documentation samples do not require API documentation.
#pragma warning disable SA1402, SA1649 // Documentation samples share a file.
#pragma warning disable SA1512, SA1513, SA1515 // Documentation region markers.

using System.Collections.Generic;
using Nerdbank.Json;
using PolyType;

// <LossyFarm>
[GenerateShape]
public partial class Farm
{
	public List<Animal> Animals { get; } =
	[
		new Cow { Name = "Bessie", Weight = 1400 },
		new Horse { Name = "Lightning", Speed = 45 },
		new Dog { Name = "Rover", Color = "Brown" },
	];
}

[GenerateShape]
public partial class Animal
{
	public string? Name { get; set; }
}

[GenerateShape]
public partial class Cow : Animal
{
	public int Weight { get; set; }
}

[GenerateShape]
public partial class Horse : Animal
{
	public int Speed { get; set; }
}

[GenerateShape]
public partial class Dog : Animal
{
	public string? Color { get; set; }
}
// </LossyFarm>

// <RoundtrippingFarmAnimal>
[GenerateShape]
[DerivedTypeShape(typeof(UnionCow))]
[DerivedTypeShape(typeof(UnionHorse))]
[DerivedTypeShape(typeof(UnionDog))]
public partial class UnionAnimal
{
	public string? Name { get; set; }
}

[GenerateShape]
public partial class UnionFarm
{
	public List<UnionAnimal> Animals { get; } =
	[
		new UnionCow { Name = "Bessie", Weight = 1400 },
		new UnionHorse { Name = "Lightning", Speed = 45 },
		new UnionDog { Name = "Rover", Color = "Brown" },
	];
}

[GenerateShape]
public partial class UnionCow : UnionAnimal
{
	public int Weight { get; set; }
}

[GenerateShape]
public partial class UnionHorse : UnionAnimal
{
	public int Speed { get; set; }
}

[GenerateShape]
public partial class UnionDog : UnionAnimal
{
	public string? Color { get; set; }
}
// </RoundtrippingFarmAnimal>

// <StringAliasTypes>
[GenerateShape]
[DerivedTypeShape(typeof(StringAliasCat), Name = "cat")]
[DerivedTypeShape(typeof(StringAliasDog), Name = "dog")]
public partial class StringAliasAnimal
{
	public string? Name { get; set; }
}

[GenerateShape]
public partial class StringAliasCat : StringAliasAnimal
{
	public int Lives { get; set; }
}

[GenerateShape]
public partial class StringAliasDog : StringAliasAnimal
{
	public string? Breed { get; set; }
}
// </StringAliasTypes>

// <IntAliasTypes>
[GenerateShape]
[DerivedTypeShape(typeof(IntAliasCat), Tag = 1)]
[DerivedTypeShape(typeof(IntAliasDog), Tag = 2)]
public partial class IntAliasAnimal
{
	public string? Name { get; set; }
}

[GenerateShape]
public partial class IntAliasCat : IntAliasAnimal
{
	public int Lives { get; set; }
}

[GenerateShape]
public partial class IntAliasDog : IntAliasAnimal
{
	public string? Breed { get; set; }
}
// </IntAliasTypes>
