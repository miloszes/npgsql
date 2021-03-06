// created on 09/07/2003 at 20:20
// Npgsql.NpgsqlParameterCollection.cs
//
// Author:
// Brar Piening (brar@gmx.de)
//
// Rewritten from the scratch to derive from MarshalByRefObject instead of ArrayList.
// Recycled some parts of the original NpgsqlParameterCollection.cs
// by Francisco Jr. (fxjrlists@yahoo.com.br)
//
// Copyright (C) 2002 The Npgsql Development Team
// npgsql-general@gborg.postgresql.org
// http://gborg.postgresql.org/project/npgsql/projdisplay.php
//
// Permission to use, copy, modify, and distribute this software and its
// documentation for any purpose, without fee, and without a written
// agreement is hereby granted, provided that the above copyright notice
// and this paragraph and the following two paragraphs appear in all copies.
//
// IN NO EVENT SHALL THE NPGSQL DEVELOPMENT TEAM BE LIABLE TO ANY PARTY
// FOR DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES,
// INCLUDING LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
// DOCUMENTATION, EVEN IF THE NPGSQL DEVELOPMENT TEAM HAS BEEN ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//
// THE NPGSQL DEVELOPMENT TEAM SPECIFICALLY DISCLAIMS ANY WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
// ON AN "AS IS" BASIS, AND THE NPGSQL DEVELOPMENT TEAM HAS NO OBLIGATIONS
// TO PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Reflection;
using System.Resources;
using Common.Logging;
using Npgsql.Localization;
using NpgsqlTypes;

#if WITHDESIGN

#endif

namespace Npgsql
{
    /// <summary>
    /// Represents a collection of parameters relevant to a <see cref="NpgsqlCommand">NpgsqlCommand</see>
    /// as well as their respective mappings to columns in a <see cref="System.Data.DataSet">DataSet</see>.
    /// This class cannot be inherited.
    /// </summary>

#if WITHDESIGN
    [ListBindable(false)]
    [Editor(typeof(NpgsqlParametersEditor), typeof(System.Drawing.Design.UITypeEditor))]
#endif

    public sealed class NpgsqlParameterCollection : DbParameterCollection
    {
        private readonly List<NpgsqlParameter> InternalList = new List<NpgsqlParameter>();

        // Dictionary lookups for GetValue to improve performance
        private Dictionary<string, int> lookup;
        private Dictionary<string, int> lookupIgnoreCase;

        /// <summary>
        /// Initializes a new instance of the NpgsqlParameterCollection class.
        /// </summary>
        internal NpgsqlParameterCollection()
        {

            InvalidateHashLookups();
        }

        /// <summary>
        /// Invalidate the hash lookup tables.  This should be done any time a change
        /// may throw the lookups out of sync with the list.
        /// </summary>
        internal void InvalidateHashLookups()
        {
            lookup = null;
            lookupIgnoreCase = null;
        }

        #region NpgsqlParameterCollection Member

        /// <summary>
        /// Gets the <see cref="NpgsqlParameter">NpgsqlParameter</see> with the specified name.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see> to retrieve.</param>
        /// <value>The <see cref="NpgsqlParameter">NpgsqlParameter</see> with the specified name, or a null reference if the parameter is not found.</value>

#if WITHDESIGN
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif

        public new NpgsqlParameter this[string parameterName]
        {
            get
            {
                int index = IndexOf(parameterName);

                if (index == -1)
                {
                    throw new IndexOutOfRangeException("Parameter not found");
                }

                return this.InternalList[index];
            }
            set
            {
                int index = IndexOf(parameterName);

                if (index == -1)
                {
                    throw new IndexOutOfRangeException("Parameter not found");
                }

                NpgsqlParameter oldValue = this.InternalList[index];

                if (value.CleanName != oldValue.CleanName)
                {
                    InvalidateHashLookups();
                }

                this.InternalList[index] = value;
            }
        }

        /// <summary>
        /// Gets the <see cref="NpgsqlParameter">NpgsqlParameter</see> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the <see cref="NpgsqlParameter">NpgsqlParameter</see> to retrieve.</param>
        /// <value>The <see cref="NpgsqlParameter">NpgsqlParameter</see> at the specified index.</value>

#if WITHDESIGN
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif

