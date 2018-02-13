﻿using System;
using System.Linq;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace AMQP.Exemplo
{
    class Program
    {
        static void Main(string[] args)
        {
            EnsureResourceCreation();

            int producerCount = 2;
            int consumerCount = 10;



            for (int threadCount = 1; threadCount <= producerCount; threadCount++)
            {
                (new System.Threading.Thread(new System.Threading.ThreadStart(Producer))).Start();
            }

            //Espera 30 segundos para começar a processar
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(30));

            for (int threadCount = 1; threadCount <= consumerCount; threadCount++)
            {
                (new System.Threading.Thread(new System.Threading.ThreadStart(Consumer))).Start();
            }
        }

        /// <summary>
        /// Envia X mensagens (em plain text) para a exchange "minha_exchange" usando routing key "app"
        /// </summary>
        static void Producer()
        {
            using (IConnection connection = BuildConnection())
            {
                using (IModel model = connection.CreateModel())
                {
                    for (long seq = 1; seq < 500_000; seq++)
                    {

                        byte[] messageBodyBytes = System.Text.Encoding.UTF8.GetBytes($"Hello, world! NUM {seq}");

                        IBasicProperties props = model.CreateBasicProperties();
                        props.ContentType = "text/plain";
                        props.DeliveryMode = 2;
                        props.Headers = new Dictionary<string, object>
                    {
                        { "latitude", 51.5252949 },
                        { "longitude", -0.0905493 }
                    };

                        model.BasicPublish("minha_exchange",
                                           "app", props,
                                           messageBodyBytes);

                    }
                }
            }
            Console.WriteLine("Foram enviadas 500 000 mensagens para a fila");
        }


        /// <summary>
        /// Consome Mensagens da Fila
        /// </summary>
        static void Consumer()
        {
            IConnection connection = BuildConnection();
            IModel model = connection.CreateModel();
            var consumer = new EventingBasicConsumer(model);
            consumer.Received += (channel, deliverEventArgs) =>
            {
                var body = deliverEventArgs.Body;
                Console.Write(".");
                model.BasicAck(deliverEventArgs.DeliveryTag, false);
            };
            String consumerTag = model.BasicConsume("queue1_work", false, consumer);
        }

        #region Infra

        private static void EnsureResourceCreation()
        {
            using (IConnection connection = BuildConnection())
            {
                using (IModel model = connection.CreateModel())
                {
                    model.ExchangeDeclare("minha_exchange", "topic", false, false, null);

                    model.QueueDeclare("queue1_work", true, false, false, null);
                    model.QueueBind("queue1_work", "minha_exchange", "app");
                }
            }
        }

        private static IConnection BuildConnection()
        {
            ConnectionFactory factory = new ConnectionFactory
            {
                UserName = "usuario",
                Password = "senha",
                VirtualHost = "exemplo_amqp",
                HostName = "rabbitmq",
                Port = 5672,
                AutomaticRecoveryEnabled = true
            };

            IConnection conn = null;
            int retryCount = 0;
            do
            {

                try
                {
                    conn = factory.CreateConnection();
                }
                catch (BrokerUnreachableException rootException) when (DecomposeExceptionTree(rootException).Any(it => it is ConnectFailureException && (it?.InnerException?.Message?.Contains("Connection refused") ?? false)))
                {
                    System.Threading.Thread.Sleep(TimeSpan.FromSeconds((retryCount + 1) * 2));
                }
                catch
                {
                    throw;
                }

            } while (conn == null && ++retryCount <= 5);
            if (conn == null)
                throw new InvalidOperationException($"Não foi possível conectar ao RabbitMQ após {retryCount} tentativas");
            return conn;
        }


        public static IEnumerable<Exception> DecomposeExceptionTree(Exception currentException)
        {
            while (currentException != null)
            {
                yield return currentException;
                currentException = currentException.InnerException;
            }
        }

        #endregion
    }
}

