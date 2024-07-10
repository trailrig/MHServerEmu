﻿using System.Collections;
using MHServerEmu.Core.Collisions;
using MHServerEmu.Core.Logging;
using MHServerEmu.Core.VectorMath;

namespace MHServerEmu.Games.Common.SpatialPartitions
{
    // NOTE: Even though this is a quadtree structure, the original source file referenced by client verify messages is called "Octree.h".
    // D:\mirrorBuilds_source05\MarvelGame_v52\Source\Game\Game\SpacialPartition\Octree.h

    // Logically it probably makes more sense to have this in the Core.Collections namespace, but for now we are sticking to how it is in the client.

    public class Quadtree<T>
    {
        private static readonly Logger Logger = LogManager.CreateLogger();

        public Node<T> Root;
        private Aabb _bounds;
        private readonly int _targetThreshold = 6;
        private readonly float _loose = 2.0f;
        private int _nodesCount;
        private int _outstandingIteratorCount;
        private int _elementsCount;
        private float _minLoose;

        public Quadtree(in Aabb bound, float minRadius)
        {
            _bounds = bound;
            _minLoose = MathF.Max(minRadius * _loose, bound.Radius2D() * _loose / 16777216);
            _elementsCount = 0;
            _nodesCount = 0;
        }

        public bool Update(T element)
        {
            if (element == null) return Logger.WarnReturn(false, "Update(): element == null");
            if (_outstandingIteratorCount > 0) return Logger.WarnReturn(false, $"Update(): _outstandingIteratorCount > 0 ({_outstandingIteratorCount})");

            var location = GetLocation(element);
            var node = location.Node;
            if (node == null) return Insert(element);

            var elementBounds = GetElementBounds(element);
            if (node.LooseBounds.FullyContainsXY(elementBounds)) return false;
            if (node.RemoveElement(location) == false) return Logger.WarnReturn(false, "Update(): node.RemoveElement(location) == false");
            _elementsCount--;

            var parent = node.Parent;
            RewindNode(node, parent);
            while (parent != null)
            {
                if (parent.LooseBounds.FullyContainsXY(elementBounds))
                    return Insert(parent, element, elementBounds, elementBounds.Center, elementBounds.Radius2D());

                node = parent;
                parent = node.Parent;
            }

            return Logger.WarnReturn(false, "Update(): Unknown failure");
        }

        public bool Insert(T element)
        {
            if (element == null) return Logger.WarnReturn(false, "Insert(): element == null");
            if (_outstandingIteratorCount > 0) return Logger.WarnReturn(false, $"Insert(): _outstandingIteratorCount > 0 ({_outstandingIteratorCount})");

            if (Root == null) AllocateNode(new(_bounds.Center, _bounds.Radius2D() * _loose), null);
            if (Root == null) return Logger.WarnReturn(false, "Insert(): Root == null");

            Aabb elementBounds = GetElementBounds(element);
            float elementRadius = elementBounds.Radius2D();
            if ((elementRadius > 0.0f && Root.LooseBounds.FullyContainsXY(elementBounds)) == false)
                return Logger.WarnReturn(false, $"Trying to insert element into quadtree with invalid size. ElementRadius={elementRadius}, ElementBounds={elementBounds}, Element={element}");

            return Insert(Root, element, elementBounds, elementBounds.Center, elementRadius);
        }

        private bool Insert(Node<T> node, T element, in Aabb elementBounds, in Vector3 elementCenter, float elementRadius)
        {
            if (node == null) return Logger.WarnReturn(false, "Insert(): node == null");
            QuadtreeLocation<T> location = GetLocation(element);

            while (true)
            {
                if (AtTargetLevel(node, elementRadius))
                {
                    node.AddElement(location, true);
                    _elementsCount++;
                    return false;
                }

                Vector2 center = node.LooseBounds.Center;
                int x = elementCenter.X > center.X ? 1 : 0;
                int y = elementCenter.Y > center.Y ? 1 : 0;
                int index = x << 1 | y;
                Node<T> child = node.Children[index];

                if (child != null)
                {
                    if (child.LooseBounds.FullyContainsXY(elementBounds))
                    {
                        node = child;
                        continue;
                    }
                    node.AddElement(location, true);
                }
                else
                {
                    child = PushDown(node, center, x, y);
                    if (child != null && child.LooseBounds.FullyContainsXY(elementBounds))
                        child.AddElement(location, AtTargetLevel(child, elementRadius));
                    else
                        node.AddElement(location, false);
                }

                _elementsCount++;
                return true;
            }
        }

