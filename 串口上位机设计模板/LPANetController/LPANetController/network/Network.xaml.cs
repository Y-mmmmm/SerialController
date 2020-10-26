using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Collections;



namespace LPANetController
{
    /// <summary>
    /// network.xaml 的交互逻辑
    /// </summary>
    public partial class Network : UserControl
    {

        Thread threadWatch = null;//监听 Socket 运行的线程
        Thread threadClient = null;
        Thread threadUDPWatch = null;
        Socket socketWatch = null;//监听 Socket
        Socket socketClient = null;
        Socket socketUDP = null;

        static UInt32 receiveBytesCount = 0;//接收字节数，没用到
        // static UInt32 sendBytesCount = 0;
        static UInt32 receiveFrameCount = 0;//一样

        string selectedMode;//下拉菜单选择网络模式

        UInt16 connectionNum = 0;
        UInt16 udpNum = 0;
        EndPoint lastRemotePoint = new IPEndPoint(0, 0);
        /// <summary>
        ///  服务器端通信套接字集合
        /// 必须在每次客户端连接成功之后，保存新建的通讯套接字，这样才能和后续的所有客户端通信
        /// </summary>
        Dictionary<int, Socket> dictSock = new Dictionary<int, Socket>();
        Dictionary<int, EndPoint> dictEndPoint = new Dictionary<int, EndPoint>();
        Dictionary<int, string> dictConnectInfo = new Dictionary<int, string>()
        {
            {0,"没有监听到连接。"}
        };
        Dictionary<string, Thread> dictThread = new Dictionary<string, Thread>();//通信线程的集合，用来接收客户端发送的信息

        private DispatcherTimer autoSendTimer = new DispatcherTimer();
        private DispatcherTimer secondTimer = new DispatcherTimer();


        //string recText = "";

        bool saveToFile_IsEnable = false;
        string saveToFile_FileName = "";
        FileStream saveFileHandle;

        private BitmapImage image;
        Stream recStream;

        DynamicBufferManager recDynBuffer = new DynamicBufferManager(10 * 1024 * 1024);

        static Semaphore writeProtect = new Semaphore(1, 1);



        public Network()
        {
            InitializeComponent();


            connectObjectComboBox.ItemsSource = dictConnectInfo;
            connectObjectComboBox.SelectedValuePath = "Key";
            connectObjectComboBox.DisplayMemberPath = "Value";
            connectObjectComboBox.SelectedIndex = 0;

            secondTimer.Tick += new EventHandler(SecondTimer_Tick);
            secondTimer.Interval = new TimeSpan(0, 0, 1);
            secondTimer.Start();
        }

        void SecondTimer_Tick(object sender, EventArgs e)
        {

            // statusFrameTextBlock.Text = receiveFrameCount.ToString();
            receiveFrameCount = 0;
            //设置新的定时时间           
            secondTimer.Interval = new TimeSpan(0, 0, 1);


        }

        private void Connect()
        {
            IPAddress address = IPAddress.Parse(IPAddressTextBox.Text.Trim());//解析IP
            int portNum = int.Parse(portNumTextBox.Text.Trim());//解析端口
            IPEndPoint endPoint = new IPEndPoint(address, portNum);//将IP和端口组合成套接字格式

            if (selectedMode == "TCP Client")
            {
                // 创建服务器端监听 Socket (IP4寻址协议，流式连接，TCP协议传输数据)
                socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    statusTextBlock.Text = ("与服务器连接中...");//提示栏显示
                    socketClient.Connect(endPoint);//连接端点
                    LocalNetworkInfo(socketClient, true);//显示本地网络信息
                }
                catch (SocketException se)//错误异常
                {
                    statusTextBlock.Text = ("与服务器连接失败！" + se.Message);
                    connectButton.IsChecked = false;
                    return;
                }
                statusTextBlock.Text = ("与服务器连接成功！");

                //显示提示文字
                connectButton.IsChecked = true;
                networkStatusEllipse.Fill = Brushes.Red;

                //// 启动监听线程开始监听接收数据
                threadClient = new Thread(RecMsg);
                threadClient.IsBackground = true;
                threadClient.Start(socketClient);
            }
            else if (selectedMode == "TCP Server")
            {

                // 创建负责监听的套接字，注意其中的参数；  
                socketWatch = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                try
                {
                    // 将负责监听的套接字绑定到唯一的ip和端口上；  
                    socketWatch.Bind(endPoint);
                }
                catch (SocketException se)
                {
                    statusTextBlock.Text = ("创建服务器失败！" + se.Message);
                    return;
                }
                // 设置监听队列的长度；  
                socketWatch.Listen(10);
                // 创建负责监听的线程；  
                threadWatch = new Thread(WatchConnecting);
                threadWatch.IsBackground = true;
                threadWatch.Start();

                statusTextBlock.Text = ("创建服务器成功！");

                //显示提示文字
                connectButton.IsChecked = true;
                networkStatusEllipse.Fill = Brushes.Red;
            }
            else if (selectedMode == "UDP")
            {
                // 创建负责监听的套接字，注意其中的参数；  
                socketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                try
                {
                    socketUDP.Bind(endPoint);
                }
                catch (SocketException se)
                {
                    statusTextBlock.Text = ("创建UDP服务器失败！" + se.Message);
                    return;
                }

                threadUDPWatch = new Thread(RecMsg);
                threadUDPWatch.Start(socketUDP);


                statusTextBlock.Text = ("创建服务器成功！");

                //显示提示文字
                connectButton.IsChecked = true;
                networkStatusEllipse.Fill = Brushes.Red;
            }
        }

