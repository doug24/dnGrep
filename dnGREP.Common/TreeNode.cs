using System;
using System.Collections.Generic;
using System.Linq;

namespace dnGREP.Common
{
    public class TreeNode<T>(T value, TreeNode<T>? parent)
    {
        private readonly T value = value;
        private readonly TreeNode<T>? parent = parent;
        private readonly List<TreeNode<T>> children = [];

        public T Value => value;

        public TreeNode<T>? Parent => parent;

        public bool IsRoot => parent == null;


        public TreeNode<T> AddChild(T value)
        {
            var child = new TreeNode<T>(value, this);
            children.Add(child);
            return child;
        }

        public TreeNode<T>? GetChild(int idx)
        {
            if (idx < children.Count)
                return children[idx];

            return null;
        }

        public void Traverse(TreeNode<T> node, Action<T> visitor)
        {
            visitor(node.value);
            foreach (TreeNode<T> child in node.children)
                Traverse(child, visitor);
        }

        public IEnumerable<T> Flatten()
        {
            return new[] { Value }.Concat(children.SelectMany(x => x.Flatten()));
        }
    }
}