        public new NpgsqlParameter this[int index]
        {
            get { return this.InternalList[index]; }
            set
            {
                NpgsqlParameter oldValue = this.InternalList[index];

                if (oldValue == value)
                {
                    // Reasigning the same value is a non-op.
                    return;
                }

                if (value.Collection != null)
                {
                    throw new InvalidOperationException("The parameter already belongs to a collection");
                }

                if (value.CleanName != oldValue.CleanName)
                {
                    InvalidateHashLookups();
                }

                this.InternalList[index] = value;
                value.Collection = this;
                oldValue.Collection = null;
            }
        }

        /// <summary>
        /// Adds the specified <see cref="NpgsqlParameter">NpgsqlParameter</see> object to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see>.
        /// </summary>
        /// <param name="value">The <see cref="NpgsqlParameter">NpgsqlParameter</see> to add to the collection.</param>
        /// <returns>The index of the new <see cref="NpgsqlParameter">NpgsqlParameter</see> object.</returns>
        public NpgsqlParameter Add(NpgsqlParameter value)
        {
            // Do not allow parameters without name.
            if (value.Collection != null)
            {
                throw new InvalidOperationException("The parameter already belongs to a collection");
            }

            this.InternalList.Add(value);
            value.Collection = this;
            this.InvalidateHashLookups();

            // Check if there is a name. If not, add a name based in the index of parameter.
            if (value.ParameterName.Trim() == String.Empty || (value.ParameterName.Length == 1 && value.ParameterName[0] == ':'))
            {
                value.ParameterName = ":" + "Parameter" + (IndexOf(value) + 1);
            }

            return value;
        }

        /// <summary>
        /// Obsolete. Use AddWithValue instead.
        /// </summary>
        /// <remarks>
        /// Use caution when using this overload of the
        /// <b>Add</b> method to specify integer parameter values.
        /// Because this overload takes a <i>value</i> of type Object,
        /// you must convert the integral value to an <b>Object</b>
        /// type when the value is zero, as the following C# example demonstrates.
        /// <code>parameters.Add(":pname", Convert.ToInt32(0));</code>
        /// If you do not perform this conversion, the compiler will assume you
        /// are attempting to call the NpgsqlParameterCollection.Add(string, DbType) overload.
        /// </remarks>
        [Obsolete("Do not call this method. Use AddWithValue instead.")]
        public NpgsqlParameter Add(string parameterName, object value)
        {
            return this.AddWithValue(parameterName, value);
        }

        /// <summary>
        /// Adds a <see cref="NpgsqlParameter">NpgsqlParameter</see> to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> given the specified parameter name and value.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see>.</param>
        /// <param name="value">The Value of the <see cref="NpgsqlParameter">NpgsqlParameter</see> to add to the collection.</param>
        /// <returns>The paramater that was added.</returns>
        public NpgsqlParameter AddWithValue(string parameterName, object value)
        {
            return this.Add(new NpgsqlParameter(parameterName, value));
        }

        /// <summary>
        /// Adds a <see cref="NpgsqlParameter">NpgsqlParameter</see> to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> given the specified parameter name and value.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see>.</param>
        /// <param name="value">The Value of the <see cref="NpgsqlParameter">NpgsqlParameter</see> to add to the collection.</param>
        /// <param name="parameterType">One of the NpgsqlDbType values.</param>
        /// <returns>The paramater that was added.</returns>
        public NpgsqlParameter AddWithValue(string parameterName, NpgsqlDbType parameterType, object value)
        {
            NpgsqlParameter param = new NpgsqlParameter(parameterName, parameterType);
            param.Value = value;
            return this.Add(param);
        }

        /// <summary>
        /// Adds a <see cref="NpgsqlParameter">NpgsqlParameter</see> to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> given the specified parameter name and value.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see>.</param>
        /// <param name="value">The Value of the <see cref="NpgsqlParameter">NpgsqlParameter</see> to add to the collection.</param>
        /// <param name="parameterType">One of the NpgsqlDbType values.</param>
        /// <param name="size">The length of the column.</param>
        /// <returns>The paramater that was added.</returns>
        public NpgsqlParameter AddWithValue(string parameterName, NpgsqlDbType parameterType, int size, object value)
        {
            NpgsqlParameter param = new NpgsqlParameter(parameterName, parameterType, size);
            param.Value = value;
            return this.Add(param);
        }