        //打开连接按钮
        private void ConnectButton_Checked(object sender, RoutedEventArgs e)
        {

            ConnectSetingEnable(false);//默认未打开连接
            Connect();

        }

        private void ConnectSetingEnable(bool isEnable)
        {
            if (isEnable)
            {
                protocalCombobox.IsEnabled = true;//判断是否选择的模式
                IPAddressTextBox.IsEnabled = true;//判断是否输入了IP地址
                portNumTextBox.IsEnabled = true;//判断是否输入了端口号

            }
            else
            {
                protocalCombobox.IsEnabled = false;
                IPAddressTextBox.IsEnabled = false;
                portNumTextBox.IsEnabled = false;

            }
        }



        //显示或隐藏本地网络信息
        void LocalNetworkInfo(Socket sock, Boolean show)
        {
            if (show == true)
            {
                IPEndPoint localEndPoint;
                localEndPoint = (IPEndPoint)sock.LocalEndPoint;
                localIPLabel.Content = localEndPoint.Address.ToString();
                localPortLabel.Content = localEndPoint.Port.ToString();

                localIPInfoLabel.Visibility = Visibility.Visible;//显示已连接上的IP
                localPortInfoLabel.Visibility = Visibility.Visible;
                localIPLabel.Visibility = Visibility.Visible;
                localPortLabel.Visibility = Visibility.Visible;
            }
            else
            {
                localIPInfoLabel.Visibility = Visibility.Hidden;//隐藏
                localPortInfoLabel.Visibility = Visibility.Hidden;
                localIPLabel.Visibility = Visibility.Hidden;
                localPortLabel.Visibility = Visibility.Hidden;
            }


        }
        void WatchConnecting()
        {


            while (true)
            {

                try
                {
                    // 开始监听客户端连接请求，Accept方法会阻断当前的线程；  
                    Socket sokConnection = socketWatch.Accept(); // 一旦监听到一个客户端的请求，就返回一个与该客户端通信的 套接字；  
                    connectionNum++;
                    // 将与客户端连接的 套接字 对象添加到集合中；  
                    dictSock.Add(connectionNum - 1, sokConnection);

                    IPEndPoint remoteEndPoint;
                    remoteEndPoint = (IPEndPoint)sokConnection.RemoteEndPoint;

                    if (connectionNum == 1)
                    {
                        dictConnectInfo[0] = remoteEndPoint.ToString();
                    }
                    else
                    {
                        dictConnectInfo.Add(connectionNum - 1, remoteEndPoint.ToString());
                    }

                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        statusTextBlock.Text = ("已与" + dictConnectInfo[connectionNum - 1] + "建立连接");

                        connectObjectComboBox.Items.Refresh();
                        connectObjectComboBox.SelectedIndex = connectionNum - 1;

                    }));

