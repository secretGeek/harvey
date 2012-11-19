using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Harvey
{
    //Major hat-tip to Derek Greer's 'RabbitMQ for Windows' articles http://lostechies.com/derekgreer/tag/rabbitmq/
    //To setup a host server for your chatting...
    //1. install erlang: http://www.erlang.org/download.html
    //2. set the ERLANG_HOME environment variable to point to the erlang folder under program files. e.g. C:\Program Files\erl5.9.2
    //3. Install rabbitMQ: http://www.rabbitmq.com/download.html
    //4. enable the rabbitmq management plugin. from an elevated cmd prompt, go to rabbit's sbin folder (e.g. %programfiles%\RabbitMQ Server\rabbitmq_server-2.8.7\sbin") and run: "rabbitmq-plugins.bat enable rabbitmq_management"
    //5. To activate the management plugin, stop, install and start the rabbitmq service:
    //           rabbitmq-service.bat stop 
    //           rabbitmq-service.bat install
    //           rabbitmq-service.bat start 
    //6. Visit http://localhost:55672/mgmt/ and see that your rabbitMQ instance is alive

    public partial class frmMain : Form
    {
        string exchangeName = "chatter";
        string clientId = Guid.NewGuid().ToString();
        IConnection connection = null;
        IModel channelSend = null;
        IModel channelReceive = null;
        Thread receivingThread = null;

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            txtUserName.Text = clientId.Substring(0, 13); //to truncate a guid is considered quite a funny joke where i come from.
            txtConversation.Text = "Do not share credit card numbers unless the person is quite convincing or insistent.\r\n";
            var connectionFactory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                VirtualHost = "/"
            };

            connection = connectionFactory.CreateConnection();
            channelSend = connection.CreateModel();
            channelSend.ExchangeDeclare(exchangeName, ExchangeType.Fanout, false, true, null);
            channelReceive = connection.CreateModel();
            channelReceive.QueueDeclare(clientId, false, false, true, null);
            channelReceive.QueueBind(clientId, exchangeName, "");
            receivingThread = new Thread(() => channelReceive.StartConsume(clientId, MessageHandler));
            receivingThread.Name = "ReceivingThread"; //name the thread so that when it goes insane you will be able to apportion blame.
            receivingThread.Start();
            txtMessage.Focus();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string input = txtUserName.Text + " > " + txtMessage.Text;
            byte[] message = Encoding.UTF8.GetBytes(input);
            channelSend.BasicPublish(exchangeName, "", null, message);
            txtMessage.Text = string.Empty;
            txtMessage.Focus();
        }

        public void MessageHandler(IModel channel, DefaultBasicConsumer consumer, BasicDeliverEventArgs eventArgs)
        {
            string message = Encoding.UTF8.GetString(eventArgs.Body) + "\r\n";

            txtConversation.InvokeIfRequired(() =>
            {
                txtConversation.Text += message;
                txtConversation.ScrollToEnd();
            });
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtConversation.Text = "";
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (channelSend.IsOpen) channelSend.Close();
            if (channelReceive.IsOpen) channelReceive.Close();
            if (connection.IsOpen) connection.Close();
        }

        private void frmMain_ResizeEnd(object sender, EventArgs e)
        {
            txtConversation.ScrollToEnd();
        }
    }

    public static class ChannelExtensions
    {
        // http://lostechies.com/derekgreer/2012/05/29/rabbitmq-for-windows-headers-exchanges/
        public static void StartConsume(this IModel channel, string queueName, Action<IModel, DefaultBasicConsumer, BasicDeliverEventArgs> callback)
        {
            QueueingBasicConsumer consumer = new QueueingBasicConsumer(channel);
            channel.BasicConsume(queueName, true, consumer);

            while (true)
            {
                try
                {
                    var eventArgs = (BasicDeliverEventArgs)consumer.Queue.Dequeue();
                    callback(channel, consumer, eventArgs);
                }
                catch (EndOfStreamException)
                {
                    // The consumer was cancelled, the model closed, or the connection went away.
                    break;
                }
            }
        }

        //hat tip: http://stackoverflow.com/questions/2367718/c-automating-the-invokerequired-code-pattern
        public static void InvokeIfRequired(this Control control, MethodInvoker action)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(action);
            }
            else
            {
                action();
            }
        }
        public static void ScrollToEnd(this TextBox textbox)
        {
            textbox.Select(textbox.Text.Length - 1, 0);
            textbox.ScrollToCaret();
        }
    }
}