        /// <summary>
        /// Adds a <see cref="NpgsqlParameter">NpgsqlParameter</see> to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> given the specified parameter name and value.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see>.</param>
        /// <param name="value">The Value of the <see cref="NpgsqlParameter">NpgsqlParameter</see> to add to the collection.</param>
        /// <param name="parameterType">One of the NpgsqlDbType values.</param>
        /// <param name="size">The length of the column.</param>
        /// <param name="sourceColumn">The name of the source column.</param>
        /// <returns>The paramater that was added.</returns>
        public NpgsqlParameter AddWithValue(string parameterName, NpgsqlDbType parameterType, int size, string sourceColumn, object value)
        {
            NpgsqlParameter param = new NpgsqlParameter(parameterName, parameterType, size, sourceColumn);
            param.Value = value;
            return this.Add(param);
        }

        /// <summary>
        /// Adds a <see cref="NpgsqlParameter">NpgsqlParameter</see> to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> given the parameter name and the data type.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="parameterType">One of the DbType values.</param>
        /// <returns>The index of the new <see cref="NpgsqlParameter">NpgsqlParameter</see> object.</returns>
        public NpgsqlParameter Add(string parameterName, NpgsqlDbType parameterType)
        {
            return this.Add(new NpgsqlParameter(parameterName, parameterType));
        }

        /// <summary>
        /// Adds a <see cref="NpgsqlParameter">NpgsqlParameter</see> to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> with the parameter name, the data type, and the column length.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="parameterType">One of the DbType values.</param>
        /// <param name="size">The length of the column.</param>
        /// <returns>The index of the new <see cref="NpgsqlParameter">NpgsqlParameter</see> object.</returns>
        public NpgsqlParameter Add(string parameterName, NpgsqlDbType parameterType, int size)
        {
            return this.Add(new NpgsqlParameter(parameterName, parameterType, size));
        }

        /// <summary>
        /// Adds a <see cref="NpgsqlParameter">NpgsqlParameter</see> to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> with the parameter name, the data type, the column length, and the source column name.
        /// </summary>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <param name="parameterType">One of the DbType values.</param>
        /// <param name="size">The length of the column.</param>
        /// <param name="sourceColumn">The name of the source column.</param>
        /// <returns>The index of the new <see cref="NpgsqlParameter">NpgsqlParameter</see> object.</returns>
        public NpgsqlParameter Add(string parameterName, NpgsqlDbType parameterType, int size, string sourceColumn)
        {
            return this.Add(new NpgsqlParameter(parameterName, parameterType, size, sourceColumn));
        }

        #endregion

        #region IDataParameterCollection Member

        /// <summary>
        /// Removes the specified <see cref="NpgsqlParameter">NpgsqlParameter</see> from the collection using the parameter name.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see> object to retrieve.</param>
        public override void RemoveAt(string parameterName)
        {
            RemoveAt(this.IndexOf(parameterName));
        }

        /// <summary>
        /// Gets a value indicating whether a <see cref="NpgsqlParameter">NpgsqlParameter</see> with the specified parameter name exists in the collection.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see> object to find.</param>
        /// <returns><b>true</b> if the collection contains the parameter; otherwise, <b>false</b>.</returns>
        public override bool Contains(string parameterName)
        {
            return (IndexOf(parameterName) != -1);
        }

