// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using PolyType.Abstractions;

public partial class JsonReferenceCycleTests : TestBase
{
	private readonly JsonSerializer allowCycles = new() { PreserveReferences = ReferencePreservationMode.AllowCycles };
	private readonly JsonSerializer rejectCycles = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };

	[Test]
	public void SelfCycle_RoundTrips()
	{
		SelfNode node = new();
		node.Next = node;

		string json = this.allowCycles.Serialize(node, Shape<SelfNode, SelfNode>());
		Assert.Equal("""{"$id":1,"$value":{"next":{"$ref":1}}}""", json);

		SelfNode? result = this.allowCycles.Deserialize(json, Shape<SelfNode, SelfNode>());
		Assert.NotNull(result);
		Assert.Same(result, result.Next);
	}

	[Test]
	public void MutualCycle_RoundTrips()
	{
		Parent parent = new() { Name = "p" };
		Child child = new() { Name = "c" };
		parent.Child = child;
		child.Parent = parent;

		string json = this.allowCycles.Serialize(parent, Shape<Parent, Parent>());
		Parent? result = this.allowCycles.Deserialize(json, Shape<Parent, Parent>());

		Assert.NotNull(result?.Child);
		Assert.Same(result, result.Child.Parent);
		Assert.Equal("c", result.Child.Name);
	}

	[Test]
	public void RepeatedReference_AllowCycles_PreservesIdentity()
	{
		Leaf2 leaf = new() { Name = "shared" };
		Pair value = new() { A = leaf, B = leaf };

		string json = this.allowCycles.Serialize(value, Shape<Pair, Pair>());
		Assert.Equal("""{"$id":1,"$value":{"a":{"$id":2,"$value":{"name":"shared"}},"b":{"$ref":2}}}""", json);

		Pair? result = this.allowCycles.Deserialize(json, Shape<Pair, Pair>());
		Assert.NotNull(result?.A);
		Assert.Same(result.A, result.B);
	}

	[Test]
	public void RejectCycles_StillThrowsOnCycle()
	{
		SelfNode node = new();
		node.Next = node;

		InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => this.rejectCycles.Serialize(node, Shape<SelfNode, SelfNode>()));
		Assert.Contains("Reference cycles", ex.Message, StringComparison.Ordinal);
	}

	[Test]
	public void AcyclicGraph_IsWireCompatibleAcrossModes()
	{
		Leaf2 leaf = new() { Name = "shared" };
		Pair value = new() { A = leaf, B = leaf };

		string rejectJson = this.rejectCycles.Serialize(value, Shape<Pair, Pair>());
		string allowJson = this.allowCycles.Serialize(value, Shape<Pair, Pair>());
		Assert.Equal(rejectJson, allowJson);

		// Each mode can read the other's acyclic output.
		Pair? viaReject = this.rejectCycles.Deserialize(allowJson, Shape<Pair, Pair>());
		Pair? viaAllow = this.allowCycles.Deserialize(rejectJson, Shape<Pair, Pair>());
		Assert.Same(viaReject!.A, viaReject.B);
		Assert.Same(viaAllow!.A, viaAllow.B);
	}

	[Test]
	public void CollectionCycle_RoundTrips()
	{
		TreeNode node = new() { Label = "root" };
		node.Children.Add(node);

		string json = this.allowCycles.Serialize(node, Shape<TreeNode, TreeNode>());
		TreeNode? result = this.allowCycles.Deserialize(json, Shape<TreeNode, TreeNode>());

		Assert.NotNull(result);
		Assert.Single(result.Children);
		Assert.Same(result, result.Children[0]);
	}

	[Test]
	public void DictionaryCycle_RoundTrips()
	{
		MapNode node = new() { Label = "root" };
		node.Links["self"] = node;

		string json = this.allowCycles.Serialize(node, Shape<MapNode, MapNode>());
		MapNode? result = this.allowCycles.Deserialize(json, Shape<MapNode, MapNode>());

		Assert.NotNull(result);
		Assert.Same(result, result.Links["self"]);
	}

	[Test]
	public void ImmutableObject_RepeatedReference_RoundTrips()
	{
		ImmutableLeaf leaf = new("shared");
		Holder value = new() { A = leaf, B = leaf };

		string json = this.allowCycles.Serialize(value, Shape<Holder, Holder>());
		Holder? result = this.allowCycles.Deserialize(json, Shape<Holder, Holder>());

		Assert.NotNull(result?.A);
		Assert.Same(result.A, result.B);
		Assert.Equal("shared", result.A.Name);
	}

	[Test]
	public void ImmutableObject_BackReferenceInCycle_ThrowsClearly()
	{
		string json = """{"$id":1,"$value":{"name":"x","next":{"$ref":1}}}""";

		FormatException ex = Assert.Throws<FormatException>(() => this.allowCycles.Deserialize(json, Shape<ImmutableCycleNode, ImmutableCycleNode>()));
		Assert.Contains("immutable or constructor-bound", ex.Message, StringComparison.Ordinal);
	}

	[Test]
	public void MutableObject_CanBeBackReferencedByImmutableChild()
	{
		string json = """{"$id":1,"$value":{"child":{"$id":2,"$value":{"parent":{"$ref":1}}}}}""";

		MixedParent? result = this.allowCycles.Deserialize(json, Shape<MixedParent, MixedParent>());
		Assert.NotNull(result?.Child);
		Assert.Same(result, result.Child.Parent);
	}

	[Test]
	public void Callbacks_RunOncePerObject_InCycle()
	{
		CallbackNode node = new();
		node.Next = node;

		string json = this.allowCycles.Serialize(node, Shape<CallbackNode, CallbackNode>());
		Assert.Equal(1, node.BeforeCount);

		CallbackNode? result = this.allowCycles.Deserialize(json, Shape<CallbackNode, CallbackNode>());
		Assert.NotNull(result);
		Assert.Same(result, result.Next);
		Assert.Equal(1, result.AfterCount);
	}

	[Test]
	public void ForwardReference_Throws()
	{
		string json = """{"$id":1,"$value":{"next":{"$ref":2}}}""";

		FormatException ex = Assert.Throws<FormatException>(() => this.allowCycles.Deserialize(json, Shape<SelfNode, SelfNode>()));
		Assert.Contains("not previously defined", ex.Message, StringComparison.Ordinal);
	}

	[Test]
	public void DuplicateId_Throws()
	{
		string json = """{"$id":1,"$value":{"next":{"$id":1,"$value":{"next":null}}}}""";

		FormatException ex = Assert.Throws<FormatException>(() => this.allowCycles.Deserialize(json, Shape<SelfNode, SelfNode>()));
		Assert.Contains("assigned more than once", ex.Message, StringComparison.Ordinal);
	}

	[Test]
	public void ReferenceObjectWithExtraProperty_Throws()
	{
		string json = """{"$id":1,"$value":{"next":{"$ref":1,"extra":2}}}""";

		Assert.Throws<FormatException>(() => this.allowCycles.Deserialize(json, Shape<SelfNode, SelfNode>()));
	}

	[Test]
	public void InvalidReferenceId_Throws()
	{
		string json = """{"$id":1,"$value":{"next":{"$ref":0}}}""";

		Assert.Throws<FormatException>(() => this.allowCycles.Deserialize(json, Shape<SelfNode, SelfNode>()));
	}

	[Test]
	public void MultiSegmentInput_Cycle_RoundTrips()
	{
		byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("""{"$id":1,"$value":{"next":{"$ref":1}}}""");
		ReadOnlySequence<byte> sequence = CreateSequence(utf8.AsMemory(0, 7), utf8.AsMemory(7, 11), utf8.AsMemory(18));

		SelfNode? result = this.allowCycles.Deserialize(sequence, Shape<SelfNode, SelfNode>());
		Assert.NotNull(result);
		Assert.Same(result, result.Next);
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	private static ReadOnlySequence<byte> CreateSequence(params ReadOnlyMemory<byte>[] segments)
	{
		Segment? first = null;
		Segment? last = null;
		foreach (ReadOnlyMemory<byte> segment in segments)
		{
			Segment current = new(segment);
			if (first is null)
			{
				first = current;
			}
			else
			{
				last!.SetNext(current);
			}

			last = current;
		}

		return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
	}

	[GenerateShape]
	internal partial class SelfNode
	{
		public SelfNode? Next { get; set; }
	}

	[GenerateShape]
	internal partial class Parent
	{
		public string? Name { get; set; }

		public Child? Child { get; set; }
	}

	[GenerateShape]
	internal partial class Child
	{
		public string? Name { get; set; }

		public Parent? Parent { get; set; }
	}

	[GenerateShape]
	internal partial class Pair
	{
		public Leaf2? A { get; set; }

		public Leaf2? B { get; set; }
	}

	[GenerateShape]
	internal partial class Leaf2
	{
		public string? Name { get; set; }
	}

	[GenerateShape]
	internal partial class TreeNode
	{
		public string? Label { get; set; }

		public List<TreeNode> Children { get; set; } = [];
	}

	[GenerateShape]
	internal partial class MapNode
	{
		public string? Label { get; set; }

		public Dictionary<string, MapNode> Links { get; set; } = new();
	}

	[GenerateShape]
	internal partial class CallbackNode : IJsonSerializationCallbacks
	{
		public CallbackNode? Next { get; set; }

		internal int BeforeCount { get; private set; }

		internal int AfterCount { get; private set; }

		public void OnBeforeSerialize() => this.BeforeCount++;

		public void OnAfterDeserialize() => this.AfterCount++;
	}

	[GenerateShape]
	internal partial record ImmutableLeaf(string Name);

	[GenerateShape]
	internal partial class Holder
	{
		public ImmutableLeaf? A { get; set; }

		public ImmutableLeaf? B { get; set; }
	}

	[GenerateShape]
	internal partial record ImmutableCycleNode(string Name, ImmutableCycleNode? Next);

	[GenerateShape]
	internal partial class MixedParent
	{
		public ImmutableChild? Child { get; set; }
	}

	[GenerateShape]
	internal partial record ImmutableChild(MixedParent Parent);

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

		internal void SetNext(Segment next)
		{
			next.RunningIndex = this.RunningIndex + this.Memory.Length;
			this.Next = next;
		}
	}
}
