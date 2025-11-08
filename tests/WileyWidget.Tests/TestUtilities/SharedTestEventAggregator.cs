using System;
using System.Collections.Generic;
using System.Linq;
using Prism.Events;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.Tests.TestUtilities
{
    // Lightweight test-friendly implementations to avoid UI-thread requirements in PubSubEvent
    public class SharedTestEventAggregator : IEventAggregator
    {
        private readonly Dictionary<Type, EventBase> _events = new();

        public TEventType GetEvent<TEventType>() where TEventType : EventBase, new()
        {
            var eventType = typeof(TEventType);
            if (!_events.ContainsKey(eventType))
            {
                if (eventType == typeof(Prism.Events.PubSubEvent))
                    _events[eventType] = new TEventType();
                else
                    _events[eventType] = new TEventType();
            }

            return (TEventType)_events[eventType];
        }
    }

    public class SharedTestRefreshDataMessage : RefreshDataMessage
    {
        private readonly List<Action<RefreshDataMessage>> _subs = new();

        public new SubscriptionToken Subscribe(Action<RefreshDataMessage> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<RefreshDataMessage>? filter)
        {
            _subs.Add(action);
            return new SubscriptionToken(_ => _subs.Remove(action));
        }

        public new SubscriptionToken Subscribe(Action<RefreshDataMessage> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
            => Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);

        public new SubscriptionToken Subscribe(Action<RefreshDataMessage> action, ThreadOption threadOption)
            => Subscribe(action, threadOption, false, null);

        public new SubscriptionToken Subscribe(Action<RefreshDataMessage> action, Predicate<RefreshDataMessage> filter)
            => Subscribe(action, ThreadOption.PublisherThread, false, filter);

        public new SubscriptionToken Subscribe(Action<RefreshDataMessage> action, bool keepSubscriberReferenceAlive)
            => Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);

        public new SubscriptionToken Subscribe(Action<RefreshDataMessage> action)
            => Subscribe(action, ThreadOption.PublisherThread, false, null);

        public new void Publish(RefreshDataMessage payload)
        {
            foreach (var s in _subs.ToArray()) s(payload);
        }
    }

    public class SharedTestEnterpriseChangedMessage : EnterpriseChangedMessage
    {
        private readonly List<Action<EnterpriseChangedMessage>> _subs = new();

        public new SubscriptionToken Subscribe(Action<EnterpriseChangedMessage> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<EnterpriseChangedMessage>? filter)
        {
            _subs.Add(action);
            return new SubscriptionToken(_ => _subs.Remove(action));
        }

        public new SubscriptionToken Subscribe(Action<EnterpriseChangedMessage> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
            => Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);

        public new SubscriptionToken Subscribe(Action<EnterpriseChangedMessage> action, ThreadOption threadOption)
            => Subscribe(action, threadOption, false, null);

        public new SubscriptionToken Subscribe(Action<EnterpriseChangedMessage> action, Predicate<EnterpriseChangedMessage> filter)
            => Subscribe(action, ThreadOption.PublisherThread, false, filter);

        public new SubscriptionToken Subscribe(Action<EnterpriseChangedMessage> action, bool keepSubscriberReferenceAlive)
            => Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive, null);

        public new SubscriptionToken Subscribe(Action<EnterpriseChangedMessage> action)
            => Subscribe(action, ThreadOption.PublisherThread, false, null);

        public new void Publish(EnterpriseChangedMessage payload)
        {
            foreach (var s in _subs.ToArray()) s(payload);
        }
    }
}