        public virtual QuadtreeLocation<T> GetLocation(T element) => default;

        private Node<T> PushDown(Node<T> node, in Vector2 center, int x, int y)
        {
            int notAtTargetCount = node.Elements.Count - node.AtTargetLevelCount;
            if (notAtTargetCount >= 0 && notAtTargetCount >= _targetThreshold)
            {
                var childBounds = ConstructChildBounds(node, center, x, y);
                var child = AllocateNode(childBounds, node, x << 1 | y);
                if (child != null)
                {
                    node.PushDown(this, child);
                    return child;
                }
            }
            return null;
        }

        private Aabb2 ConstructChildBounds(Node<T> node, in Vector2 center, int x, int y)
        {
            float childDiameter = node.LooseBounds.Width / 2.0f;
            float looseRadius = childDiameter / (_loose * 2.0f);
            Vector2 offset = new(x == 0 ? -looseRadius : looseRadius, y == 0 ? -looseRadius : looseRadius);
            Vector2 childCenter = center + offset;
            return new(childCenter, childDiameter);
        }

        public bool AtTargetLevel(Node<T> node, float elementRadius)
        {
            float nodeRadius = node.GetRadius();
            return nodeRadius <= _minLoose || elementRadius >= nodeRadius * 0.5;
        }

        public virtual Aabb GetElementBounds(T element) => default;

        private Node<T> AllocateNode(in Aabb2 bound, Node<T> parent, int index = 0)
        {
            Node<T> child = new(this, parent, bound);
            if (parent != null)
                parent.Children[index] = child;
            else
                Root = child;
            _nodesCount++;
            return child;
        }

        private void DeallocateNode(Node<T> node)
        {
            if (node != null) _nodesCount--;
        }

        public bool Remove(T element)
        {
            if (element == null) return Logger.WarnReturn(false, "Remove(): element == null");
            if (_outstandingIteratorCount > 0) return Logger.WarnReturn(false, $"Remove(): _outstandingIteratorCount > 0 ({_outstandingIteratorCount})");

            var location = GetLocation(element);
            var node = location.Node;

            if (node == null) return Logger.WarnReturn(false, "Remove(): node == null");
            if (node.RemoveElement(location) == false) return Logger.WarnReturn(false, "Remove(): node.RemoveElement(location) == false");

            _elementsCount--;

            return RewindNode(node);
        }

        private bool RewindNode(Node<T> node, Node<T> root = default)
        {
            bool result = false;
            while (node.IsEmpty())
            {
                var parent = node.Parent;
                UnlinkChild(parent, node);
                DeallocateNode(node);
                result = true;

                if (parent == root) break;
                node = parent;
            }
            return result;
        }

        private bool UnlinkChild(Node<T> parent, Node<T> child)
        {
            if (parent != null)
            {
                for (int index = 0; index < 4; index++)
                {
                    if (parent.Children[index] == child)
                    {
                        parent.Children[index] = null;
                        return true;
                    }
                }
            }
            else if (Root == child)
            {
                Root = null;
                return true;
            }
            return false;
        }

        public IEnumerable<T> IterateElementsInVolume<B>(B volume) where B : IBounds
        {
            var iterator = new ElementIterator<B>(this, volume);

            try
            {
                while (iterator.End() == false)
                {
                    var element = iterator.Current;
                    iterator.MoveNext();
                    yield return element;
                }
            }
            finally
            {
                iterator.Clear();
            }
        }

        public class ElementIterator<B> : IEnumerator<T> where B : IBounds
        {
            public Quadtree<T> Tree { get; private set; }
            public B Volume { get; private set; }
            private CandidateNode _currentNode;
            private QuadtreeLocation<T> _currentElement;
            private Stack<CandidateNode> _stack = new();

            private struct CandidateNode // Struct, not class!
            {
                public Node<T> Node;
                public bool Contains;

                public CandidateNode(Node<T> node = null, bool contains = false)
                {
                    Node = node;
                    Contains = contains;
                }
            }

            public ElementIterator(B volume)
            {
                Tree = null;
                _currentNode = new();
                _currentElement = default;
                Volume = volume;
            }

