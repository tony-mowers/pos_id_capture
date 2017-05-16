﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Media;
using CH.Alika.POS.Hardware.Logging;

namespace CH.Alika.POS.Hardware
{

    public class MMMSwipeReader : IScanSource
    {
        private static readonly ILog log = LogProvider.For<MMMSwipeReader>();

        private MMM.Readers.Modules.Swipe.SwipeSettings swipeSettings;
        public event EventHandler<CodeLineScanEvent> OnCodeLineScanEvent;
        public event EventHandler<ScanSourceEvent> OnScanSourceEvent;

        public MMMSwipeReader()
        {
            OnCodeLineScanEvent += delegate(Object sender, CodeLineScanEvent e) { };
            OnScanSourceEvent += delegate(Object sender, ScanSourceEvent e) { };
        }

        public void Activate()
        {
            log.Debug("Begin Swipe Reader Activation");
            // Initialise logging and error handling first. The error handler callback
            // will receive all error messages generated by the 3M Page Reader SDK
            MMM.Readers.Modules.Reader.SetErrorHandler(
                new MMM.Readers.ErrorDelegate(DeviceErrorHandler),
                IntPtr.Zero
            );

            InitalizeLogging();
            LoadSwipeSettings(ref swipeSettings);
            InitializeSwipeReader(
                swipeSettings,
                new MMM.Readers.Modules.Swipe.DataDelegate(DeviceDataHandler),
                new MMM.Readers.FullPage.EventDelegate(DeviceEventHandler));

            log.Debug("End Swipe Reader Activation");
            log.Info("Swipe Reader Acquired");
        }

        public override String ToString()
        {
            // Display the hardware device and protocol in use
            string lProtocolName = new string(swipeSettings.Protocol.ProtocolName).Replace("\0", "");

            if (lProtocolName.StartsWith("RTE"))
            {
                // For RTE_INTERRUPT and RTE_POLLED modes, the Swipe Reader API can 
                // automatically send Enable Device commands once finished reading so
                // that you do not have to
                if (
                    !lProtocolName.Equals("RTE_NATIVE") &&
                    swipeSettings.Protocol.RTE.AutoSendEnableDevice > 0
                )
                {
                    lProtocolName = string.Concat(
                        lProtocolName,
                        ", Auto Send Enable Command"
                    );
                }

                if (swipeSettings.Protocol.RTE.UseBCC > 0)
                {
                    lProtocolName = string.Concat(
                        lProtocolName,
                        ", with BCC"
                    );
                }
                else
                {
                    lProtocolName = string.Concat(
                        lProtocolName,
                        ", no BCC"
                    );
                }
            }

            return string.Format(
                "MMMSwipeReader ProtocolSettings[{0}]",
                lProtocolName
            );
        }

        private void InitializeSwipeReader(
            MMM.Readers.Modules.Swipe.SwipeSettings swipeSettings,
            MMM.Readers.Modules.Swipe.DataDelegate dataDelegate,
            MMM.Readers.FullPage.EventDelegate eventDelegate
            )
        {
            log.Debug("Initialize Swipe Reader with settings : [" + swipeSettings + "]");

            MMM.Readers.ErrorCode lErrorCode = MMM.Readers.Modules.Swipe.Initialise(
                    swipeSettings,
                    dataDelegate,
                    eventDelegate);
            if (lErrorCode != MMM.Readers.ErrorCode.NO_ERROR_OCCURRED)
            {
                String message = String.Format("Failed SwipeReader Initialization {0} {1}", (int)lErrorCode, lErrorCode.ToString());
                throw new PosHardwareException(message);
            }
        }

        private void LoadSwipeSettings(ref MMM.Readers.Modules.Swipe.SwipeSettings swipeSettings)
        {
            MMM.Readers.ErrorCode lErrorCode = MMM.Readers.Modules.Reader.LoadSwipeSettings(
                    ref swipeSettings
                );

            if (lErrorCode != MMM.Readers.ErrorCode.NO_ERROR_OCCURRED)
            {

                String message = String.Format("Failed to Load Swipe Settings {0} {1}", (int)lErrorCode, lErrorCode.ToString());
                log.Warn(message);
                throw new PosHardwareException(message);
            }


        }

        private void InitalizeLogging()
        {
            MMM.Readers.ErrorCode lErrorCode = MMM.Readers.Modules.Reader.InitialiseLogging(
                true,
                3,
                -1,
                "SwipeReader.Net.log"
            );

            if (lErrorCode != MMM.Readers.ErrorCode.NO_ERROR_OCCURRED)
            {
                String message = String.Format("Failed Logger Initialization {0} {1}", (int)lErrorCode, lErrorCode.ToString());
                throw new PosHardwareException(message);
            }

        }


        private void DeviceDataHandler(MMM.Readers.Modules.Swipe.SwipeItem swipeItem, object swipeData)
        {
            log.DebugFormat("Device data: swipe item [{0}], swipe data [{1}]", swipeItem, swipeData);
            NotifyListeners(new ScanSourceEvent(swipeItem, swipeData));

            if (swipeItem == MMM.Readers.Modules.Swipe.SwipeItem.OCR_CODELINE)
            {
                
                MMM.Readers.CodelineData codeLineData = (MMM.Readers.CodelineData)swipeData;
                using (LogProvider.OpenNestedContext(codeLineData.Surname)) {
                    log.InfoFormat("CodeLineData ValidationResult [{0}]", codeLineData.CodelineValidationResult);
                    NotifyListeners(codeLineData);
                }
            }
        }

        private void NotifyListeners(MMM.Readers.CodelineData codeLineData)
        {
            log.Info("Notifying listeners of document scan");
            log.DebugFormat("Begin notification of CodeLineScanEvent listeners [{0}]", codeLineData);
            try { OnCodeLineScanEvent(this, new CodeLineScanEvent(codeLineData)); }
            catch { };
            log.DebugFormat("End notification of CodeLineScanEvent listeners [{0}]", codeLineData);
        }

        private void NotifyListeners(ScanSourceEvent e) {
            log.DebugFormat("Begin notifying ScanSourceEvent listeners [{0}]", e);
            try { OnScanSourceEvent(this, e); }
            catch { };
            log.DebugFormat("End notifying ScanSourceEvent listeners");
        }

        private void DeviceErrorHandler(MMM.Readers.ErrorCode errorCode, string errorMessage)
        {
            NotifyListeners(new ScanSourceEvent(errorCode,errorMessage));
        }

        private void DeviceEventHandler(MMM.Readers.FullPage.EventCode eventCode)
        {
            NotifyListeners(new ScanSourceEvent(eventCode));
        }

        public void Dispose()
        {
            log.Debug("Begin disposing of SwipeReader");
            MMM.Readers.Modules.Swipe.Shutdown();
            log.Debug("End disposing of SwipeReader");
            log.Info("Swipe Reader Released");
        }


    }
}