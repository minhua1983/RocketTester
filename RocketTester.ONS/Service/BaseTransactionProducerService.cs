﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RocketTester.ONS.Enum;
using RocketTester.ONS.Model;
using System.Web;
using System.Reflection;
using System.Collections.Concurrent;
using System.Configuration;
using ons;
using RocketTester.ONS.Util;
using Redis.Framework;
using Newtonsoft.Json;

namespace RocketTester.ONS.Service
{
    public abstract class BaseTransactionProducerService<T>
    {
        public ONSMessageTopic Topic { get; private set; }
        public ONSMessageTag Tag { get; private set; }

        //redis地址
        static string _RedisExchangeHosts = ConfigurationManager.AppSettings["RedisExchangeHosts"] ?? "";
        //启用redis第N个数据库
        static int _ONSRedisDBNumber = string.IsNullOrEmpty(ConfigurationManager.AppSettings["ONSRedisDBNumber"]) ? 11 : int.Parse(ConfigurationManager.AppSettings["ONSRedisDBNumber"]);
        //获取当前环境，p代表生产环境production，s代表测试环境staging，d代表开发环境development
        static string _Environment = ConfigurationManager.AppSettings["Environment"] ?? "p";

        public BaseTransactionProducerService(ONSMessageTopic topic, ONSMessageTag tag)
        {
            Topic = topic;
            Tag = tag;
        }

        /*不建议在静态构造中初始化生产者和消费者，因为你不能保证他在global.appaction_start之后执行，正犹豫这个原因可能导致rocketmq内部错误。
        static BaseTransactionService()
        { 
            
        }
        //*/

        /// <summary>
        /// Transact抽象方法，主要用于派生类重写它逻辑，即上游生产者事务方法。
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        protected abstract ONSTransactionResult ProcessCore(T model);

        /// <summary>
        /// 通过反射调用
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        protected ONSTransactionResult InternalProcess(T model)
        {
            //此处预留可以做干预
            ONSTransactionResult result = ProcessCore(model);
            //此处预留可以做干预
            return result;
        }

        /// <summary>
        /// 上游生产者的实现rocketmq的核心方法，其中会由rocketmq自动间接调用BaseTransactionProducerService实例的InternalProduce方法
        /// </summary>
        /// <param name="model">接收的参数</param>
        /// <returns>事务执行结果</returns>
        public ONSTransactionResult Process(T model)
        {
            //body不能为空，否则要报错，Func<string,TransactionResult>对应方法中，lambda什么的错误，实际根本没错，就是Message实体的body为空
            Message message = new Message(_Environment + "_" + Topic.ToString().ToLower(), Tag.ToString(), "no content");
            string key = _Environment + "_" + Topic.ToString().ToLower() + "_" + Tag.ToString() + "_" + Guid.NewGuid().ToString();

            //设置key作为自定义的消息唯一标识，不能用ONS消息自带的MsgId作为消息的唯一标识，因为它不保证一定不出现重复。
            message.setKey(key);

            LogHelper.Log("topic " + Topic);
            LogHelper.Log("tag " + Tag);

            //Func<T, ONSTransactionResult> func = this.InternalProduce;
            MethodInfo methodInfo = this.GetType().GetMethod("InternalProcess", BindingFlags.NonPublic | BindingFlags.Instance);
            //MethodInfo methodInfo = this.GetType().GetMethod("InternalProcess");

            LogHelper.Log("this.GetType().name " + this.GetType().FullName);

            string data = JsonConvert.SerializeObject(model);

            LogHelper.Log("data " + data);
            LogHelper.Log("methodInfo.Name " + methodInfo.Name);
            LogHelper.Log("methodInfo.ReflectedType.FullName " + methodInfo.ReflectedType.FullName);


            string executerMethodName = methodInfo.ReflectedType.FullName + "." + methodInfo.Name;
            string checkerMethodName = methodInfo.ReflectedType.FullName + "." + methodInfo.Name;

            //将方式实例和方式实例的参数都存到消息的属性中去。
            message.putUserProperties("executerMethodParameter", data);
            message.putUserProperties("executerMethod", executerMethodName);
            message.putUserProperties("checkerMethodParameter", data);
            message.putUserProperties("checkerMethod", checkerMethodName);

            //方式实例字典中不存在的话，则试图新增到字典中去
            if (!ONSHelper.ExecuterMethodDictionary.ContainsKey(executerMethodName))
            {
                ONSHelper.ExecuterMethodDictionary.TryAdd(executerMethodName, methodInfo);
            }
            if (!ONSHelper.CheckerMethodDictionary.ContainsKey(checkerMethodName))
            {
                ONSHelper.CheckerMethodDictionary.TryAdd(checkerMethodName, methodInfo);
            }

            //实例化LocalTransactionExecuter对象
            ONSLocalTransactionExecuter executer = new ONSLocalTransactionExecuter();

            LogHelper.Log("get ready to send message...");

            //生成半消息，并调用LocalTransactionExecuter对象的execute方法，它内部会执行委托实例（同时会将执行后的TransactionResult以Message的key为redis的key存入redis中），根据执行结果再决定是否要将消息状态设置为rollback或commit
            SendResultONS sendResultONS = ONSHelper.TransactionProducer.send(message, executer);

            LogHelper.Log("key " + key);

            //实例化redis工具
            RedisTool RT = new RedisTool(_ONSRedisDBNumber, _RedisExchangeHosts);
            //在redis中按消息的key获取其值（即委托实例执行后返回的一个TransactionResult对象，并做json序列化）
            string result = RT.StringGet(key) ?? "";

            LogHelper.Log("result " + result);
            LogHelper.Log("");

            //反序列化获取到一个TransactionResult对象
            return JsonConvert.DeserializeObject<ONSTransactionResult>(result);
        }


    }
}