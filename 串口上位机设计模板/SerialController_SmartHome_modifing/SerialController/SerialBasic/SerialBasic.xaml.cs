using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using Microsoft.Win32;

using System.Media;
using System.Runtime.InteropServices;

namespace SerialController
{
    /// <summary>
    /// SerialBasic.xaml 的交互逻辑
    /// </summary>
    public partial class SerialBasic : UserControl
    {
        #region 变量定义

        #region 内部变量
        private SerialPort serial = new SerialPort();

        private string receiveData;

        private DispatcherTimer autoDetectionTimer = new DispatcherTimer();//涉及UI的定时器需要使用DispatcherTimer

        #endregion

        #endregion

        public SerialBasic()
        {

            InitializeComponent();//初始化窗体控件

            GetValuablePortName();//自动更新串口号

            // 设置自动检测1秒1次
            autoDetectionTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            autoDetectionTimer.Tick += new EventHandler(AutoDectionTimer_Tick);
            //开启定时器
            autoDetectionTimer.Start();

            //设置状态栏提示
            statusTextBlock.Text = "准备就绪";
        }

        
        #region 自动更新串口号
        //自动检测串口名
        private void GetValuablePortName()
        {
            //检测有效的串口并添加到combobox
            string[] serialPortName = System.IO.Ports.SerialPort.GetPortNames();

            foreach (string name in serialPortName)
            {
                portNamesCombobox.Items.Add(name);            
            }        
        }

        //自动检测串口时间到
        private void AutoDectionTimer_Tick(object sender, EventArgs e)
        {
            
            string[] serialPortName = System.IO.Ports.SerialPort.GetPortNames();

            if(turnOnButton.IsChecked == true)
            { 
                //在找到的有效串口号中遍历当前打开的串口号
                foreach (string name in serialPortName)
                {
                    if (serial.PortName == name)
                        return;                 //找到，则返回，不操作               
                }

                //若找不到已打开的串口:表示当前打开的串口已失效
                //按钮回弹
                turnOnButton.IsChecked = false;
                //删除combobox中的名字
                portNamesCombobox.Items.Remove(serial.PortName);
                portNamesCombobox.SelectedIndex = 0;
                //提示消息
                statusTextBlock.Text = "串口已失效！";
            }
            else
            {
                //检查有效串口和combobox中的串口号个数是否不同
                if (portNamesCombobox.Items.Count != serialPortName.Length)
                {
                    //串口数不同，清空combobox
                    portNamesCombobox.Items.Clear();

                    //重新添加有效串口
                    foreach (string name in serialPortName)
                    {
                        portNamesCombobox.Items.Add(name);
                    }
                    portNamesCombobox.SelectedIndex = 0;

                    statusTextBlock.Text = "串口列表已更新！";

                }
            }  
        }
        #endregion

        #region 串口配置面板

        //使能或关闭串口配置面板相关的控件
        private void serialSettingControlState(bool state)
        {
            portNamesCombobox.IsEnabled = state; 
            baudRateCombobox.IsEnabled = state;
            parityCombobox.IsEnabled = state;
            dataBitsCombobox.IsEnabled = state;
            stopBitsCombobox.IsEnabled = state;
        }

        //打开串口
        private void TurnOnButton_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                //配置串口
                serial.PortName = portNamesCombobox.Text;
                serial.BaudRate = Convert.ToInt32(baudRateCombobox.Text);
                serial.Parity = (System.IO.Ports.Parity)Enum.Parse(typeof(System.IO.Ports.Parity), parityCombobox.Text);
                serial.DataBits = Convert.ToInt16(dataBitsCombobox.Text);
                serial.StopBits = (System.IO.Ports.StopBits)Enum.Parse(typeof(System.IO.Ports.StopBits), stopBitsCombobox.Text);

                //设置串口编码为default：获取操作系统的当前 ANSI 代码页的编码。
                serial.Encoding = Encoding.Default;

                //添加串口事件处理
                serial.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(ReceiveData);
                
                //开启串口
                serial.Open();
                
                //关闭串口配置面板（禁止配置）
                serialSettingControlState(false);

                statusTextBlock.Text = "串口已开启";
                
                //显示提示文字
                turnOnButton.Content = "关闭串口";
                
