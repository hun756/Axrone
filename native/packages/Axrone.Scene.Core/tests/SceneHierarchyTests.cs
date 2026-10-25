namespace Axrone.Scene.Core.Tests;

using Axrone.Scene;
using FluentAssertions;
using Xunit;

public sealed class SceneHierarchyTests
{
    [Fact]
    public void Children_EnumerateInSiblingOrder()
    {
        SceneActorTable actors = new(4);
        var hierarchy = new SceneHierarchy(actors, observer: null);
        SceneActor root = CreateActor(actors);
        SceneActor first = CreateActor(actors);
        SceneActor second = CreateActor(actors);

        hierarchy.SetParent(first, root);
        hierarchy.SetParent(second, root);

        List<SceneActor> children = new();
        foreach (SceneActor child in hierarchy.Children(root))
        {
            children.Add(child);
        }

        children.Should().Equal(second, first);
        hierarchy.ChildCount(root).Should().Be(2);
    }

    [Fact]
    public void VisitDescendants_UsesPreOrderAndDepth()
    {
        SceneActorTable actors = new(4);
        var hierarchy = new SceneHierarchy(actors, observer: null);
        SceneActor root = CreateActor(actors);
        SceneActor a = CreateActor(actors);
        SceneActor b = CreateActor(actors);
        SceneActor c = CreateActor(actors);

        hierarchy.SetParent(a, root);
        hierarchy.SetParent(b, a);
        hierarchy.SetParent(c, root);

        var context = new TraversalBuffer(new SceneActor[8], new int[8]);
        var visitor = new CollectorVisitor();

        hierarchy.VisitDescendants(root, ref visitor, ref context).Should().BeTrue();

        context.Actors[..context.Count].ToArray().Should().Equal(c, a, b);
        context.Depths[..context.Count].ToArray().Should().Equal(1, 1, 2);
    }

    [Fact]
    public void VisitAncestors_WalksToRoot()
    {
        SceneActorTable actors = new(4);
        var hierarchy = new SceneHierarchy(actors, observer: null);
        SceneActor root = CreateActor(actors);
        SceneActor middle = CreateActor(actors);
        SceneActor leaf = CreateActor(actors);

        hierarchy.SetParent(middle, root);
        hierarchy.SetParent(leaf, middle);

        List<SceneActor> ancestors = new();
        foreach (SceneActor ancestor in hierarchy.Ancestors(leaf))
        {
            ancestors.Add(ancestor);
        }

        ancestors.Should().Equal(middle, root);
    }

    [Fact]
    public void SetParent_RejectsCycles()
    {
        SceneActorTable actors = new(4);
        var hierarchy = new SceneHierarchy(actors, observer: null);
        SceneActor root = CreateActor(actors);
        SceneActor child = CreateActor(actors);
        hierarchy.SetParent(child, root);

        Action cycle = () => hierarchy.SetParent(root, child);

        cycle.Should().Throw<SceneValidationException>();
    }

    [Fact]
    public void Reparent_UpdatesDepthAndOrder()
    {
        SceneActorTable actors = new(4);
        var hierarchy = new SceneHierarchy(actors, observer: null);
        SceneActor root = CreateActor(actors);
        SceneActor a = CreateActor(actors);
        SceneActor b = CreateActor(actors);

        hierarchy.SetParent(a, root);
        hierarchy.SetParent(b, a);

        hierarchy.SetParent(b, root);

        hierarchy.DepthOf(b).Should().Be(1);
        hierarchy.ParentOf(b).Should().Be(root);
        hierarchy.ChildCount(a).Should().Be(0);
        hierarchy.ChildCount(root).Should().Be(2);
    }

    [Fact]
    public void VisitSubtree_IncludesRootAndDescendants()
    {
        SceneActorTable actors = new(4);
        var hierarchy = new SceneHierarchy(actors, observer: null);
        SceneActor root = CreateActor(actors);
        SceneActor child = CreateActor(actors);
        hierarchy.SetParent(child, root);

        var context = new TraversalBuffer(new SceneActor[4], new int[4]);
        var visitor = new CollectorVisitor();

        hierarchy.VisitSubtree(root, ref visitor, ref context).Should().BeTrue();

        context.Actors[..context.Count].ToArray().Should().Equal(root, child);
        context.Depths[..context.Count].ToArray().Should().Equal(0, 1);
    }

    private static SceneActor CreateActor(SceneActorTable actors) =>
        actors.Allocate(SceneStringId.Invalid, SceneStringId.Invalid, 0, active: true, persistent: false, pooled: false);

    private sealed class TraversalBuffer
    {
        public SceneActor[] Actors;
        public int[] Depths;
        public int Count;

        public TraversalBuffer(SceneActor[] actors, int[] depths)
        {
            Actors = actors;
            Depths = depths;
            Count = 0;
        }
    }

    private struct CollectorVisitor : ISceneHierarchyVisitor<TraversalBuffer>
    {
        public bool Visit(SceneActor actor, int depth, scoped ref TraversalBuffer context)
        {
            if ((uint)context.Count >= (uint)context.Actors.Length)
            {
                return false;
            }

            context.Actors[context.Count] = actor;
            context.Depths[context.Count] = depth;
            context.Count++;
            return true;
        }
    }
}
