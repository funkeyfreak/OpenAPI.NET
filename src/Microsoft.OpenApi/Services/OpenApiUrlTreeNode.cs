﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.OpenApi.Models;

namespace Microsoft.OpenApi.Services
{
    /// <summary>
    /// A directory structure representing the paths of an OpenAPI document.
    /// </summary>
    public class OpenApiUrlTreeNode
    {
        private const string RootPathSegment = "/";
        private const string PathSeparator = "\\";

        /// <summary>
        /// All the subdirectories of a node.
        /// </summary>
        public IDictionary<string, OpenApiUrlTreeNode> Children { get; } = new Dictionary<string, OpenApiUrlTreeNode>();

        /// <summary>
        /// The relative directory path of the current node from the root node.
        /// </summary>
        public string Path { get; private set; } = "";

        /// <summary>
        /// Dictionary of labels and Path Item objects that describe the operations available on a node.
        /// </summary>
        public IDictionary<string, OpenApiPathItem> PathItems { get; } = new Dictionary<string, OpenApiPathItem>();

        /// <summary>
        /// A dictionary of key value pairs that contain information about a node.
        /// </summary>
        public IDictionary<string, List<string>> AdditionalData { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// Flag indicating whether a node segment is a path parameter.
        /// </summary>
        public bool IsParameter => Segment.StartsWith("{");

        /// <summary>
        /// The subdirectory of a relative path.
        /// </summary>
        public string Segment { get; private set; }

        /// <summary>
        /// Flag indicating whether the node's PathItems dictionary has operations
        /// under a given label.
        /// </summary>
        /// <param name="label">The name of the key for the target operations
        /// in the node's PathItems dictionary.</param>
        /// <returns>true or false.</returns>
        public bool HasOperations(string label)
        {
            Utils.CheckArgumentNullOrEmpty(label, nameof(label));

            if (!(PathItems?.ContainsKey(label) ?? false))
            {
                return false;
            }

            return PathItems[label].Operations?.Any() ?? false;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="segment">The subdirectory of a relative path.</param>
        private OpenApiUrlTreeNode(string segment)
        {
            Segment = segment;
        }

        /// <summary>
        /// Creates an empty structured directory of <see cref="OpenApiUrlTreeNode"/> node.
        /// </summary>
        /// <returns>The root node of the created <see cref="OpenApiUrlTreeNode"/> directory structure.</returns>
        public static OpenApiUrlTreeNode Create()
        {
            return new OpenApiUrlTreeNode(RootPathSegment);
        }

        /// <summary>
        /// Creates a structured directory of <see cref="OpenApiUrlTreeNode"/> nodes from the paths of an OpenAPI document.
        /// </summary>
        /// <param name="doc">The OpenAPI document.</param>
        /// <param name="label">Name tag for labelling the <see cref="OpenApiUrlTreeNode"/> nodes in the directory structure.</param>
        /// <returns>The root node of the created <see cref="OpenApiUrlTreeNode"/> directory structure.</returns>
        public static OpenApiUrlTreeNode Create(OpenApiDocument doc, string label)
        {
            Utils.CheckArgumentNull(doc, nameof(doc));
            Utils.CheckArgumentNullOrEmpty(label, nameof(label));

            var root = Create();

            var paths = doc.Paths;
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    root.Attach(path: path.Key,
                                pathItem: path.Value,
                                label: label);
                }
            }

            return root;
        }