        /// <summary>
        /// Gets the location of the <see cref="NpgsqlParameter">NpgsqlParameter</see> in the collection with a specific parameter name.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see> object to find.</param>
        /// <returns>The zero-based location of the <see cref="NpgsqlParameter">NpgsqlParameter</see> in the collection.</returns>
        public override int IndexOf(string parameterName)
        {
            int retIndex;
            int scanIndex;

            if ((parameterName[0] == ':') || (parameterName[0] == '@'))
            {
                parameterName = parameterName.Remove(0, 1);
            }

            // Using a dictionary is much faster for 5 or more items            
            if (this.InternalList.Count >= 5)
            {            
                if (this.lookup == null)
                {
                    this.lookup = new Dictionary<string, int>();
                    for (scanIndex = 0 ; scanIndex < this.InternalList.Count ; scanIndex++)
                    {
                        var item = this.InternalList[scanIndex];

                        // Store only the first of each distinct value
                        if (! this.lookup.ContainsKey(item.CleanName))
                        {
                            this.lookup.Add(item.CleanName, scanIndex);
                        }
                    }
                }

                // Try to access the case sensitive parameter name first
                if (this.lookup.TryGetValue(parameterName, out retIndex))
                {
                    return retIndex;
                }

                // Case sensitive lookup failed, generate a case insensitive lookup
                if (this.lookupIgnoreCase == null)
                {
                    this.lookupIgnoreCase = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
                    for (scanIndex = 0 ; scanIndex < this.InternalList.Count ; scanIndex++)
                    {
                        var item = this.InternalList[scanIndex];
                        
                        // Store only the first of each distinct value
                        if (! this.lookupIgnoreCase.ContainsKey(item.CleanName))
                        {
                            this.lookupIgnoreCase.Add(item.CleanName, scanIndex);
                        }
                    }
                }

                // Then try to access the case insensitive parameter name
                if (this.lookupIgnoreCase.TryGetValue(parameterName, out retIndex))
                {
                    return retIndex;
                }

                return -1;
            }

            retIndex = -1;

            // Scan until a case insensitive match is found, and save its index for possible return.
            // Items that don't match loosely cannot possibly match exactly.
            for (scanIndex = 0 ; scanIndex < this.InternalList.Count ; scanIndex++)
            {
                var item = this.InternalList[scanIndex];

                if (string.Compare(parameterName, item.CleanName, StringComparison.InvariantCultureIgnoreCase) == 0)
                {
                    retIndex = scanIndex;

                    break;
                }
            }

            // Then continue the scan until a case sensitive match is found, and return it.
            // If a case insensitive match was found, it will be re-checked for an exact match.
            for ( ; scanIndex < this.InternalList.Count ; scanIndex++)
            {
                var item = this.InternalList[scanIndex];

                if(item.CleanName == parameterName)
                {
                    return scanIndex;
                }
            }

            // If a case insensitive match was found, it will be returned here.
            return retIndex;
        }

        #endregion

        #region IList Member

        /// <summary>
        /// Report whether the collection is read only.  Always false.
        /// </summary>
        public override bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Removes the specified <see cref="NpgsqlParameter">NpgsqlParameter</see> from the collection using a specific index.
        /// </summary>
        /// <param name="index">The zero-based index of the parameter.</param>
        public override void RemoveAt(int index)
        {
            if(this.InternalList.Count - 1 < index)
            {
                throw new IndexOutOfRangeException();
            }
            Remove(this.InternalList[index]);
        }

        /// <summary>
        /// Inserts a <see cref="NpgsqlParameter">NpgsqlParameter</see> into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index where the parameter is to be inserted within the collection.</param>
        /// <param name="oValue">The <see cref="NpgsqlParameter">NpgsqlParameter</see> to add to the collection.</param>
        public override void Insert(int index, object oValue)
        {
            CheckType(oValue);
            NpgsqlParameter value = oValue as NpgsqlParameter;
            if (value.Collection != null)
            {
                throw new InvalidOperationException("The parameter already belongs to a collection");
            }

            value.Collection = this;
            this.InternalList.Insert(index, value);
            this.InvalidateHashLookups();
        }

        /// <summary>
        /// Removes the specified <see cref="NpgsqlParameter">NpgsqlParameter</see> from the collection.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see> to remove from the collection.</param>
        public void Remove(string parameterName)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
            {
                throw new InvalidOperationException("No parameter with the specified name exists in the collection");
            }
            RemoveAt(index);
        }

        /// <summary>
        /// Removes the specified <see cref="NpgsqlParameter">NpgsqlParameter</see> from the collection.
        /// </summary>
        /// <param name="oValue">The <see cref="NpgsqlParameter">NpgsqlParameter</see> to remove from the collection.</param>
        public override void Remove(object oValue)
        {
            CheckType(oValue);
            Remove(oValue as NpgsqlParameter);
        }

