﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace DeepStreamNet
{
    class JsonNetChangeListener : INotifyPropertyChanged, IDisposable
    {
        readonly INotifyCollectionChanged Collection;
        readonly HashSet<JsonNetChangeListener> subListener = new HashSet<JsonNetChangeListener>();

        public event PropertyChangedEventHandler PropertyChanged;

        protected JsonNetChangeListener(INotifyCollectionChanged collection)
        {
            Collection = collection;

            foreach (var item in ((IEnumerable)collection).OfType<JProperty>().Where(w => w.Value.Type == JTokenType.Array || w.Value.Type == JTokenType.Object))
            {
                var nlistener = Create((INotifyCollectionChanged)item.Value);
                subListener.Add(nlistener);
                nlistener.PropertyChanged += OnPropertyChanged;
            }

            Collection.CollectionChanged += Collection_CollectionChanged;
        }

        void OnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            PropertyChanged?.Invoke(Collection, args);
        }

        void Collection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Replace || e.Action == NotifyCollectionChangedAction.Remove || e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var item in e.OldItems)
                {
                    if (subListener.Contains(item))
                    {
                        var notify = subListener.FirstOrDefault(f => f == item);
                        notify.PropertyChanged -= OnPropertyChanged;
                        subListener.Remove(notify);
                        notify.Dispose();
                    }
                }
            }

            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (var item in e.NewItems.OfType<JContainer>().Where(w => w.Type == JTokenType.Array || w.Type == JTokenType.Object))
                {
                    var nlistener = Create((INotifyCollectionChanged)item);
                    subListener.Add(nlistener);
                    nlistener.PropertyChanged += OnPropertyChanged;
                }
            }

            if (e.Action != NotifyCollectionChangedAction.Add && e.Action != NotifyCollectionChangedAction.Replace)
                return;

            foreach (var item in e.NewItems.OfType<JValue>())
            {
                PropertyChanged?.Invoke(Collection, new PropertyChangedEventArgs(item.Path));
            }

            foreach (var item in e.NewItems.OfType<JProperty>()/*.Where(w => w.Value.Type != JTokenType.Array && w.Value.Type != JTokenType.Object)*/)
            {
                PropertyChanged?.Invoke(Collection, new PropertyChangedEventArgs(item.Path));
            }

            foreach (var item in e.NewItems.OfType<JObject>())
            {
                PropertyChanged?.Invoke(Collection, new PropertyChangedEventArgs(item.Path));
            }
        }

        public void Dispose()
        {
            Collection.CollectionChanged -= Collection_CollectionChanged;
            foreach (var item in subListener)
            {
                item.PropertyChanged -= OnPropertyChanged;
                item.Dispose();
            }
        }

        public static JsonNetChangeListener Create(INotifyCollectionChanged collection)
        {
            return new JsonNetChangeListener(collection);
        }
    }
}
