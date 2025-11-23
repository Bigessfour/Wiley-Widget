using System;
using CommunityToolkit.Mvvm.Messaging;
using WileyWidget.Services.Messages;

namespace WileyWidget.Services
{
    /// <summary>
    /// Loosely-coupled service that publishes data messages via IMessenger.
    /// Replace Prism's IEventAggregator with CommunityToolkit.Mvvm's IMessenger.
    /// </summary>
    public class DataService
    {
        private readonly IMessenger _messenger;

        public DataService(IMessenger messenger)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        }

        /// <summary>
        /// Publish the given data as a strongly-typed message.
        /// Consumers can register for DataMessage{T} to receive payloads.
        /// </summary>
        public void PublishData<T>(T data)
        {
            var message = new DataMessage<T>(data);
            _messenger.Send(message);
        }
    }
}