        /// <summary>
        /// Gets a value indicating whether a <see cref="NpgsqlParameter">NpgsqlParameter</see> exists in the collection.
        /// </summary>
        /// <param name="value">The value of the <see cref="NpgsqlParameter">NpgsqlParameter</see> object to find.</param>
        /// <returns>true if the collection contains the <see cref="NpgsqlParameter">NpgsqlParameter</see> object; otherwise, false.</returns>
        public override bool Contains(object value)
        {
            if (!(value is NpgsqlParameter))
            {
                return false;
            }
            return this.InternalList.Contains((NpgsqlParameter) value);
        }

        /// <summary>
        /// Gets a value indicating whether a <see cref="NpgsqlParameter">NpgsqlParameter</see> with the specified parameter name exists in the collection.
        /// </summary>
        /// <param name="parameterName">The name of the <see cref="NpgsqlParameter">NpgsqlParameter</see> object to find.</param>
        /// <param name="parameter">A reference to the requested parameter is returned in this out param if it is found in the list.  This value is null if the parameter is not found.</param>
        /// <returns><b>true</b> if the collection contains the parameter and param will contain the parameter; otherwise, <b>false</b>.</returns>
        public bool TryGetValue(string parameterName, out NpgsqlParameter parameter)
        {
            int index = IndexOf(parameterName);

            if (index != -1)
            {
                parameter = InternalList[index];

                return true;
            }
            else
            {
                parameter = null;

                return false;
            }
        }

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        public override void Clear()
        {
            foreach(NpgsqlParameter toRemove in this.InternalList)
            {
                // clean up the parameter so it can be added to another command if required.
                toRemove.Collection = null;
            }
            this.InternalList.Clear();
            this.InvalidateHashLookups();
        }

        /// <summary>
        /// Gets the location of a <see cref="NpgsqlParameter">NpgsqlParameter</see> in the collection.
        /// </summary>
        /// <param name="value">The value of the <see cref="NpgsqlParameter">NpgsqlParameter</see> object to find.</param>
        /// <returns>The zero-based index of the <see cref="NpgsqlParameter">NpgsqlParameter</see> object in the collection.</returns>
        public override int IndexOf(object value)
        {
            CheckType(value);
            return this.InternalList.IndexOf((NpgsqlParameter) value);
        }

        /// <summary>
        /// Adds the specified <see cref="NpgsqlParameter">NpgsqlParameter</see> object to the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see>.
        /// </summary>
        /// <param name="value">The <see cref="NpgsqlParameter">NpgsqlParameter</see> to add to the collection.</param>
        /// <returns>The zero-based index of the new <see cref="NpgsqlParameter">NpgsqlParameter</see> object.</returns>
        public override int Add(object value)
        {
            CheckType(value);
            this.Add((NpgsqlParameter) value);
            return Count - 1;
        }

        /// <summary>
        /// Report whether the collection is fixed size.  Always false.
        /// </summary>
        public override bool IsFixedSize { get { return false; } }

        #endregion

        #region ICollection Member

        /// <summary>
        /// Report whether the collection is synchronized.
        /// </summary>
        public override bool IsSynchronized { get { return (InternalList as ICollection).IsSynchronized; } }

        /// <summary>
        /// Gets the number of <see cref="NpgsqlParameter">NpgsqlParameter</see> objects in the collection.
        /// </summary>
        /// <value>The number of <see cref="NpgsqlParameter">NpgsqlParameter</see> objects in the collection.</value>

#if WITHDESIGN
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif

        public override int Count { get { return this.InternalList.Count; } }

        /// <summary>
        /// Copies <see cref="NpgsqlParameter">NpgsqlParameter</see> objects from the <see cref="NpgsqlParameterCollection">NpgsqlParameterCollection</see> to the specified array.
        /// </summary>
        /// <param name="array">An <see cref="System.Array">Array</see> to which to copy the <see cref="NpgsqlParameter">NpgsqlParameter</see> objects in the collection.</param>
        /// <param name="index">The starting index of the array.</param>
        public override void CopyTo(Array array, int index)
        {
            (InternalList as ICollection).CopyTo(array, index);
            IRaiseItemChangedEvents x = InternalList as IRaiseItemChangedEvents;
        }

        /// <summary>
        /// Sync root.
        /// </summary>
        public override object SyncRoot { get { return (InternalList as ICollection).SyncRoot; } }

        #endregion

        #region IEnumerable Member

