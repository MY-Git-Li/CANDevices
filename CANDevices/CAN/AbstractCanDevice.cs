using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CANDevices.CAN.PhysicalInterface;
using System.Diagnostics;
using System.Threading;


namespace CANDevices.CAN
{
    

    /// <summary>
    /// 一桢CAN数据结构体
    /// </summary>
    public struct CanStandardData
    {
        public uint timeStamp;
        public UInt32 canId;
        public uint baudrate;
        /// <summary>
        /// 标准帧，扩展帧
        /// </summary>
        public byte dataType; //0-标准帧，1-扩展帧
        /// <summary>
        /// 数据帧，远程帧
        /// </summary>
        public byte dataFormat; //0-数据帧，1远程帧
        public byte[] datas;
    }

    /// <summary>
    /// 用户不应直接调用此类，从ClassCmdProcess中调用相应函数
    /// </summary>
    public abstract class AbstractCanDevice
    {
        /// <summary>
        /// 连接并初始化的错误类型
        /// 1 - no error
        /// 0x0000 - connect error
        /// 0x1111 - reset error
        /// 0x2222 - set baudrate error
        /// 0x3333 - initial error
        /// 0x4444 - set filter error
        /// 0x5555 - start CAN error
        /// </summary>
        public enum Ecm_CanInitialErrorTypes:uint
        { 
            OK = 0x01,
            ConnectError = 0x0000,
            ResetError = 0x1111,
            SetBaudrateError = 0x2222,
            InitialError = 0x3333,
            SetFilterError = 0x4444,
            StartCanError = 0x5555
        }

        /// <summary>
        /// 周立功CAN的类型，ValueCAN不用枚举，自动查找
        /// </summary>
        protected enum ZlgCanType : int
        {
            //若修改，则同时需要修改构造函数ClassUsbCan的Can类型加值，本实例为20
            PCI5121 = 1,
            PCI9810 = 2,
            USBCAN1 = 3,
            USBCAN2 = 4,
            USBCAN2A = 4,
            PCI9820 = 5,
            CAN232 = 6,
            PCI5110 = 7,
            CANLITE = 8,
            ISA9620 = 9,
            ISA5420 = 10,
            PC104CAN = 11,
            CANETUDP = 12,
            CANETE = 12,
            DNP9810 = 13,
            PCI9840 = 14,
            PC104CAN2 = 15,
            PCI9820I = 16,
            CANETTCP = 17,
            PEC9920 = 18,
            PCI5010U = 19,
            USBCAN_E_U = 20,
            USBCAN_2E_U = 21,
            PCI5020U = 22,
            EG20T_CAN = 23,
        }

        /// <summary>
        ///  ValueCAN Network ID，类似于USBCAN的CANindex
        /// </summary>
        protected enum ValueCANNetwork_ID : int
        {
            NETID_HSCAN = 1,
            NETID_MSCAN = 2
        }


        /// <summary>
        /// 回调函数委托类型定义
        /// </summary>
        /// <param name="canRecDatas"></param>
        public delegate void Delegate_ClaAbstCanReceiveCallback(string canIndex, CanStandardData canRecDatas);
        /// <summary>
        /// 推送接收数据回调
        /// </summary>
        public Delegate_ClaAbstCanReceiveCallback m_receiveCallback;
        
        /// <summary>
        /// 是否已开启连接
        /// </summary>
        protected bool m_IsOpen;

        /// <summary>
        /// 设备索引
        /// </summary>
        protected int m_DevIndex;

        /// <summary>
        /// 一个CAN设备里面的多路CAN
        /// </summary>
        protected string[] m_CanIndexs;

        /// <summary>
        /// 多路CAN的波特率
        /// </summary>
        protected uint[] m_Baudrates;

        /// <summary>
        /// 查找设备列表，主要是ValueCAN会用到
        /// </summary>
        /// <returns></returns>
        public abstract List<string> FindDeviceList();

        /// <summary>
        /// 查找CAN类型,ValueCAN用不到
        /// </summary>
        /// <returns></returns>
        public abstract List<string> FindCANTypes();

        /// <summary>
        /// 查找CAN索引类型
        /// </summary>
        /// <returns></returns>
        public abstract List<string> FindCANIndexs();

        #region 配置、启动、停止
        /// <summary>
        /// 配置CAN参数
        /// canType为CAN类型，列表可从FindCANTypes获取
        /// devIndex为设备索引，针对插入了多个CAN设备的
        /// canIndexs为CAN集合，列表可从FindCANIndexs获取
        /// receiveCallback为设置回调函数，若是有多路CAN，通过索引区分
        /// </summary>
        /// <param name="devType"></param>
        /// <param name="devIndex"></param>
        /// <param name="canIndexs"></param>
        /// <param name="baudrate"></param>
        /// <returns></returns>
        public abstract bool SetCanConfig(string canType, int devIndex);


        /// <summary>
        /// 启动所有设备
        /// </summary>
        /// <returns>返回true表示没问题，返回false表示有问题</returns>
        public abstract Ecm_CanInitialErrorTypes ConnectAndInitial(string[] canIndexs, UInt32[] baudrates);

        /// <summary>
        /// 断开所有设备
        /// </summary>
        /// <returns>返回true表示没问题，返回false表示有问题</returns>
        public abstract bool DisConnect();
        #endregion

        #region 发送数据
        /// <summary>
        /// 异步从CAN发送数据
        /// </summary>
        /// <param name="myCanId">myId表示发送时所使用的本机CAN的ID</param>
        /// <param name="datas">datas表示byte数组</param>
        /// <returns>返回true表示没问题，返回false表示有问题</returns>
        public abstract bool ASyncSendBytes(ref string canIndex, ref UInt32 myCanId, ref byte[] datas);

        /// <summary>
        /// 同步从CAN发送数据
        /// </summary>
        public abstract bool SyncSendBytes(ref string canIndex, ref UInt32 myCanId, ref byte[] datas);
        #endregion


        #region 异步回调方法接收数据
        private delegate void ReceiveTakesAWhileDelegate();

        /// <summary>
        /// 初始化异步读取回调函数
        /// </summary>
        public virtual void InitCallback()
        {
            ReceiveTakesAWhileDelegate receiveAsyncTask = ReceiveCanDatas;

            receiveAsyncTask.BeginInvoke(ReceiveTakesCompletedOnce, receiveAsyncTask);

        }


        /// <summary>
        /// 异步回调一次完成通知
        /// </summary>
        /// <param name="ar"></param>
        private void ReceiveTakesCompletedOnce(IAsyncResult ar)
        {
            if (ar == null) throw new ArgumentNullException("ar");

            ReceiveTakesAWhileDelegate d1 = ar.AsyncState as ReceiveTakesAWhileDelegate;

            if (m_IsOpen)
            {
                Trace.Assert(d1 != null, "Invalid object type");
                d1.BeginInvoke(ReceiveTakesCompletedOnce, d1); //再次回调
            }
            else
            {
                d1.EndInvoke(ar);
            }
        }


        /// <summary>
        /// 异步回调被回调函数
        /// </summary>
        protected abstract void ReceiveCanDatas();
        #endregion

    }
}
