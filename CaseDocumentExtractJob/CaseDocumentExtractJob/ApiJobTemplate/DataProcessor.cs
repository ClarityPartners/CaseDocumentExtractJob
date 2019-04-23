using JobProcessingInterface;
using MSXML3;
using System;
using System.IO;
using Tyler.Odyssey.JobProcessing;
using Tyler.Odyssey.Utils;
using CaseDocumentExtractJob.Helpers;
using System.Linq;
using CaseDocumentExtractJob.Exceptions;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;
using Tyler.Odyssey.API.JobTemplate;
using Tyler.Integration.Framework;
using System.Xml.Serialization;
using Tyler.Odyssey.API.Shared;
using Tyler.Integration.General;
using System.Collections.Generic;

namespace CaseDocumentExtractJob
{
    internal class DataProcessor : TaskProcessor
    {
        // Constructor
        public DataProcessor(string SiteID, string JobTaskXML) : base(SiteID, JobTaskXML)
        {
            Logger.WriteToLog("JobTaskXML:\r\n" + JobTaskXML, LogLevel.Basic);

            // New up the context object
            Context = new Context(Logger);

            Logger.WriteToLog("Completed instantiation of context object", LogLevel.Verbose);

            // Retrieve the parameters for the job (which flags to add/remove)
            Context.DeriveParametersFromJobTaskXML(SiteID, JobTaskXML);
            Context.ValidateParameters();

            Logger.WriteToLog("Finished deriving parameters", LogLevel.Verbose);

            // TODO:  Add the code tables that need to be updated to the following function (Context.AddCacheItems())
            Context.AddCacheItems();
            Context.UpdateCache();

            Logger.WriteToLog("Completed cache update.", LogLevel.Verbose);
        }

        // Static constructor
        static DataProcessor()
        {
            Logger = new UtilsLogger(LogManager);
            Logger.WriteToLog("Logger Instantiated", LogLevel.Basic);
        }

        // Destructor
        ~DataProcessor()
        {
            Logger.WriteToLog("Disposing!", LogLevel.Basic);

            if (Context != null)
                Context.Dispose();
        }

        public static IUtilsLogManager LogManager = new UtilsLogManagerBase(Constants.LOG_REGISTRY_KEY);
        public static readonly UtilsLogger Logger;

        public IXMLDOMDocument TaskDocument { get; set; }

        internal Context Context { get; set; }

        public ITYLJobTaskUtility TaskUtility { get; set; }

        private object taskParms;
        public object TaskParms { get { return taskParms; } set { taskParms = value; } }

        public override void Run()
        {
            Logger.WriteToLog("Beginning Run Method", LogLevel.Basic);

            // TODO: Update API Processing Logic
            try
            {
                // Find all Documents Tied to the Case
                string transactionResponse = ProcessCaseDocumentIDSearchTransaction();

                // Extract documentIDs from response xml
                List<string> caseDocumentIDList = ExtractCurrentDocumentVersionIDs(transactionResponse);                

                if (caseDocumentIDList != null)
                    {
                    foreach (var documentID in caseDocumentIDList)
                    {
                        GetDocument(documentID);
                    }
                }
                else
                {
                    Logger.WriteToLog("No documents are found for case number: " + Context.Parameters.CaseNumber, LogLevel.Verbose);
                }
            }
            catch (Exception e)
            {
                Context.Errors.Add(new BaseCustomException(e.Message));
            }

            // TODO: Handle errors we've collected during the job run.
            if (Context.Errors.Count > 0)
            {
                // Add a message to the job indicating that something went wrong.
                AddInformationToJob();

                // Collect errors, write them to a file, and attach the file to the job.
                LogErrors();
            }

            ContinueWithProcessing("Job Completed Successfully");
        }

