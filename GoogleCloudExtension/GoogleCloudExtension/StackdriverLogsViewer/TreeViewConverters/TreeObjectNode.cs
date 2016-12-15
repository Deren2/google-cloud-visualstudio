﻿// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Apis.Logging.v2.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace GoogleCloudExtension.StackdriverLogsViewer.TreeViewConverters
{
    /// <summary>
    /// Utility methods primarily used by ObjectNodeTree 
    /// </summary>
    public static class TypeUtil
    {
        /// <summary>
        /// Check if the type is a IList generic type.
        /// </summary>
        public static bool IsListType(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }

        /// <summary>
        /// Check if the type is IDictionary type.
        /// </summary>
        public static bool IsDictionaryType(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

        /// <summary>
        /// Check if the object is a IList type
        /// </summary>
        public static bool IsListObject(this object obj)
        {
            return obj != null && obj is IList && obj.GetType().IsListType();
        }

        /// <summary>
        /// Check if the object is IDictionary
        /// </summary>
        public static bool IsDictionaryObject(this object obj)
        {
            return obj != null && obj.GetType().IsDictionaryType();
        }

        /// <summary>
        /// Check if the object is Numeric type
        /// </summary>
        public static bool IsNumericType(this object obj)
        {
            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Log Viewer detail tree view object node.
    /// An object node contains the object name, obj and object properties as children.
    /// The object properties are of ObjectNodeTree type or Payload type. 
    /// With the children, the ObjectNodeTree itself forms a tree structure.
    /// </summary>
    internal class ObjectNodeTree
    {
        /// <summary>
        /// The list of supported class. 
        /// </summary>
        private readonly static Type[] s_supportedTypes = new Type[]
        {
            typeof(MonitoredResource),
            typeof(HttpRequest),
            typeof(LogEntryOperation),
            typeof(SourceLocation),
            typeof(LogLine),
            typeof(LogEntry)
        };

        /// <summary>
        /// Gets the DisplayValue
        /// Tree node displays label in format of Name : DisplayValue.  
        /// </summary>
        public string NodeValue { get; private set; }

        /// <summary>
        /// Gets the obj visibility. 
        /// Do not display ":" if the NodeValue 
        /// </summary>
        public Visibility ValueVisibility => 
            String.IsNullOrWhiteSpace(NodeValue) ? Visibility.Hidden : Visibility.Visible;

        /// <summary>
        /// Gets the object name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets tree node children.
        /// It contains all properties of the node object.
        /// </summary>
        public object Children { get; private set; }

        /// <summary>
        /// Create an instance of the <seealso cref="ObjectNodeTree"/> class.
        /// </summary>
        /// <param name="obj">An object</param>
        public ObjectNodeTree(object obj): this("root", obj)
        { }

        /// <summary>
        /// Create an instance of the <seealso cref="ObjectNodeTree"/> class.
        /// </summary>
        /// <param name="name">object name</param>
        /// <param name="obj">An object</param>
        private ObjectNodeTree(string name, object obj)
        {
            Name = name;
            if (obj == null)
            {
                NodeValue = null;
                return;
            }

            // Children = new ObservableCollection<object>();
            ParseObjectTree(obj);
        }

        #region parser

        private void TryParse(Action parser)
        {
            try
            {
                parser();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                Debug.Assert(false);
            }
        }

        private void ParseEnumerable(object obj)
        {
            var collection = new ObservableCollection<object>();
            Children = collection;
            IEnumerable arr = obj as IEnumerable;
            int i = 0;
            foreach (var ele in arr)
            {
                collection.Add(new ObjectNodeTree("[" + i + "]", ele));
                ++i;
            }

            NodeValue = $"[{i}]";
        }

        private void ParseDictionary(object obj)
        {
            //var dict = obj as IDictionary<string, object>;
            Children = obj;
        }

        private void ParseClassProperties(object obj, Type type)
        {
            var collection = new ObservableCollection<object>();
            Children = collection;
            PropertyInfo[] properties = type.GetProperties();
            foreach (PropertyInfo p in properties)
            {
                if (!p.PropertyType.IsPublic)
                {
                    continue;
                }

                try
                {
                    object v = p.GetValue(obj);
                    if (v != null)
                    {
                        collection.Add(new ObjectNodeTree(p.Name, v));
                    }
                }
                catch (Exception ex)
                {
                    if (ex is ArgumentException || ex is TargetException || ex is TargetParameterCountException
                        || ex is MethodAccessException || ex is TargetInvocationException)
                    {
                        collection.Add(new Payload(p.Name, $"Type: {p.PropertyType}. PropertyInfo.GetValue failed."));
                        Debug.WriteLine(ex.ToString());
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
        #endregion



        /// <summary>
        /// Parse the object properties recursively.
        /// </summary>
        private void ParseObjectTree_2_delete(string name, object obj)
        {
            ParseObjectTree(obj);

            //// To be sure NodeValue
            //if (Children?.Count > 0)
            //{
            //    // NodeValue = null;
            //}
            //else
            //{
            //    if (NodeValue == null && obj != null)
            //    {
            //        NodeValue = obj.ToString();
            //    }
            //}
        }

        /// <summary>
        /// Parsing the object properties as a tree.
        /// </summary>
        private void ParseObjectTree(object obj)
        {
            Type type = obj.GetType();

            if (obj.IsNumericType() || obj is string || obj is DateTime)
            {
                NodeValue = obj.ToString();
            }
            else if (type.IsArray)
            {
                ParseEnumerable(obj);
            }            
            // There is no easy way to parse generic IDictionarytype into ObjectNodeTree.
            // Display them as Payload object.
            else if (obj.IsDictionaryObject())
            {
                // Children.Add(new Payload(name, obj));
                ParseDictionary(obj);
            }
            else if (s_supportedTypes.Contains(type))
            {
                ParseClassProperties(obj, type);
            }
            else
            {
                // The best effort.
                NodeValue = obj.ToString();
            }
        }
    }
}
