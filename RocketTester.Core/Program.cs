using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using ons;
using RocketTester.ONS.Model;
using RocketTester.ONS.Util;

namespace RocketTester.Core
{
    class Program
    {
        
        private static string _ONSTopic = ConfigurationManager.AppSettings["ONSTopic"] ?? "";
        /*
        private static string _ONSProducerId = ConfigurationManager.AppSettings["ONSProducerId"] ?? "";
        private static string _ONSConsumerId = ConfigurationManager.AppSettings["ONSConsumerId"] ?? "";
        private static string _ONSAccessKey = ConfigurationManager.AppSettings["ONSAccessKey"] ?? "";
        private static string _ONSSecretKey = ConfigurationManager.AppSettings["ONSSecretKey"] ?? "";
        //*/

        static void Main(string[] args)
        {
            /*
            ONSFactoryProperty factoryInfo = new ONSFactoryProperty();
            factoryInfo.setFactoryProperty(ONSFactoryProperty.AccessKey, _ONSAccessKey);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.SecretKey, _ONSSecretKey);
            //factoryInfo.setFactoryProperty(ONSFactoryProperty.ProducerId, _ONSProducerId);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.ConsumerId, _ONSConsumerId);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.PublishTopics, _ONSTopic);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.LogPath, "D://log/rocketmq/consumer");

            // 集群订阅方式 (默认)
            // factoryInfo.setFactoryProperty(ONSFactoryProperty.MessageModel, ONSFactoryProperty.CLUSTERING);
            // 广播订阅方式
            // factoryInfo.setFactoryProperty(ONSFactoryProperty.MessageModel, ONSFactoryProperty.BROADCASTING);

            //push方式被动获取消息
            PushConsumer consumer = ONSFactory.getInstance().createPushConsumer(factoryInfo);
            //运行一段时间后会无法收到消息，这是因为c#垃圾回收机制把MyMessageListener实例给回收了，PushConsumer也存在这个问题，用单利来解决这个问题
            consumer.subscribe(_ONSTopic, "*", new MyMessageListener());
            consumer.start();

            string command = Console.ReadLine();
            consumer.shutdown();
            //*/

            //*
            ONSHelper.PushConsumer.subscribe(_ONSTopic, "*", new ONSMessageListener());
            ONSHelper.PushConsumer.start();

            string command = Console.ReadLine();
            ONSHelper.PushConsumer.shutdown();
            //*/
        }
    }
}
