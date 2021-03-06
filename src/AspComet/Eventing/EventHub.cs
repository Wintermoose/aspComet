﻿using System;
using System.Collections.Generic;

namespace AspComet.Eventing
{
    public static class EventHub
    {
        private static Lookup<Type, Delegate> Subscriptions = new Lookup<Type, Delegate>();
        private static Dictionary<Type, Lookup<ChannelPattern, Delegate>> ChannelSubscriptions = new Dictionary<Type, Lookup<ChannelPattern, Delegate>>();

        public static void Subscribe<T>(Action<T> action) where T : IEvent
        {
            Subscriptions.Add(typeof(T), action);
        }

        // NOTE: we assume, that there's just a few channel subscriptions, so that we can loop over them in
        // search of matches. If this is not true, we'd need better data structure 
        public static void Subscribe<T>( string channelPattern, Action<T> action ) where T : IChannelEvent
        {
            lock ( ChannelSubscriptions ) {
                if ( !ChannelSubscriptions.ContainsKey( typeof(T) ) ) {
                    ChannelSubscriptions[typeof( T )] = new Lookup<ChannelPattern, Delegate>();
                }
                ChannelSubscriptions[typeof( T )].Add( new ChannelPattern( channelPattern ), action );
            }
        }

        public static void Publish<T>(T ev) where T : IEvent
        {
            // NOTE: This should be thread safe as we can't currently unsubscribe, no need to lock
            if ( Subscriptions.Contains( typeof( T ) ) ) {
              // First run all generic handlers
              foreach ( Action<T> action in Subscriptions[typeof( T )] ) {
                TryAndInvoke( action, ev );
              }
            }

            // And then channel handlers
            if ( ev is IChannelEvent ) {
                Lookup<ChannelPattern, Delegate> handlers;
                if ( ChannelSubscriptions.TryGetValue( typeof(T), out handlers ) ) {
                    string channel = ((IChannelEvent)ev).Channel;
                    foreach ( ChannelPattern pattern in handlers.Keys ) {
                        if ( !pattern.Matches( channel ) ) continue;
                        foreach ( Action<T> action in handlers[pattern] ) {
                            TryAndInvoke( action, ev );
                        }
                    }
                }
            }
        }

        private static void TryAndInvoke<T>(Action<T> action, T ev) where T : IEvent
        {
            try
            {
                action(ev);
            }
            catch
            {
                ; // TODO: What if one of the handlers throws an exception?
            }
        }

        public static void Reset()
        {
            Subscriptions = new Lookup<Type, Delegate>();
        }
    }
}