﻿#region License
// Copyright (C) 2010-2016 Devexperts LLC
//
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at
// http://mozilla.org/MPL/2.0/.
#endregion

using com.dxfeed.api.candle;
using com.dxfeed.api.events;
using com.dxfeed.api.events.market;
using com.dxfeed.api.util;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace com.dxfeed.api
{
    //TODO: comments
    public class DXFeed : IDXFeed
    {

        //TODO: restore AssemblyInfo versions in all projects
        //TODO: update new samples with MSBuild commands

        private DXEndpoint endpoint = null;
        private HashSet<object> attachedSubscriptions = new HashSet<object>();

        internal DXFeed(DXEndpoint endpoint)
        {
            this.endpoint = endpoint;
        }

        /// <summary>
        /// Returns a default application-wide singleton instance of feed. Most applications use only a single
        /// data-source and should rely on this method to get one.
        /// </summary>
        /// <returns>Singleton instance of feed.</returns>
        public static DXFeed GetInstance()
        {
            return DXEndpoint.GetInstance().Feed;
        }

        /// <summary>
        /// Creates new subscription for a single event type that is attached to this feed.
        /// For multiple event types in one subscription use
        /// createSubscription(Class... eventTypes)
        /// This method creates new DXFeedSubscription.
        ///
        /// @see DXFeedSubscription#DXFeedSubscription(Class)
        /// @see #attachSubscription(DXFeedSubscription)
        /// </summary>
        /// <typeparam name="E">The type of events.</typeparam>
        /// <param name="eventType">The class of event types.</param>
        /// <returns>New DXFeedSubscription for a single event type.</returns>
        public IDXFeedSubscription<E> CreateSubscription<E>()
        {
            IDXFeedSubscription<E> subscription = new DXFeedSubscription<E>(endpoint) as IDXFeedSubscription<E>;
            subscription.Attach(this);
            return subscription;
        }

        /// <summary>
        /// Creates new subscription for multiple event types that is <i>attached</i> to this feed.
        /// For a single event type use CreateSubscription<E>().
        /// This method creates new DXFeedSubscription and invokes AttachSubscription.
        /// </summary>
        /// <typeparam name="E">The type of events.</typeparam>
        /// <param name="eventTypes">The classes of event types.</param>
        /// <returns>The new DXFeedSubscription.</returns>
        public IDXFeedSubscription<E> CreateSubscription<E>(params Type[] eventTypes)
        {
            IDXFeedSubscription<E> subscription = new DXFeedSubscription<E>(endpoint, eventTypes) as IDXFeedSubscription<E>;
            subscription.Attach(this);
            return subscription;
        }

        /// <summary>
        /// Attaches the given subscription to this feed. This method does nothing if the
        /// corresponding subscription is already attached to this feed.
        /// This feed publishes data to the attached subscription.
        /// 
        /// Application can attach DXFeedEventListener via DXFeedSubscription.AddEventListener 
        /// to get notified about data changes and can change its data subscription via 
        /// DXFeedSubscription methods.
        /// </summary>
        /// <typeparam name="E">The type of events.</typeparam>
        /// <param name="subscription">The subscription.</param>
        public void AttachSubscription<E>(IDXFeedSubscription<E> subscription)
        {
            if (attachedSubscriptions.Contains(subscription))
                return;
            attachedSubscriptions.Add(subscription);
        }

        /// <summary>
        /// Detaches the given subscription from this feed. This method does nothing if the
        /// corresponding subscription is not attached to this feed.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        public void DetachSubscription<E>(IDXFeedSubscription<E> subscription)
        {
            attachedSubscriptions.Remove(subscription);
        }

        /// <summary>
        ///     Requests the last event for the specified event type and symbol.
        ///     This method works only for event types that implement <see cref="LastingEvent"/> 
        ///     marker interface.
        ///     This method requests the data from the the uplink data provider, creates new event 
        ///     of the specified event type <c>E</c>, and completes the resulting promise with 
        ///     this event.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method is designed for retrieval of a snapshot only.
        ///         Use <see cref="DXFeedSubscription{E}"/> if you need event updates in real time.
        ///     </para>
        ///     <para>
        ///         The promise is <see cref="TaskStatus.Canceled"/> when the the underlying 
        ///         <see cref="DXEndpoint"/> is <see cref="DXEndpoint.Close()"/>.
        ///         If the event is not available for any transient reason (no subscription, no 
        ///         connection to uplink, etc), then the resulting promise completes when the issue 
        ///         is resolved, which may involve an arbitrarily long wait.
        ///         Use <see cref="CancellationTokenSource"/> class constructors and methods to 
        ///         specify timeout while waiting for promise to complete.
        ///         If the event is permanently not available (not supported), then the promise
        ///         completes exceptionally with <see cref="AggregateException"/>.
        ///     </para>
        ///     <para>
        ///         There is a bulk version of this method that works much faster for a single event 
        ///         type and multiple symbols. 
        ///         See <see cref="GetLastEventsPromises{E}(ICollection{object}, CancellationToken)"/>.
        ///     </para>
        ///     <para>
        ///         Note, that this method does not work when <see cref="DXEndpoint"/> was created 
        ///         with {@link DXEndpoint.Role#STREAM_FEED STREAM_FEED} role (promise completes 
        ///         exceptionally).
        ///     </para>
        ///     <para>Threads</para>
        ///     <para>
        ///         Use <see cref="Task.ContinueWith(Action{Task})"/> method on the resulting 
        ///         promise to receive notification when the promise becomes done.
        ///     </para>
        /// </remarks>
        /// <example>
        ///     Use the following pattern of code to acquire multiple events (either for multiple 
        ///     symbols and/or multiple events) and wait with a single timeout for all of them:
        ///     <code>
        ///         List&lt;Task&lt;LastingEvent&gt;&gt; promises = new List&lt;Task&lt;LastingEvent&gt;&gt;();
        ///         // iterate the following line for all events and/or symbols that are needed
        ///         promises.Add(feed.GetLastEventPromise&lt;eventType&gt;(symbol, new CancellationTokenSource(taskTimeout).Token));
        ///         // combine the list of promises into one with Task utility method and wait
        ///         try
        ///         {
        ///             Task.WaitAll(tasks.ToArray());
        ///         }
        ///         catch (AggregateException) {}
        ///         // now iterate the promises to retrieve results
        ///         foreach (Task&lt;LastingEvent&gt; promise in promises)
        ///             // result received exceptionally if this event was not found
        ///             // so first check that task completes successfully
        ///             if (promise.Status == TaskStatus.RanToCompletion)
        ///                 doSomethingWith(promise.Result);
        ///     </code>
        /// </example>
        /// <typeparam name="E">The event type.</typeparam>
        /// <param name="symbol">The symbol.</param>
        /// <param name="cancellationToken">The task cancellation token.</param>
        /// <returns>The promise for the result of the request.</returns>
        /// <exception cref="ArgumentException"> 
        ///     The <paramref name="symbol"/> symbol is not one of string or <see cref= "CandleSymbol"/>.
        /// </exception >
        /// <exception cref="ArgumentNullException">The <paramref name="symbol"/> is null.</exception>
        public async Task<LastingEvent> GetLastEventPromise<E>(object symbol,
            CancellationToken cancellationToken)
            where E : class, LastingEvent
        {

            MarketEventSymbols.ValidateSymbol(symbol);

            return await Task.Run(() =>
            {
                EventType events = EventTypeUtil.GetEventsType(typeof(E));
                LastingEventsCollector<E> collector = new LastingEventsCollector<E>();

                IDXFeedSubscription<E> s = CreateSubscription<E>();
                s.AddSymbols(symbol);
                s.AddEventListener(collector);
                while (!collector.HasEvent<E>(symbol))
                {
                    if (endpoint.State == DXEndpoint.EndpointState.Closed)
                        throw new OperationCanceledException("Endpoint was been closed.");
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return collector.GetEvent<E>(symbol);
            }, cancellationToken);
        }

        /// <summary>
        ///     Requests the last events for the specified event type and a collection of symbols.
        ///     This method works only for event types that implement <see cref="LastingEvent"/> 
        ///     marker interface.
        ///     This method requests the data from the the uplink data provider, creates new 
        ///     events of the specified event type <c>E</c>, and completes the resulting promises 
        ///     with these events.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is a bulk version of <see cref="GetLastEventPromise{E}(object, CancellationToken)"/> 
        ///         method.
        ///     </para>
        ///     <para>
        ///         The promise is <see cref="TaskStatus.Canceled"/> when the the underlying 
        ///         <see cref="DXEndpoint"/> is <see cref="DXEndpoint.Close()"/>.
        ///         If the event is not available for any transient reason (no subscription, no 
        ///         connection to uplink, etc), then the resulting promise completes when the 
        ///         issue is resolved, which may involve an arbitrarily long wait.
        ///         Use <see cref="CancellationTokenSource"/> class constructors and methods to 
        ///         specify timeout while waiting for promise to complete.
        ///         If the event is permanently not available (not supported), then the promise
        ///         completes exceptionally with <see cref="AggregateException"/>.
        ///     </para>
        ///     <para>
        ///         Note, that this method does not work when <see cref="DXEndpoint"/>  was created 
        ///         with {@link DXEndpoint.Role#STREAM_FEED STREAM_FEED} role (promise completes 
        ///         exceptionally).
        ///     </para>
        ///     <para>
        ///         Threads
        ///     </para>
        ///     <para>
        ///         Use <see cref="Task.ContinueWith(Action{Task})"/> method on the resulting 
        ///         promise to receive notification when the promise becomes done.
        ///     </para>
        /// </remarks>
        /// <example>
        ///     Use the following pattern of code to acquire multiple events (either for multiple 
        ///     symbols and/or multiple events) and wait with a single timeout for all of them:
        ///     <code>
        ///         List&lt;Task&lt;LastingEvent&gt;&gt; promises = feed.GetLastEventsPromises&lt;IDxTrade&gt;(
        ///             symbols, 
        ///             new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
        ///         // combine the list of promises into one with Task utility method and wait
        ///         try
        ///         {
        ///             Task.WaitAll(promises.ToArray());
        ///         }
        ///         catch (AggregateException) { }
        ///         // now iterate the promises to retrieve results
        ///         foreach (var promise in promises)
        ///             // result received exceptionally if this event was not found
        ///             // so first check that task completes successfully
        ///             if (promise.Status == TaskStatus.RanToCompletion)
        ///                 Console.WriteLine(promise.Result);
        ///     </code>
        /// </example>
        /// <typeparam name="E">The event type.</typeparam>
        /// <param name="symbols">The collection of symbols.</param>
        /// <param name="cancellationToken">The task cancellation token.</param>
        /// <returns>The list of promises for the result of the requests, one item in list per symbol.</returns>
        /// <exception cref="ArgumentException"> 
        ///     The one of <paramref name="symbols"/> is not <c>string</c> or <see cref="CandleSymbol"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">The one of <paramref name="symbols"/> is <c>null</c>.</exception>
        public List<Task<LastingEvent>> GetLastEventsPromises<E>(ICollection<object> symbols,
            CancellationToken cancellationToken)
            where E : class, LastingEvent
        {
            List<Task<LastingEvent>> result = new List<Task<LastingEvent>>(symbols.Count);
            foreach (object symbol in symbols)
            {
                result.Add(GetLastEventPromise<E>(symbol, cancellationToken));
            }
            return result;
        }

        /// <summary>
        ///     Requests a list of indexed events for the specified event type, symbol, and source.
        ///     This method works only for event types that implement <see cref="IndexedEvent"/> 
        ///     interface.
        ///     This method requests the data from the the uplink data provider, creates a list of 
        ///     events of the specified event type <c>E</c>, and completes the resulting promise 
        ///     with this list.
        ///     The events are ordered by <see cref="IndexedEvent.Index"/> in the list.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method is designed for retrieval of a snapshot only.
        ///         Use <see cref="IDxConnection.CreateSnapshotSubscription(EventType, long, IDxSnapshotListener)"/> 
        ///         if you need a list of time-series events that updates in real time.
        ///     </para>
        ///     <para>
        ///         The promise is <see cref="TaskStatus.Canceled"/> when the the underlying 
        ///         <see cref="DXEndpoint"/> is <see cref="DXEndpoint.Close()"/>.
        ///         If the event is not available for any transient reason (no subscription, no 
        ///         connection to uplink, etc), then the resulting promise completes when the 
        ///         issue is resolved, which may involve an arbitrarily long wait.
        ///         Use <see cref="CancellationTokenSource"/> class constructors and methods to 
        ///         specify timeout while waiting for promise to complete.
        ///         If the event is permanently not available (not supported), then the promise
        ///         completes exceptionally with <see cref="AggregateException"/>.
        ///     </para>
        ///     <para>
        ///         Note, that this method does not work when <see cref="DXEndpoint"/>  was created 
        ///         with {@link DXEndpoint.Role#STREAM_FEED STREAM_FEED} role (promise completes 
        ///         exceptionally).
        ///     </para>
        ///     <para>
        ///         Event source
        ///     </para>
        ///     <para>
        ///         Use the <see cref="IndexedEventSource.DEFAULT"/> value for <c>source</c> with 
        ///         events that do not have multiple sources (like <see cref="IDxSeries"/>). For 
        ///         events with multiple sources (like <see cref="IDxOrder"/> and <see cref="IDxSpreadOrder"/>), 
        ///         use an even-specific source class (for example, <see cref="OrderSource"/>).
        ///         This method does not support synthetic sources of orders (orders that are 
        ///         automatically generated from <see cref="IDxQuote"/> events).
        ///     </para>
        ///     <para>
        ///         Event flags and consistent snapshot
        ///     </para>
        ///     <para>
        ///         This method completes promise only when a consistent snapshot of time series has 
        ///         been received from the data feed. The <see cref="IndexedEvent.EventFlags"/> 
        ///         property of the events in the resulting list is always zero.
        ///     </para>
        ///     <para>
        ///         Threads
        ///     </para>
        ///     <para>
        ///         Use <see cref="Task.ContinueWith(Action{Task})"/> method on the resulting 
        ///         promise to receive notification when the promise becomes done.
        ///     </para>
        /// </remarks>
        /// <typeparam name="E">The event type.</typeparam>
        /// <param name="symbols">The collection of symbols.</param>
        /// <param name="source">The source.</param>
        /// <param name="cancellationToken">The task cancellation token.</param>
        /// <returns>The promise for the result of the request.</returns>
        /// <exception cref="ArgumentException"> 
        ///     The <paramref name="symbol"/> symbol is not one of string or <see cref= "CandleSymbol"/>.
        /// </exception >
        /// <exception cref="ArgumentNullException">
        ///     The <paramref name="symbol"/> or <paramref name="source"/> is null.
        /// </exception>
        public async Task<List<E>> GetIndexedEventsPromise<E>(object symbol,
            IndexedEventSource source, CancellationToken cancellationToken)
            where E : IndexedEvent
        {
            MarketEventSymbols.ValidateSymbol(symbol);
            if (source == null)
                throw new ArgumentNullException("Source is null!");
            return await FetchOrSubscribeFromHistory<E>(symbol, 0, long.MaxValue, source, cancellationToken);
        }

        /// <summary>
        ///     Requests time series of events for the specified event type, symbol, and a range 
        ///     of time.
        ///     This method works only for event types that implement <see cref="TimeSeriesEvent"/> 
        ///     interface.
        ///     This method requests the data from the the uplink data provider, creates a list of 
        ///     events of the specified event type <c>E</c>, and completes the resulting promise 
        ///     with this list.
        ///     The events are ordered by <see cref="TimeSeriesEvent.Time"/> in the list.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This method is designed for retrieval of a snapshot only.
        ///         Use <see cref="IDxConnection.CreateSnapshotSubscription(EventType, long, IDxSnapshotListener)"/> 
        ///         if you need a list of time-series events that updates in real time.
        ///     </para>
        ///     <para>
        ///         The range and depth of events that are available with this service is typically 
        ///         constrained by upstream data provider.
        ///     </para>
        ///     <para>
        ///         The promise is <see cref="TaskStatus.Canceled"/> when the the underlying 
        ///         <see cref="DXEndpoint"/> is <see cref="DXEndpoint.Close()"/>.
        ///         If the event is not available for any transient reason (no subscription, no 
        ///         connection to uplink, etc), then the resulting promise completes when the 
        ///         issue is resolved, which may involve an arbitrarily long wait.
        ///         Use <see cref="CancellationTokenSource"/> class constructors and methods to 
        ///         specify timeout while waiting for promise to complete.
        ///         If the event is permanently not available (not supported), then the promise
        ///         completes exceptionally with <see cref="AggregateException"/>.
        ///     </para>
        ///     <para>
        ///         Note, that this method does not work when <see cref="DXEndpoint"/>  was created 
        ///         with {@link DXEndpoint.Role#STREAM_FEED STREAM_FEED} role (promise completes 
        ///         exceptionally).
        ///     </para>
        ///     <para>
        ///         Event flags
        ///     </para>
        ///     <para>
        ///         This method completes promise only when a consistent snapshot of time series 
        ///         has been received from the data feed. The <see cref="IndexedEvent.EventFlags"/> 
        ///         property of the events in the resulting list is always zero.
        ///     </para>
        ///     <para>
        ///         Threads
        ///     </para>
        ///     <para>
        ///         Use <see cref="Task.ContinueWith(Action{Task})"/> method on the resulting 
        ///         promise to receive notification when the promise becomes done.
        ///     </para>
        /// </remarks>
        /// <typeparam name="E">The event type.</typeparam>
        /// <param name="symbol">The symbol.</param>
        /// <param name="fromTime">
        ///     The time, inclusive, to request events from <see cref="TimeSeriesEvent.Time"/>.
        /// </param>
        /// <param name="toTime">
        ///     The time, inclusive, to request events to <see cref="TimeSeriesEvent.Time"/>.
        ///     Use <see cref="long.MaxValue"/> to retrieve events without an upper limit on time.
        /// </param>
        /// <param name="cancellationToken">The task cancellation token.</param>
        /// <returns>The promise for the result of the request.</returns>
        /// <exception cref="ArgumentException"> 
        ///     The <paramref name="symbol"/> symbol is not one of string or <see cref= "CandleSymbol"/>.
        /// </exception >
        /// <exception cref="ArgumentNullException">The <paramref name="symbol"/> is null.</exception>
        public async Task<List<E>> GetTimeSeriesPromise<E>(object symbol, long fromTime,
            long toTime, CancellationToken cancellationToken)
            where E : TimeSeriesEvent
        {
            MarketEventSymbols.ValidateSymbol(symbol);
            return await FetchOrSubscribeFromHistory<E>(symbol, fromTime, toTime,
                IndexedEventSource.DEFAULT, cancellationToken);
        }

        #region Internal methods

        /// <summary>
        ///     Creates new snapshot subscription for a single event type that is attached to this feed.
        ///     This method creates new DXFeedSubscription.
        /// </summary>
        /// <typeparam name="E">The type of events.</typeparam>
        /// <param name="eventType">The class of event types.</param>
        /// <returns>New DXFeedSubscription for a single event type.</returns>
        internal IDXFeedSubscription<E> CreateSubscription<E>(long time, IndexedEventSource source)
        {
            IDXFeedSubscription<E> subscription = new DXFeedSubscription<E>(endpoint, time, source) as IDXFeedSubscription<E>;
            subscription.Attach(this);
            return subscription;
        }

        #endregion

        #region Private methods

        private class HistoryEventsCollector<E> : DXFeedSnapshotCollector<E>
            where E : IndexedEvent
        {
            private long fromTime;
            private long toTime;

            public HistoryEventsCollector(long fromTime, long toTime) : base()
            {
                this.fromTime = fromTime;
                this.toTime = toTime;
            }

            protected override IList<E> FilterEvents(IList<E> events)
            {
                IList<E> result = new List<E>();
                foreach (E e in events)
                {
                    long time = e is TimeSeriesEvent ? (e as TimeSeriesEvent).TimeStamp : 0;
                    if (time >= fromTime && time <= toTime)
                    {
                        e.EventFlags = 0;
                        result.Add(e);
                    }
                }
                return result;
            }
        }

        private async Task<List<E>> FetchOrSubscribeFromHistory<E>(object symbol, long fromTime,
            long toTime, IndexedEventSource source, CancellationToken cancellationToken)
            where E : IndexedEvent
        {
            MarketEventSymbols.ValidateSymbol(symbol);

            return await Task.Run(() =>
            {
                EventType events = EventTypeUtil.GetEventsType(typeof(E));
                HistoryEventsCollector<E> collector = new HistoryEventsCollector<E>(fromTime, toTime);

                IDXFeedSubscription<E> s = CreateSubscription<E>(fromTime, source);
                s.AddSymbols(symbol);
                s.AddEventListener(collector);

                while (!collector.IsDone)
                {
                    if (endpoint.State == DXEndpoint.EndpointState.Closed)
                        throw new OperationCanceledException("Endpoint was been closed.");
                    cancellationToken.ThrowIfCancellationRequested();
                }
                List<E> eventsList = collector.Events;
                eventsList.Reverse();
                return eventsList;
            }, cancellationToken);
        }
        #endregion
    }
}