                //串口状态图形灯变红
                serialPortStatusEllipse.Fill = Brushes.Red;


            }
            catch
            {
                statusTextBlock.Text = "配置串口出错！";
            }
          
        }


        //关闭串口
        private void TurnOnButton_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                serial.Close();

                //使能串口配置面板（允许配置）
                serialSettingControlState(true);

                statusTextBlock.Text = "串口已关闭";

                //显示提示文字
                turnOnButton.Content = "打开串口";

                //串口状态图形灯变灰
                serialPortStatusEllipse.Fill = Brushes.Gray;

            }
            catch 
            {                
                
            }
            
        }

        #endregion

        #region 播放Mp3
        public class Mp3Player
        {
            // <summary>
            /// 使用API
            /// </summary>
            static uint SND_ASYNC = 0x0001; // play asynchronously 
            static uint SND_FILENAME = 0x00020000; // name is file name
            [DllImport("winmm.dll")]
            static extern int mciSendString(string m_strCmd, string m_strReceive, int m_v1, int m_v2);

            [DllImport("Kernel32", CharSet = CharSet.Auto)]
            static extern Int32 GetShortPathName(String path, StringBuilder shortPath, Int32 shortPathLength);

            public static void Play(string MusicFile)
            {
                if (!System.IO.File.Exists(MusicFile)) return;
                StringBuilder shortpath = new StringBuilder(80);
                int result = GetShortPathName(MusicFile, shortpath, shortpath.Capacity);
                MusicFile = shortpath.ToString();
                mciSendString(@"close all", null, 0, 0);
                mciSendString(@"open " + MusicFile + " alias song", null, 0, 0); //打开
                mciSendString("play song", null, 0, 0); //播放
            }

            public static void Close()
            {
                mciSendString(@"close all", null, 0, 0);

            }
        }
        #endregion 
        
 

        #region 接收显示窗口

        //接收数据
        private delegate void UpdateUiTextDelegate(string text);//delegate声明一个委托

        private void ReceiveData(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            receiveData = serial.ReadExisting();

            //同步更新主线程控件的显示，DispatcherPriority.Send 枚举类型，Send=10优先级最高
            Dispatcher.Invoke(DispatcherPriority.Send, new UpdateUiTextDelegate(ShowData), receiveData);
        }

        //显示数据
        private void ShowData(string text)
        {
            string receiveText = text;
              
            byte[] recData = System.Text.Encoding.Default.GetBytes(receiveText);// 将接受到的字符串据转化成数组；  

            //在此处对数据包解码,并放入指定文本框显示
            //需要将之前的显示清除再放入新的
            if(recData[0]==0x26) //温湿度数据包的帧头
            {
                TempureNumber.Clear();
                HumidityNumber.Clear();

                TempureNumber.AppendText(string.Format("{0:D2}",recData[1]));
                HumidityNumber.AppendText(string.Format("{0:D2}", recData[2]));
            }
            else if(recData[0]==0x36)//节点上线的帧头
            {
                switch(recData[1])
                {
                //节点上线
                    case 0xA1:
                        Node1StatusEllipse.Fill = Brushes.Red;
                        Node1statusTextBlock.Text = "节点1已上线";
                        break;
                    case 0xA2:
                        Node2StatusEllipse.Fill = Brushes.Red;
                        Node2statusTextBlock.Text = "节点2已上线";
                        break;
                    case 0xA3:
                        Node3StatusEllipse.Fill = Brushes.Red;
                        Node3statusTextBlock.Text = "节点3已上线";
                        break;
                    case 0xA4:
                        Node4StatusEllipse.Fill = Brushes.Red;
                        Node4statusTextBlock.Text = "节点4已上线";
                        break;
                    case 0xA5:
                        Node5StatusEllipse.Fill = Brushes.Red;
                        Node5statusTextBlock.Text = "节点5已上线";
                        break;
                //节点掉线
                    case 0xB1:
                        Node1StatusEllipse.Fill = Brushes.Gray;
                        Node1statusTextBlock.Text = "节点1已离线";
                        break;
                    case 0xB2:
                        Node2StatusEllipse.Fill = Brushes.Gray;
                        Node2statusTextBlock.Text = "节点2已离线";
                        break;
                    case 0xB3:
                        Node3StatusEllipse.Fill = Brushes.Gray;
                        Node3statusTextBlock.Text = "节点3已离线";
                        break;
                    case 0xB4:
                        Node4StatusEllipse.Fill = Brushes.Gray;
                        Node4statusTextBlock.Text = "节点4已离线";
                        break;
                    case 0xB5:                
                        Node5StatusEllipse.Fill = Brushes.Gray;
                        Node5statusTextBlock.Text = "节点5已离线";
                        break;
                    default:
                        break;

                }

            }
            else if (recData[0] == 0x46)
            {
                if (recData[1] == 0xAA)
                { 
                    //方法一
                    //Mp3Player.Play(@"C:\Users\11315\Desktop\SerialController_SmartHome\Alarm.mp3");
                    //if (MessageBox.Show("警告！检测到烟雾报警！请及时处理", "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                    //    Mp3Player.Close();
                 
                    //方法二
                    SoundPlayer sp = new SoundPlayer(SerialController.Properties.Resources.Alarm);
                    sp.Load();
                    sp.PlayLooping();
                    if (MessageBox.Show("警告！检测到烟雾报警！请及时处理", "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                        sp.Stop(); 
                                     
                }
                else if (recData[1] == 0xBB)
                {
                    //方法一
                    //Mp3Player.Play(@"C:\Users\11315\Desktop\SerialController_SmartHome\Alarm.mp3");                   
                    //if(MessageBox.Show("警告！检测到入侵报警！请及时处理", "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning)== MessageBoxResult.OK)
                    //      Mp3Player.Close();

                    //方法二
                    SoundPlayer sp = new SoundPlayer(SerialController.Properties.Resources.Alarm);
                    sp.Load();
                    sp.PlayLooping();
                    if (MessageBox.Show("警告！检测到入侵报警！请及时处理", "警告", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK)
                        sp.Stop();
                    
                }
            }

        }

        #endregion

       
       
         #region 发送控制面板

        //发送数据
         private void SerialPortSend(byte[] data)
         { 
             if(!serial.IsOpen)
             {
                 statusTextBlock.Text = "请先打开串口！";
     
                 MessageBox.Show("请先打开串口！", "提示",MessageBoxButton.OK,MessageBoxImage.Warning);
                 return;
             }
             try
             {
                 /*------------发送数据----------------*/
                 serial.Write(data, 0, data.Length);
             
             }
             catch 
             {
             
             }
         
         }

         #endregion


   
         private void baudRateCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {

         }


/*-----------------------------------Code below are functions of new define -----------------------------------------------*/
   
        //灯光控制
         private void LightModeConboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
         {

             byte[] sendData = new byte[10];
             Int32 mode=0;
             //模式的值目前都是0，错误；默认模式1，第一次点击需要弹窗提醒：当前模式就是模式1；可以增加修改模式成功状态栏
            // string LightMode = LightModeConboBox.Text;//将conbobox控件的自定义项转换成整型
             Int32 LightMode = 0;
                    LightMode = LightModeConboBox.SelectedIndex;
             switch (LightMode)
             {
                 case 0:
                     mode = 0xA1;
                     break;
                 case 1:
                     mode = 0xA2;
                     break;
                 case 2:
                     mode = 0xA3;
                     break;
                 case 3:
                     mode = 0xA4;
                     break;

             }
            
             sendData[0] = 0x26;
             sendData[1] = 0xEB;
             sendData[2] = 0x05;
             sendData[3] = (byte)mode;
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;
             SerialPortSend(sendData);
 
         }

         //继电器总开关打开
         private void TotalSwitchOpen_Checked(object sender, RoutedEventArgs e)
         {
            if(Switch1Open.IsChecked ==false)
             Switch1Open.IsChecked = true;
            if (Switch2Open.IsChecked == false)
             Switch2Open.IsChecked = true;
            if (Switch3Open.IsChecked == false)
             Switch3Open.IsChecked = true;
            if (Switch4Open.IsChecked == false)
             Switch4Open.IsChecked = true;
            if (Switch5Open.IsChecked == false)
             Switch5Open.IsChecked = true;

             TotalSwitchOpen.Content = "已打开";
            
             
         }
        //继电器总开关关闭
         private void TotalSwitchOpen_UnChecked(object sender, RoutedEventArgs e)
         {
             if (Switch1Open.IsChecked == true)
                 Switch1Open.IsChecked = false;
             if (Switch2Open.IsChecked == true)
                 Switch2Open.IsChecked = false;
             if (Switch3Open.IsChecked == true)
                 Switch3Open.IsChecked = false;
             if (Switch4Open.IsChecked == true)
                 Switch4Open.IsChecked = false;
             if (Switch5Open.IsChecked == true)
                 Switch5Open.IsChecked = false;
         
             TotalSwitchOpen.Content = "总开关";
           

         }

        //继电器开关1打开
         private void Switch1Open_Checked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x1F; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch1Open.Content = "已打开";
         }

         //继电器开关1关闭
         private void Switch1Open_Unchecked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x1E; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch1Open.Content = "关闭";
         }

        //继电器开关2打开
         private void Switch2Open_Checked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x2F; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch2Open.Content = "已打开";
         }

        //继电器开关2关闭
         private void Switch2Open_Unchecked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x2E; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch2Open.Content = "关闭";
         }

        //继电器开关3打开
         private void Switch3Open_Checked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x3F; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch3Open.Content = "已打开";
         }

        //继电器开关3关闭
         private void Switch3Open_Unchecked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x05; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch3Open.Content = "关闭";
         }

        //继电器开关4打开
         private void Switch4Open_Checked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x4F; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch4Open.Content = "已打开";
         }

        //继电器开关4关闭
         private void Switch4Open_Unchecked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x4E; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch4Open.Content = "关闭";
         }


         //继电器开关5打开
         private void Switch5Open_Checked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x5F; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch5Open.Content = "已打开";



         }
         //继电器开关5关闭
         private void Switch5Open_Unchecked(object sender, RoutedEventArgs e)
         {
             byte[] sendData = new byte[10];
             sendData[0] = 0x26; //帧头
             sendData[1] = 0xEB; //功能码
             sendData[2] = 0x05; //数据包长度
             sendData[3] = 0x5E; //数据位
             sendData[4] = 0x00;
             sendData[5] = 0x00;
             sendData[6] = 0x00;
             sendData[7] = 0x00;
             sendData[8] = (byte)(sendData[1] + sendData[2] + sendData[6] + sendData[7]);//校验
             sendData[9] = 0x2A;//帧尾
             SerialPortSend(sendData);
             Switch5Open.Content = "关闭";

         }
      

    }


}
