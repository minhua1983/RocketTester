using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ons;
using RocketTester.UI.Model;
using Message = ons.Message;

namespace RocketTester.UI
{
    public partial class Form1 : Form
    {
        TransactionProducer producer;
        ONSFactoryProperty factoryInfo;
        private static string Ons_Topic = "order_msg";
        private static string Ons_ProducerId = "PID_PO_Maker";
        private static string Ons_ConsumerId = "CID_PO_RCVER";
        private static string Ons_AccessKey = "";
        private static string Ons_SecretKey = "";

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            factoryInfo = new ONSFactoryProperty();
            factoryInfo.setFactoryProperty(ONSFactoryProperty.AccessKey, Ons_AccessKey);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.SecretKey, Ons_SecretKey);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.ProducerId, Ons_ProducerId);
            //factoryInfo.setFactoryProperty(ONSFactoryProperty.ConsumerId, Ons_ConsumerId);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.PublishTopics, Ons_Topic);
            factoryInfo.setFactoryProperty(ONSFactoryProperty.LogPath, "D://log/rocketmq/producer");

            LocalTransactionChecker myChecker = new MyLocalTransactionChecker();

            producer = ONSFactory.getInstance().createTransactionProducer(factoryInfo, myChecker);

            producer.start();
            
        }

        void ActiveForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 在应用退出前，必须销毁 Producer 对象，否则会导致内存泄露等问题；
            // shutdown 之后不能重新 start 此 producer
            producer.shutdown();
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            Form1.ActiveForm.FormClosing += ActiveForm_FormClosing;

            /*
            Message msg = new Message(
            //Message Topic
                factoryInfo.getPublishTopics(),
                //Message Tag
                "TagA",
                //Message Body
                factoryInfo.getMessageContent()
            );
            //*/

            Message msg = new Message(
                //Message Topic
                factoryInfo.getPublishTopics(),
                //Message Tag
                "TagA",
                //Message Body
                messageTextBox.Text.Trim()
            );

            // 设置代表消息的业务关键属性，请尽可能全局唯一
            // 以方便您在无法正常收到消息情况下，可通过 MQ 控制台查询消息并补发。
            // 注意：不设置也不会影响消息正常收发
            //msg.setKey("ORDERID_100");

            // 发送消息，只要不抛出异常，就代表发送成功
            try
            {
                LocalTransactionExecuter myExecuter = new MyLocalTransactionExecuter();
                SendResultONS sendResult = producer.send(msg, myExecuter);
            }
            catch (Exception exception)
            {
                Console.WriteLine("\nexception of sendmsg:{0}", exception.ToString());
            }
            
        }
    }
}