        /// <summary>
        /// Retrieves the paths from an OpenAPI document and appends the items to an <see cref="OpenApiUrlTreeNode"/> node.
        /// </summary>
        /// <param name="doc">The OpenAPI document.</param>
        /// <param name="label">Name tag for labelling related <see cref="OpenApiUrlTreeNode"/> nodes in the directory structure.</param>
        public void Attach(OpenApiDocument doc, string label)
        {
            Utils.CheckArgumentNull(doc, nameof(doc));
            Utils.CheckArgumentNullOrEmpty(label, nameof(label));

            var paths = doc.Paths;
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    Attach(path: path.Key,
                           pathItem: path.Value,
                           label: label);
                }
            }
        }

        /// <summary>
        /// Appends a path and the PathItem to an <see cref="OpenApiUrlTreeNode"/> node.
        /// </summary>
        /// <param name="path">An OpenAPI path.</param>
        /// <param name="pathItem">Path Item object that describes the operations available on an OpenAPI path.</param>
        /// <param name="label">A name tag for labelling the <see cref="OpenApiUrlTreeNode"/> node.</param>
        /// <returns>An <see cref="OpenApiUrlTreeNode"/> node describing an OpenAPI path.</returns>
        public OpenApiUrlTreeNode Attach(string path,
                                         OpenApiPathItem pathItem,
                                         string label)
        {
            Utils.CheckArgumentNullOrEmpty(label, nameof(label));
            Utils.CheckArgumentNullOrEmpty(path, nameof(path));
            Utils.CheckArgumentNull(pathItem, nameof(pathItem));

            if (path.StartsWith(RootPathSegment))
            {
                // Remove leading slash
                path = path.Substring(1);
            }

            var segments = path.Split('/');

            return Attach(segments: segments,
                          pathItem: pathItem,
                          label: label,
                          currentPath: "");
        }

        /// <summary>
        /// Assembles the constituent properties of an <see cref="OpenApiUrlTreeNode"/> node.
        /// </summary>
        /// <param name="segments">IEnumerable subdirectories of a relative path.</param>
        /// <param name="pathItem">Path Item object that describes the operations available on an OpenAPI path.</param>
        /// <param name="label">A name tag for labelling the <see cref="OpenApiUrlTreeNode"/> node.</param>
        /// <param name="currentPath">The relative path of a node.</param>
        /// <returns>An <see cref="OpenApiUrlTreeNode"/> node with all constituent properties assembled.</returns>
        private OpenApiUrlTreeNode Attach(IEnumerable<string> segments,
                                          OpenApiPathItem pathItem,
                                          string label,
                                          string currentPath)
        {
            var segment = segments.FirstOrDefault();
            if (string.IsNullOrEmpty(segment))
            {
                if (PathItems.ContainsKey(label))
                {
                    throw new ArgumentException("A duplicate label already exists for this node.", nameof(label));
                }

                Path = currentPath;
                PathItems.Add(label, pathItem);
                return this;
            }

            // If the child segment has already been defined, then insert into it
            if (Children.ContainsKey(segment))
            {
                var newPath = currentPath + PathSeparator + segment;

                return Children[segment].Attach(segments: segments.Skip(1),
                                                pathItem: pathItem,
                                                label: label,
                                                currentPath: newPath);
            }
            else
            {
                var newPath = currentPath + PathSeparator + segment;

                var node = new OpenApiUrlTreeNode(segment)
                {
                    Path = newPath
                };

                Children[segment] = node;

                return node.Attach(segments: segments.Skip(1),
                                   pathItem: pathItem,
                                   label: label,
                                   currentPath: newPath);
            }
        }

        /// <summary>
        /// Adds additional data information to the AdditionalData property of the node.
        /// </summary>
        /// <param name="additionalData">A dictionary of key value pairs that contain information about a node.</param>
        public void AddAdditionalData(Dictionary<string, List<string>> additionalData)
        {
            Utils.CheckArgumentNull(additionalData, nameof(additionalData));

            foreach (var item in additionalData)
            {
                if (AdditionalData.ContainsKey(item.Key))
                {
                    AdditionalData[item.Key] = item.Value;
                }
                else
                {
                    AdditionalData.Add(item.Key, item.Value);
                }
            }
        }

        /// <summary>
        /// Write tree as Mermaid syntax
        /// </summary>
        /// <param name="writer">StreamWriter to write the Mermaid content to</param>
        public void WriteMermaid(StreamWriter writer)
        {
            writer.WriteLine("graph LR");
            foreach (var color in MermaidColorScheme)
            {
                writer.WriteLine($"classDef {color.Key} fill:{color.Value},stroke:#333,stroke-width:4px");
            }

            ProcessNode(this, writer);
        }

        /// <summary>
        /// Dictionary that maps a set of HTTP methods to HTML color.  Keys are sorted, uppercased, concatenated HTTP methods.
        /// </summary>
        public static Dictionary<string, string> MermaidColorScheme = new Dictionary<string, string>
        {
            { "GET", "lightSteelBlue" },
            { "POST", "SteelBlue" },
            { "GET_POST", "forestGreen" },
            { "DELETE_GET_PATCH", "yellowGreen" },
            { "DELETE_GET_PUT", "olive" },
            { "DELETE_GET", "DarkSeaGreen" },
            { "DELETE", "tomato" },
            { "OTHER", "white" }
        };
        
        private static void ProcessNode(OpenApiUrlTreeNode node, StreamWriter writer)
        {
            var path = string.IsNullOrEmpty(node.Path) ? "/" : SanitizeMermaidNode(node.Path);
            foreach (var child in node.Children)
            {
                writer.WriteLine($"{path} --> {SanitizeMermaidNode(child.Value.Path)}[\"{child.Key}\"]");
                ProcessNode(child.Value, writer);
            }
            var methods = String.Join("_", node.PathItems.SelectMany(p => p.Value.Operations.Select(o => o.Key))
                .Distinct()
                .Select(o => o.ToString().ToUpper())
                .OrderBy(o => o)
                .ToList());
            if (String.IsNullOrEmpty(methods)) methods = "OTHER";
            writer.WriteLine($"class {path} {methods}");
        }

        private static string SanitizeMermaidNode(string token)
        {
            return token.Replace("\\", "/")
                    .Replace("{", ":")
                    .Replace("}", "")
                    .Replace(".", "_")
                    .Replace(";", "_")
                    .Replace("-", "_")
                    .Replace("default", "def_ault");  // default is a reserved word for classes
        }
    }
}
