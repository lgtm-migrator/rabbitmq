﻿using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using System.Text;
using System;

using plugin;
using System.Diagnostics;

namespace rabbitmq
{
    [PluginAttribute(PluginName = "RabbitMQ")]
    public class ORabbitMQ : IOutputPlugin
    {
        public void Execute(string jsonData, JObject set)
        {
            try
            {
                var settings = set.ToObject<Settings.RabbitMQ>();

                foreach(Settings.RabbitMQ.Server server in settings.Servers)
                {
                    var factory = new ConnectionFactory()
                    {
                        HostName = server.HostName,
                        VirtualHost = server.VirtualHost,
                        UserName = server.UserName,
                        Password = server.Password
                    };

                    try
                    {
                        using (var connection = factory.CreateConnection(server.HostName))
                        {
                            using (var channel = connection.CreateModel())
                            {
                                channel.ExchangeDeclare(exchange: server.ExchangeName,
                                                        type: server.ExchangeType);

                                channel.QueueDeclare(queue: server.QueueName,
                                                     durable: true,
                                                     exclusive: false,
                                                     autoDelete: false,
                                                     arguments: null);

                                channel.QueueBind(queue: server.QueueName,
                                                  exchange: server.ExchangeName,
                                                  routingKey: server.RoutingKey ?? server.QueueName);

                                var correlationId = Guid.NewGuid().ToString();
                                var properties = channel.CreateBasicProperties();
                                properties.CorrelationId = correlationId;
                                properties.Persistent = true;

                                var body = Encoding.UTF8.GetBytes(jsonData);

                                channel.BasicPublish(exchange: server.ExchangeName,
                                                     routingKey: server.RoutingKey ?? server.QueueName,
                                                     basicProperties: properties,
                                                     body: body);
                            }
                        }
                    }
                    catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException)
                    {
                        // Server Unrecheable, keep the execution going
                        using (EventLog eventLog = new EventLog("Application"))
                        {
                            eventLog.Source = "Winagent";
                            eventLog.WriteEntry(String.Format("RabbitMQ: Could not reach {0}", server.HostName), EventLogEntryType.Error, 0, 5);
                        }
                    }
                }

            }
            catch(Newtonsoft.Json.JsonReaderException jre)
            {
                throw new Exceptions.UnexpectedSettingValueException("RabbitMQ: Unexpected setting value", jre);
            }
            catch(Newtonsoft.Json.JsonSerializationException jse)
            {
                throw new Exceptions.RequiredSettingNotFound("RabbitMQ: Could not find a required setting", jse);
            }
        }
    }
}