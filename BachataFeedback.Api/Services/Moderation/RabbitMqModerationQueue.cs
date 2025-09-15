using System;
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

            // Открываем соединение/канал (в 7.x всё async)
            _conn = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _ch = _conn.CreateChannelAsync().GetAwaiter().GetResult();

            // Объявляем топологию (await в конструкторе нельзя — поэтому синхронно ждём)
            _ch.ExchangeDeclareAsync(
                exchange: _exchange,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null,
                noWait: false
            ).GetAwaiter().GetResult();

            _ch.QueueDeclareAsync(
                queue: _queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                passive: false,
                noWait: false
            ).GetAwaiter().GetResult();

            _ch.QueueBindAsync(
                queue: _queue,
                exchange: _exchange,
                routingKey: "",
                arguments: null
            ).GetAwaiter().GetResult();
        }

        public async Task EnqueueAsync(ModerationMessage message, CancellationToken ct = default)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            // В 7.x — асинхронная публикация
            await _ch.BasicPublishAsync(
                exchange: _exchange,
                routingKey: "",
                mandatory: false,
                body: body,
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