                    Thread threadServerRec = new Thread(RecMsg);
                    threadServerRec.IsBackground = true;
                    threadServerRec.Start(sokConnection);
                    //dictThread.Add(sokConnection.RemoteEndPoint.ToString(), thr);  //  将新建的线程 添加 到线程的集合中去。  
                }
                catch (SocketException se)
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        statusTextBlock.Text = ("服务器已失效！");
                        connectButton.IsChecked = false;
                    }));

                    return;
                }
                catch
                { return; }
            }
        }

        //处理tcp接收的数据。
        void RecMsg(object sockConnectionparn)
        {
            Socket sockConnection = sockConnectionparn as Socket;
            EndPoint remoteEndPoint = new IPEndPoint(0, 0); ;
            byte[] buffMsgRec = new byte[1024 * 1024 * 10];

            int length = -1;

            while (true)
            {

                try
                {
                    if (selectedMode == "UDP")
                    {

                        length = sockConnection.ReceiveFrom(buffMsgRec, ref remoteEndPoint);
                        if (!dictConnectInfo.Values.Contains(remoteEndPoint.ToString()))
                        {
                            udpNum++;
                            if (udpNum == 1)
                                dictConnectInfo[0] = remoteEndPoint.ToString();
                            else
                                dictConnectInfo.Add(udpNum - 1, remoteEndPoint.ToString());


                            this.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                connectObjectComboBox.Items.Refresh();
                                connectObjectComboBox.SelectedIndex = udpNum - 1;
                            }));

                        }


                    }
                    else if (selectedMode == "TCP Server")
                    {
                        length = sockConnection.Receive(buffMsgRec);

                    }
                    else if (selectedMode == "TCP Client")
                    {
                        length = sockConnection.Receive(buffMsgRec);


                        //this.Dispatcher.Invoke(new Action(delegate
                        //{
                        //                       //更新接收字节数
                        //receiveBytesCount += (UInt32)length;
                        //statusReceiveByteTextBlock.Text = receiveBytesCount.ToString();

                        //}));

                        recDynBuffer.WriteBuffer(buffMsgRec, 0, length);

                        //length = sockConnection.Receive(recDynBuffer.Buffer, recDynBuffer.DataCount ,10*1024*1024, SocketFlags.None);
                        //recDynBuffer.DataCount += length;

                    }

                }

                catch (SocketException se)
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        statusTextBlock.Text = ("接收数据出错！" + se.Message);

                    }));
                    return;

                }
                catch (ThreadAbortException te)
                {
                    //this.Dispatcher.BeginInvoke(new Action(delegate
                    //{
                    //    statusTextBlock.Text = (te.Message);

                    //}));
                    return;

                }
                catch (Exception e)
                {
                    this.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        statusTextBlock.Text = ("异常！" + e.Message);

                    }));
                    return;

                }


                this.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (selectedMode == "UDP" && remoteEndPoint != lastRemotePoint)
                    {
                        //receiveTextBox.Text += "\r\nreceive from[" + remoteEndPoint.ToString() + "]\r\n";
                        lastRemotePoint = remoteEndPoint;
                    }

                    ShowData(buffMsgRec, length);
                    //ShowData(recDynBuffer.Buffer, recDynBuffer.DataCount); 

                }));

                Thread.Sleep(10);


            }


        }

        int lastStartPosition = -1;
        int lastEndPosition = -1;
        //显示数据
        private void ShowData(byte[] recData, int dataLength)
        {
            //更新接收字节数
            receiveBytesCount += (UInt32)dataLength;
            // statusReceiveByteTextBlock.Text = receiveBytesCount.ToString();

            if (saveToFile_IsEnable)
            {
                try
                {
                    saveFileHandle.Seek(0, SeekOrigin.End);
                    saveFileHandle.WriteAsync(recData, 0, dataLength);
                }
                catch
                { return; }

            }
            else
            {
                int startPosition = -1;
                int endPosition = -1;

#if false

                if (lastStartPosition != -1 && lastEndPosition != -1)
                {
                    recArrary.RemoveRange(lastStartPosition, lastEndPosition - lastStartPosition);
                    lastStartPosition = lastEndPosition = -1;

                }


                foreach(byte b in recSegment)
                    recArrary.Add(b);


                startPosition = IndexOf(recArrary, startCode, 0, recArrary.Count);
                if (startPosition != -1)
                {
                    endPosition = IndexOf(recArrary, endCode, startPosition, recArrary.Count);
                     if (endPosition != -1)
                     {

                         lastStartPosition = startPosition;
                         lastEndPosition = endPosition;
                         recStream = new MemoryStream((byte[])recArrary.ToArray(typeof(byte)), (int)startPosition, (int)(endPosition - startPosition));
                         image = new BitmapImage();

                         image.BeginInit();
                         image.StreamSource = recStream;
                         image.EndInit();
                         picImage.Source = image;

                         picImage.Height = image.PixelHeight;
                         picImage.Width = image.PixelWidth;

                        
                         //recArrary.Clear();
                     }
                     else 
                     {                           
                         //Array.Copy(recData,startPosition,tempData,0,dataLength);
                         return;                     
                     }                
                }                
                else
                { 
                    return;
                }

#else

                if (lastStartPosition != -1 && lastEndPosition != -1)
                {
                    recDynBuffer.Clear(lastEndPosition);
                    lastStartPosition = lastEndPosition = -1;

                }


                //recDynBuffer.WriteBuffer(recData,0,dataLength);


                //try
                //{
                //    saveFileHandle.Seek(0, SeekOrigin.End);
                //    saveFileHandle.WriteAsync(recData, 0, dataLength);
                //}
                //catch
                //{ return; }

                //if (lastStartPosition == -1)
                startPosition = IndexOf(recDynBuffer.Buffer, startCode, 0, recDynBuffer.DataCount);
                //else
                //    startPosition = lastStartPosition;

                if (startPosition != -1)
                {
                    lastStartPosition = startPosition;
                    endPosition = IndexOf(recDynBuffer.Buffer, endCode, startPosition, recDynBuffer.DataCount);
                    if (endPosition != -1)
                    {


                        lastEndPosition = endPosition;
                        recStream = new MemoryStream(recDynBuffer.Buffer, startPosition, endPosition - startPosition);
                        image = new BitmapImage();

                        //image.BeginInit();
                        //image.StreamSource = recStream;
                        //image.EndInit();
                        //picImage.Source = image;

                        //picImage.Height = image.PixelHeight;
                        //picImage.Width = image.PixelWidth;


                        ImageSourceConverter converterS = new ImageSourceConverter();

                        //picImage.Source = converterS.ConvertFrom(recStream) as BitmapFrame;
                        // picImage.Height = picScrollViewer.Height;
                        // picImage.Width = picScrollViewer.Width;
                        //recArrary.Clear();

                        //更新帧数
                        receiveFrameCount++;

                    }
                    else
                    {
                        //Array.Copy(recData,startPosition,tempData,0,dataLength);            

                        return;
                    }
                }
                else
                {

                    return;
                }


#endif


#if false
                //没有关闭数据显示
                if (stopShowingButton.IsChecked == false)
                {
                    try
                    {
                        //字符串显示
                        if (hexadecimalDisplayCheckBox.IsChecked == false)
                        {
                            string receiveText = System.Text.Encoding.Default.GetString(recData, 0, dataLength);// 将接受到的字节数据转化成字符串；  
                            receiveTextBox.AppendText(receiveText);

                        }
                        else //16进制显示
                        {
                            for (UInt32 i = 0; i < dataLength; i++)
                                receiveTextBox.AppendText(string.Format("{0:X2} ", recData[i]));

                        }
                    }
                    catch (SocketException se)
                    {
                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            statusTextBlock.Text = ("发送数据出错！" + se.Message);
                        }));
                        return;

                    }
                    catch (ThreadAbortException te)
                    { return; }
                    catch (Exception e)
                    {
                        this.Dispatcher.BeginInvoke(new Action(delegate
                        {
                            statusTextBlock.Text = ("异常！" + e.Message);

                        }));
                        return;

                    }

                }