        /// <summary>
        /// Returns an enumerator that can iterate through the collection.
        /// </summary>
        /// <returns>An <see cref="System.Collections.IEnumerator">IEnumerator</see> that can be used to iterate through the collection.</returns>
        public override IEnumerator GetEnumerator()
        {
            return this.InternalList.GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Add an Array of parameters to the collection.
        /// </summary>
        /// <param name="values">Parameters to add.</param>
        public override void AddRange(Array values)
        {
            foreach (NpgsqlParameter parameter in values)
            {
                Add(parameter);
            }
        }

        /// <summary>
        /// Get parameter.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        protected override DbParameter GetParameter(string parameterName)
        {
            return this[parameterName];
        }

        /// <summary>
        /// Get parameter.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        protected override DbParameter GetParameter(int index)
        {
            return this[index];
        }

        /// <summary>
        /// Set parameter.
        /// </summary>
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            this[parameterName] = (NpgsqlParameter) value;
        }

        /// <summary>
        /// Set parameter.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        protected override void SetParameter(int index, DbParameter value)
        {
            this[index] = (NpgsqlParameter) value;
        }

        /// <summary>
        /// In methods taking an object as argument this method is used to verify
        /// that the argument has the type <see cref="NpgsqlParameter">NpgsqlParameter</see>
        /// </summary>
        /// <param name="Object">The object to verify</param>
        private void CheckType(object Object)
        {
            if (!(Object is NpgsqlParameter))
            {
                throw new InvalidCastException(
                    String.Format(L10N.WrongType, Object.GetType()));
            }
        }

/*
        /// <summary>
        /// In methods taking an array as argument this method is used to verify
        /// that the argument has the type <see cref="NpgsqlParameter">NpgsqlParameter</see>[]
        /// </summary>
        /// <param name="array">The array to verify</param>
        private void CheckType(Array array)
        {
            NpgsqlEventLog.LogMethodEnter(LogLevel.Debug, CLASSNAME, "CheckType", array);
            if (array.GetType() != typeof (NpgsqlParameter[]))
            {
                throw new InvalidCastException(
                    String.Format(this.resman.GetString("Exception_WrongType"), array.GetType().ToString()));
            }
        }
*/

        /// <summary>
        /// Report the offset within the collection of the given parameter.
        /// </summary>
        /// <param name="item">Parameter to find.</param>
        /// <returns>Index of the parameter, or -1 if the parameter is not present.</returns>
        public int IndexOf(NpgsqlParameter item)
        {
            return InternalList.IndexOf(item);
        }

        /// <summary>
        /// Insert the specified parameter into the collection.
        /// </summary>
        /// <param name="index">Index of the existing parameter before which to insert the new one.</param>
        /// <param name="item">Parameter to insert.</param>
        public void Insert(int index, NpgsqlParameter item)
        {
            if (item.Collection != null)
            {
                throw new Exception("The parameter already belongs to a collection");
            }

            InternalList.Insert(index, item);
            item.Collection = this;
            this.InvalidateHashLookups();
        }

        /// <summary>
        /// Report whether the specified parameter is present in the collection.
        /// </summary>
        /// <param name="item">Parameter to find.</param>
        /// <returns>True if the parameter was found, otherwise false.</returns>
        public bool Contains(NpgsqlParameter item)
        {
            return InternalList.Contains(item);
        }

        /// <summary>
        /// Remove the specified parameter from the collection.
        /// </summary>
        /// <param name="item">Parameter to remove.</param>
        /// <returns>True if the parameter was found and removed, otherwise false.</returns>
        public bool Remove(NpgsqlParameter item)
        {
            if(item == null)
            {
                return false;
            }
            if(item.Collection != this)
            {
                throw new InvalidOperationException("The item does not belong to this collection");
            }
            if(InternalList.Remove(item))
            {
                item.Collection = null;
                this.InvalidateHashLookups();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Convert collection to a System.Array.
        /// </summary>
        /// <param name="array">Destination array.</param>
        /// <param name="arrayIndex">Starting index in destination array.</param>
        public void CopyTo(NpgsqlParameter[] array, int arrayIndex)
        {
            InternalList.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Convert collection to a System.Array.
        /// </summary>
        /// <returns>NpgsqlParameter[]</returns>
        public NpgsqlParameter[] ToArray()
        {
            return InternalList.ToArray();
        }
    }
}
