﻿namespace Tenray.ZoneTree.Collections.BTree;

/// <summary>
/// In memory B+Tree.
/// This class is thread-safe.
/// </summary>
/// <typeparam name="TKey">Key Type</typeparam>
/// <typeparam name="TValue">Value Type</typeparam>
public partial class BTree<TKey, TValue>
{
    public bool Upsert(in TKey key, in TValue value, out long opIndex)
    {
        try
        {
            WriteLock();
            while (true)
            {
                var root = Root;
                root.WriteLock();
                if (root != Root)
                {
                    root.WriteUnlock();
                    continue;
                }

                if (!root.IsFull)
                {
                    return UpsertNonFull(root, in key, in value, out opIndex);
                }
                var newRoot = new Node(GetNodeLocker(), NodeSize);
                newRoot.Children[0] = root;
                newRoot.WriteLock();
                SplitChild(newRoot, 0, root);
                var result = UpsertNonFull(newRoot, in key, in value, out opIndex);
                Root = newRoot;
                root.WriteUnlock();
                return result;
            }
        }
        finally
        {
            WriteUnlock();
        }
    }

    public bool TryInsert(in TKey key, in TValue value, out long opIndex)
    {
        try
        {
            WriteLock();
            while (true)
            {
                var root = Root;
                root.WriteLock();
                if (root != Root)
                {
                    root.WriteUnlock();
                    continue;
                }
                if (!root.IsFull)
                {
                    return TryInsertNonFull(root, in key, in value, out opIndex);
                }
                var newRoot = new Node(GetNodeLocker(), NodeSize);
                newRoot.Children[0] = root;
                newRoot.WriteLock();
                SplitChild(newRoot, 0, root);
                var result = TryInsertNonFull(newRoot, in key, in value, out opIndex);
                Root = newRoot;
                root.WriteUnlock();
                return result;
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
            Root.WriteUnlock();
            throw;
        }
        finally
        {
            WriteUnlock();
        }
    }

    public delegate AddOrUpdateResult AddDelegate(ref TValue value);

    public delegate AddOrUpdateResult UpdateDelegate(ref TValue value);

    public AddOrUpdateResult AddOrUpdate(
        in TKey key,
        AddDelegate adder,
        UpdateDelegate updater,
        out long opIndex)
    {
        try
        {
            WriteLock();
            while (true)
            {
                var root = Root;
                root.WriteLock();
                if (root != Root)
                {
                    root.WriteUnlock();
                    continue;
                }
                if (!root.IsFull)
                {
                    return TryAddOrUpdateNonFull(root, in key, adder, updater, out opIndex);
                }
                var newRoot = new Node(GetNodeLocker(), NodeSize);
                newRoot.Children[0] = root;
                newRoot.WriteLock();
                SplitChild(newRoot, 0, root);
                AddOrUpdateResult result =
                    TryAddOrUpdateNonFull(newRoot, in key, adder, updater, out opIndex);
                Root = newRoot;
                root.WriteUnlock();
                return result;
            }
        }
        finally
        {
            WriteUnlock();
        }
    }

    void SplitChild(Node parent, int parentInsertPosition, Node child)
    {
        var pivotPosition = (child.Length + 1) / 2;
        ref var pivotKey = ref child.Keys[pivotPosition];
        if (child is LeafNode childLeaf)
        {
            (var left, var right) = childLeaf
                .SplitLeaf(pivotPosition, LeafSize, GetNodeLocker(), GetNodeLocker());

            left.Previous = childLeaf.Previous;
            right.Next = childLeaf.Next;

            var pre = childLeaf.Previous;
            var next = childLeaf.Next;
            
            if (pre == null)
                FirstLeafNode = left;
            else
                pre.Next = left;

            if (next == null)
                LastLeafNode = right;
            else
                next.Previous = right;

            parent.InsertKeyAndChild(parentInsertPosition, in pivotKey, left, right);
        }
        else
        {
            (var left, var right) = child
                .Split(pivotPosition, NodeSize, GetNodeLocker(), GetNodeLocker());
            parent.InsertKeyAndChild(parentInsertPosition, in pivotKey, left, right);
        }
    }

    bool UpsertNonFull(Node node, in TKey key, in TValue value, out long opIndex)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                opIndex = IncrementalIdProvider.NextId();
                if (found)
                {
                    leaf.Update(position, in key, in value);
                    node.WriteUnlock();                    
                    return false;
                }
                leaf.Insert(position, in key, in value);
                node.WriteUnlock();
                Interlocked.Increment(ref _length);
                return true;
            }

            var child = node.Children[position];
            child.WriteLock();
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                child.WriteUnlock();

                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                    ++position;

                child = node.Children[position];
                child.WriteLock();
            }
            node.WriteUnlock();
            node = child;
        }
    }

    bool TryInsertNonFull(Node node, in TKey key, in TValue value, out long opIndex)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                if (found)
                {
                    node.WriteUnlock();
                    opIndex = 0;
                    return false;
                }

                opIndex = IncrementalIdProvider.NextId();
                leaf.Insert(position, in key, in value);
                node.WriteUnlock();
                Interlocked.Increment(ref _length);
                return true;
            }

            var child = node.Children[position];
            child.WriteLock();
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                child.WriteUnlock();

                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                    ++position;

                child = node.Children[position];
                child.WriteLock();
            }
            node.WriteUnlock();
            node = child;
        }
    }

    AddOrUpdateResult TryAddOrUpdateNonFull(
        Node node, in TKey key, AddDelegate adder, UpdateDelegate updater, 
        out long opIndex)
    {
        while (true)
        {
            var found = node.TryGetPosition(Comparer, in key, out var position);
            if (node is LeafNode leaf)
            {
                opIndex = IncrementalIdProvider.NextId();
                if (found)
                {
                    updater(ref leaf.Values[position]);
                    node.WriteUnlock();
                    return AddOrUpdateResult.UPDATED;
                }
                TValue value = default;
                adder(ref value);
                leaf.Insert(position, in key, in value);
                node.WriteUnlock();
                Interlocked.Increment(ref _length);
                return AddOrUpdateResult.ADDED;
            }

            var child = node.Children[position];
            child.WriteLock();
            if (child.IsFull)
            {
                SplitChild(node, position, child);
                child.WriteUnlock();

                if (Comparer.Compare(in key, in node.Keys[position]) > 0)
                    ++position;

                child = node.Children[position];
                child.WriteLock();
            }
            node.WriteUnlock();
            node = child;
        }
    }
}