#endif
            }
        }


        //断开连接
        private void ConnectButton_Unchecked(object sender, RoutedEventArgs e)
        {
            ConnectSetingEnable(true);
            if (selectedMode == "TCP Server")
            {
                try
                {
                    for (UInt16 i = 0; i < connectionNum; i++)
                        dictSock[i].Close();

                    socketWatch.Close();
                    threadWatch.Abort();

                }
                catch
                {
                    return;
                }

                dictConnectInfo.Clear();
                connectObjectComboBox.Items.Refresh();
                connectObjectComboBox.SelectedIndex = 0;
                //显示提示文字
                connectButton.Content = "监听";
            }
            else if (selectedMode == "TCP Client")
            {

                try
                {
                    threadClient.Abort();
                    socketClient.Close();
                }
                catch
                {
                    return;
                }

                //隐藏本地网络信息
                LocalNetworkInfo(null, false);
                //显示提示文字
                connectButton.Content = "连接";
            }

            else if (selectedMode == "UDP")
            {

                try
                {
                    threadUDPWatch.Abort();
                    socketUDP.Close();
                }
                catch
                {
                    return;
                }

                //隐藏本地网络信息
                LocalNetworkInfo(null, false);
                //显示提示文字
                connectButton.Content = "连接";
            }

            networkStatusEllipse.Fill = Brushes.Gray;

            statusTextBlock.Text = ("连接已关闭！");
            ConnectSetingEnable(true);


        }




        private void WindowClosed(object sender, ExecutedRoutedEventArgs e)
        {

            try
            {
                socketUDP.Close();
                socketClient.Close();
                for (UInt16 i = 0; i < connectionNum; i++)
                    dictSock[i].Close();
            }
            catch (Exception)
            {

                return;
            }

        }


        private void ProtocalCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedMode = ((ComboBoxItem)protocalCombobox.SelectedItem).Content.ToString();
            try
            {
                if (selectedMode == "TCP Client")
                {
                    IPAddressInfoTextBox.Content = "服务器IP地址";
                    portNumInfoTextBox.Content = "服务器端口号";

                    connectObjectLabel.Visibility = Visibility.Hidden;
                    connectObjectComboBox.Visibility = Visibility.Hidden;
                    //显示提示文字
                    connectButton.Content = "连接";

                }
                else if (selectedMode == "TCP Server")
                {
                    IPAddressInfoTextBox.Content = "本地IP地址";
                    portNumInfoTextBox.Content = "本地端口号";

                    connectObjectLabel.Visibility = Visibility.Visible;
                    connectObjectComboBox.Visibility = Visibility.Visible;

                    dictConnectInfo[0] = "没有监听到连接。";

                    connectObjectComboBox.Items.Refresh();
                    connectObjectComboBox.SelectedIndex = 0;
                    connectObjectComboBox.IsEditable = false;

                    //显示提示文字
                    connectButton.Content = "监听";
                }
                else if (selectedMode == "UDP")
                {
                    IPAddressInfoTextBox.Content = "服务器IP地址";
                    portNumInfoTextBox.Content = "服务器端口号";

                    connectObjectLabel.Visibility = Visibility.Visible;
                    connectObjectComboBox.Visibility = Visibility.Visible;

                    dictConnectInfo[0] = "输入远端地址，格式 IP:Port";

                    connectObjectComboBox.Items.Refresh();
                    connectObjectComboBox.SelectedIndex = 0;
                    connectObjectComboBox.IsEditable = true;
                    //显示提示文字
                    connectButton.Content = "连接";
                }

            }
            catch { return; }



        }


        /// <summary>  
        /// 报告指定的 System.Byte[] 在此实例中的第一个匹配项的索引。  
        /// </summary>  
        /// <param name="srcBytes">被执行查找的 System.Byte[]。</param>  
        /// <param name="searchBytes">要查找的 System.Byte[]。</param>  
        /// <returns>如果找到该字节数组，则为 searchBytes 的索引位置；如果未找到该字节数组，则为 -1。如果 searchBytes 为 null 或者长度为0，则返回值为 -1。</returns>  
        internal int IndexOf(byte[] srcBytes, byte[] searchBytes)
        {
            if (srcBytes == null) { return -1; }
            if (searchBytes == null) { return -1; }
            if (srcBytes.Length == 0) { return -1; }
            if (searchBytes.Length == 0) { return -1; }
            if (srcBytes.Length < searchBytes.Length) { return -1; }
            for (int i = 0; i < srcBytes.Length - searchBytes.Length; i++)
            {
                if (srcBytes[i] == searchBytes[0])
                {
                    if (searchBytes.Length == 1) { return i; }
                    bool flag = true;
                    for (int j = 1; j < searchBytes.Length; j++)
                    {
                        if (srcBytes[i + j] != searchBytes[j])
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag) { return i; }
                }
            }
            return -1;
        }
        /// <summary>  
        /// 报告指定的 System.Byte[] 在此实例中的第一个匹配项的索引。  
        /// </summary>  
        /// <param name="srcBytes">被执行查找的 System.Byte[]。</param>  
        /// <param name="searchBytes">要查找的 System.Byte[]。</param>  
        /// <param name="startIndex">源字节数组起始位置</param>  
        /// <param name="srcLength">源字节数组长度</param>  
        /// <returns>如果找到该字节数组，则为 searchBytes 的索引位置；如果未找到该字节数组，则为 -1。如果 searchBytes 为 null 或者长度为0，则返回值为 -1。</returns>  

        internal int IndexOf(byte[] srcBytes, byte[] searchBytes, int startIndex, int srcLength)
        {
            if (srcBytes == null) { return -1; }
            if (searchBytes == null) { return -1; }
            if (srcLength == 0 || srcLength > srcBytes.Length) { return -1; }
            if (startIndex >= srcBytes.Length || startIndex < 0) { return -1; }
            if (searchBytes.Length == 0) { return -1; }
            if (srcLength < searchBytes.Length) { return -1; }
            for (int i = startIndex; i < srcLength - searchBytes.Length; i++)
            {
                if (srcBytes[i] == searchBytes[0])
                {
                    if (searchBytes.Length == 1) { return i; }
                    bool flag = true;
                    for (int j = 1; j < searchBytes.Length; j++)
                    {
                        if (srcBytes[i + j] != searchBytes[j])
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag) { return i; }
                }
            }
            return -1;
        }

        /// <summary>  
        /// 报告指定的 System.Byte[] 在此实例中的第一个匹配项的索引。  
        /// </summary>  
        /// <param name="srcBytes">被执行查找的 System.Byte[]。</param>  
        /// <param name="searchBytes">要查找的 System.Byte[]。</param>  
        /// <param name="startIndex">源字节数组起始位置</param>  
        /// <param name="srcLength">源字节数组长度</param>  
        /// <returns>如果找到该字节数组，则为 searchBytes 的索引位置；如果未找到该字节数组，则为 -1。如果 searchBytes 为 null 或者长度为0，则返回值为 -1。</returns>  

        internal int IndexOf(ArrayList srcBytes, byte[] searchBytes, int startIndex, int srcLength)
        {
            if (srcBytes == null) { return -1; }
            if (searchBytes == null) { return -1; }
            if (srcLength == 0 || srcLength > srcBytes.Count) { return -1; }
            if (startIndex >= srcBytes.Count || startIndex < 0) { return -1; }
            if (searchBytes.Length == 0) { return -1; }
            if (srcLength < searchBytes.Length) { return -1; }
            for (int i = startIndex; i < srcLength - searchBytes.Length; i++)
            {
                if ((byte)srcBytes[i] == searchBytes[0])
                {
                    if (searchBytes.Length == 1) { return i; }
                    bool flag = true;
                    for (int j = 1; j < searchBytes.Length; j++)
                    {
                        if ((byte)srcBytes[i + j] != searchBytes[j])
                        {
                            flag = false;
                            break;
                        }
                    }
                    if (flag) { return i; }
                }
            }
            return -1;
        }


        //int startPosition = 0;
        //int endPosition = 0;
        byte[] startCode = { 0xff, 0xd8, 0xff };
        byte[] endCode = { 0xff, 0xd9 };
        byte[] tempData = new byte[1024 * 1024 * 10];

        private void SendMsg(object sockConnectionparn, byte[] data)
        {
            Socket sockConnection = sockConnectionparn as Socket;

            if (!sockConnection.IsBound)
            {
                statusTextBlock.Text = "请先建立连接";
                return;
            }
            try
            {
                byte[] arrData = data;
                sockConnection.Send(arrData);

            }

            catch
            {

            }

        }

        private void SendMsg(EndPoint remoteEP, byte[] data)
        {
            //Socket sockConnection = sockConnectionparn as Socket;

            if (!socketUDP.IsBound)
            {
                statusTextBlock.Text = "请先建立连接";
                return;
            }
            try
            {
                byte[] arrData = data;
                socketUDP.SendTo(arrData, remoteEP);

            }

            catch
            {

            }

        }


        private void SendMsg_AllProtocal(byte[] data)
        {
            if (selectedMode == "TCP Client")
                SendMsg(socketClient, data);
            else if (selectedMode == "TCP Server")
            {
                //发送数据到选择的客户端
                SendMsg(dictSock[connectObjectComboBox.SelectedIndex], data);
            }
            else if (selectedMode == "UDP")
            {
                EndPoint remoteEndPoint = new IPEndPoint(0, 0);
                string[] addr = connectObjectComboBox.Text.Split(':', '：');
                try
                {
                    remoteEndPoint = new IPEndPoint(IPAddress.Parse(addr[0]), Convert.ToInt32(addr[1]));
                }
                catch
                {
                    statusTextBlock.Text = "输入的IP地址格式不对,示例 192.168.1.31:8088";
                    return;
                }

                SendMsg(remoteEndPoint, data);
            }
        }


        /// <summary>
        /// RUN
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] sendData = { 0x26, 0xF0, 0x01, 0x00, 0xF1, 0x2A };
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);
        }
        /// <summary>
        /// STOP
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] sendData = { 0x26, 0xEF, 0x01, 0x00, 0xF0, 0x2A };
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);
        }
        /// <summary>
        /// PURGE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PurgeButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] sendData = { 0x26, 0xFB, 0x01, 0x00, 0xFC, 0x2A };
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);
        }
        /// <summary>
        /// PMAX
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PmaxSendButton_Click(object sender, RoutedEventArgs e)
        {

            int portNum = int.Parse(PmaxTextBox.Text.Trim());
            if (portNum > 0x16)
                MessageBox.Show("请输入1-22之间的数");
            byte[] sendData = new byte[10];
            sendData[0] = 0x26;
            sendData[1] = 0xEB;
            sendData[2] = 0x05;
            sendData[3] = 0x00;
            sendData[4] = 0x00;
            sendData[5] = 0x00;
            sendData[6] = 0x00;
            sendData[7] = (byte)portNum;
            sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
            sendData[9] = 0x2A;
            SendMsg_AllProtocal(sendData);
            // socketClient.Send(sendData);
        }
        /// <summary>
        /// PMIN
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PminSendButton_Click(object sender, RoutedEventArgs e)
        {
            int portNum = int.Parse(PminTextBox.Text.Trim());
            if (portNum > 0x16)
                MessageBox.Show("请输入1-22之间的数");
            byte[] sendData = new byte[10];
            sendData[0] = 0x26;
            sendData[1] = 0xEC;
            sendData[2] = 0x05;
            sendData[3] = 0x00;
            sendData[4] = 0x00;
            sendData[5] = 0x00;
            sendData[6] = 0x00;
            sendData[7] = (byte)portNum;
            sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
            sendData[9] = 0x2A;
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);

        }
        /// <summary>
        /// FLOW
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FlowSendButton_Click(object sender, RoutedEventArgs e)
        {
            float portNum = float.Parse(FlowTextBox.Text.Trim());
            byte[] bytes = BitConverter.GetBytes(portNum);
            byte[] sendData = new byte[12];
            sendData[0] = 0x26;
            sendData[1] = 0xED;
            sendData[2] = 0x07;
            sendData[3] = 0x00;
            sendData[4] = 0x00;
            sendData[5] = 0x00;
            sendData[6] = bytes[0];///低位
            sendData[7] = bytes[1];
            sendData[8] = bytes[2];
            sendData[9] = bytes[3];///高位
            sendData[10] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7] + sendData[8] + sendData[9]);//校验
            sendData[11] = 0x2A;
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);

        }
        /// <summary>
        /// SOLVENT
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SolventSendButton_Click(object sender, RoutedEventArgs e)
        {
            int portNum = int.Parse(SolventTextBox.Text.Trim());
            if (portNum > 0x0F)
                MessageBox.Show("请输入0-15之间的数");
            byte[] sendData = new byte[8];
            sendData[0] = 0x26;
            sendData[1] = 0xEA;
            sendData[2] = 0x03;
            sendData[3] = 0x00;
            sendData[4] = 0x00;
            sendData[5] = (byte)portNum;
            sendData[6] = (byte)(sendData[1] + sendData[2] + sendData[5]);//校验
            sendData[7] = 0x2A;
            SendMsg_AllProtocal(sendData);
            // socketClient.Send(sendData);

        }


        /// <summary>
        /// ACTIVATE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] sendData = { 0x26, 0xF1, 0x01, 0x00, 0xF2, 0x2A, 0xEE };
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);

        }
        /// <summary>
        /// SET_parameters
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SetButton_Click(object sender, RoutedEventArgs e)
        {
            int i = 0;
            byte checksum = 0;
            int MthdData = int.Parse(MethdTextBox.Text.Trim());
            if (MthdData > 0x09)
                MessageBox.Show("METHD请输入0-9之间的数");
            int LinkData = int.Parse(LinkTextBox.Text.Trim());
            if (LinkData > 0x09)
                MessageBox.Show("LINK请输入0-9之间的数");
            float TotalFlowData = float.Parse(TotalFlowTextBox.Text.Trim());
            byte[] bytes = BitConverter.GetBytes(TotalFlowData);
            int SolvAData = int.Parse(SolvntATextBox.Text.Trim());
            int SolvBData = int.Parse(SolvntBTextBox.Text.Trim());
            int T00Data = int.Parse(T00TextBox.Text.Trim()); int T01Data = int.Parse(T01TextBox.Text.Trim());
            int T02Data = int.Parse(T02TextBox.Text.Trim()); int T03Data = int.Parse(T03TextBox.Text.Trim());
            int T04Data = int.Parse(T04TextBox.Text.Trim()); int T05Data = int.Parse(T05TextBox.Text.Trim());
            int T06Data = int.Parse(T06TextBox.Text.Trim()); int T07Data = int.Parse(T07TextBox.Text.Trim());
            int T08Data = int.Parse(T08TextBox.Text.Trim()); int T09Data = int.Parse(T09TextBox.Text.Trim());
            int T10Data = int.Parse(T10TextBox.Text.Trim()); int T11Data = int.Parse(T11TextBox.Text.Trim());
            int T12Data = int.Parse(T12TextBox.Text.Trim()); int T13Data = int.Parse(T13TextBox.Text.Trim());
            int T14Data = int.Parse(T14TextBox.Text.Trim()); int T15Data = int.Parse(T15TextBox.Text.Trim());
            int T16Data = int.Parse(T16TextBox.Text.Trim()); int T17Data = int.Parse(T17TextBox.Text.Trim());
            int T18Data = int.Parse(T18TextBox.Text.Trim()); int T19Data = int.Parse(T19TextBox.Text.Trim());

            int A00Data = int.Parse(A00TextBox.Text.Trim()); int A01Data = int.Parse(A01TextBox.Text.Trim());
            int A02Data = int.Parse(A02TextBox.Text.Trim()); int A03Data = int.Parse(A03TextBox.Text.Trim());
            int A04Data = int.Parse(A04TextBox.Text.Trim()); int A05Data = int.Parse(A05TextBox.Text.Trim());
            int A06Data = int.Parse(A06TextBox.Text.Trim()); int A07Data = int.Parse(A07TextBox.Text.Trim());
            int A08Data = int.Parse(A08TextBox.Text.Trim()); int A09Data = int.Parse(A09TextBox.Text.Trim());
            int A10Data = int.Parse(A10TextBox.Text.Trim()); int A11Data = int.Parse(A11TextBox.Text.Trim());
            int A12Data = int.Parse(A12TextBox.Text.Trim()); int A13Data = int.Parse(A13TextBox.Text.Trim());
            int A14Data = int.Parse(A14TextBox.Text.Trim()); int A15Data = int.Parse(A15TextBox.Text.Trim());
            int A16Data = int.Parse(A16TextBox.Text.Trim()); int A17Data = int.Parse(A17TextBox.Text.Trim());
            int A18Data = int.Parse(A18TextBox.Text.Trim()); int A19Data = int.Parse(A19TextBox.Text.Trim());


            byte[] sendData = new byte[61];
            sendData[0] = 0x26;
            sendData[1] = 0xE7;
            sendData[2] = 0x37;
            sendData[3] = 0x00; sendData[4] = 0x00; sendData[5] = 0x00;
            sendData[6] = (byte)MthdData;
            sendData[7] = (byte)LinkData;
            sendData[8] = 0x00; sendData[9] = 0x00;
            sendData[10] = bytes[0];
            sendData[11] = bytes[1];
            sendData[12] = bytes[2];
            sendData[13] = bytes[3];
            sendData[14] = 0x00;
            sendData[15] = (byte)SolvAData;
            sendData[16] = 0x00;
            sendData[17] = (byte)SolvBData;
            sendData[18] = (byte)T00Data; sendData[19] = (byte)T01Data;
            sendData[20] = (byte)T02Data; sendData[21] = (byte)T03Data;
            sendData[22] = (byte)T04Data; sendData[23] = (byte)T05Data;
            sendData[24] = (byte)T06Data; sendData[25] = (byte)T07Data;
            sendData[26] = (byte)T08Data; sendData[27] = (byte)T09Data;
            sendData[28] = (byte)T10Data; sendData[29] = (byte)T11Data;
            sendData[30] = (byte)T12Data; sendData[31] = (byte)T13Data;
            sendData[32] = (byte)T14Data; sendData[33] = (byte)T15Data;
            sendData[34] = (byte)T16Data; sendData[35] = (byte)T17Data;
            sendData[36] = (byte)T18Data; sendData[37] = (byte)T19Data;
            sendData[38] = (byte)A00Data; sendData[39] = (byte)A01Data;
            sendData[40] = (byte)A02Data; sendData[41] = (byte)A03Data;
            sendData[42] = (byte)A04Data; sendData[43] = (byte)A05Data;
            sendData[44] = (byte)A06Data; sendData[45] = (byte)A07Data;
            sendData[46] = (byte)A08Data; sendData[47] = (byte)A09Data;
            sendData[48] = (byte)A10Data; sendData[49] = (byte)A11Data;
            sendData[50] = (byte)A12Data; sendData[51] = (byte)A13Data;
            sendData[52] = (byte)A14Data; sendData[53] = (byte)A15Data;
            sendData[54] = (byte)A16Data; sendData[55] = (byte)A17Data;
            sendData[56] = (byte)A18Data; sendData[57] = (byte)A19Data;
            for (i = 1; i < 58; i++)
            {
                checksum = (byte)(checksum + (sendData[i]));

            }
            sendData[58] = checksum;//校验
            sendData[59] = 0x2A;
            sendData[60] = 0xEE;
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);
        }

        private void ActRunButton_Click(object sender, RoutedEventArgs e)
        {
            byte[] sendData = { 0x26, 0xE8, 0x01, 0x00, 0xE9, 0x2A, 0xEE };
            SendMsg_AllProtocal(sendData);
            //socketClient.Send(sendData);

        }



    }
}




