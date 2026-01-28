using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WileyWidget.WinForms.Extensions
{
    /// <summary>
    /// Bridges ObservableCollection&lt;T&gt; to WinForms BindingList&lt;T&gt; for seamless data binding.
    /// 
    /// When an ObservableCollection is used as a DataSource in WinForms controls (e.g., SfComboBox),
    /// changes to the collection (Add/Remove) do not automatically trigger UI updates. This adapter
    /// wraps an ObservableCollection and exposes a BindingList interface that properly notifies
    /// WinForms controls of collection changes.
    ///
    /// USAGE:
    /// <code>
    /// // In panel binding code:
    /// var adapter = new ObservableCollectionToBindingListAdapter&lt;string&gt;(viewModel.AvailableDepartments);
    /// _comboDepartment.DataSource = adapter;
    /// // Store adapter reference for disposal
    /// _departmentAdapter = adapter;
    /// </code>
    ///
    /// The adapter automatically syncs changes in both directions:
    /// - ObservableCollection → BindingList (when items are added/removed in ViewModel)
    /// - BindingList → ObservableCollection (when items are modified via WinForms)
    ///
    /// DISPOSAL:
    /// Call Dispose() when the adapter is no longer needed (typically in panel Dispose method).
    /// </summary>
    public sealed class ObservableCollectionToBindingListAdapter<T> : BindingList<T>, IDisposable
    {
        private readonly ObservableCollection<T> _source;
        private bool _isUpdatingFromSource;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new adapter instance.
        /// </summary>
        /// <param name="source">The source ObservableCollection to adapt.</param>
        /// <exception cref="ArgumentNullException">Thrown if source is null.</exception>
        public ObservableCollectionToBindingListAdapter(ObservableCollection<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source), "ObservableCollection source cannot be null");

            _source = source;
            _isUpdatingFromSource = false;
            _isDisposed = false;

            // Populate initial data from source
            foreach (var item in _source)
            {
                Add(item);
            }

            // Subscribe to source collection changes
            _source.CollectionChanged += Source_CollectionChanged;
        }

        /// <summary>
        /// Handles collection changes from the source ObservableCollection.
        /// Syncs Add/Remove operations to the BindingList to trigger WinForms UI updates.
        /// </summary>
        private void Source_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isUpdatingFromSource || _isDisposed)
                return;

            try
            {
                _isUpdatingFromSource = true;

                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems != null)
                        {
                            for (int i = 0; i < e.NewItems.Count; i++)
                            {
                                var newItem = e.NewItems[i];
#pragma warning disable CS8600
                                var item = (T)newItem!;
#pragma warning restore CS8600
                                int index = e.NewStartingIndex + i;
                                if (index >= 0 && index <= Count)
                                {
                                    Insert(index, item);
                                }
                                else
                                {
                                    Add(item);
                                }
                            }
                        }
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null)
                        {
                            // Remove in reverse order to maintain correct indices
                            for (int i = e.OldItems.Count - 1; i >= 0; i--)
                            {
                                var oldItem = e.OldItems[i];
#pragma warning disable CS8600
                                var item = (T)oldItem!;
#pragma warning restore CS8600
                                int index = IndexOf(item);
                                if (index >= 0)
                                {
                                    RemoveAt(index);
                                }
                            }
                        }
                        break;

                    case NotifyCollectionChangedAction.Replace:
                        if (e.OldItems != null && e.NewItems != null)
                        {
                            for (int i = 0; i < e.NewItems.Count; i++)
                            {
                                var newItem = e.NewItems[i];
                                int index = e.NewStartingIndex + i;
                                if (newItem != null && index >= 0 && index < Count)
                                {
#pragma warning disable CS8600
                                    var item = (T)newItem!;
#pragma warning restore CS8600
                                    this[index] = item;
                                }
                            }
                        }
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        Clear();
                        foreach (var item in _source)
                        {
                            Add(item);
                        }
                        break;

                    case NotifyCollectionChangedAction.Move:
                        // Move is handled by Remove + Add
                        if (e.OldItems != null && e.OldItems.Count > 0)
                        {
                            var oldItem = e.OldItems[0];
                            if (oldItem != null)
                            {
#pragma warning disable CS8600
                                var item = (T)oldItem!;
#pragma warning restore CS8600
                                int oldIndex = e.OldStartingIndex;
                                int newIndex = e.NewStartingIndex;
                                if (oldIndex >= 0 && oldIndex < Count)
                                {
                                    RemoveAt(oldIndex);
                                    if (newIndex >= 0 && newIndex <= Count)
                                    {
                                        Insert(newIndex, item);
                                    }
                                    else
                                    {
                                        Add(item);
                                    }
                                }
                            }
                        }
                        break;
                }

                // Raise ListChanged event to notify WinForms controls
                OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing collection: {ex.Message}");
            }
            finally
            {
                _isUpdatingFromSource = false;
            }
        }

        /// <summary>
        /// Clean up by unsubscribing from events and clearing data.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                _source.CollectionChanged -= Source_CollectionChanged;
                Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during adapter disposal: {ex.Message}");
            }
            finally
            {
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
