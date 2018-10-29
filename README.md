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
    <add key="ONSRedisTransactionResultExpireIn" value="存储ONSTransactionResult的超时时间（单位秒），如18000"/>
    <add key="ONSAccessKey" value="RAM账号的AccessKey"/>
    <add key="ONSSecretKey" value="RAM账号的SecretKey"/>
    <!--环境，p代表线上生产环境production，s代表线上测试环境staging，d代表本地开发环境development-->
    <add key="Environment" value="s" />
    <add key="ApplicationAlias" value="应用的别名" />
```

接着需要在订单系统中的Global.asax.cs中加入如下代码
```
    void Application_Start(object sender, EventArgs e)
    {
        ...
        ...
        ...
        
        //初始化消息生产者和消息消费者对象
        ONSHelper.Initialize();
    }
    
    void Application_End(object sender, EventArgs e)
    {
        //销毁消息生产者和消息消费者对象
        ONSHelper.Destroy();
    }
```

现在有了阿里云RocketMQ的事务消息后，经过封装后，可以这样调用。
```
//实例化Order对象
Order order = new Order();

//实例化订单服务类
OrderService orderService = new OrderService();

//创建订单，并获取结果
ServiceResult serviceResult = orderService.Create(order);
```

当然OrderService类类的ProcessCore的参数类型是可以任意类型的的，因为基类使用的是泛型，返回类型必须是ServiceResult类型。此ProcessCore方法是基类的抽象方法，因此必须override。如果此方法执行过程中出现异常，框架会将消息以TransactionStatus.Unknow状态提交，之后0~5秒内执行第一次回查（即调用Checker.check方法），之后还是TransactionStatus.Unknow状态的话，会每5秒回查一次，直至消息状态为TransactionStatus.CommitTransaction或TransactionStatus.RollbackTransaction。
```
public class OrderService:AbstractTransportProducerService<Order>
{
    //需要指定构造函数，并把ONSMessageTopic和ONSMessageTag的枚举值给到基类去持久化为属性
    public OrderService():base(ONSMessageTopic.ORDER_MSG, ONSMessageTag.ORDER_CREATED)
    {
    
    }

    //复写基类的抽象方法，在这个方法中写事务逻辑，此方法必须是幂等的，因为在以重试执行该方法时是不存在HttpContext实例的
    protected override ServiceResult ProcessCore(Order orderInfoJson)
    {
        ...
        ...
        ...
        return new ServiceResult()
        {
            //是否需要把消息推送到消息中心
            Pushable = true,
            
            //信息（取消推送的话，可以把取消推送的原因写在这里；确认要需要推送的话，可以把成功的信息写在这里）
            Message = "创建订单执行成功",
            
            //要传递什么数据给到下游订阅者（建议使用json字符把当前对象的实例数据传递给到下游）
            Data = data,
            
            //这个参数可以不传递，它目前只用于分区顺序消息的Sharding Key的赋值
            Parameter = null
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
    <add key="ONSAccessKey" value="RAM账号的AccessKey"/>
    <add key="ONSSecretKey" value="RAM账号的SecretKey"/>
    <!--环境，p代表线上生产环境production，s代表线上测试环境staging，d代表本地开发环境development-->
    <add key="Environment" value="s" />
    <add key="ApplicationAlias" value="应用的别名" />
```

接着需要在订单系统中的Global.asax.cs中加入如下代码
```
    void Application_Start(object sender, EventArgs e)
    {
        ...
        ...
        ...
        
        //初始化消息生产者和消息消费者对象
        ONSHelper.Initialize();
    }
    
    void Application_End(object sender, EventArgs e)
    {
        //销毁消息生产者和消息消费者对象
        ONSHelper.Destroy();
    }
```

OrderReceiverService类的ProcessCore的参数类型是可以任意类型的的，因为基类使用的是泛型，它的内容实际就是ServiceResult实例的Data属性，返回类型必须使用bool类型，此ProcessCore方法是基类的抽象方法，因此必须override。逻辑上执行没问题的话，返回true，如果逻辑上执行时遇到异常，或返回不是期待的结果，可以反回false，框架会按Action.ReconsumeLater状态提交消费状态，直至消费状态以Action.CommitMessage被提交。如果消费状态一直以Action.ReconsumeLater状态提交的话，消息中心会在4小时46分钟内一共发送16次重试消费，之后就不会再次发送了，具体重试的时间间隔见如下链接：https://help.aliyun.com/document_detail/43490.html。
```
public class OrderReceiverService: AbstractConsumerService<Order>
{
    //需要指定构造函数，并把ONSMessageTopic和ONSMessageTag的枚举值给到基类去持久化为属性
    public OrderReceiverService():base(new List<TopicTag>(){ 
        new TopicTag() {
            Topic = ONSMessageTopic.ORDER_MSG, Tag = ONSMessageTag.ORDER_CREATED
        }
    })
    {
    
    }
    
    //复写基类的抽象方法，在这个方法中写事务逻辑，此方法必须是幂等的，因为在以重试执行该方法时是不存在HttpContext实例的
    protected override bool ProcessCore(Order data)
    {
        ...
        ...
        ...
        return true;
    }
    ...
    ...
    ...
}
```