        private string ProcessCaseDocumentIDSearchTransaction()
        {
            TransactionEntity txn = new TransactionEntity();
            txn.TransactionType = "CaseDocumentExtractJob";
            txn.ReferenceNumber = "CaseDocumentExtractJob";
            txn.Source = "CaseDocumentExtractJob";

            // Create Data Propagation
            AddDataPropagationEntity dataPropagationEntity = new AddDataPropagationEntity();

            // 1. Adding CaseID

            txn.DataPropagation.AddIntraTxnDataPropagationEntry("#|CaseID|#", @"//TxnResponse/Result[@MessageType='FindCaseByCaseNumber']/CaseID");            

            // 2. Adding Messages            
            Logger.WriteToLog("Building FindCaseByCaseNumber", LogLevel.Verbose);
            
            try
            {
                // a. Find Case By Case Number
                Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberEntity findCaseByCaseNumberEntity = new Tyler.Odyssey.API.JobTemplate.FindCaseByCaseNumberEntity();                
                findCaseByCaseNumberEntity.SetStandardAttributes(1, "CaseDocumentExtractJob", 
                    Context.UserID, "CaseDocumentExtractJob", Context.SiteID);
                findCaseByCaseNumberEntity.NodeID = "1";
                findCaseByCaseNumberEntity.CaseNumber = Context.Parameters.CaseNumber;
                txn.Messages.Add(findCaseByCaseNumberEntity);            

                // b. Get Case Documents
                Logger.WriteToLog("Building GetDocumentInfoByEntity", LogLevel.Verbose);
                GetDocumentInfoByEntity getDocumentInfoByEntity = new GetDocumentInfoByEntity();
                getDocumentInfoByEntity.SetStandardAttributes(0, "CaseDocumentExtractJob", Context.UserID, "CaseDocumentExtractJob", Context.SiteID);
                getDocumentInfoByEntity.ReferenceNumber = "CaseDocumentExtractJob";
                getDocumentInfoByEntity.Source = "CaseDocumentExtractJob";
                getDocumentInfoByEntity.UserID = Constants.systemUserID;
                getDocumentInfoByEntity.EntityID = "#|CaseID|#";
                getDocumentInfoByEntity.EntityType = DocumentByEntityEntityType.Case;
                txn.Messages.Add(getDocumentInfoByEntity);
                
                // 3. Process Transaction
                string response = ProcessTransaction(txn.ToOdysseyTransactionXML()); 
                return response;       
            }
            catch (Exception e)
            {
                Logger.WriteToLog("Error forming Document Transaction XML: " + e, LogLevel.Verbose);
                return null;
            }           
        }

        private List<string> ExtractCurrentDocumentVersionIDs(string response)
        {            
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(response);
            XmlNodeList documentIdNodes = doc.SelectNodes("/TxnResponse/Result[@MessageType='GetDocumentInfoByEntity']/Documents/Document/CurrentDocumentVersionID");

            var documentIDList = documentIdNodes.Cast<XmlNode>()
                .Select(node => node.InnerText)
                .ToList();
            
            return documentIDList;
        }

        private GetDocumentResultEntity GetDocument(string CurrentDocumentVersionID)
        {
            try
            {
                // Create GetDocument API
                Tyler.Odyssey.API.JobTemplate.GetDocumentEntity entity = new Tyler.Odyssey.API.JobTemplate.GetDocumentEntity();
                entity.SetStandardAttributes(0, "GetDocument", Context.UserID, "GetDocument", Context.SiteID);
                entity.ReferenceNumber = "CaseDocumentExtractJob";
                entity.Source = "CaseDocumentExtractJob";
                entity.UserID = Constants.systemUserID;
                entity.ItemElementName = Tyler.Odyssey.API.JobTemplate.ItemChoiceType.FilePath;
                entity.Item = Context.Parameters.DocumentPath;
                entity.VersionID = CurrentDocumentVersionID;                
                OdysseyMessage msg = new OdysseyMessage(entity.ToOdysseyMessageXml(), Context.SiteID);

                // Process GetDocument API
                MessageHandlerFactory.Instance.ProcessMessage(msg);
                StringReader reader = new StringReader(msg.ResponseDocument.OuterXml);
                XmlSerializer serializer = new XmlSerializer(typeof(GetDocumentResultEntity));
                GetDocumentResultEntity result = (GetDocumentResultEntity)serializer.Deserialize(reader);
                Logger.WriteToLog("Current Document Version ID " + CurrentDocumentVersionID + " was successfully dropped to specified folder.", LogLevel.Verbose);
                return result;
            }
            catch (Exception e)
            {
                Logger.WriteToLog("There was an error extracting current document version ID: " 
                    + CurrentDocumentVersionID + ". Error: " + e, LogLevel.Verbose);
                Context.Errors.Add(new BaseCustomException(e.Message));
                return null;
            }
        }

