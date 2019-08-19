using System.Collections.Generic;
using Tyler.Odyssey.Utils;
using System;
using System.Xml;
using CaseDocumentExtractJob.Exceptions;
using System.IO;

namespace CaseDocumentExtractJob.Helpers
{
    public class Context : ServerContext
    {
        public XmlElement taskNode;
        public XmlElement paramNode; //Added This
        public string jobProcessID;
        public string taskID;

        // Constructors
        public Context(UtilsLogger logger) : base()
        {
            Errors = new List<BaseCustomException>();
            Logger = logger;
        }

        public Context(ServerContext serverContext, UtilsLogger logger) : base(serverContext)
        {
            Logger = logger;
        }

        public Parameters Parameters { get; set; }
        public List<BaseCustomException> Errors;
        public UtilsLogger Logger { get; set; }

        internal void DeriveParametersFromJobTaskXML(string siteID, string jobTaskXML)
        {
            Logger.WriteToLog("Beginning DeriveParametersFromJobTaskXML()", LogLevel.Verbose);

            XmlDocument jobTaskDoc = new XmlDocument();
            jobTaskDoc.LoadXml(jobTaskXML);

            Logger.WriteToLog("Loaded DOM document", LogLevel.Verbose);

            XmlElement jobNode = (XmlElement)jobTaskDoc.SelectSingleNode("/Job");
            // Original
            // taskNode = (XmlElement)jobTaskDoc.SelectSingleNode("/Job/Params/Input/Task/Params");

            // Fix
            paramNode = (XmlElement)jobTaskDoc.SelectSingleNode("/Job/Params/Input/Task/Params");
            taskNode = (XmlElement)jobTaskDoc.SelectSingleNode("/Job/Params/Input/Task");

            Logger.WriteToLog("Created Job Node", LogLevel.Verbose);

            if (taskNode == null)
                throw new Exception("Task node does not exist in provided parameters");

            jobProcessID = jobNode.GetAttribute("JobProcessID");
            taskID = taskNode.GetAttribute("ID");
            int userID = 0;
            bool success = Int32.TryParse(jobNode.GetAttribute("UserID"), out userID);

            Logger.WriteToLog("Finished setting job process and task ids", LogLevel.Verbose);

            if (success)
                // Original
                // DeriveParameters(siteID, userID, taskNode);
                // Fix
                DeriveParameters(siteID, userID, paramNode);

            Logger.WriteToLog("Completed DeriveParameters()", LogLevel.Verbose);
        }

        private void DeriveParameters(string siteID, int userId, XmlElement taskNode)
        {
            // We need to set the user id and site id on the base class,
            // because it will allow the base context object to do
            // code queries and user rights lookups.
            Logger.WriteToLog("About to set userid and site id", LogLevel.Verbose);

            base.UserID = userId;
            base.SiteID = siteID;

            Logger.WriteToLog("Completed setting user id and site id", LogLevel.Verbose);

            Parameters = new Parameters(taskNode, Logger);

            Logger.WriteToLog("Completed instantiation of Parameters object", LogLevel.Verbose);
        }

        public void ValidateParameters()
        {
            ValidateRequiredJobParameter(Parameters.CaseNumber, "Case Number");
            ValidateRequiredJobParameter(Parameters.NodeID, "Node ID");
            ValidateRequiredJobParameter(Parameters.BaseDocumentPath, "Base Document Path");
            ValidateRequiredJobParameter(Parameters.InputFolderName, "Input Folder Name");
            ValidateRequiredJobParameter(Parameters.ReportFolderName, "ReportFolderName");
        }

        private void ValidateRequiredJobParameter(string parameterName, string parameterValue)
        {
            if (string.IsNullOrEmpty(parameterValue))
                throw new BaseCustomException(string.Format("The required Job Parameter is missing: {1}", parameterName));
        }

        internal void AddCacheItems()
        {
            this.AddCacheItem("Justice", "uCaseFlag");
        }
    }
}