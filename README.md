# RocketTester
这是一个基于阿里云RocketMQ封装的简单例子
## 为何需要用rocketmq
由于业务的增长，各个应用之间无可避免会有依赖。举个简单例子：订单系统生成一个订单后，必须要将订单等信息传递给下游的物流系统做进一步处理。

以往我们可以用web service或http或mq将请求将订单信息发送到物流系统。但是如果当信息发送时网络出现了问题，无法保证数据的最终一致性。

为了解决这个问题，必须得开发人员实现重试机制，这对开发人员来说是额外的工作。现在有了rocketmq的事物消息后，可以轻松帮我们解决这个重试的问题。

## 上游订单系统以基于http传统调用接口的方式
上游订单系统基于http通知下游物流系统的伪代码
```
//实例化订单服务类
OrderService orderService = new OrderService();

//创建订单，并获取结果
ServiceResult serviceResult = orderService.Create(orderInfo);

//判断结果是否成功（假定ReturnCode为1时，说明订单创建成功）
if(serviceResult.ReturnCode == 1)
{
    //生成HttpWebRequest对象发送订单信息给到物流系统
    HttpWebRequest request = (HttpWebRequest)WebRequest.Create("物流系统的接口地址");
    ...
    ...
    ...
}

```
这种代码是我们最常见的处理方式，如果考虑再细一些，可以对“发送订单信息给到物流系统”这步做一个重试的机制（这个重试机制开发有一定复杂度），进而尽可能地实现最终一致性。

## 上游订单系统以阿里云rocketmq事务消息调用的方式
先在订单系统的项目中引用RocketTester.ONS.dll，然后在web.config的AppSettings中加入如下节点
```
    <!--Redis地址-->
    <add key="RedisExchangeHosts" value="redis连接字符串" />
    <!--ONS设置-->
    <add key="ONSRedisDBNumber" value="redis所用的数据库号码"/>
    <add key="ONSRedisTransactionResultExpireIn" value="存储TransactionResult的超时时间（单位秒），如18000"/>
    <add key="ONSTopic" value="消息的主题"/>
    <add key="ONSProducerId" value="消息生产者唯一标识"/>
    <add key="ONSConsumerId" value="消息消费者唯一标识"/>
    <add key="ONSAccessKey" value="RAM账号的AccessKey"/>
    <add key="ONSSecretKey" value="RAM账号的SecretKey"/>
    <add key="ONSCheckerTest" value="是否启用checker测试模式，默认得用false"/>
```

接着需要在订单系统中的Global.asax.cs中加入如下代码
```
    void Application_Start(object sender, EventArgs e)
    {
        ...
        ...
        ...
        
        //调用帮助类的初始化方法，它会以单例模式生成TransactionProducer实例和PushConsumer实例。
        ONSHelper.Initialize();
    }
    
    void Application_End(object sender, EventArgs e)
    {
        //调用帮助类的销毁方法，它会销毁之前生成的那个TransactionProducer实例和PushConsumer实例。
        ONSHelper.Destroy();
    }
```

现在有了阿里云RocketMQ的事物消息后，经过封装后，可以这样调用。
```
//json序列化orderInfo对象
string orderInfoJson = JsonConvert.SerializeObject(orderInfo)

//实例化订单服务类
OrderService orderService = new OrderService();

//创建订单，并获取结果
TransactionResult transactionResult = ONSHelper.Transact(orderService.Create, orderInfoJson);
```

当然OrderService类的Create方法需要加上[ONSProducer]特性，用来设置消息的主题和标签。标签由框架定义为枚举。参数必须是string类型，返回类型必须是TransactionResult类型。
```
public class OrderService
{
    [ONSProducer(ONSMessageTopic.ORDER_MSG, ONSMessageTag.OrderCreated)]
    public TransactionResult Create(string orderInfoJson)
    {
        ...
        ...
        ...
        return new TransactionResult()
        {
            //是否需要把消息推送到消息中心
            IsToPush = true,
            
            //信息（取消推送的话，可以把取消推送的原因写在这里；确认要需要推送的话，可以把成功的信息写在这里）
            Message = "用户注册业务执行成功",
            
            //要传递什么数据给到下游订阅者（建议使用json字符把数据传递给到下游）
            Data = data
        };
    }
    ...
    ...
    ...
}
```


## 下游物流系统以消息订阅方式来接收消息
接下来需要设置一下物流系统的处理方式了，还是先在订单系统的项目中引用RocketTester.ONS.dll，然后在web.config的AppSettings中加入如下节点
```
    <!--Redis地址-->
    <add key="RedisExchangeHosts" value="redis连接字符串" />
    <!--ONS设置-->
    <add key="ONSRedisDBNumber" value="redis所用的数据库号码"/>
    <add key="ONSRedisTransactionResultExpireIn" value="存储TransactionResult的超时时间（单位秒），如18000"/>
    <add key="ONSTopic" value="消息的主题"/>
    <add key="ONSProducerId" value="消息生产者唯一标识"/>
    <add key="ONSConsumerId" value="消息消费者唯一标识"/>
    <add key="ONSAccessKey" value="RAM账号的AccessKey"/>
    <add key="ONSSecretKey" value="RAM账号的SecretKey"/>
    <add key="ONSCheckerTest" value="是否启用checker测试模式，默认得用false"/>
```

接着需要在订单系统中的Global.asax.cs中加入如下代码
```
    void Application_Start(object sender, EventArgs e)
    {
        ...
        ...
        ...
        
        //调用帮助类的初始化方法，它会以单例模式生成TransactionProducer实例和PushConsumer实例。
        ONSHelper.Initialize();
    }
    
    void Application_End(object sender, EventArgs e)
    {
        //调用帮助类的销毁方法，它会销毁之前生成的那个TransactionProducer实例和PushConsumer实例。
        ONSHelper.Destroy();
    }
```

OrderReceiverService类的Receive方法需要加上[ONSConsumer]特性，用来设置消息的主题和标签。参数必须是string类型，它的内容实际就是TransactionResult实例的Data属性。返回类型不做强制要求。
```
public class OrderReceiverService
{
    [ONSConsumer(ONSMessageTopic.ORDER_MSG, ONSMessageTag.OrderCreated)]
    public void Receive(string data)
    {
        ...
    }
    ...
    ...
    ...
}
```