        // Process Transaction
        public string ProcessTransaction(string transXml)
        {
            string txnResults = string.Empty;

            try
            {
                OdysseyTransaction txn = new OdysseyTransaction(0, transXml, Context.SiteID);
                TransactionProcessor txnProcessor = new TransactionProcessor();
                txnProcessor.ProcessTransaction(txn);

                if (txn.TransactionRejected)
                    throw new Exception(txn.RejectReason);
                else
                  if (txn.ResponseDocument != null)
                    txnResults = txn.ResponseDocument.OuterXml;
            }
            // if a schema exceptions is thrown, then throw the new exception with the data from the schema error.
            catch (SchemaValidationException svex)
            {
                throw new Exception(svex.ReplacementStrings[0]);
            }
            catch (DataConversionException dcex)
            {
                // check for an xslCodeQuery exception type in the inner exception
                // so we can report a better, more descriptive error 
                if (dcex.InnerException.GetType().Equals(typeof(XslCodeQueryException)))
                {
                    XslCodeQueryException xcqe = (XslCodeQueryException)dcex.InnerException;
                    throw new Exception(xcqe.ReplacementStrings[0], dcex);
                }
                else
                {
                    throw new Exception(dcex.Message);
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }

            return txnResults;
        }

        private void AddInformationToJob()
        {
            int jobTaskID = 0;
            int jobProcessID = 0;

            if (Int32.TryParse(Context.taskID, out jobTaskID) && Int32.TryParse(Context.jobProcessID, out jobProcessID))
            {
                object Parms = new object[,] { { "SEVERITY" }, { "2" } };

                ITYLJobTaskUtility taskUtility = (JobProcessingInterface.ITYLJobTaskUtility)Activator.CreateInstance(Type.GetTypeFromProgID("Tyler.Odyssey.JobProcessing.TYLJobTaskUtility.cTask"));

                taskUtility.AddTextMessage(Context.SiteID, jobProcessID, jobTaskID, "The job completed successfully, but errors occurred. Please see the log file for the errors associated with the job.", ref Parms);
            }
        }

        private void LogErrors()
        {
            using (StreamWriter writer = GetTempFile())
            {
                Logger.WriteToLog("Beginning to write to temp file.", LogLevel.Intermediate);

                // Write the file header
                writer.WriteLine("CaseNumber,CaseID,CaseFlag,Error");

                // For each error, write some information.
                Context.Errors.ForEach((BaseCustomException f) => WriteErrorToLog(f, writer));

                Logger.WriteToLog("Finished writing to temp file.", LogLevel.Intermediate);

                AttachTempFileToJobOutput(writer, @"Add Remove Case Flags Action - Errors");
            }
        }

        private void WriteErrorToLog(BaseCustomException exception, StreamWriter writer)
        {
            writer.WriteLine(string.Format("\"{0}\"", exception.CustomMessage));
        }

        private StreamWriter GetTempFile()
        {
            if (TaskUtility == null)
                return null;

            string filePath = TaskUtility.GenerateFile(Context.SiteID, ref taskParms);
            StreamWriter fileWriter = new StreamWriter(filePath, true);

            Logger.WriteToLog("Created temp file at location: " + filePath, LogLevel.Basic);

            return fileWriter;
        }

        private void AttachTempFileToJobOutput(StreamWriter writer, string errorFileName)
        {
            Logger.WriteToLog("Begining AttachTempFileToJobOutput()", LogLevel.Intermediate);
            Logger.WriteToLog(writer == null ? "File is NULL" : "File is NOT NULL", LogLevel.Intermediate);

            if (writer != null && TaskUtility != null)
            {
                FileStream fileStream = writer.BaseStream as FileStream;
                string filePath = fileStream.Name;
                Logger.WriteToLog("File Path: " + filePath, LogLevel.Intermediate);

                writer.Close();

                if (filePath.Length > 0 && errorFileName.Length > 0)
                    AttachFile(filePath, errorFileName);

                Logger.WriteToLog("Completed AttachTempFileToJobOutput()", LogLevel.Intermediate);
            }
        }

        private void AttachFile(string filepath, string filename)
        {
            DataProcessor.Logger.WriteToLog("Begin AttachFile()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
            int nodeID = 0;
            int taskIDInt = 0;
            int jobProcessIDInt = 0;

            if (TaskUtility != null)
            {
                if (Int32.TryParse(Context.taskID, out taskIDInt) && Int32.TryParse(Context.jobProcessID, out jobProcessIDInt))
                {
                    int documentID = TaskUtility.AddOutputDocument(this.siteKey, taskIDInt, jobProcessIDInt, -1, filepath, Context.UserID, nodeID, ref taskParms);

                    if (documentID > 0)
                    {
                        TaskUtility.AddOutputParams(this.siteKey, taskIDInt, "TEXT", documentID, filename, TaskDocument, ref taskParms);

                        TaskUtility.DeleteTempFile(filepath);

                        this.OutputJobTaskXML = TaskDocument.documentElement.xml;
                    }
                }
            }

            DataProcessor.Logger.WriteToLog("End Attach()", Tyler.Odyssey.Utils.LogLevel.Intermediate);
        }
    }
}