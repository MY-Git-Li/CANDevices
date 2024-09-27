using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CANDevices.CAN.PhysicalInterface;
using System.Diagnostics;
using System.Threading;
using System.Windows;


namespace CANDevices.CAN
{

    /// <summary>
    /// Zlg
    /// </summary>
    public class ZlgCanDevice:AbstractCanDevice
    {
        /// <summary>
        /// 周立功函数封装类，CAN最底层函数
        /// </summary>
        ZlgUsbCan zlgCan;


        public ZlgCanDevice(Delegate_ClaAbstCanReceiveCallback receiveCallback)
        {
            m_receiveCallback = receiveCallback; //接收数据回调函数
            InitCallback();
        }

        /// <summary>
        /// 查找设备列表，主要是ValueCAN会用到
        /// </summary>
        /// <returns></returns>
        public override List<string> FindDeviceList()
        {
            return null;
        }


        /// <summary>
        /// 查找CAN类型
        /// </summary>
        /// <returns></returns>
        public override List<string> FindCANTypes()
        {
            List<string> allCANTypes = Enum.GetNames(typeof(ZlgCanType)).ToList();
            return allCANTypes;
        }

        /// <summary>
        /// 查找CAN索引类型
        /// </summary>
        /// <returns></returns>
        public override List<string> FindCANIndexs()
        {
            List<string> allCANIndexs = new List<string>() { "0", "1" };
            return allCANIndexs;
        }

        #region 配置、启动、停止
        /// <summary>
        /// 配置CAN参数
        /// devType为CAN类型
        /// devIndex为设备索引
        /// canIndexs为CAN集合，假设有2路CAN，若是只打开第2路，new uint[1]{1}，若是都打开new uint[2]{0,1}
        /// receiveCallback为设置回调函数，若是有多路CAN，通过索引区分
        /// </summary>
        /// <param name="devType"></param>
        /// <param name="devIndex"></param>
        /// <param name="canIndexs"></param>
        /// <param name="baudrate"></param>
        /// <returns></returns>
        public override bool SetCanConfig(string canType, int devIndex)
        {
            int icanType = (int)Enum.Parse(typeof(ZlgCanType), canType);
            zlgCan = new ZlgUsbCan(icanType, (uint)devIndex);

            return true;
        }


        /// <summary>
        /// 启动所有设备
        /// </summary>
        /// <returns>返回true表示没问题，返回false表示有问题</returns>
        public override Ecm_CanInitialErrorTypes ConnectAndInitial(string[] canIndexs, UInt32[] baudrates)
        {
            Ecm_CanInitialErrorTypes errorType = Ecm_CanInitialErrorTypes.OK;
            //连接CAN
            if (!zlgCan.ConnectCan())
            {
                errorType = Ecm_CanInitialErrorTypes.ConnectError;
                return errorType;
            }


            m_IsOpen = true;
            m_CanIndexs = canIndexs;
            uint[] ucanIndexs = new uint[canIndexs.Length];
            for (int i = 0; i < canIndexs.Length; i++)
                ucanIndexs[i] = Convert.ToUInt32(canIndexs[i]); //CAN索引
            m_Baudrates = baudrates; //波特率集合

            //循环初始化多路CAN
            for (byte i = 0; i < m_CanIndexs.Length; i++)
            {
                UInt32 rec = zlgCan.InitialCan(ucanIndexs[i],m_Baudrates[i]);
                switch ( rec)
                {
                    case 1: break; //ok
                    case 0x1111: errorType = Ecm_CanInitialErrorTypes.ResetError; break;
                    case 0x2222: errorType = Ecm_CanInitialErrorTypes.SetBaudrateError; break;
                    case 0x3333: errorType = Ecm_CanInitialErrorTypes.InitialError; break;
                    case 0x4444: errorType = Ecm_CanInitialErrorTypes.SetFilterError; break;
                    case 0x5555: errorType = Ecm_CanInitialErrorTypes.StartCanError; break;
                    default: break;
                }

            }

            if (errorType == Ecm_CanInitialErrorTypes.OK)
            {
                InitCallback(); //初始化异步回调，使用回调函数接收数据
            }
            

            return errorType;
        }

        /// <summary>
        /// 断开所有设备
        /// </summary>
        /// <returns>返回true表示没问题，返回false表示有问题</returns>
        public override bool DisConnect()
        {
            try
            {
                bool result = false;
                m_IsOpen = false;

                if (zlgCan == null)
                    return true;

                //断开CAN
               result = zlgCan.DisConnectCan();

                return result;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region 发送数据
        /// <summary>
        /// 异步从CAN发送数据
        /// </summary>
        /// <param name="myCanId">myId表示发送时所使用的本机CAN的ID</param>
        /// <param name="datas">datas表示byte数组</param>
        /// <returns>返回true表示没问题，返回false表示有问题</returns>
        public override bool ASyncSendBytes(ref string canIndex, ref UInt32 myCanId, ref byte[] datas)
        {
            //bool result;

            if (!m_CanIndexs.Contains(canIndex)) //canIndex设置不正确
                canIndex = m_CanIndexs[0]; //默认为第一个

            CanStandardData canSendData = new CanStandardData();

            canSendData.dataFormat = 0; //数据帧
            canSendData.dataType = 1; //扩展帧
            canSendData.canId = myCanId;
            canSendData.datas = datas;

          
            AutoResetEvent aWait = new AutoResetEvent(false);
            uint canIndexTmp = Convert.ToUInt32(canIndex);
            Thread a = new Thread(() =>
            {
                zlgCan.SyncSendCanData(ref canIndexTmp, ref canSendData);
                aWait.Set();
            });
            a.Start();
            aWait.WaitOne(50);
            a.Abort();

            return true;

        }

        /// <summary>
        /// 同步从CAN发送数据
        /// </summary>
        public override bool SyncSendBytes(ref string canIndex, ref UInt32 myCanId, ref byte[] datas)
        {
            //bool result;

            if (!m_CanIndexs.Contains(canIndex)) //canIndex设置不正确
                canIndex = m_CanIndexs[0]; //默认为第一个

            CanStandardData canSendData = new CanStandardData();

            canSendData.dataFormat = 0; //数据帧
            canSendData.dataType = 1; //扩展帧
            canSendData.canId = myCanId;
            canSendData.datas = datas;

            uint canIndexTmp = Convert.ToUInt32(canIndex);
            zlgCan.SyncSendCanData(ref canIndexTmp, ref canSendData);
            return true;

        }
        #endregion


        
        
        /// <summary>
        /// 异步回调被回调函数
        /// </summary>
        protected override void ReceiveCanDatas()
        {
            try
            {
                if (m_IsOpen)
                {
                    //多路CAN接收，分别推送
                    for (byte i = 0; i < m_CanIndexs.Length; i++)
                    {
                        uint ucanIndex = Convert.ToUInt32(m_CanIndexs[i]);
                        ZlgUsbCan.CanReceiveDatas canRecDatas = zlgCan.ReceiveCanData(ucanIndex);

                        if (!canRecDatas.isReceiveSuccess)
                            continue;

                        foreach (CanStandardData csdData in canRecDatas.receiveCanDatas)
                        {
                            m_receiveCallback(m_CanIndexs[i], csdData); //推送到上一层的回调函数
                        }
                    }
                    
                }
            }
            catch(System.Exception ex)
            {
                MessageBox.Show("Can 接受消息错误！！\n" + ex.Message);
            }
        }

    }
}