            public ElementIterator(Quadtree<T> tree, B volume)
            {
                Tree = tree;
                Volume = volume;
                _currentNode = new();
                _currentElement = default;
                Tree.IncrementIteratorCount();
                Reset();
            }

            public void Initialize(Quadtree<T> tree)
            {
                Tree = tree;
                _currentNode = new();
                _currentElement = default;
                Tree.IncrementIteratorCount();
                Reset();
            }

            public bool End() => _currentElement == null;

            public void Reset() // init
            {
                if (Tree == null || Tree.Root == null) return;

                ContainmentType contains = Volume.Contains(Tree.Root.LooseBounds);
                if (contains == ContainmentType.Disjoint) return;

                _currentNode.Node = Tree.Root;
                _currentNode.Contains = contains == ContainmentType.Contains;

                _currentElement = GetFirstElement(_currentNode);
                if (_currentElement == null) NextNode();
            }

            public void Dispose() { }

            public void Clear()
            {
                if (Tree != null) Tree.DecrementIteratorCount();
                _stack.Clear();
            }

            public T Current { get => _currentElement.Element; }
            object IEnumerator.Current => Current;

            public bool MoveNext() // advanceNext
            {
                if (_currentNode.Node == null || _currentElement == null) return false;

                var linkNode = _currentNode.Node.Elements.Find(_currentElement).Next;
                while (linkNode != null)
                {
                    _currentElement = linkNode.Value;
                    if (_currentNode.Contains || Volume.Intersects(Tree.GetElementBounds(_currentElement.Element))) return true;
                    linkNode = linkNode.Next;
                }
                return NextNode();
            }

            private bool NextNode()
            {
                var node = _currentNode;
                while (node.Node != null)
                {
                    bool hasChildren = false;
                    if (IterateChildren(ref node, ref hasChildren)) return true;

                    if (hasChildren == false)
                    {
                        if (_stack.Count == 0)
                        {
                            _currentNode = new(null, false);
                            _currentElement = null;
                            return false;
                        }
                        else
                        {
                            node = _stack.Pop();
                            if (SetCurrentNode(node)) return true;
                        }
                    }
                }
                return false;
            }

            private bool IterateChildren(ref CandidateNode node, ref bool checkChildren)
            {
                for (int index = 0; index < 4; index++)
                {
                    var child = node.Node.Children[index];
                    if (child == null) continue;

                    if (node.Contains)
                    {
                        for (index++; index < 4; index++)
                            if (node.Node.Children[index] != null)
                                _stack.Push(new(node.Node.Children[index], true));
                    }
                    else
                    {
                        var childContains = Volume.Contains(child.LooseBounds);
                        if (childContains == ContainmentType.Disjoint) continue;

                        for (index++; index < 4; index++)
                        {
                            var otherChild = node.Node.Children[index];
                            if (otherChild == null) continue;

                            var otherContains = Volume.Contains(otherChild.LooseBounds);
                            if (otherContains != ContainmentType.Disjoint)
                                _stack.Push(new(otherChild, otherContains == ContainmentType.Contains));
                        }
                        node.Contains = childContains == ContainmentType.Contains;
                    }

                    node.Node = child;
                    if (SetCurrentNode(node)) return true;

                    checkChildren = true;
                    break;
                }
                return false;
            }

            private bool SetCurrentNode(in CandidateNode node)
            {
                var element = GetFirstElement(node);
                if (element != null)
                {
                    _currentNode = node;
                    _currentElement = element;
                    return true;
                }
                return false;
            }

            private QuadtreeLocation<T> GetFirstElement(in CandidateNode node)
            {
                if (node.Contains)
                {
                    QuadtreeLocation<T> element = null;
                    if (node.Node.Elements.Count > 0)
                        element = node.Node.Elements.First.Value;
                    if (element != null) return element;
                }
                else
                {
                    foreach (var element in node.Node.Elements)
                        if (Volume.Intersects(Tree.GetElementBounds(element.Element))) return element;
                }
                return default;
            }

            public IEnumerator<T> GetEnumerator() => this;
        }

        private void DecrementIteratorCount()
        {
            if (_outstandingIteratorCount > 0) _outstandingIteratorCount--;
        }

        private void IncrementIteratorCount() => _outstandingIteratorCount++;

    }
}
