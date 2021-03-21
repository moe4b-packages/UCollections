using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace UCollections
{
    [Serializable]
    public abstract class UDictionary : UCollection
    {
        [CustomPropertyDrawer(typeof(UDictionary), true)]
        public class Drawer : BaseDrawer
        {
            protected override SerializedProperty GetList() => property.FindPropertyRelative("keys");

            public SerializedProperty keys;
            SerializedProperty values;

            public bool IsAligned => keys.arraySize == values.arraySize;

            public const float NestedElementSpacing = 2f;

            public const float KeyValuePadding = 10f;

            protected override void Init()
            {
                base.Init();

                keys = list;
                values = property.FindPropertyRelative("values");

                gui.onAddCallback = Add;
                gui.onRemoveCallback = Remove;
                gui.onReorderCallbackWithDetails += Reorder;
            }

            #region Height
            protected override float CalculateHeight(float height)
            {
                if (IsAligned)
                    return base.CalculateHeight(height);
                else
                    return height + gui.headerHeight;
            }

            protected override float GetElementHeight(int index)
            {
                SerializedProperty key = keys.GetArrayElementAtIndex(index);
                SerializedProperty value = values.GetArrayElementAtIndex(index);

                var kHeight = GetChildernSingleHeight(key, NestedElementSpacing);
                var vHeight = GetChildernSingleHeight(value, NestedElementSpacing);

                var max = Math.Max(kHeight, vHeight);

                max = Math.Max(max, SingleLineHeight);

                return max + ElementHeightPadding;
            }
            #endregion

            #region Draw
            protected override void Draw(Rect rect)
            {
                if (IsAligned)
                    base.Draw(rect);
                else
                    DrawAlignmentWarning(rect);
            }

            #region Draw Element
            protected override void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
            {
                rect.height -= ElementHeightPadding;
                rect.y += ElementHeightPadding / 2;

                var key = keys.GetArrayElementAtIndex(index);
                var value = values.GetArrayElementAtIndex(index);

                var areas = Split(rect, 40, 60);

                DrawKey(areas[0], key, index);
                DrawValue(areas[1], value, index);
            }

            void DrawKey(Rect rect, SerializedProperty property, int index)
            {
                EditorGUIUtility.labelWidth = 60;

                rect.x += KeyValuePadding / 2f;
                rect.width -= KeyValuePadding;

                DrawField(rect, property);
            }

            void DrawValue(Rect rect, SerializedProperty property, int index)
            {
                EditorGUIUtility.labelWidth = 80;

                rect.x += KeyValuePadding / 2f;
                rect.width -= KeyValuePadding;

                DrawField(rect, property);
            }

            protected override void DrawField(Rect rect, SerializedProperty property)
            {
                rect.height = SingleLineHeight;

                if (IsInline(property))
                {
                    EditorGUI.PropertyField(rect, property, GUIContent.none);
                }
                else
                {
                    foreach (var child in IterateChildern(property))
                    {
                        EditorGUI.PropertyField(rect, child, false);

                        rect.y += SingleLineHeight + NestedElementSpacing;
                    }
                }
            }
            #endregion

            void DrawAlignmentWarning(Rect rect)
            {
                var width = 80f;
                var spacing = 5f;

                rect.width -= width;

                EditorGUI.HelpBox(rect, $" Misalignment of {property.displayName} Detected", MessageType.Error);

                rect.x += rect.width + spacing;
                rect.width = width - spacing;

                if (GUI.Button(rect, "Fix"))
                {
                    if (keys.arraySize > values.arraySize)
                    {
                        var difference = keys.arraySize - values.arraySize;

                        for (int i = 0; i < difference; i++)
                        {
                            keys.GetArrayElementAtIndex(0).objectReferenceValue = null;
                            keys.DeleteArrayElementAtIndex(0);
                        }
                    }
                    else if (keys.arraySize < values.arraySize)
                    {
                        var difference = values.arraySize - keys.arraySize;

                        for (int i = 0; i < difference; i++)
                        {
                            values.GetArrayElementAtIndex(0).objectReferenceValue = null;
                            values.DeleteArrayElementAtIndex(0);
                        }
                    }
                }
            }
            #endregion

            #region Operation Callbacks
            void Add(ReorderableList list)
            {
                values.InsertArrayElementAtIndex(values.arraySize);

                defaults.DoAddButton(list);
            }

            void Remove(ReorderableList list)
            {
                values.DeleteArrayElementAtIndex(list.index);

                defaults.DoRemoveButton(list);
            }

            void Reorder(ReorderableList list, int oldIndex, int newIndex)
            {
                values.MoveArrayElement(oldIndex, newIndex);
            }
            #endregion
        }
    }

    [Serializable]
    public class UDictionary<TKey, TValue> : UDictionary, IDictionary<TKey, TValue>
    {
        [SerializeField]
        List<TKey> keys;
        public List<TKey> Keys => keys;
        ICollection<TKey> IDictionary<TKey, TValue>.Keys => keys;

        [SerializeField]
        List<TValue> values;
        public List<TValue> Values => values;
        ICollection<TValue> IDictionary<TKey, TValue>.Values => values;

        public override int Count => keys.Count;

        public bool IsReadOnly => false;

        Dictionary<TKey, TValue> cache;

        public bool Cached => cache != null;

        public Dictionary<TKey, TValue> Dictionary
        {
            get
            {
                if (cache == null)
                {
                    cache = new Dictionary<TKey, TValue>();

                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (keys[i] == null) continue;

                        cache[keys[i]] = values[i];
                    }
                }

                return cache;
            }
        }

        public TValue this[TKey key]
        {
            get => Dictionary[key];
            set
            {
                var index = keys.IndexOf(key);

                if (index < 0)
                {
                    Add(key, value);
                }
                else
                {
                    values[index] = value;
                    if (Cached) Dictionary[key] = value;
                }
            }
        }

        public bool TryGetValue(TKey key, out TValue value) => Dictionary.TryGetValue(key, out value);

        public bool ContainsKey(TKey key) => Dictionary.ContainsKey(key);
        public bool Contains(KeyValuePair<TKey, TValue> item) => ContainsKey(item.Key);

        public void Add(TKey key, TValue value)
        {
            if (ContainsKey(key)) throw new ArgumentException($"Key {key} Already Added With Collection");

            keys.Add(key);
            values.Add(value);

            if (Cached) Dictionary.Add(key, value);
        }
        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public void AddAll(IDictionary<TKey, TValue> collection)
        {
            foreach (var pair in collection)
                this[pair.Key] = pair.Value;
        }

        public bool Remove(TKey key)
        {
            var index = keys.IndexOf(key);

            if (index < 0) return false;

            keys.RemoveAt(index);
            values.RemoveAt(index);

            if (Cached) Dictionary.Remove(key);

            return true;
        }
        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

        public void Clear()
        {
            keys.Clear();
            values.Clear();

            if (Cached) Dictionary.Clear();
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => (Dictionary as IDictionary).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Dictionary.GetEnumerator();

        public UDictionary()
        {
            values = new List<TValue>();
            keys = new List<TKey>();
        }
    }
}