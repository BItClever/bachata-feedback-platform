using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace BachataFeedback.Api.Services.Moderation
{
    public sealed class RabbitMqModerationQueue : IModerationQueue, IDisposable
    {
        private readonly IConnection _conn;
        private readonly IChannel _ch;
        private readonly string _exchange;
        private readonly string _queue;

        public RabbitMqModerationQueue(IConfiguration cfg)
        {
            var s = cfg.GetSection("RabbitMQ");

            var factory = new ConnectionFactory
            {
                HostName = s.GetValue<string>("Host") ?? "localhost",
                Port = s.GetValue<int?>("Port") ?? 5672,
                UserName = s.GetValue<string>("User") ?? "guest",
                Password = s.GetValue<string>("Password") ?? "guest",
                ClientProvidedName = "api-moderation-producer"
            };

            _exchange = s.GetValue<string>("Exchange") ?? "moderation";
            _queue = s.GetValue<string>("Queue") ?? "moderation.jobs";

            // Соединение и канал (в 7.x всё async, в ctor ждём синхронно)
            _conn = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _ch = _conn.CreateChannelAsync().GetAwaiter().GetResult();

            // Топология: durable exchange/queue, lazy-очередь (хранить на диске)
            _ch.ExchangeDeclareAsync(
                exchange: _exchange,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null,
                noWait: false
            ).GetAwaiter().GetResult();

            var args = new Dictionary<string, object?>
            {
                ["x-queue-mode"] = "lazy"
            };

            _ch.QueueDeclareAsync(
                queue: _queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args,
                passive: false,
                noWait: false
            ).GetAwaiter().GetResult();

            _ch.QueueBindAsync(
                queue: _queue,
                exchange: _exchange,
                routingKey: "",
                arguments: null
            ).GetAwaiter().GetResult();

            // Вариант без publisher confirms (в 7.x их API сильно изменилось).
            // Мы опираемся на durable + persistent + lazy queue.
        }

        public async Task EnqueueAsync(ModerationMessage message, CancellationToken ct = default)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(message);

            // В 7.x свойства создаются напрямую. DeliveryMode — enum DeliveryModes.
            var props = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent, // важное: запись на диск
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _ch.BasicPublishAsync(
                exchange: _exchange,
                routingKey: "",
                mandatory: false,
                body: body,
                basicProperties: props,
                cancellationToken: ct
            );
        }

        public void Dispose()
        {
            try { _ch.Dispose(); } catch { /* ignore */ }
            try { _conn.Dispose(); } catch { /* ignore */ }
        }
    }
}